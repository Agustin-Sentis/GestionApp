using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Infrastructure.ExternalService;
using GastosBackend.Tests.Helpers;
using Microsoft.Extensions.Options;
using Moq;

namespace GastosBackend.Tests.Services;

public class AuthServiceTests
{
    private const string Password = "Demo1234!";

    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHash> _passwordHashMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHashMock = new Mock<IPasswordHash>();
        _passwordHashMock.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");

        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = new string('k', 40),
            Issuer = "Gastos",
            Audience = "GastosClients",
            ExpirationMinutes = 60
        });
        _authService = new AuthService(
            _userRepositoryMock.Object, _passwordHashMock.Object, _backgroundMock.Object, jwtOptions, _twoFactorMock.Object, _clock);
    }

    private readonly Mock<IBackgroundRunner> _backgroundMock = new();
    private readonly Mock<ITwoFactorService> _twoFactorMock = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 12, 15, 0, 0, TimeSpan.Zero));

    private static User ExistingUser() => new User
    {
        Id = Guid.NewGuid(),
        Email = "demo@demo.com",
        // Low work factor only to keep the tests fast
        Password = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4)
    };

    [Fact]
    public async Task SignIn_ReturnsToken_WhenCredentialsAreValid()
    {
        var user = ExistingUser();
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync(user);

        var result = await _authService.SignIn(new SignInRequest { Email = " Demo@Demo.com ", Password = Password });

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal(user.Id, result.UserId);
    }

    [Fact]
    public async Task SignIn_Token_CarriesTheMomentItWasIssued()
    {
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync(ExistingUser());
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await _authService.SignIn(new SignInRequest { Email = "demo@demo.com", Password = Password });

        var iat = long.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(result.Token).Claims
            .Single(c => c.Type == "iat").Value);
        Assert.InRange(iat, before, before + 5);
    }

    [Fact]
    public async Task SignIn_Throws_WhenTheUserDoesNotExist()
    {
        _userRepositoryMock.Setup(r => r.GetByEmail(It.IsAny<string>())).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.SignIn(new SignInRequest { Email = "nobody@demo.com", Password = Password }));

        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    [Fact]
    public async Task SignIn_ReturnsTheSameError_WhenThePasswordIsWrong()
    {
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync(ExistingUser());

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.SignIn(new SignInRequest { Email = "demo@demo.com", Password = "Wrong1234!" }));

        // Same message as an unknown email, so the response does not reveal which emails exist
        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("demo@demo.com", "")]
    public async Task SignIn_Throws_AndDoesNotQueryTheDatabase_WhenARequiredFieldIsMissing(string email, string password)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _authService.SignIn(new SignInRequest { Email = email, Password = password }));

        _userRepositoryMock.Verify(r => r.GetByEmail(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SignUp_Throws_WhenTheEmailIsAlreadyRegistered()
    {
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync(ExistingUser());

        await Assert.ThrowsAsync<ConflictException>(
            () => _authService.SignUp(new SignUpRequest { Email = "demo@demo.com", Password = Password }));

        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _backgroundMock.Verify(b => b.Run(It.IsAny<Func<ISettingsService, Task>>()), Times.Never);
    }

    [Fact]
    public async Task SignUp_QueuesTheConfirmationMail_ForTheNewUser_WithoutWaitingForIt()
    {
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.Add(It.IsAny<User>())).ReturnsAsync((User u) => { u.Id = Guid.NewGuid(); return u; });
        var settings = new Mock<ISettingsService>();
        Func<ISettingsService, Task>? queued = null;
        _backgroundMock.Setup(b => b.Run(It.IsAny<Func<ISettingsService, Task>>()))
            .Callback((Func<ISettingsService, Task> work) => queued = work);

        var result = await _authService.SignUp(new SignUpRequest { Email = "demo@demo.com", Password = Password });

        // The account exists and the response is ready; the mail is only queued, and it is for this user
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.NotNull(queued);
        settings.Verify(s => s.ResendConfirmation(It.IsAny<Guid>()), Times.Never);
        await queued!(settings.Object);
        settings.Verify(s => s.ResendConfirmation(result.UserId), Times.Once);
    }

    [Fact]
    public async Task SignUp_ReturnsConflict_WhenAnotherRequestRegisteredTheEmailFirst()
    {
        // The email looked free, but the unique index rejected the insert (two signups racing)
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.Add(It.IsAny<User>())).ThrowsAsync(new DuplicateException("duplicate"));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _authService.SignUp(new SignUpRequest { Email = "demo@demo.com", Password = Password }));

        Assert.Equal("Ya existe un usuario con ese email.", ex.Message);
    }

    // --- Two-step verification ---

    private User TwoFactorUser()
    {
        var user = ExistingUser();
        user.TwoFactorEnabled = true;
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.Get(user.Id)).ReturnsAsync(user);
        return user;
    }

    private async Task<string> Challenge()
    {
        var response = await _authService.SignIn(new SignInRequest { Email = "demo@demo.com", Password = Password });
        return response.ChallengeToken!;
    }

    [Fact]
    public async Task SignIn_WithoutTwoFactor_ReturnsTheSessionAsBefore_AndNoChallenge()
    {
        var user = ExistingUser();
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync(user);

        var result = await _authService.SignIn(new SignInRequest { Email = "demo@demo.com", Password = Password });

        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.ChallengeToken);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("demo@demo.com", result.Email);
        Assert.NotNull(result.ExpiresAtUtc);
    }

    [Fact]
    public async Task SignIn_WithTwoFactor_ReturnsAChallenge_AndNoSession()
    {
        TwoFactorUser();

        var result = await _authService.SignIn(new SignInRequest { Email = "demo@demo.com", Password = Password });

        Assert.True(result.RequiresTwoFactor);
        Assert.False(string.IsNullOrEmpty(result.ChallengeToken));
        Assert.Null(result.Token);
        Assert.Null(result.UserId);
        Assert.Null(result.ExpiresAtUtc);
    }

    [Fact]
    public async Task SignIn_WithTwoFactor_AndAWrongPassword_ReturnsTheSameErrorAsAlways_NotAChallenge()
    {
        TwoFactorUser();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.SignIn(new SignInRequest { Email = "demo@demo.com", Password = "Wrong1234!" }));

        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    [Fact]
    public async Task VerifyTwoFactor_WithAValidCode_ReturnsTheSessionOfTheUser()
    {
        var user = TwoFactorUser();
        var challenge = await Challenge();

        var session = await _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" });

        Assert.False(string.IsNullOrEmpty(session.Token));
        Assert.Equal(user.Id, session.UserId);
        _twoFactorMock.Verify(t => t.VerifyCode(user, "123456"), Times.Once);
    }

    [Fact]
    public async Task VerifyTwoFactor_TheSessionItReturns_IsALoginToken_NotAChallenge()
    {
        TwoFactorUser();
        var challenge = await Challenge();

        var session = await _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" });

        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(session.Token).Claims.ToList();
        Assert.DoesNotContain(claims, c => c.Type == "purpose");
        Assert.Contains(claims, c => c.Type == "iat");
    }

    [Fact]
    public async Task VerifyTwoFactor_WithAWrongCode_PassesTheErrorOn_AndReturnsNoSession()
    {
        var user = TwoFactorUser();
        _twoFactorMock.Setup(t => t.VerifyCode(user, "000000")).ThrowsAsync(new ValidationException("Código incorrecto o vencido."));
        var challenge = await Challenge();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "000000" }));

        Assert.Equal("Código incorrecto o vencido.", ex.Message);
    }

    [Fact]
    public async Task VerifyTwoFactor_WhenLocked_PassesTheLockOn()
    {
        var user = TwoFactorUser();
        _twoFactorMock.Setup(t => t.VerifyCode(user, It.IsAny<string>())).ThrowsAsync(new TooManyRequestsException(600));
        var challenge = await Challenge();

        await Assert.ThrowsAsync<TooManyRequestsException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" }));
    }

    [Fact]
    public async Task VerifyTwoFactor_AChallengeExpiresAfterFiveMinutes()
    {
        TwoFactorUser();
        var challenge = await Challenge();

        _clock.Advance(TimeSpan.FromMinutes(4));
        await _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" });

        _clock.Advance(TimeSpan.FromMinutes(2));
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" }));
        Assert.Equal("Tu verificación venció, ingresá de nuevo.", ex.Message);
    }

    [Fact]
    public async Task VerifyTwoFactor_ALoginTokenIsNotAChallenge()
    {
        var user = ExistingUser();
        _userRepositoryMock.Setup(r => r.GetByEmail("demo@demo.com")).ReturnsAsync(user);
        _userRepositoryMock.Setup(r => r.Get(user.Id)).ReturnsAsync(user);
        var session = (await _authService.SignIn(new SignInRequest { Email = "demo@demo.com", Password = Password })).Token!;
        user.TwoFactorEnabled = true;

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = session, Code = "123456" }));

        _twoFactorMock.Verify(t => t.VerifyCode(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    public async Task VerifyTwoFactor_GarbageIsNotAChallenge(string garbage)
    {
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = garbage, Code = "123456" }));
    }

    [Fact]
    public async Task VerifyTwoFactor_AChallengeSignedWithAnotherKey_IsRefused()
    {
        TwoFactorUser();
        var other = new AuthService(
            _userRepositoryMock.Object, _passwordHashMock.Object, _backgroundMock.Object,
            Options.Create(new JwtOptions { Key = new string('z', 40), Issuer = "Gastos", Audience = "GastosClients" }),
            _twoFactorMock.Object, _clock);
        var foreign = (await other.SignIn(new SignInRequest { Email = "demo@demo.com", Password = Password })).ChallengeToken!;

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = foreign, Code = "123456" }));
    }

    [Fact]
    public async Task VerifyTwoFactor_AChallengeBeforeAPasswordChange_IsRefused()
    {
        var user = TwoFactorUser();
        var challenge = await Challenge();
        _clock.Advance(TimeSpan.FromSeconds(10));
        user.PasswordChangedAt = _clock.GetUtcNow();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" }));
    }

    [Fact]
    public async Task VerifyTwoFactor_IfTheTwoFactorWasTurnedOffMeanwhile_OrTheUserIsGone_TheChallengeIsRefused()
    {
        var user = TwoFactorUser();
        var challenge = await Challenge();

        user.TwoFactorEnabled = false;
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" }));

        user.TwoFactorEnabled = true;
        _userRepositoryMock.Setup(r => r.Get(user.Id)).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = "123456" }));
    }

    [Theory]
    [InlineData("", "123456")]
    [InlineData("abc", "")]
    public async Task VerifyTwoFactor_Throws_WhenARequiredFieldIsMissing(string challenge, string code)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _authService.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = code }));
    }
}
