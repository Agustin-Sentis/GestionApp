using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    public class ReportService : IReportService
    {
        /// <summary>The first year the app accepts. The month before January of this year is not reported.</summary>
        private const int FirstYear = 2000;

        private readonly ITransactionRepository _transactionRepository;
        private readonly IDolarApiService _dolarApiService;

        public ReportService(ITransactionRepository transactionRepo, IDolarApiService dolarApiService)
        {
            _transactionRepository = transactionRepo;
            _dolarApiService = dolarApiService;
        }

        public async Task<MonthlyReportResponse> GetMonthly(int year, int month, Guid userId)
        {
            if (month < 1 || month > 12 || year < FirstYear || year > 2100)
            {
                throw new ValidationException("El año o el mes no son válidos.");
            }

            var from = new DateOnly(year, month, 1);
            var rows = await _transactionRepository.GetMonthlyTotals(userId, from, from.AddMonths(1));

            // The month before, to compare against. January of the first year has none.
            var previousFrom = from.AddMonths(-1);
            var previousRows = previousFrom.Year >= FirstYear
                ? await _transactionRepository.GetMonthlyTotals(userId, previousFrom, from)
                : null;

            // Row totals already come in ARS: each USD transaction was converted with the rate stored with it, of the dollar it
            // was saved with. The current rate is only requested for the ones that have none yet, one request per dollar
            // type, and only once even when both months need it.
            var rates = new CurrentRates(_dolarApiService);

            var current = await Summarize(rows, rates);
            var income = current.ByCategory.Where(c => c.Type == TransactionType.Income).Sum(c => c.Total);
            var expense = current.ByCategory.Where(c => c.Type == TransactionType.Expense).Sum(c => c.Total);

            return new MonthlyReportResponse
            {
                Year = year,
                Month = month,
                Currency = "ARS",
                Income = income,
                Expense = expense,
                Balance = income - expense,
                ByCategory = current.ByCategory,
                PendingConversions = current.Pending,
                HasUnconvertedUsd = current.Pending.Any(p => p.Rate == null),
                Previous = previousRows == null ? null : await SummarizePreviousMonth(previousFrom, previousRows, rates)
            };
        }

        /// <summary>The totals per category of a month, and which dollars needed today's rate to complete them.</summary>
        private static async Task<(List<CategoryTotalResponse> ByCategory, List<PendingConversion> Pending)> Summarize(
            List<CategoryTotalRow> rows, CurrentRates rates)
        {
            // Degrade instead of failing the whole report: a dollar whose rate is not available leaves its pending
            // transactions out, and the report says so.
            var pending = new List<PendingConversion>();
            var rateOf = new Dictionary<DollarType, decimal?>();
            foreach (var type in rows.Where(r => r.PendingUsd > 0).Select(r => r.DollarType).Distinct().OrderBy(t => t))
            {
                var rate = await rates.Get(type);
                rateOf[type] = rate;
                pending.Add(new PendingConversion { DollarType = type, Rate = rate });
            }

            decimal ToArs(CategoryTotalRow row) =>
                row.Total + (row.PendingUsd > 0 && rateOf[row.DollarType] is { } rate ? row.PendingUsd * rate : 0m);

            var byCategory = rows
                .GroupBy(r => new { r.Name, r.Type })
                .Select(g => new CategoryTotalResponse
                {
                    Name = g.Key.Name,
                    Type = g.Key.Type,
                    Total = Math.Round(g.Sum(r => ToArs(r)), 2)
                })
                .OrderByDescending(c => c.Total)
                .ToList();

            return (byCategory, pending);
        }

        private static async Task<PreviousMonthSummary> SummarizePreviousMonth(
            DateOnly previousFrom, List<CategoryTotalRow> rows, CurrentRates rates)
        {
            // What it needed is not reported as "converted with today's rate": that note describes the month on screen
            var summary = await Summarize(rows, rates);
            var income = summary.ByCategory.Where(c => c.Type == TransactionType.Income).Sum(c => c.Total);
            var expense = summary.ByCategory.Where(c => c.Type == TransactionType.Expense).Sum(c => c.Total);

            return new PreviousMonthSummary
            {
                Year = previousFrom.Year,
                Month = previousFrom.Month,
                Income = income,
                Expense = expense,
                Balance = income - expense,
                HasUnconvertedUsd = summary.Pending.Any(p => p.Rate == null)
            };
        }

        /// <summary>Asks for the current USD rate of each dollar type the first time it is needed and remembers the answer, failed or not.</summary>
        private sealed class CurrentRates
        {
            private readonly IDolarApiService _service;
            private readonly Dictionary<DollarType, decimal?> _answers = new();

            public CurrentRates(IDolarApiService service)
            {
                _service = service;
            }

            public async Task<decimal?> Get(DollarType type)
            {
                if (!_answers.TryGetValue(type, out var rate))
                {
                    try
                    {
                        rate = await _service.GetUsdSellRate(type);
                    }
                    catch (ExternalServiceException)
                    {
                        rate = null;
                    }

                    _answers[type] = rate;
                }

                return rate;
            }
        }
    }
}
