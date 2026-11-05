using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    public class ReportService : IReportService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IDolarApiService _dolarApiService;

        public ReportService(ITransactionRepository transactionRepo, IDolarApiService dolarApiService)
        {
            _transactionRepository = transactionRepo;
            _dolarApiService = dolarApiService;
        }

        public async Task<MonthlyReportResponse> GetMonthly(int year, int month, string sub)
        {
            if (!Guid.TryParse(sub, out Guid userId))
            {
                throw new ValidationException("El ID enviado no es válido");
            }

            if (month < 1 || month > 12 || year < 2000 || year > 2100)
            {
                throw new ValidationException("El año o el mes no son válidos.");
            }

            var from = new DateOnly(year, month, 1);
            var rows = await _transactionRepository.GetMonthlyTotals(userId, from, from.AddMonths(1));

            // Solo se consulta la cotización si hay movimientos en USD
            decimal? usdRate = null;
            if (rows.Any(r => r.Currency == Currency.USD))
            {
                usdRate = await _dolarApiService.GetUsdSellRate();
            }

            decimal ToArs(CategoryTotalRow row) =>
                row.Currency == Currency.USD ? row.Total * usdRate!.Value : row.Total;

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

            var income = byCategory.Where(c => c.Type == TransactionType.Income).Sum(c => c.Total);
            var expense = byCategory.Where(c => c.Type == TransactionType.Expense).Sum(c => c.Total);

            return new MonthlyReportResponse
            {
                Year = year,
                Month = month,
                Currency = "ARS",
                Income = income,
                Expense = expense,
                Balance = income - expense,
                ByCategory = byCategory,
                UsdRateUsed = usdRate
            };
        }
    }
}
