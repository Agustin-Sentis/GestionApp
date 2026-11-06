using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastosBackend.Infrastructure
{
    /// <summary>
    /// Aplica las migraciones pendientes al iniciar la aplicación (útil con docker compose).
    /// Es un IHostedService y no código en Program.cs para que "dotnet ef" pueda construir el host sin conectarse a la base.
    /// </summary>
    public class DatabaseMigrationService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(IServiceProvider services, ILogger<DatabaseMigrationService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GastosDbContext>();

            if (!context.Database.GetMigrations().Any())
            {
                throw new InvalidOperationException(
                    "No hay migraciones en el proyecto. Generá la inicial con: " +
                    "dotnet ef migrations add Init --project GastosBackend.Infrastructure " +
                    "--startup-project GastosBackend --output-dir Migrations");
            }

            await context.Database.MigrateAsync(cancellationToken);

            await BackfillExchangeRates(scope.ServiceProvider, context, cancellationToken);
        }

        /// <summary>
        /// Gives every USD transaction without a stored rate the current rate.
        /// Idempotent: once rates are filled there is nothing left to update and the rate API is not called.
        /// Never blocks the startup: if the rate cannot be obtained it is retried on the next start.
        /// </summary>
        private async Task BackfillExchangeRates(
            IServiceProvider services, GastosDbContext context, CancellationToken cancellationToken)
        {
            var pending = context.Transactions
                .Where(t => t.Currency == Currency.USD && t.ExchangeRate == null && !t.IsDeleted);

            if (!await pending.AnyAsync(cancellationToken))
            {
                return;
            }

            try
            {
                var rate = await services.GetRequiredService<IDolarApiService>().GetUsdSellRate();

                var updated = await pending.ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.ExchangeRate, rate), cancellationToken);

                _logger.LogInformation("Backfilled the USD rate ({Rate}) on {Count} transactions.", rate, updated);
            }
            catch (ExternalServiceException)
            {
                _logger.LogWarning("USD rate backfill skipped: the rate is not available. It will be retried on the next start.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
