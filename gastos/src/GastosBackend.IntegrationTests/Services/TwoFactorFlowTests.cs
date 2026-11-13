using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
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
using Microsoft.IdentityModel.Tokens;
using OtpNet;

namespace GastosBackend.IntegrationTests.Services;

/// <summary>
/// The two-step verification from the activation to the login and the lock, with the real services and repositories on a
/// real PostgreSQL. The clock is moved by hand (the codes change every 30 seconds). Each step uses its own context, as every
/// request of the API does.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TwoFactorFlowTests
{
    private const string Password = "Demo1234!";
    private const string NewPassword = "Nueva1234!";
    private const string Email = "ana@example.com";

    private readonly PostgresFixture _postgres;

    public TwoFactorFlowTests(PostgresFixture postgres)
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

    private sealed class NoBackgroundWork : IBackgroundRunner
    {
        public void Run<TService>(Func<TService, Task> work) where TService : notnull
        {
        }
    }

    private sealed class World
    {
        public required TestDatabase Database { get; init; }
        public required Guid UserId { get; init; }
        public ManualClock Clock { get; } = new();
        public RecordingEmailSender Mail { get; } = new();

        /// <summary>What the "running app" knows about encryption. Replace it to simulate a restart.</summary>
        public required IDataProtectionProvider Protection { get; set; }

        /// <summary>Runs one request: new context, new services.</summary>
        public async Task<T> Request<T>(Func<AuthService, TwoFactorService, Task<T>> work)
        {
            await using var context = Database.CreateContext();
            return await work(
                AuthWiring.NewAuth(context, Protection, Clock, new NoBackgroundWork()),
                AuthWiring.NewTwoFactor(context, Protection, Clock));
        }

        public async Task Run(Func<AuthService, TwoFactorService, Task> work) =>
            await Request(async (auth, twoFactor) => { await work(auth, twoFactor); return true; });

        public Task<SignInResponse> SignIn(string password = Password) =>
            Request((auth, _) => auth.SignIn(new SignInRequest { Email = Email, Password = password }));

        public Task<AuthResponse> Verify(string challenge, string code) =>
            Request((auth, _) => auth.VerifyTwoFactor(new VerifyTwoFactorRequest { ChallengeToken = challenge, Code = code }));

        public Task<TwoFactorSetupResponse> Setup() =>
            Request((_, twoFactor) => twoFactor.Setup(UserId, Password));

        public Task<RecoveryCodesResponse> Enable(string code) =>
            Request((_, twoFactor) => twoFactor.Enable(UserId, code));

        /// <summary>The 6 digits the authenticator app shows right now.</summary>
        public string Code(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(Clock.GetUtcNow().UtcDateTime);

        /// <summary>Setup and Enable; leaves the clock one step later, so the code of the activation is not the current one.</summary>
        public async Task<(string Secret, IReadOnlyList<string> RecoveryCodes)> TurnOn()
        {
            var secret = (await Setup()).Secret;
            var codes = (await Enable(Code(secret))).Codes;
            Clock.Advance(TimeSpan.FromSeconds(30));
            return (secret, codes);
        }

        public async Task<User> LoadUser()
        {
            await using var context = Database.CreateContext();
            return await context.Users.SingleAsync();
        }

        public async Task<bool> IsSessionValid(string jwt)
        {
            await using var context = Database.CreateContext();
            var issuedAt = long.Parse(new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims.Single(c => c.Type == "iat").Value);
            return await new UserSessionValidator(new UserRepository(context)).IsValid(UserId, issuedAt);
        }

        /// <summary>Resets the password with the mail link, through the real service.</summary>
        public async Task ResetPassword(string newPassword)
        {
            await using (var context = Database.CreateContext())
            {
                await NewResetService(context).RequestReset(new ForgotPasswordRequest { Email = Email });
            }

            var link = Mail.Sent.Last().TextBody.Split('\n').Single(line => line.Contains("/restablecer-contrasena?token="));
            var token = Uri.UnescapeDataString(link.Split("token=")[1]);

            await using (var context = Database.CreateContext())
            {
                await NewResetService(context).ResetPassword(new ResetPasswordRequest { Token = token, NewPassword = newPassword });
            }
        }

        /// <summary>The recovery mail is sent in the background: here it runs at once, with a service on a new context.</summary>
        private PasswordResetService NewResetService(GastosDbContext context) =>
            new(
                new UserRepository(context),
                new UserTokenRepository(context),
                Mail,
                new PasswordHash(),
                new InlineRunner(() => NewResetService(Database.CreateContext())),
                new EmailOptions { AppBaseUrl = "https://dualbalance.example" },
                Clock);
    }

    private sealed class InlineRunner : IBackgroundRunner
    {
        private readonly Func<IPasswordResetService> _newService;

        public InlineRunner(Func<IPasswordResetService> newService)
        {
            _newService = newService;
        }

        public void Run<TService>(Func<TService, Task> work) where TService : notnull
        {
            work((TService)_newService()).GetAwaiter().GetResult();
        }
    }

    private async Task<World> Setup()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();

        var userId = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            context.Users.Add(new User { Id = userId, Email = Email, Password = new PasswordHash().Hash(Password), CreatedDate = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
        }

        return new World { Database = database, UserId = userId, Protection = AuthWiring.ProtectionFromDatabase(database) };
    }

    [Fact]
    public async Task TheWholeFlow_Setup_Enable_SignInAsksForTheCode_AndTheCodeGivesTheSession()
    {
        var world = await Setup();

        var setup = await world.Setup();
        var pending = await world.LoadUser();
        Assert.False(pending.TwoFactorEnabled);
        Assert.NotNull(pending.TwoFactorSecret);
        Assert.DoesNotContain(setup.Secret, pending.TwoFactorSecret); // only the encrypted secret is stored

        // Still off: the sign in works as always
        Assert.NotNull((await world.SignIn()).Token);

        var recovery = await world.Enable(world.Code(setup.Secret));
        Assert.Equal(10, recovery.Codes.Count);
        var stored = await world.Database.QueryAsync("SELECT \"CodeHash\" FROM \"RecoveryCodes\" WHERE \"IsDeleted\" = false");
        Assert.Equal(10, stored.Count);
        Assert.All(stored, row => Assert.DoesNotContain((string)row[0], recovery.Codes)); // only hashes
        world.Clock.Advance(TimeSpan.FromSeconds(30));

        var challenge = await world.SignIn();
        Assert.True(challenge.RequiresTwoFactor);
        Assert.Null(challenge.Token);

        var session = await world.Verify(challenge.ChallengeToken!, world.Code(setup.Secret));

        Assert.True(await world.IsSessionValid(session.Token));
        Assert.Equal(world.UserId, session.UserId);
    }

    [Fact]
    public async Task AUserWithoutTwoFactor_SignsInExactlyAsBefore()
    {
        var world = await Setup();

        var result = await world.SignIn();

        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.ChallengeToken);
        Assert.True(await world.IsSessionValid(result.Token!));
    }

    [Fact]
    public async Task TheSameCodeCannotBeUsedTwice_NotEvenTheOneOfTheActivation()
    {
        var world = await Setup();
        var secret = (await world.Setup()).Secret;
        await world.Enable(world.Code(secret));

        // Same 30-second step as the activation: that code was already used, and that is stored in the database
        var challenge = await world.SignIn();
        await Assert.ThrowsAsync<ValidationException>(() => world.Verify(challenge.ChallengeToken!, world.Code(secret)));

        world.Clock.Advance(TimeSpan.FromSeconds(30));
        var code = world.Code(secret);
        await world.Verify((await world.SignIn()).ChallengeToken!, code);
        await Assert.ThrowsAsync<ValidationException>(async () => await world.Verify((await world.SignIn()).ChallengeToken!, code));
    }

    [Fact]
    public async Task ARecoveryCode_SignsIn_AndWorksOnlyOnce()
    {
        var world = await Setup();
        var (_, recovery) = await world.TurnOn();

        var session = await world.Verify((await world.SignIn()).ChallengeToken!, recovery[2].ToLowerInvariant());

        Assert.True(await world.IsSessionValid(session.Token));
        await Assert.ThrowsAsync<ValidationException>(async () => await world.Verify((await world.SignIn()).ChallengeToken!, recovery[2]));
        var used = await world.Database.QueryAsync("SELECT count(*) FROM \"RecoveryCodes\" WHERE \"UsedAt\" IS NOT NULL");
        Assert.Equal(1L, (long)used[0][0]);
        // The others still work
        await world.Verify((await world.SignIn()).ChallengeToken!, recovery[3]);
    }

    [Fact]
    public async Task FiveWrongCodes_LockTheSecondStep_EvenForTheCorrectCode_UntilFifteenMinutesPass()
    {
        var world = await Setup();
        var (secret, _) = await world.TurnOn();
        var challenge = (await world.SignIn()).ChallengeToken!;

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<ValidationException>(() => world.Verify(challenge, "000000"));
        }

        var locked = await Assert.ThrowsAsync<TooManyRequestsException>(() => world.Verify(challenge, world.Code(secret)));
        Assert.Equal(15 * 60, locked.RetryAfterSeconds);

        // The lock is in the database: it survives a restart and applies to a new sign in as well
        world.Protection = AuthWiring.ProtectionFromDatabase(world.Database);
        await Assert.ThrowsAsync<TooManyRequestsException>(
            async () => await world.Verify((await world.SignIn()).ChallengeToken!, world.Code(secret)));

        world.Clock.Advance(TimeSpan.FromMinutes(15));
        var session = await world.Verify((await world.SignIn()).ChallengeToken!, world.Code(secret));
        Assert.True(await world.IsSessionValid(session.Token));
        Assert.Equal(0, (await world.LoadUser()).TwoFactorFailedAttempts);
    }

    [Fact]
    public async Task TheLock_AlsoStopsTurningTheTwoFactorOffAndReplacingTheCodes()
    {
        var world = await Setup();
        var (secret, _) = await world.TurnOn();
        var challenge = (await world.SignIn()).ChallengeToken!;
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<ValidationException>(() => world.Verify(challenge, "000000"));
        }

        await Assert.ThrowsAsync<TooManyRequestsException>(
            () => world.Run((_, twoFactor) => twoFactor.Disable(world.UserId, Password, world.Code(secret))));
        await Assert.ThrowsAsync<TooManyRequestsException>(
            () => world.Request((_, twoFactor) => twoFactor.RegenerateRecoveryCodes(world.UserId, Password, world.Code(secret))));
        Assert.True((await world.LoadUser()).TwoFactorEnabled);
    }

    [Fact]
    public async Task ResettingThePassword_DoesNotSkipTheTwoFactor()
    {
        var world = await Setup();
        await world.TurnOn();
        world.Clock.Advance(TimeSpan.FromSeconds(2));

        await world.ResetPassword(NewPassword);

        // The new password is right, and still it is only the first step
        var result = await world.SignIn(NewPassword);
        Assert.True(result.RequiresTwoFactor);
        Assert.Null(result.Token);
        Assert.True((await world.LoadUser()).TwoFactorEnabled);
    }

    [Fact]
    public async Task ChangingThePassword_InvalidatesTheChallengeOfBefore()
    {
        var world = await Setup();
        var (secret, _) = await world.TurnOn();
        var challenge = (await world.SignIn()).ChallengeToken!;
        world.Clock.Advance(TimeSpan.FromSeconds(2));

        await world.ResetPassword(NewPassword);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => world.Verify(challenge, world.Code(secret)));
        Assert.Equal("Tu verificación venció, ingresá de nuevo.", ex.Message);
    }

    [Fact]
    public async Task AChallenge_IsNotASession_ForTheJwtOfTheApi()
    {
        var world = await Setup();
        await world.TurnOn();
        var challenge = (await world.SignIn()).ChallengeToken!;

        // The parameters the API validates every request with (see Program.cs): the key of the sessions
        var sessionParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = AuthWiring.Jwt.Issuer,
            ValidateAudience = true, ValidAudience = AuthWiring.Jwt.Audience,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthWiring.Jwt.Key))
        };

        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(challenge, sessionParameters, out _));
    }

    [Fact]
    public async Task Disabling_RemovesTheSecretAndTheCodes_AndTheSignInIsAsBeforeAgain()
    {
        var world = await Setup();
        var (secret, recovery) = await world.TurnOn();

        await world.Run((_, twoFactor) => twoFactor.Disable(world.UserId, Password, world.Code(secret)));

        var user = await world.LoadUser();
        Assert.False(user.TwoFactorEnabled);
        Assert.Null(user.TwoFactorSecret);
        var left = await world.Database.QueryAsync("SELECT count(*) FROM \"RecoveryCodes\" WHERE \"IsDeleted\" = false");
        Assert.Equal(0L, (long)left[0][0]);
        Assert.NotNull((await world.SignIn()).Token);
        Assert.Equal(10, recovery.Count);
    }

    [Fact]
    public async Task Regenerating_ReplacesTheCodes_TheOldOnesStopWorking()
    {
        var world = await Setup();
        var (secret, oldCodes) = await world.TurnOn();

        var fresh = await world.Request((_, twoFactor) => twoFactor.RegenerateRecoveryCodes(world.UserId, Password, world.Code(secret)));
        world.Clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(10, fresh.Codes.Count);
        await Assert.ThrowsAsync<ValidationException>(async () => await world.Verify((await world.SignIn()).ChallengeToken!, oldCodes[0]));
        await world.Verify((await world.SignIn()).ChallengeToken!, fresh.Codes[0]);
        var active = await world.Database.QueryAsync("SELECT count(*) FROM \"RecoveryCodes\" WHERE \"IsDeleted\" = false");
        Assert.Equal(10L, (long)active[0][0]);
    }

    [Fact]
    public async Task TheSecrets_SurviveARestartOfTheApp_BecauseTheKeysAreInTheDatabase()
    {
        var world = await Setup();
        var (secret, _) = await world.TurnOn();
        var keys = await world.Database.QueryAsync("SELECT count(*) FROM \"DataProtectionKeys\"");
        Assert.True((long)keys[0][0] >= 1);

        // A new process: it only knows what the database holds
        world.Protection = AuthWiring.ProtectionFromDatabase(world.Database);

        var session = await world.Verify((await world.SignIn()).ChallengeToken!, world.Code(secret));
        Assert.True(await world.IsSessionValid(session.Token));
    }

    [Fact]
    public async Task WithoutThePersistedKeys_TheSecretCannotBeRead_WhichIsWhyTheyAreInTheDatabase()
    {
        var world = await Setup();
        var (secret, _) = await world.TurnOn();

        // A restart that loses the keys (what a container without a disk would do if they were files)
        world.Protection = new EphemeralDataProtectionProvider();

        var challenge = (await world.SignIn()).ChallengeToken!;
        await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(() => world.Verify(challenge, world.Code(secret)));
    }
}
