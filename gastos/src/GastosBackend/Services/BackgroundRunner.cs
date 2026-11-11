using GastosBackend.Application.Interfaces;

namespace GastosBackend.Presentation.Services
{
    public class BackgroundRunner : IBackgroundRunner
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundRunner> _logger;

        public BackgroundRunner(IServiceScopeFactory scopeFactory, ILogger<BackgroundRunner> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Run<TService>(Func<TService, Task> work) where TService : notnull
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    await work(scope.ServiceProvider.GetRequiredService<TService>());
                }
                catch (Exception ex)
                {
                    // The request that asked for this already got its answer. Whatever it was (a mail) is tried again by the next one.
                    _logger.LogWarning(ex, "A background task of {Service} failed.", typeof(TService).Name);
                }
            });
        }
    }
}
