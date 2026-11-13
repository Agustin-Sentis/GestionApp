using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace GastosBackend.Tests.Integration;

/// <summary>
/// The real API pipeline for tests that must not need a database: the startup migrations are removed,
/// the auth service is a fake and the connection string points to nothing (so /api/health reports it as down).
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    private const string JwtKey = "integration-tests-signing-key-0123456789";

    /// <summary>High by default so tests that are not about rate limiting never hit it.</summary>
    protected virtual int AuthPermitLimit => 1000;

    /// <summary>On by default; tests about the docs being hidden turn it off.</summary>
    protected virtual bool DocsEnabled => true;

    /// <summary>Any other setting a test needs (for example the HSTS flag), applied last so it can override the defaults.</summary>
    protected virtual IReadOnlyDictionary<string, string> ExtraSettings => new Dictionary<string, string>();

    /// <summary>
    /// Decides which sessions are accepted. Every one by default: the real validator looks the user up in the database, and this
    /// factory has none. A test about sessions overrides it.
    /// </summary>
    protected virtual IUserSessionValidator SessionValidator => new AcceptAllSessions();

    private sealed class AcceptAllSessions : IUserSessionValidator
    {
        public Task<bool> IsValid(Guid userId, long? issuedAtUnixSeconds) => Task.FromResult(true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Run like production, so nothing depends on appsettings.Development.json: every setting a test needs is set below
        builder.UseEnvironment("Production");
        builder.UseSetting("Docs:Enabled", DocsEnabled.ToString());
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("RateLimiting:Auth:PermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
        // Nothing listens on port 1: connecting fails right away
        builder.UseSetting("ConnectionStrings:GastosConnectionStrings",
            "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1");
        foreach (var (key, value) in ExtraSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            var migrationService = services.Single(d => d.ImplementationType == typeof(DatabaseMigrationService)
                                                        && d.ServiceType == typeof(IHostedService));
            services.Remove(migrationService);

            services.RemoveAll<IAuthService>();
            services.AddScoped<IAuthService, FakeAuthService>();

            services.RemoveAll<IUserSessionValidator>();
            services.AddSingleton(SessionValidator);
        });
    }

    /// <summary>
    /// A valid JWT, signed with the key the factory configured. For any new user unless <paramref name="userId"/> says which;
    /// with the "iat" claim only when <paramref name="issuedAt"/> is given.
    /// </summary>
    public static string CreateToken(Guid? userId = null, DateTimeOffset? issuedAt = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, (userId ?? Guid.NewGuid()).ToString()) };
        if (issuedAt != null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Iat, issuedAt.Value.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        }

        var token = new JwtSecurityToken(
            issuer: "Gastos",
            audience: "GastosClients",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private class FakeAuthService : IAuthService
    {
        public Task<AuthResponse> SignUp(SignUpRequest request) => Task.FromResult(new AuthResponse { Token = "jwt" });
        public Task<SignInResponse> SignIn(SignInRequest request) => Task.FromResult(new SignInResponse { Token = "jwt" });
        public Task<AuthResponse> VerifyTwoFactor(VerifyTwoFactorRequest request) => Task.FromResult(new AuthResponse { Token = "jwt" });
    }
}
