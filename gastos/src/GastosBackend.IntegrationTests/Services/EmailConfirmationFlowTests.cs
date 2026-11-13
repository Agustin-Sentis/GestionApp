using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using GastosBackend.Infrastructure;
using GastosBackend.Infrastructure.ExternalService;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GastosBackend.IntegrationTests.Services;

/// <summary>
/// The confirmation of the address from the sign up to the click on the link, with the real services and repositories on a real
/// PostgreSQL and the real clock: only the mail is replaced, by one that keeps what it was asked to send. Each step uses its own
/// context, as every request of the API does.
/// </summary>
[Collection(PostgresCollection.Name)]
public class EmailConfirmationFlowTests
{
    private const string Password = "Demo1234!";

    private readonly PostgresFixture _postgres;

    public EmailConfirmationFlowTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = new();

        /// <summary>When set, the mail "cannot be sent".</summary>
        public bool Fails { get; set; }

        public Task Send(EmailMessage message)
        {
            if (Fails)
            {
                throw new ExternalServiceException("down");
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Runs the background work as soon as it is asked for, against a service of its own (a new context). Like the real runner,
    /// it never lets a failure out: nobody is waiting for the result.
    /// </summary>
    private sealed class InlineRunner : IBackgroundRunner
    {
        public Func<ISettingsService>? NewService { get; set; }

        public void Run<TService>(Func<TService, Task> work) where TService : notnull
        {
            try
            {
                work((TService)NewService!()).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
            }
        }
    }

    private sealed class Scenario
    {
        public required TestDatabase Database { get; init; }
        public required RecordingEmailSender Mail { get; init; }
        public required InlineRunner Runner { get; init; }

        /// <summary>A service on a new context, like the one a request gets.</summary>
        public (ISettingsService Service, GastosDbContext Context) NewSettings()
        {
            var context = Database.CreateContext();
            var service = new SettingsService(
                new UserRepository(context),
                new UserTokenRepository(context),
                Mail,
                new PasswordHash(),
                new EmailOptions { AppBaseUrl = "https://dualbalance.example" },
                TimeProvider.System);
            return (service, context);
        }

        public async Task<GastosBackend.Application.dtos.Responses.AuthResponse> SignUp(string email = "ana@example.com")
        {
            await using var context = Database.CreateContext();
            var auth = AuthWiring.NewAuth(context, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), TimeProvider.System, Runner);
            return await auth.SignUp(new SignUpRequest { Email = email, Password = Password });
        }

        public async Task Confirm(string? token)
        {
            var (service, context) = NewSettings();
            await using (context)
            {
                await service.ConfirmEmail(new ConfirmEmailRequest { Token = token });
            }
        }

        public async Task ChangeEmail(Guid userId, string email, string password = Password)
        {
            var (service, context) = NewSettings();
            await using (context)
            {
                await service.ChangeEmail(new ChangeEmailRequest { Email = email, Password = password }, userId);
            }
        }

        public async Task Resend(Guid userId)
        {
            var (service, context) = NewSettings();
            await using (context)
            {
                await service.ResendConfirmation(userId);
            }
        }
    }

    private async Task<Scenario> Setup()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();

        var scenario = new Scenario { Database = database, Mail = new RecordingEmailSender(), Runner = new InlineRunner() };
        scenario.Runner.NewService = () => scenario.NewSettings().Service;
        return scenario;
    }

    private static string TokenOf(EmailMessage mail)
    {
        var link = mail.TextBody.Split('\n').Single(line => line.Contains("/confirmar-correo?token="));
        return Uri.UnescapeDataString(link.Split("token=")[1]);
    }

    [Fact]
    public async Task SignUp_MailsTheConfirmationLink_AndTheLinkConfirmsTheAddress()
    {
        var scenario = await Setup();

        var signedUp = await scenario.SignUp();

        var mail = Assert.Single(scenario.Mail.Sent);
        Assert.Equal("ana@example.com", mail.To);
        await using (var before = scenario.Database.CreateContext())
        {
            Assert.Null((await before.Users.SingleAsync()).EmailConfirmedAt);
        }

        await scenario.Confirm(TokenOf(mail));

        await using var check = scenario.Database.CreateContext();
        var user = await check.Users.SingleAsync();
        Assert.Equal(signedUp.UserId, user.Id);
        Assert.NotNull(user.EmailConfirmedAt);
        Assert.NotNull((await check.UserTokens.SingleAsync()).UsedAt);
    }

    [Fact]
    public async Task SignUp_Succeeds_AndTheAccountExists_WhenTheMailCannotBeSent()
    {
        var scenario = await Setup();
        scenario.Mail.Fails = true;

        var signedUp = await scenario.SignUp();

        Assert.False(string.IsNullOrEmpty(signedUp.Token));
        await using var check = scenario.Database.CreateContext();
        Assert.Equal("ana@example.com", (await check.Users.SingleAsync()).Email);
        Assert.Empty(await check.UserTokens.ToListAsync());
    }

    [Fact]
    public async Task Resend_RightAfterTheSignUp_IsRefused_BecauseOfTheMinuteBetweenMails()
    {
        var scenario = await Setup();
        var signedUp = await scenario.SignUp();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => scenario.Resend(signedUp.UserId));

        Assert.Equal("Esperá un minuto antes de pedir otro correo.", ex.Message);
        Assert.Single(scenario.Mail.Sent);
    }

    [Fact]
    public async Task ChangeEmail_AfterATypo_TheOldLinkIsDead_AndTheNewLinkConfirmsTheNewAddress()
    {
        var scenario = await Setup();
        var signedUp = await scenario.SignUp("ana@exmaple.com"); // the typo
        var oldLink = TokenOf(Assert.Single(scenario.Mail.Sent));

        // Seconds after the sign up: the minute between mails does not apply to a new address
        await scenario.ChangeEmail(signedUp.UserId, "ana@example.com");

        Assert.Equal("ana@example.com", scenario.Mail.Sent.Last().To);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => scenario.Confirm(oldLink));
        Assert.Equal("El enlace venció o ya se usó.", ex.Message);
        await using (var unconfirmed = scenario.Database.CreateContext())
        {
            var user = await unconfirmed.Users.SingleAsync();
            Assert.Equal("ana@example.com", user.Email);
            Assert.Null(user.EmailConfirmedAt);
        }

        await scenario.Confirm(TokenOf(scenario.Mail.Sent.Last()));

        await using var check = scenario.Database.CreateContext();
        Assert.NotNull((await check.Users.SingleAsync()).EmailConfirmedAt);
        // Both tokens are spent: the old one was closed, the new one used
        Assert.All(await check.UserTokens.ToListAsync(), t => Assert.NotNull(t.UsedAt));
    }

    [Fact]
    public async Task ChangeEmail_ClosesTheRecoveryLinkSentToTheOldAddress()
    {
        var scenario = await Setup();
        var signedUp = await scenario.SignUp("ana@exmaple.com");
        await using (var context = scenario.Database.CreateContext())
        {
            // The link a "forgot my password" request mailed to the mistyped address
            context.UserTokens.Add(new UserToken
            {
                Id = Guid.NewGuid(), UserId = signedUp.UserId, Purpose = UserTokenPurpose.PasswordReset,
                TokenHash = SecureToken.Hash("a-recovery-link"), ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                CreatedDate = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await scenario.ChangeEmail(signedUp.UserId, "ana@example.com");

        await using var check = scenario.Database.CreateContext();
        var reset = await check.UserTokens.SingleAsync(t => t.Purpose == UserTokenPurpose.PasswordReset);
        Assert.NotNull(reset.UsedAt);
    }

    [Fact]
    public async Task ChangeEmail_WithAWrongPassword_ChangesNothing_AndSendsNothing()
    {
        var scenario = await Setup();
        var signedUp = await scenario.SignUp();

        await Assert.ThrowsAsync<ValidationException>(() => scenario.ChangeEmail(signedUp.UserId, "otro@example.com", "Otra1234!"));

        Assert.Single(scenario.Mail.Sent);
        await using var check = scenario.Database.CreateContext();
        Assert.Equal("ana@example.com", (await check.Users.SingleAsync()).Email);
    }

    [Fact]
    public async Task ChangeEmail_ToAnAddressOfAnotherAccount_IsRefused_AgainstTheUniqueIndex()
    {
        var scenario = await Setup();
        await scenario.SignUp("otra@example.com");
        var signedUp = await scenario.SignUp();
        var mailsBefore = scenario.Mail.Sent.Count;

        await Assert.ThrowsAsync<ConflictException>(() => scenario.ChangeEmail(signedUp.UserId, "OTRA@example.com"));

        Assert.Equal(mailsBefore, scenario.Mail.Sent.Count);
        await using var check = scenario.Database.CreateContext();
        Assert.Equal("ana@example.com", (await check.Users.SingleAsync(u => u.Id == signedUp.UserId)).Email);
    }

    [Fact]
    public async Task ChangeEmail_Refuses_WhenTheCurrentAddressIsConfirmed()
    {
        var scenario = await Setup();
        var signedUp = await scenario.SignUp();
        await scenario.Confirm(TokenOf(Assert.Single(scenario.Mail.Sent)));

        await Assert.ThrowsAsync<ConflictException>(() => scenario.ChangeEmail(signedUp.UserId, "otro@example.com"));

        await using var check = scenario.Database.CreateContext();
        Assert.Equal("ana@example.com", (await check.Users.SingleAsync()).Email);
    }

    [Fact]
    public async Task Resend_WhenTheFirstMailNeverLeft_SendsOne_AndOnlyTheNewLinkWorks()
    {
        var scenario = await Setup();
        scenario.Mail.Fails = true;
        var signedUp = await scenario.SignUp();
        scenario.Mail.Fails = false;

        await scenario.Resend(signedUp.UserId);

        await scenario.Confirm(TokenOf(Assert.Single(scenario.Mail.Sent)));
        await using var check = scenario.Database.CreateContext();
        Assert.NotNull((await check.Users.SingleAsync()).EmailConfirmedAt);
    }
}
