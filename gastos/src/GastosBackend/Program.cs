using System.Text;
using System.Text.Json.Serialization;
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

// API externa de cotización del dólar: cliente nombrado + reintentos + circuit breaker (Polly)
ApiClientConfigurationDTO dolarApiResilienceConfiguration = new ApiClientConfigurationDTO()
{
    RetryCount = 2,
    RetryAttemptInSeconds = 2,
    DurationOfBreakInSeconds = 60,
    HandledEventsAllowedBeforeBreaking = 5
};

builder.Services.AddHttpClient(DolarApiService.HttpClientName, client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["DolarApi:BaseUrl"] ?? "https://dolarapi.com/");
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddPolicyHandler(PollyResiliencePolicies.GetRetryPolicy(dolarApiResilienceConfiguration))
    .AddPolicyHandler(PollyResiliencePolicies.GetCircuitBreakerPolicy(dolarApiResilienceConfiguration));

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

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapOpenApi();            // /openapi/v1.json
app.MapScalarApiReference(); // /scalar

app.UseRouting();

app.UseCors(x => x
    .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod());

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
