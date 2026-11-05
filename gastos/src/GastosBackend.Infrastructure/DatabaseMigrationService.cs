using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GastosBackend.Infrastructure
{
    /// <summary>
    /// Aplica las migraciones pendientes al iniciar la aplicación (útil con docker compose).
    /// Es un IHostedService y no código en Program.cs para que "dotnet ef" pueda construir el host sin conectarse a la base.
    /// </summary>
    public class DatabaseMigrationService : IHostedService
    {
        private readonly IServiceProvider _services;

        public DatabaseMigrationService(IServiceProvider services)
        {
            _services = services;
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
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
