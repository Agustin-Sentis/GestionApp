using GastosBackend.Application.Interfaces;

namespace GastosBackend.Presentation.Services
{
    /// <summary>Runs the budget alert check in the background, after the expense was saved.</summary>
    public class BudgetAlertDispatcher : IBudgetAlertDispatcher
    {
        private readonly IBackgroundRunner _runner;

        public BudgetAlertDispatcher(IBackgroundRunner runner)
        {
            _runner = runner;
        }

        public void Dispatch(Guid userId, Guid categoryId, DateOnly date)
        {
            _runner.Run<IBudgetAlertService>(service => service.CheckAndNotify(userId, categoryId, date));
        }
    }
}
