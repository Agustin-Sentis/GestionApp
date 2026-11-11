using GastosBackend.Application.Configuration;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    public class BudgetAlertService : IBudgetAlertService
    {
        /// <summary>Argentina has no daylight saving time: "this month" for the user is the month at UTC-3.</summary>
        private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

        private readonly IUserRepository _userRepository;
        private readonly IReportService _reportService;
        private readonly IBudgetAlertRepository _alertRepository;
        private readonly IEmailSender _emailSender;
        private readonly EmailOptions _emailOptions;
        private readonly TimeProvider _time;

        public BudgetAlertService(
            IUserRepository userRepo,
            IReportService reportService,
            IBudgetAlertRepository alertRepo,
            IEmailSender emailSender,
            EmailOptions emailOptions,
            TimeProvider time)
        {
            _userRepository = userRepo;
            _reportService = reportService;
            _alertRepository = alertRepo;
            _emailSender = emailSender;
            _emailOptions = emailOptions;
            _time = time;
        }

        public async Task CheckAndNotify(Guid userId, Guid categoryId, DateOnly date)
        {
            // Only the month in progress: loading old expenses that were missing from the notebook must not send mails
            var today = _time.GetUtcNow().ToOffset(ArgentinaOffset);
            if (date.Year != today.Year || date.Month != today.Month)
            {
                return;
            }

            var user = await _userRepository.Get(userId);
            if (user == null || user.IsDeleted || !user.BudgetEmailAlerts || user.EmailConfirmedAt == null)
            {
                return;
            }

            // The same numbers and rules as the summary of the app, so the mail never disagrees with the screen
            var report = await _reportService.GetMonthly(date.Year, date.Month, userId);
            var budget = report.Budgets.FirstOrDefault(b => b.CategoryId == categoryId);
            if (budget == null || budget.Status == BudgetStatus.Ok)
            {
                return;
            }

            // Already told the user they were over: coming back under and up again does not warn them of the lesser thing
            if (budget.Status == BudgetStatus.Warning
                && await _alertRepository.Exists(userId, categoryId, date.Year, date.Month, BudgetStatus.Exceeded))
            {
                return;
            }

            var mark = new BudgetAlert
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CategoryId = categoryId,
                Year = date.Year,
                Month = date.Month,
                Level = budget.Status
            };

            // The mark goes first: of several requests crossing the same threshold at once, only one gets to send
            if (!await _alertRepository.TryAdd(mark))
            {
                return;
            }

            try
            {
                await _emailSender.Send(EmailTemplates.BudgetAlert(
                    user.Email, budget.Name, budget.Spent, budget.MonthlyBudget, budget.UsedPercent, budget.Status, _emailOptions.AppBaseUrl));
            }
            catch
            {
                // Not sent: forget the mark so the next expense tries again
                await _alertRepository.Delete(mark.Id);
                throw;
            }
        }
    }
}
