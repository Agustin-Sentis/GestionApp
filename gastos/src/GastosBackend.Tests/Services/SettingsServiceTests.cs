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

public class SettingsServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
    private const string CurrentPassword = "Demo1234!";

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUserTokenRepository> _tokenRepositoryMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<IPasswordHash> _passwordHashMock = new();
    private readonly TestClock _clock = new(Start);
    private readonly SettingsService _service;
    private readonly User _user = new User { Id = Guid.NewGuid(), Email = "a@x.com", DollarType = DollarType.Official };

    /// <summary>The tokens "stored" by the fake repository, so a test can see what was saved and what a lookup finds.</summary>
    private readonly List<UserToken> _tokens = new();

    public SettingsServiceTests()
    {
        _service = new SettingsService(
            _userRepositoryMock.Object,
            _tokenRepositoryMock.Object,
            _emailSenderMock.Object,
            _passwordHashMock.Object,
            new EmailOptions { AppBaseUrl = "https://dualbalance.example/" },
            _clock);
        _userRepositoryMock.Setup(r => r.Get(_user.Id)).ReturnsAsync(_user);
        _user.Password = "stored-hash";
        _passwordHashMock.Setup(p => p.Verify(CurrentPassword, "stored-hash")).Returns(true);

        _tokenRepositoryMock.Setup(r => r.Add(It.IsAny<UserToken>()))
            .ReturnsAsync((UserToken t) =>
            {
                t.CreatedDate = _clock.GetUtcNow();
                _tokens.Add(t);
                return t;
            });
        _tokenRepositoryMock.Setup(r => r.GetOpenByUser(_user.Id, It.IsAny<UserTokenPurpose>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((Guid _, UserTokenPurpose purpose, DateTimeOffset now) =>
                _tokens.Where(t => t.UserId == _user.Id && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now)
                    .OrderByDescending(t => t.CreatedDate).ToList());
        _tokenRepositoryMock.Setup(r => r.GetOpen(UserTokenPurpose.EmailConfirmation, It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((UserTokenPurpose _, string hash, DateTimeOffset now) =>
                _tokens.FirstOrDefault(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now));
    }

    /// <summary>The token of the last confirmation mail sent, taken from its link.</summary>
    private string LastTokenSent()
    {
        var mail = (EmailMessage)_emailSenderMock.Invocations.Last(i => i.Method.Name == nameof(IEmailSender.Send)).Arguments[0];
        var link = mail.TextBody.Split('\n').Single(line => line.Contains("/confirmar-correo?token="));
        return Uri.UnescapeDataString(link.Split("token=")[1]);
    }

    [Theory]
    [InlineData(DollarType.Official)]
    [InlineData(DollarType.Blue)]
    [InlineData(DollarType.Mep)]
    public async Task Get_ReturnsTheDollarTheUserChose(DollarType chosen)
    {
        _user.DollarType = chosen;

        var settings = await _service.Get(_user.Id);

        Assert.Equal(chosen, settings.DollarType);
    }

    [Fact]
    public async Task Get_Throws_WhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.Get(Guid.NewGuid()));
    }

    [Fact]
    public async Task Get_Throws_WhenTheUserWasDeleted()
    {
        _user.IsDeleted = true;

        await Assert.ThrowsAsync<NotFoundException>(() => _service.Get(_user.Id));
    }

    [Theory]
    [InlineData(DollarType.Blue)]
    [InlineData(DollarType.Mep)]
    [InlineData(DollarType.Official)]
    public async Task Update_SavesTheNewDollar_AndReturnsIt(DollarType chosen)
    {
        _user.DollarType = chosen == DollarType.Official ? DollarType.Blue : DollarType.Official;

        var settings = await _service.Update(new SettingsRequest { DollarType = chosen }, _user.Id);

        Assert.Equal(chosen, settings.DollarType);
        Assert.Equal(chosen, _user.DollarType);
        _userRepositoryMock.Verify(r => r.Update(_user), Times.Once);
    }

    [Fact]
    public async Task Update_Throws_AndSavesNothing_WhenTheDollarIsNotOneOfTheThree()
    {
        var request = new SettingsRequest { DollarType = (DollarType)7 };

        await Assert.ThrowsAsync<ValidationException>(() => _service.Update(request, _user.Id));

        Assert.Equal(DollarType.Official, _user.DollarType);
        _userRepositoryMock.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Update_Throws_WhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.Update(new SettingsRequest { DollarType = DollarType.Blue }, Guid.NewGuid()));
    }

    [Fact]
    public async Task Update_OnlyTouchesTheDollar_NotTheEmailOrThePassword()
    {
        _user.Password = "hash";

        await _service.Update(new SettingsRequest { DollarType = DollarType.Mep }, _user.Id);

        Assert.Equal("a@x.com", _user.Email);
        Assert.Equal("hash", _user.Password);
    }

    [Fact]
    public async Task Get_ReportsWhetherTheTwoFactorIsOn_AndNeverTheSecret()
    {
        Assert.False((await _service.Get(_user.Id)).TwoFactorEnabled);

        _user.TwoFactorEnabled = true;
        _user.TwoFactorSecret = "protected-secret";

        var settings = await _service.Get(_user.Id);

        Assert.True(settings.TwoFactorEnabled);
        Assert.DoesNotContain("protected-secret", System.Text.Json.JsonSerializer.Serialize(settings));
    }

    [Fact]
    public async Task EveryAnswerOfTheSettings_CarriesTheTwoFactorState()
    {
        _user.TwoFactorEnabled = true;

        Assert.True((await _service.Update(new SettingsRequest { DollarType = DollarType.Blue }, _user.Id)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Get_ReturnsTheEmailAndTheAlertState()
    {
        _user.BudgetEmailAlerts = true;
        _user.EmailConfirmedAt = Start;

        var settings = await _service.Get(_user.Id);

        Assert.Equal("a@x.com", settings.Email);
        Assert.True(settings.BudgetEmailAlerts);
        Assert.True(settings.EmailConfirmed);
    }

    [Fact]
    public async Task Get_SaysTheAlertsAreOffAndTheEmailIsNotConfirmed_ForANewUser()
    {
        var settings = await _service.Get(_user.Id);

        Assert.False(settings.BudgetEmailAlerts);
        Assert.False(settings.EmailConfirmed);
    }

    // ---------- Budget alerts: the switch and the confirmation of the address ----------

    [Fact]
    public async Task Enable_SendsTheConfirmationMail_SavesTheFlag_AndKeepsOnlyTheHashOfTheToken()
    {
        var settings = await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);

        Assert.True(settings.BudgetEmailAlerts);
        Assert.False(settings.EmailConfirmed);
        Assert.True(_user.BudgetEmailAlerts);

        var mail = (EmailMessage)_emailSenderMock.Invocations.Single(i => i.Method.Name == nameof(IEmailSender.Send)).Arguments[0];
        Assert.Equal("a@x.com", mail.To);
        Assert.Contains("https://dualbalance.example/confirmar-correo?token=", mail.TextBody);

        var stored = Assert.Single(_tokens);
        var token = LastTokenSent();
        Assert.Equal(SecureToken.Hash(token), stored.TokenHash);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(Start + TimeSpan.FromHours(24), stored.ExpiresAt);
        Assert.Equal(UserTokenPurpose.EmailConfirmation, stored.Purpose);
    }

    [Fact]
    public async Task Enable_WhenTheEmailIsAlreadyConfirmed_SendsNothing()
    {
        _user.EmailConfirmedAt = Start.AddDays(-3);

        var settings = await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);

        Assert.True(settings.BudgetEmailAlerts);
        Assert.True(settings.EmailConfirmed);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
        Assert.Empty(_tokens);
    }

    [Fact]
    public async Task Enable_WhenTheMailFails_SavesNothing_AndThrows()
    {
        _emailSenderMock.Setup(s => s.Send(It.IsAny<EmailMessage>())).ThrowsAsync(new ExternalServiceException("down"));

        await Assert.ThrowsAsync<ExternalServiceException>(
            () => _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id));

        Assert.False(_user.BudgetEmailAlerts);
        Assert.Empty(_tokens);
        _userRepositoryMock.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Enable_TwiceWithinAMinute_ThrowsTheSecondTime_AndSendsOnlyOneMail()
    {
        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);
        _clock.Advance(TimeSpan.FromSeconds(59));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id));

        Assert.Equal("Esperá un minuto antes de pedir otro correo.", ex.Message);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Once);
        Assert.Single(_tokens);
    }

    [Fact]
    public async Task Enable_AfterAMinute_SendsANewMail_AndOnlyTheNewLinkWorks()
    {
        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);
        var oldToken = LastTokenSent();
        _clock.Advance(TimeSpan.FromSeconds(61));

        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);
        var newToken = LastTokenSent();

        Assert.NotEqual(oldToken, newToken);
        await Assert.ThrowsAsync<ValidationException>(() => _service.ConfirmEmail(new ConfirmEmailRequest { Token = oldToken }));
        await _service.ConfirmEmail(new ConfirmEmailRequest { Token = newToken });
        Assert.NotNull(_user.EmailConfirmedAt);
    }

    [Fact]
    public async Task Disable_SavesFalse_AndSendsNothing()
    {
        _user.BudgetEmailAlerts = true;

        var settings = await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = false }, _user.Id);

        Assert.False(settings.BudgetEmailAlerts);
        Assert.False(_user.BudgetEmailAlerts);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task SetBudgetAlerts_Throws_WhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, Guid.NewGuid()));
    }

    // ---------- The confirmation mail on its own: the sign up and the "Reenviar" button ----------

    [Fact]
    public async Task ResendConfirmation_SendsTheMail_AndStoresOnlyTheHash_WhenTheEmailIsNotConfirmed()
    {
        await _service.ResendConfirmation(_user.Id);

        var mail = (EmailMessage)_emailSenderMock.Invocations.Single(i => i.Method.Name == nameof(IEmailSender.Send)).Arguments[0];
        Assert.Equal("a@x.com", mail.To);
        var stored = Assert.Single(_tokens);
        Assert.Equal(SecureToken.Hash(LastTokenSent()), stored.TokenHash);
        Assert.Equal(UserTokenPurpose.EmailConfirmation, stored.Purpose);
        // Only the mail: the alerts switch is not touched
        Assert.False(_user.BudgetEmailAlerts);
        _userRepositoryMock.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ResendConfirmation_DoesNothing_WhenTheEmailIsAlreadyConfirmed()
    {
        _user.EmailConfirmedAt = Start.AddDays(-1);

        await _service.ResendConfirmation(_user.Id);

        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
        Assert.Empty(_tokens);
    }

    [Fact]
    public async Task ResendConfirmation_Throws_WhenAMailWasSentLessThanAMinuteAgo()
    {
        await _service.ResendConfirmation(_user.Id);
        _clock.Advance(TimeSpan.FromSeconds(30));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ResendConfirmation(_user.Id));

        Assert.Equal("Esperá un minuto antes de pedir otro correo.", ex.Message);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Once);
    }

    [Fact]
    public async Task ResendConfirmation_AfterAMinute_OnlyTheNewLinkWorks()
    {
        await _service.ResendConfirmation(_user.Id);
        var oldToken = LastTokenSent();
        _clock.Advance(TimeSpan.FromSeconds(61));

        await _service.ResendConfirmation(_user.Id);

        await Assert.ThrowsAsync<ValidationException>(() => _service.ConfirmEmail(new ConfirmEmailRequest { Token = oldToken }));
        await _service.ConfirmEmail(new ConfirmEmailRequest { Token = LastTokenSent() });
        Assert.NotNull(_user.EmailConfirmedAt);
    }

    [Fact]
    public async Task ResendConfirmation_WhenTheMailFails_SavesNothing_AndThrows()
    {
        _emailSenderMock.Setup(s => s.Send(It.IsAny<EmailMessage>())).ThrowsAsync(new ExternalServiceException("down"));

        await Assert.ThrowsAsync<ExternalServiceException>(() => _service.ResendConfirmation(_user.Id));

        Assert.Empty(_tokens);
    }

    [Fact]
    public async Task ResendConfirmation_Throws_WhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ResendConfirmation(Guid.NewGuid()));
    }

    // ---------- Changing the address while it is not confirmed ----------

    private static ChangeEmailRequest Change(string email = "nuevo@x.com", string password = CurrentPassword) =>
        new() { Email = email, Password = password };

    /// <summary>A recovery link already sent to the address the user is about to leave.</summary>
    private UserToken AddOpenToken(UserTokenPurpose purpose)
    {
        var token = new UserToken
        {
            Id = Guid.NewGuid(), UserId = _user.Id, Purpose = purpose, TokenHash = Guid.NewGuid().ToString(),
            ExpiresAt = Start.AddHours(1), CreatedDate = Start
        };
        _tokens.Add(token);
        return token;
    }

    [Fact]
    public async Task ChangeEmail_ReplacesTheAddress_MailsTheNewOne_AndLeavesItUnconfirmed()
    {
        var settings = await _service.ChangeEmail(Change(" Nuevo@X.com "), _user.Id);

        Assert.Equal("nuevo@x.com", _user.Email);
        Assert.Equal("nuevo@x.com", settings.Email);
        Assert.False(settings.EmailConfirmed);
        Assert.Null(_user.EmailConfirmedAt);
        var mail = (EmailMessage)_emailSenderMock.Invocations.Single(i => i.Method.Name == nameof(IEmailSender.Send)).Arguments[0];
        Assert.Equal("nuevo@x.com", mail.To);
        var stored = Assert.Single(_tokens, t => t.UsedAt == null);
        Assert.Equal(SecureToken.Hash(LastTokenSent()), stored.TokenHash);
        _userRepositoryMock.Verify(r => r.Update(_user), Times.Once);
    }

    [Fact]
    public async Task ChangeEmail_Throws_WhenTheCurrentAddressIsAlreadyConfirmed()
    {
        _user.EmailConfirmedAt = Start.AddDays(-1);

        await Assert.ThrowsAsync<ConflictException>(() => _service.ChangeEmail(Change(), _user.Id));

        Assert.Equal("a@x.com", _user.Email);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task ChangeEmail_WithAWrongPassword_Throws400_NotUnauthorized_AndChangesNothing()
    {
        // A 401 would close the session of the user in the app: a mistyped password must not do that
        await Assert.ThrowsAsync<ValidationException>(() => _service.ChangeEmail(Change(password: "Otra1234!"), _user.Id));

        Assert.Equal("a@x.com", _user.Email);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
        _userRepositoryMock.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ChangeEmail_Throws_WhenAnotherAccountHasTheAddress()
    {
        _userRepositoryMock.Setup(r => r.GetByEmail("nuevo@x.com")).ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "nuevo@x.com" });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.ChangeEmail(Change(), _user.Id));

        Assert.Equal("Ya existe un usuario con ese email.", ex.Message);
        Assert.Equal("a@x.com", _user.Email);
        _emailSenderMock.Verify(s => s.Send(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    public async Task ChangeEmail_Throws_ForAnInvalidAddress(string email)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ChangeEmail(Change(email), _user.Id));

        Assert.Equal("a@x.com", _user.Email);
    }

    [Fact]
    public async Task ChangeEmail_Throws_WithoutAPassword()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ChangeEmail(Change(password: ""), _user.Id));

        _passwordHashMock.Verify(p => p.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangeEmail_ClosesTheOpenConfirmationAndRecoveryLinks_OfTheOldAddress()
    {
        // The link mailed to the old address must not confirm the new one, nor let its reader reset the password
        var oldConfirmation = AddOpenToken(UserTokenPurpose.EmailConfirmation);
        var oldReset = AddOpenToken(UserTokenPurpose.PasswordReset);
        _clock.Advance(TimeSpan.FromMinutes(5));

        await _service.ChangeEmail(Change(), _user.Id);

        Assert.NotNull(oldConfirmation.UsedAt);
        Assert.NotNull(oldReset.UsedAt);
        await _service.ConfirmEmail(new ConfirmEmailRequest { Token = LastTokenSent() });
        Assert.NotNull(_user.EmailConfirmedAt);
    }

    [Fact]
    public async Task ChangeEmail_RightAfterTheSignUp_IsNotHeldBackByTheMinuteBetweenMails()
    {
        // The typical case: the mail of the sign up went to a mistyped address a few seconds ago
        await _service.ResendConfirmation(_user.Id);
        _clock.Advance(TimeSpan.FromSeconds(10));

        await _service.ChangeEmail(Change(), _user.Id);

        Assert.Equal("nuevo@x.com", _user.Email);
        Assert.Equal(2, _emailSenderMock.Invocations.Count(i => i.Method.Name == nameof(IEmailSender.Send)));
    }

    [Fact]
    public async Task ChangeEmail_ToTheSameAddressInOtherCase_IsNotAConflict_AndKeepsTheMinuteBetweenMails()
    {
        await _service.ResendConfirmation(_user.Id);
        _clock.Advance(TimeSpan.FromSeconds(10));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ChangeEmail(Change(" A@X.com "), _user.Id));

        // Nothing to take over: the address is the same one, so it works as a "Reenviar" (and "Reenviar" waits a minute)
        Assert.Equal("Esperá un minuto antes de pedir otro correo.", ex.Message);
        Assert.Equal("a@x.com", _user.Email);
        _userRepositoryMock.Verify(r => r.GetByEmail(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangeEmail_WhenTheMailFails_SavesNothing_AndThrows()
    {
        var oldConfirmation = AddOpenToken(UserTokenPurpose.EmailConfirmation);
        _emailSenderMock.Setup(s => s.Send(It.IsAny<EmailMessage>())).ThrowsAsync(new ExternalServiceException("down"));

        await Assert.ThrowsAsync<ExternalServiceException>(() => _service.ChangeEmail(Change(), _user.Id));

        Assert.Equal("a@x.com", _user.Email);
        _userRepositoryMock.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        Assert.Null(oldConfirmation.UsedAt);
        Assert.Single(_tokens);
    }

    [Fact]
    public async Task ChangeEmail_WhenTheAddressWasTakenInTheMeantime_IsAConflict_AndStoresNoLink()
    {
        // The check said "free", but the unique index rejected the update: two accounts racing for the same address
        _userRepositoryMock.Setup(r => r.Update(_user)).ThrowsAsync(new DuplicateException("duplicate"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.ChangeEmail(Change(), _user.Id));

        Assert.Equal("Ya existe un usuario con ese email.", ex.Message);
        // The mail already left, but its link was never stored: it works for nobody
        Assert.Empty(_tokens);
    }

    [Fact]
    public async Task ChangeEmail_Throws_WhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ChangeEmail(Change(), Guid.NewGuid()));
    }

    [Fact]
    public async Task ConfirmEmail_ConfirmsTheAddress_AndTheTokenWorksOnlyOnce()
    {
        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);
        var token = LastTokenSent();

        await _service.ConfirmEmail(new ConfirmEmailRequest { Token = token });

        Assert.Equal(Start, _user.EmailConfirmedAt);
        Assert.NotNull(Assert.Single(_tokens).UsedAt);
        // The alerts were turned on before the click: they stay on
        Assert.True(_user.BudgetEmailAlerts);
        await Assert.ThrowsAsync<ValidationException>(() => _service.ConfirmEmail(new ConfirmEmailRequest { Token = token }));
    }

    [Fact]
    public async Task ConfirmEmail_DoesNotTurnTheAlertsOn_WhenTheUserTurnedThemOffMeanwhile()
    {
        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);
        var token = LastTokenSent();
        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = false }, _user.Id);

        await _service.ConfirmEmail(new ConfirmEmailRequest { Token = token });

        Assert.NotNull(_user.EmailConfirmedAt);
        Assert.False(_user.BudgetEmailAlerts);
    }

    [Fact]
    public async Task ConfirmEmail_Throws_WhenTheLinkExpired()
    {
        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);
        var token = LastTokenSent();
        _clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ConfirmEmail(new ConfirmEmailRequest { Token = token }));

        Assert.Equal("El enlace venció o ya se usó.", ex.Message);
        Assert.Null(_user.EmailConfirmedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    [InlineData("' OR 1=1 --")]
    public async Task ConfirmEmail_Throws_TheSameError_ForEmptyAndUnknownTokens(string? token)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ConfirmEmail(new ConfirmEmailRequest { Token = token }));

        Assert.Equal("El enlace venció o ya se usó.", ex.Message);
        Assert.Null(_user.EmailConfirmedAt);
    }

    [Fact]
    public async Task ConfirmEmail_Throws_WithoutLookingUpAHugeToken()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.ConfirmEmail(new ConfirmEmailRequest { Token = new string('a', 5000) }));

        _tokenRepositoryMock.Verify(
            r => r.GetOpen(It.IsAny<UserTokenPurpose>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmail_Throws_WhenTheUserOfTheTokenWasDeleted()
    {
        await _service.SetBudgetAlerts(new BudgetAlertsRequest { Enabled = true }, _user.Id);
        var token = LastTokenSent();
        _user.IsDeleted = true;

        await Assert.ThrowsAsync<ValidationException>(() => _service.ConfirmEmail(new ConfirmEmailRequest { Token = token }));
    }
}
