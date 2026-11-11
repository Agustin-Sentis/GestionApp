using GastosBackend.Application.Interfaces;

namespace GastosBackend.Presentation.Services
{
    /// <summary>
    /// Runs the budget alert check after the response is on its way. The work gets its own DI scope, because everything
    /// the request used (the database context above all) is released when the request ends.
    /// </summary>
    public class BudgetAlertDispatcher : IBudgetAlertDispatcher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BudgetAlertDispatcher> _logger;

        public BudgetAlertDispatcher(IServiceScopeFactory scopeFactory, ILogger<BudgetAlertDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Dispatch(Guid userId, Guid categoryId, DateOnly date)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<IBudgetAlertService>();
                    await service.CheckAndNotify(userId, categoryId, date);
                }
                catch (Exception ex)
                {
                    // Nobody is waiting for this: the expense is already saved. The mail will be tried again with the next one.
                    _logger.LogWarning(ex, "The budget alert for category {CategoryId} could not be processed.", categoryId);
                }
            });
        }
    }
}
