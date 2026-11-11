using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using GastosBackend.Tests.Helpers;
using Moq;

namespace GastosBackend.Tests.Services;

public class PasswordResetServiceTests
{
    private const string NewPassword = "Nueva1234!";
    private const string InvalidLink = "El enlace venció o ya se usó.";
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Takes the work the service wants done in the background, so a test decides when (and whether) it runs.</summary>
    private sealed class CapturingRunner : IBackgroundRunner
    {
        public List<Func<IPasswordResetService, Task>> Jobs { get; } = new();

        public void Run<TService>(Func<TService, Task> work) where TService : notnull
        {
            Jobs.Add(service => work((TService)service));
        }
    }

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUserTokenRepository> _tokenRepositoryMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<IPasswordHash> _passwordHashMock = new();
    private readonly CapturingRunner _runner = new();
    private readonly TestClock _clock = new(Start);
    private readonly PasswordResetService _service;

    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "ana@example.com", Password = "old-hash" };
    private readonly List<UserToken> _tokens = new();

    public PasswordResetServiceTests()
    {
        _service = new PasswordResetService(
            _userRepositoryMock.Object,
            _tokenRepositoryMock.Object,
            _emailSenderMock.Object,
            _passwordHashMock.Object,
            _runner,
            new EmailOptions { AppBaseUrl = "https://dualbalance.example/" },
            _clock);

        _userRepositoryMock.Setup(r => r.GetByEmail("ana@example.com")).ReturnsAsync(_user);
        _userRepositoryMock.Setup(r => r.Get(_user.Id)).ReturnsAsync(_user);
        _passwordHashMock.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => "hash:" + p);

        _tokenRepositoryMock.Setup(r => r.Add(It.IsAny<UserToken>()))
            .ReturnsAsync((UserToken t) =>
            {
                t.CreatedDate = _clock.GetUtcNow();
                _tokens.Add(t);
                return t;
            });
        _tokenRepositoryMock.Setup(r => r.GetOpenByUser(It.IsAny<Guid>(), It.IsAny<UserTokenPurpose>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((Guid userId, UserTokenPurpose purpose, DateTimeOffset now) =>
                _tokens.Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now)
                    .OrderByDescending(t => t.CreatedDate).ToList());
        _tokenRepositoryMock.Setup(r => r.GetOpen(It.IsAny<UserTokenPurpose>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((UserTokenPurpose purpose, string hash, DateTimeOffset now) =>
                _tokens.FirstOrDefault(t => t.Purpose == purpose && t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now));
    }

    private IEnumerable<EmailMessage> Sent() =>
        _emailSenderMock.Invocations.Where(i => i.Method.Name == nameof(IEmailSender.Send)).Select(i => (EmailMessage)i.Arguments[0]);

    /// <summary>Asks for the recovery mail and runs the background job, like the app does. Returns the token of the mail.</summary>
    private async Task<string> RequestAndSend()
    {
        _runner.Jobs.Clear();
        await _service.RequestReset(new ForgotPasswordRequest { Email = "ana@example.com" });
        await _runner.Jobs.Single()(_service);

        var link = Sent().Last().TextBody.Split('\n').Single(line => line.Contains("/restablecer-contrasena?token="));
        return Uri.UnescapeDataString(link.Split("token=")[1]);
    }

    private static ResetPasswordRequest Reset(string? token, string password = NewPassword) =>
        new() { Token = token, NewPassword = password };

    // ---------- Asking for the link ----------

    [Fact]
    public async Task RequestReset_ForARegisteredEmail_SchedulesTheMail_WithoutSendingItInTheRequest()
    {
        await _service.RequestReset(new ForgotPasswordRequest { Email = "ana@example.com" });

        Assert.Single(_runner.Jobs);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task RequestReset_ForAnUnknownEmail_SchedulesNothing_AndDoesNotThrow()
    {
        _userRepositoryMock.Setup(r => r.GetByEmail("nadie@example.com")).ReturnsAsync((User?)null);

        await _service.RequestReset(new ForgotPasswordRequest { Email = "nadie@example.com" });

        Assert.Empty(_runner.Jobs);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task RequestReset_NormalizesTheEmailLikeTheSignIn()
    {
        await _service.RequestReset(new ForgotPasswordRequest { Email = "  ANA@Example.com " });

        _userRepositoryMock.Verify(r => r.GetByEmail("ana@example.com"), Times.Once);
        Assert.Single(_runner.Jobs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-email")]
    public async Task RequestReset_Throws_WhenTheEmailIsEmptyOrHasNoValidFormat(string email)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.RequestReset(new ForgotPasswordRequest { Email = email }));

        _userRepositoryMock.Verify(r => r.GetByEmail(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RequestReset_Throws_WhenTheEmailIsLongerThanAnEmailCanBe()
    {
        var email = new string('a', 250) + "@x.com";

        await Assert.ThrowsAsync<ValidationException>(() => _service.RequestReset(new ForgotPasswordRequest { Email = email }));
    }

    // ---------- The mail (the background half) ----------

    [Fact]
    public async Task SendResetEmail_MailsTheAccount_WithTheLinkOfTheApp_AndKeepsOnlyTheHash()
    {
        var token = await RequestAndSend();

        var mail = Assert.Single(Sent());
        Assert.Equal("ana@example.com", mail.To);
        Assert.Equal("Restablecé tu contraseña en DualBalance", mail.Subject);
        Assert.StartsWith("https://dualbalance.example/restablecer-contrasena?token=", mail.TextBody.Split('\n').Single(l => l.StartsWith("https")));

        var stored = Assert.Single(_tokens);
        Assert.Equal(UserTokenPurpose.PasswordReset, stored.Purpose);
        Assert.Equal(SecureToken.Hash(token), stored.TokenHash);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(Start + TimeSpan.FromHours(1), stored.ExpiresAt);
    }

    [Fact]
    public async Task SendResetEmail_WithinAMinute_SendsAndSavesNothingMore()
    {
        await RequestAndSend();
        _clock.Advance(TimeSpan.FromSeconds(59));

        _runner.Jobs.Clear();
        await _service.RequestReset(new ForgotPasswordRequest { Email = "ana@example.com" });
        await _runner.Jobs.Single()(_service);

        Assert.Single(Sent());
        Assert.Single(_tokens);
    }

    [Fact]
    public async Task SendResetEmail_AfterAMinute_SendsANewOne_AndTheOldLinkStopsWorking()
    {
        var oldToken = await RequestAndSend();
        _clock.Advance(TimeSpan.FromSeconds(61));

        var newToken = await RequestAndSend();

        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(2, Sent().Count());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(oldToken)));
        Assert.Equal(InvalidLink, ex.Message);
        await _service.ResetPassword(Reset(newToken));
    }

    [Fact]
    public async Task SendResetEmail_WhenTheMailFails_Throws_AndSavesNothing()
    {
        _emailSenderMock.Setup(s => s.Send(It.IsAny<EmailMessage>())).ThrowsAsync(new ExternalServiceException("down"));
        await _service.RequestReset(new ForgotPasswordRequest { Email = "ana@example.com" });

        // The request already answered: the failure belongs to the background job, which logs it
        await Assert.ThrowsAsync<ExternalServiceException>(() => _runner.Jobs.Single()(_service));

        Assert.Empty(_tokens);
    }

    [Fact]
    public async Task SendResetEmail_WhenTheMailFails_DoesNotUseUpTheLinkAlreadySent()
    {
        var token = await RequestAndSend();
        _clock.Advance(TimeSpan.FromMinutes(2));
        _emailSenderMock.Setup(s => s.Send(It.IsAny<EmailMessage>())).ThrowsAsync(new ExternalServiceException("down"));

        await Assert.ThrowsAsync<ExternalServiceException>(() => _service.SendResetEmail(_user.Id));

        // The first mail did arrive: its link keeps working
        await _service.ResetPassword(Reset(token));
    }

    [Fact]
    public async Task SendResetEmail_ForADeletedUser_SendsNothing()
    {
        _user.IsDeleted = true;

        await _service.SendResetEmail(_user.Id);

        Assert.Empty(Sent());
        Assert.Empty(_tokens);
    }

    // ---------- Setting the new password ----------

    [Fact]
    public async Task ResetPassword_ChangesThePassword_ClosesTheSessions_ConfirmsTheEmail_AndUsesUpTheLink()
    {
        var token = await RequestAndSend();
        _clock.Advance(TimeSpan.FromMinutes(5));

        await _service.ResetPassword(Reset(token));

        Assert.Equal("hash:" + NewPassword, _user.Password);
        Assert.Equal(_clock.GetUtcNow(), _user.PasswordChangedAt);
        Assert.Equal(_clock.GetUtcNow(), _user.EmailConfirmedAt);
        Assert.NotNull(Assert.Single(_tokens).UsedAt);
        _userRepositoryMock.Verify(r => r.Update(_user), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_TellsTheUserTheirPasswordWasChanged()
    {
        var token = await RequestAndSend();

        await _service.ResetPassword(Reset(token));

        var notice = Sent().Last();
        Assert.Equal("Tu contraseña de DualBalance fue cambiada", notice.Subject);
        Assert.Equal("ana@example.com", notice.To);
        Assert.Contains("https://dualbalance.example/login?modo=recuperar", notice.TextBody);
    }

    [Fact]
    public async Task ResetPassword_KeepsTheDateOfAnEmailThatWasAlreadyConfirmed()
    {
        var confirmedAt = Start.AddDays(-20);
        _user.EmailConfirmedAt = confirmedAt;
        var token = await RequestAndSend();

        await _service.ResetPassword(Reset(token));

        Assert.Equal(confirmedAt, _user.EmailConfirmedAt);
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("sinmayuscula1!")]
    [InlineData("SINMINUSCULA1!")]
    [InlineData("SinNumero!!")]
    [InlineData("SinEspecial1")]
    public async Task ResetPassword_WithAPasswordThatBreaksTheRules_Throws_AndKeepsTheLinkUsable(string weak)
    {
        var token = await RequestAndSend();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(token, weak)));

        Assert.NotEqual(InvalidLink, ex.Message);
        Assert.Equal("old-hash", _user.Password);
        Assert.Null(_user.PasswordChangedAt);
        Assert.Null(Assert.Single(_tokens).UsedAt);
        // The user fixes the password and tries again with the same link
        await _service.ResetPassword(Reset(token));
        Assert.Equal("hash:" + NewPassword, _user.Password);
    }

    [Fact]
    public async Task ResetPassword_WorksUpToTheLastSecondOfTheHour()
    {
        var token = await RequestAndSend();
        _clock.Advance(TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59));

        await _service.ResetPassword(Reset(token));

        Assert.Equal("hash:" + NewPassword, _user.Password);
    }

    [Fact]
    public async Task ResetPassword_FailsOnceTheHourPassed()
    {
        var token = await RequestAndSend();
        _clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(token)));

        Assert.Equal(InvalidLink, ex.Message);
        Assert.Equal("old-hash", _user.Password);
    }

    [Fact]
    public async Task ResetPassword_TheLinkWorksOnlyOnce()
    {
        var token = await RequestAndSend();
        await _service.ResetPassword(Reset(token));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(token, "Otra12345!")));

        Assert.Equal(InvalidLink, ex.Message);
        Assert.Equal("hash:" + NewPassword, _user.Password);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    [InlineData("' OR 1=1 --")]
    public async Task ResetPassword_Throws_TheSameError_ForEmptyAndUnknownTokens(string? token)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(token)));

        Assert.Equal(InvalidLink, ex.Message);
        Assert.Equal("old-hash", _user.Password);
    }

    [Fact]
    public async Task ResetPassword_Throws_WithoutLookingUpAHugeToken()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(new string('a', 5000))));

        _tokenRepositoryMock.Verify(
            r => r.GetOpen(It.IsAny<UserTokenPurpose>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_DoesNotAcceptTheTokenOfAnotherPurpose()
    {
        // A confirmation link must never reset a password
        var (token, hash) = SecureToken.Generate();
        _tokens.Add(new UserToken
        {
            Id = Guid.NewGuid(), UserId = _user.Id, Purpose = UserTokenPurpose.EmailConfirmation, TokenHash = hash,
            ExpiresAt = Start.AddHours(24), CreatedDate = Start
        });

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(token)));

        Assert.Equal(InvalidLink, ex.Message);
        Assert.Equal("old-hash", _user.Password);
    }

    [Fact]
    public async Task ResetPassword_Throws_WhenTheUserOfTheLinkWasDeleted()
    {
        var token = await RequestAndSend();
        _user.IsDeleted = true;

        await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(token)));
    }

    [Fact]
    public async Task ResetPassword_StillChangesThePassword_WhenTheNoticeMailFails()
    {
        var token = await RequestAndSend();
        _emailSenderMock.Setup(s => s.Send(It.IsAny<EmailMessage>())).ThrowsAsync(new ExternalServiceException("down"));

        await _service.ResetPassword(Reset(token));

        Assert.Equal("hash:" + NewPassword, _user.Password);
        Assert.NotNull(_user.PasswordChangedAt);
    }

    [Fact]
    public async Task ResetPassword_UsesUpAnyOtherLinkStillOpen()
    {
        var token = await RequestAndSend();
        var (otherToken, otherHash) = SecureToken.Generate();
        _tokens.Add(new UserToken
        {
            Id = Guid.NewGuid(), UserId = _user.Id, Purpose = UserTokenPurpose.PasswordReset, TokenHash = otherHash,
            ExpiresAt = Start.AddHours(1), CreatedDate = Start.AddSeconds(-1)
        });

        await _service.ResetPassword(Reset(token));

        Assert.All(_tokens, t => Assert.NotNull(t.UsedAt));
        await Assert.ThrowsAsync<ValidationException>(() => _service.ResetPassword(Reset(otherToken, "Otra12345!")));
    }
}
