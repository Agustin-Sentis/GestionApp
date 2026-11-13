using System.IdentityModel.Tokens.Jwt;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Infrastructure;
using GastosBackend.Infrastructure.ExternalService;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.IntegrationTests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GastosBackend.IntegrationTests.Services;

/// <summary>
/// The password recovery from the first request to the closed sessions, with the real services and repositories on a real
/// PostgreSQL and the real clock: only the mail is replaced, by one that keeps what it was asked to send. Each step uses its
/// own context, as every request of the API does.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PasswordResetFlowTests
{
    private const string OldPassword = "Vieja1234!";
    private const string NewPassword = "Nueva1234!";

    private readonly PostgresFixture _postgres;

    public PasswordResetFlowTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = new();

        public Task Send(EmailMessage message)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Runs the background work as soon as it is asked for, against a service of its own (a new context).</summary>
    private sealed class InlineRunner : IBackgroundRunner
    {
        public Func<IPasswordResetService>? NewService { get; set; }

        public void Run<TService>(Func<TService, Task> work) where TService : notnull
        {
            work((TService)NewService!()).GetAwaiter().GetResult();
        }
    }

    /// <summary>For the sign in of these tests: the confirmation mail of a sign up is not what they are about.</summary>
    private sealed class NoBackgroundWork : IBackgroundRunner
    {
        public void Run<TService>(Func<TService, Task> work) where TService : notnull
        {
        }
    }

    private sealed class Scenario
    {
        public required TestDatabase Database { get; init; }
        public required User User { get; init; }
        public required RecordingEmailSender Mail { get; init; }
        public required InlineRunner Runner { get; init; }

        /// <summary>A service on a new context, like the one a request gets.</summary>
        public (IPasswordResetService Service, GastosDbContext Context) NewService()
        {
            var context = Database.CreateContext();
            var service = new PasswordResetService(
                new UserRepository(context),
                new UserTokenRepository(context),
                Mail,
                new PasswordHash(),
                Runner,
                new EmailOptions { AppBaseUrl = "https://dualbalance.example" },
                TimeProvider.System);
            return (service, context);
        }

        public async Task<AuthService> NewAuthService()
        {
            var context = Database.CreateContext();
            return await Task.FromResult(AuthWiring.NewAuth(
                context, new EphemeralDataProtectionProvider(), TimeProvider.System, new NoBackgroundWork()));
        }

        public UserSessionValidator NewValidator() => new(new UserRepository(Database.CreateContext()));
    }

    private async Task<Scenario> Setup()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();

        await using (var context = database.CreateContext())
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(), Email = "ana@example.com", Password = new PasswordHash().Hash(OldPassword),
                CreatedDate = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using var read = database.CreateContext();
        var user = await read.Users.SingleAsync();
        var scenario = new Scenario { Database = database, User = user, Mail = new RecordingEmailSender(), Runner = new InlineRunner() };
        scenario.Runner.NewService = () => scenario.NewService().Service;
        return scenario;
    }

    private static async Task Request(Scenario scenario, string email = "ana@example.com")
    {
        var (service, context) = scenario.NewService();
        await using (context)
        {
            await service.RequestReset(new ForgotPasswordRequest { Email = email });
        }
    }

    private static async Task Reset(Scenario scenario, string? token, string password = NewPassword)
    {
        var (service, context) = scenario.NewService();
        await using (context)
        {
            await service.ResetPassword(new ResetPasswordRequest { Token = token, NewPassword = password });
        }
    }

    private static string TokenOf(EmailMessage mail)
    {
        var link = mail.TextBody.Split('\n').Single(line => line.Contains("/restablecer-contrasena?token="));
        return Uri.UnescapeDataString(link.Split("token=")[1]);
    }

    private static long IssuedAt(string jwt) =>
        long.Parse(new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims.Single(c => c.Type == "iat").Value);

    [Fact]
    public async Task TheWholeFlow_ChangesThePassword_AndClosesTheSessionsOpenedBefore()
    {
        var scenario = await Setup();

        // The user is signed in somewhere
        var before = await (await scenario.NewAuthService()).SignIn(new SignInRequest { Email = "ana@example.com", Password = OldPassword });
        await Task.Delay(1100); // "iat" has whole seconds: the reset has to happen in a later one

        await Request(scenario);
        var resetMail = Assert.Single(scenario.Mail.Sent);
        var token = TokenOf(resetMail);
        Assert.Equal("ana@example.com", resetMail.To);

        // Only the hash is in the database, never the token
        var stored = await scenario.Database.QueryAsync("SELECT \"TokenHash\", \"UsedAt\" FROM \"UserTokens\"");
        var row = Assert.Single(stored);
        Assert.Equal(SecureToken.Hash(token), (string)row[0]);
        Assert.DoesNotContain(token, (string)row[0]);
        Assert.Equal(DBNull.Value, row[1]);

        await Reset(scenario, token);

        await using var check = scenario.Database.CreateContext();
        var user = await check.Users.SingleAsync();
        Assert.NotNull(user.PasswordChangedAt);
        Assert.NotNull(user.EmailConfirmedAt);
        Assert.NotNull((await check.UserTokens.SingleAsync()).UsedAt);

        // The new password signs in, the old one does not
        var after = await (await scenario.NewAuthService()).SignIn(new SignInRequest { Email = "ana@example.com", Password = NewPassword });
        await Assert.ThrowsAsync<UnauthorizedException>(async () =>
            await (await scenario.NewAuthService()).SignIn(new SignInRequest { Email = "ana@example.com", Password = OldPassword }));

        // The session of before is closed and the one of now is not
        Assert.False(await scenario.NewValidator().IsValid(user.Id, IssuedAt(before.Token!)));
        Assert.True(await scenario.NewValidator().IsValid(user.Id, IssuedAt(after.Token!)));

        // And the user was told
        Assert.Equal("Tu contraseña de DualBalance fue cambiada", scenario.Mail.Sent.Last().Subject);
    }

    [Fact]
    public async Task TheLink_WorksOnlyOnce_AgainstTheDatabase()
    {
        var scenario = await Setup();
        await Request(scenario);
        var token = TokenOf(Assert.Single(scenario.Mail.Sent));
        await Reset(scenario, token);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Reset(scenario, token, "Otra12345!"));

        Assert.Equal("El enlace venció o ya se usó.", ex.Message);
    }

    [Fact]
    public async Task AWeakPassword_DoesNotUseUpTheLink_AgainstTheDatabase()
    {
        var scenario = await Setup();
        await Request(scenario);
        var token = TokenOf(Assert.Single(scenario.Mail.Sent));

        await Assert.ThrowsAsync<ValidationException>(() => Reset(scenario, token, "debil"));
        await Reset(scenario, token);

        await using var check = scenario.Database.CreateContext();
        Assert.NotNull((await check.Users.SingleAsync()).PasswordChangedAt);
    }

    [Fact]
    public async Task AnExpiredLink_IsRefused_AgainstTheDatabase()
    {
        var scenario = await Setup();
        await Request(scenario);
        var token = TokenOf(Assert.Single(scenario.Mail.Sent));
        await scenario.Database.ExecuteAsync(
            "UPDATE \"UserTokens\" SET \"ExpiresAt\" = @past", new NpgsqlParameter("past", DateTimeOffset.UtcNow.AddSeconds(-1)));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Reset(scenario, token));

        Assert.Equal("El enlace venció o ya se usó.", ex.Message);
    }

    [Fact]
    public async Task TwoRequestsWithinAMinute_SendOneMail_WithTheRealTimestamps()
    {
        var scenario = await Setup();

        await Request(scenario);
        await Request(scenario);

        Assert.Single(scenario.Mail.Sent);
        Assert.Equal(1L, await scenario.Database.ScalarAsync<long>("SELECT COUNT(*) FROM \"UserTokens\""));
    }

    [Fact]
    public async Task AnUnknownEmail_SendsNothing_AndSavesNothing()
    {
        var scenario = await Setup();

        await Request(scenario, "nadie@example.com");

        Assert.Empty(scenario.Mail.Sent);
        Assert.Equal(0L, await scenario.Database.ScalarAsync<long>("SELECT COUNT(*) FROM \"UserTokens\""));
    }

    [Fact]
    public async Task ResettingOneUsersPassword_DoesNotCloseTheSessionsOfAnother()
    {
        var scenario = await Setup();
        await using (var context = scenario.Database.CreateContext())
        {
            context.Users.Add(new User { Id = Guid.NewGuid(), Email = "otra@example.com", Password = "x", CreatedDate = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
        }

        await Request(scenario);
        await Reset(scenario, TokenOf(Assert.Single(scenario.Mail.Sent)));

        await using var check = scenario.Database.CreateContext();
        var other = await check.Users.SingleAsync(u => u.Email == "otra@example.com");
        Assert.Null(other.PasswordChangedAt);
        Assert.True(await scenario.NewValidator().IsValid(other.Id, 1));
    }
}
