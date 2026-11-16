using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using System.Text.RegularExpressions;

namespace GastosBackend.Application.Services
{
    public class ReportService : IReportService
    {
        /// <summary>The first year the app accepts. The month before January of this year is not reported.</summary>
        private const int FirstYear = 2000;

        /// <summary>The last year the app accepts.</summary>
        private const int LastYear = 2100;

        /// <summary>The longest range the evolution report answers, both months included.</summary>
        private const int MaxEvolutionMonths = 24;

        /// <summary>A month as the evolution report receives it: four digits of year, a dash and two of month (01-12).</summary>
        private static readonly Regex MonthFormat = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

        /// <summary>From this share of the budget on, a category is "close to the limit".</summary>
        private const decimal WarningShare = 0.8m;

        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IDolarApiService _dolarApiService;

        public ReportService(
            ITransactionRepository transactionRepo,
            ICategoryRepository categoryRepo,
            IDolarApiService dolarApiService)
        {
            _transactionRepository = transactionRepo;
            _categoryRepository = categoryRepo;
            _dolarApiService = dolarApiService;
        }

        public async Task<MonthlyReportResponse> GetMonthly(int year, int month, Guid userId)
        {
            if (month < 1 || month > 12 || year < FirstYear || year > LastYear)
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

            var categories = await _categoryRepository.GetAllByUser(userId);

            return new MonthlyReportResponse
            {
                Year = year,
                Month = month,
                Currency = "ARS",
                Income = income,
                Expense = expense,
                Balance = income - expense,
                ByCategory = current.ByCategory,
                Budgets = BudgetProgress(categories, current.ByCategory),
                PendingConversions = current.Pending,
                HasUnconvertedUsd = current.Pending.Any(p => p.Rate == null),
                Previous = previousRows == null ? null : await SummarizePreviousMonth(previousFrom, previousRows, rates)
            };
        }

        public async Task<EvolutionReportResponse> GetEvolution(string from, string to, Guid userId)
        {
            var first = ParseMonth(from);
            var last = ParseMonth(to);

            if (first.Year < FirstYear || first.Year > LastYear || last.Year < FirstYear || last.Year > LastYear)
            {
                throw new ValidationException("El año o el mes no son válidos.");
            }
            if (first > last)
            {
                throw new ValidationException("El mes inicial no puede ser posterior al final.");
            }

            var monthCount = (last.Year - first.Year) * 12 + last.Month - first.Month + 1;
            if (monthCount > MaxEvolutionMonths)
            {
                throw new ValidationException($"El rango no puede superar los {MaxEvolutionMonths} meses.");
            }

            // One query for the whole range: the database groups by month
            var rows = await _transactionRepository.GetTotalsByMonth(userId, first, last.AddMonths(1));
            var byMonth = rows.ToLookup(r => (r.Year, r.Month));

            // Same rule as the monthly report: the current rate is only asked for the dollars that have pending USD, once
            // each, and a rate that is not available leaves those transactions out of the totals
            var rates = new CurrentRates(_dolarApiService);
            var pending = new List<PendingConversion>();
            var rateOf = new Dictionary<DollarType, decimal?>();
            foreach (var type in rows.Where(r => r.PendingUsd > 0).Select(r => r.DollarType).Distinct().OrderBy(t => t))
            {
                var rate = await rates.Get(type);
                rateOf[type] = rate;
                pending.Add(new PendingConversion { DollarType = type, Rate = rate });
            }

            decimal Sum(IEnumerable<MonthTotalRow> monthRows) =>
                Math.Round(monthRows.Sum(r => ToArs(r.Total, r.PendingUsd, r.PendingUsd > 0 ? rateOf[r.DollarType] : null)), 2);

            // Months with no movements are in the answer with zeros, so a chart has no holes
            var months = Enumerable.Range(0, monthCount)
                .Select(offset =>
                {
                    var date = first.AddMonths(offset);
                    var monthRows = byMonth[(date.Year, date.Month)].ToList();
                    var income = Sum(monthRows.Where(r => r.Type == TransactionType.Income));
                    var expense = Sum(monthRows.Where(r => r.Type == TransactionType.Expense));

                    return new MonthEvolutionResponse
                    {
                        Year = date.Year,
                        Month = date.Month,
                        Income = income,
                        Expense = expense,
                        Balance = income - expense,
                        HasUnconvertedUsd = monthRows.Any(r => r.PendingUsd > 0 && rateOf[r.DollarType] == null)
                    };
                })
                .ToList();

            var totalIncome = months.Sum(m => m.Income);
            var totalExpense = months.Sum(m => m.Expense);

            return new EvolutionReportResponse
            {
                From = from,
                To = to,
                Currency = "ARS",
                Months = months,
                Totals = new PeriodTotalsResponse { Income = totalIncome, Expense = totalExpense, Balance = totalIncome - totalExpense },
                PendingConversions = pending,
                HasUnconvertedUsd = pending.Any(p => p.Rate == null)
            };
        }

        /// <summary>The first day of a month written yyyy-MM. Anything else (including a missing value) is a validation error.</summary>
        private static DateOnly ParseMonth(string? value)
        {
            if (value is null || !MonthFormat.IsMatch(value))
            {
                throw new ValidationException("El rango debe tener el formato AAAA-MM.");
            }

            return new DateOnly(int.Parse(value[..4]), int.Parse(value[5..]), 1);
        }

        /// <summary>A total in ARS plus the USD that had no stored rate, converted with the current one (left out when there is none).</summary>
        private static decimal ToArs(decimal total, decimal pendingUsd, decimal? rate) =>
            total + (pendingUsd > 0 && rate is { } current ? pendingUsd * current : 0m);

        /// <summary>
        /// The budget of each expense category that has one, measured against what the month spent on it. A category with no
        /// spending is not in the totals, so it is built from the categories and counts as zero. The most urgent goes first.
        /// </summary>
        private static List<BudgetProgressResponse> BudgetProgress(List<Category> categories, List<CategoryTotalResponse> byCategory)
        {
            var spentByName = byCategory
                .Where(c => c.Type == TransactionType.Expense)
                .ToDictionary(c => c.Name, c => c.Total);

            return categories
                .Where(c => c.Type == TransactionType.Expense && c.MonthlyBudget is > 0)
                .Select(c =>
                {
                    var budget = c.MonthlyBudget!.Value;
                    var spent = spentByName.GetValueOrDefault(c.Name);

                    return new BudgetProgressResponse
                    {
                        CategoryId = c.Id,
                        Name = c.Name,
                        MonthlyBudget = budget,
                        Spent = spent,
                        UsedPercent = Math.Round(spent / budget * 100, 1),
                        // Spending exactly the whole budget is not over it yet
                        Status = spent > budget ? BudgetStatus.Exceeded
                            : spent >= budget * WarningShare ? BudgetStatus.Warning
                            : BudgetStatus.Ok
                    };
                })
                .OrderByDescending(b => b.Status)
                .ThenByDescending(b => b.UsedPercent)
                .ThenBy(b => b.Name)
                .ToList();
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

            var byCategory = rows
                .GroupBy(r => new { r.Name, r.Type })
                .Select(g => new CategoryTotalResponse
                {
                    Name = g.Key.Name,
                    Type = g.Key.Type,
                    Total = Math.Round(g.Sum(r => ToArs(r.Total, r.PendingUsd, r.PendingUsd > 0 ? rateOf[r.DollarType] : null)), 2)
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
