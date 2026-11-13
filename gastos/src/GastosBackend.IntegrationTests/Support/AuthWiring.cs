using GastosBackend.Application.Configuration;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Infrastructure;
using GastosBackend.Infrastructure.ExternalService;
using GastosBackend.Infrastructure.Repository;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GastosBackend.IntegrationTests.Support;

/// <summary>A clock the test moves by hand (the 2FA codes change every 30 seconds, so a test must control time).</summary>
public sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan time) => _now += time;
}

/// <summary>The real auth and two-step services on a real context, wired as the API does it (one context per request).</summary>
public static class AuthWiring
{
    public static readonly JwtOptions Jwt = new()
    {
        Key = new string('k', 40), Issuer = "Gastos", Audience = "GastosClients", ExpirationMinutes = 60
    };

    /// <summary>
    /// A Data Protection provider that keeps its keys in the database, like the API. Each call is a "restart of the app": a
    /// new provider that knows nothing but what the database holds.
    /// </summary>
    public static IDataProtectionProvider ProtectionFromDatabase(TestDatabase database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => database.CreateContext());
        services.AddDataProtection().SetApplicationName("DualBalance").PersistKeysToDbContext<GastosDbContext>();
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    public static TwoFactorService NewTwoFactor(GastosDbContext context, IDataProtectionProvider protection, TimeProvider time) =>
        new(
            new UserRepository(context),
            new RecoveryCodeRepository(context),
            new TotpService(time),
            new SecretProtector(protection),
            new RecoveryCodeGenerator(),
            new PasswordHash(),
            time);

    public static AuthService NewAuth(
        GastosDbContext context, IDataProtectionProvider protection, TimeProvider time, IBackgroundRunner background) =>
        new(
            new UserRepository(context),
            new PasswordHash(),
            background,
            Options.Create(Jwt),
            NewTwoFactor(context, protection, time),
            time);
}
