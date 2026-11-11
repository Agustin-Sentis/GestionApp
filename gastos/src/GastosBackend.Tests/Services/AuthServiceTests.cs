using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Infrastructure.ExternalService;
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
        _authService = new AuthService(_userRepositoryMock.Object, _passwordHashMock.Object, jwtOptions);
    }

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
}
