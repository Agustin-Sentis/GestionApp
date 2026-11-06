using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Infrastructure;
using GastosBackend.Infrastructure.ExternalService;
using GastosBackend.Infrastructure.HttpResiliencePolicies;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.Presentation.Middlewares;
using GastosBackend.Presentation.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using static GastosBackend.Infrastructure.HttpResiliencePolicies.HttpPollyPolicies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Los enums viajan como texto ("Expense", "ARS"), no como números
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    // Binding errors (a bad query value, a malformed body) use the same { "error": "..." } shape as every other error
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(new { error = ModelStateErrorMessage.Build(context.ModelState) });
    });
builder.Services.AddCors();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<DolarApiOptions>(builder.Configuration.GetSection("DolarApi"));

builder.Services.AddDbContext<GastosDbContext>(
    options => options.UseNpgsql(builder.Configuration.GetConnectionString("GastosConnectionStrings")));
builder.Services.AddHostedService<DatabaseMigrationService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPasswordHash, PasswordHash>();

// External exchange rate API: named client + retry + circuit breaker + per-attempt timeout (Polly).
// Tuned to fail fast: with the API down a request waits about 3.5 s at most (1.5 s + 0.5 s + 1.5 s),
// and once the circuit is open it answers immediately. dolarapi.com normally answers in 100-300 ms.
ApiClientConfigurationDTO dolarApiResilienceConfiguration = new ApiClientConfigurationDTO()
{
    RetryCount = 1,
    RetryBaseDelayInMilliseconds = 500,
    TimeoutInMilliseconds = 1500,
    HandledEventsAllowedBeforeBreaking = 3,
    DurationOfBreakInSeconds = 30
};

builder.Services.AddHttpClient(DolarApiService.HttpClientName, client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["DolarApi:BaseUrl"] ?? "https://dolarapi.com/");
        // Safety net only; the Polly policies are what bounds the time
        client.Timeout = TimeSpan.FromSeconds(6);
    })
    // The first handler added is the outermost: retry > circuit breaker > timeout (one timeout per attempt)
    .AddPolicyHandler(PollyResiliencePolicies.GetRetryPolicy(dolarApiResilienceConfiguration))
    .AddPolicyHandler(PollyResiliencePolicies.GetCircuitBreakerPolicy(dolarApiResilienceConfiguration))
    .AddPolicyHandler(PollyResiliencePolicies.GetTimeoutPolicy(dolarApiResilienceConfiguration));

builder.Services.AddScoped<IDolarApiService, DolarApiService>();

// Autenticación JWT
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key debe tener al menos 32 caracteres (user-secrets en desarrollo, variable Jwt__Key en producción).");
}

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// How long browsers remember to use HTTPS only. Short at first (30 days): raise it once HTTPS is known to be solid.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(builder.Configuration.GetValue("Https:Hsts:MaxAgeDays", 30));
});

// Rate limiting for the auth endpoints: slows down password guessing and account spam.
// Fixed window per client IP (in memory, so each instance counts on its own).
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
var authWindowSeconds = builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AuthRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0
            }));

    // Same { "error": "..." } body as every other error of the API
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = "Demasiados intentos. Probá de nuevo en unos minutos." }),
            cancellationToken);
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// Behind a reverse proxy (Render) the real client IP and scheme come in X-Forwarded-* headers.
// Off by default: trusting these headers is only safe when ALL traffic goes through that proxy,
// otherwise a client could fake its IP and get around the rate limiting.
// Render adds the client IP as the last X-Forwarded-For entry, which the client cannot forge (ForwardLimit = 1).
if (builder.Configuration.GetValue("ForwardedHeaders:Enabled", false))
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    };
    // The proxy addresses are not fixed, so the default "loopback only" trust lists must be cleared
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

// HSTS tells browsers to use HTTPS only for this host. Render already redirects HTTP to HTTPS, so the app does not
// redirect on its own. It is only sent on responses the app considers HTTPS: behind a proxy that needs ForwardedHeaders
// above to be on. Off by default, because a browser that received it cannot go back to HTTP until it expires.
if (builder.Configuration.GetValue("Https:Hsts:Enabled", false))
{
    app.UseHsts();
}

// API docs list every endpoint, so they are off by default and turned on explicitly (Development, docker compose)
if (builder.Configuration.GetValue("Docs:Enabled", false))
{
    app.MapOpenApi();            // /openapi/v1.json
    app.MapScalarApiReference(); // /scalar
}

app.UseRouting();

app.UseCors(x => x
    .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod());

// After CORS so a 429 still carries the CORS headers and the browser can read it
app.UseRateLimiter();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// "*" accepts any Host header. That is fine locally, but a deployed API should only answer to its own domain.
// It is only a warning: restricting it is a deliberate step (and the default stays "*"), because a wrong value would
// make the API reject every request, including the hosting platform's health checks.
var allowedHosts = app.Configuration["AllowedHosts"];
var allowsAnyHost = string.IsNullOrWhiteSpace(allowedHosts)
    || allowedHosts.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains("*");
if (allowsAnyHost && !app.Environment.IsDevelopment())
{
    app.Logger.LogWarning(
        "AllowedHosts is '*': the API answers to any Host header. In production set the AllowedHosts environment variable to the API domain.");
}

app.Run();

public partial class Program
{
    /// <summary>Name of the rate limiting policy applied to the auth endpoints.</summary>
    public const string AuthRateLimitPolicy = "auth";
}
