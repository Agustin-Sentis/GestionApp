using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class CategoryTotalResponse
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>
    /// Monthly report expressed in ARS. USD transactions are converted with the rate stored on each one.
    /// UsdRateUsed is only set when the current rate was needed for USD transactions without a stored rate.
    /// HasUnconvertedUsd is true when some USD transactions were left out because no rate was available.
    /// </summary>
    public class MonthlyReportResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Currency { get; set; } = "ARS";
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance { get; set; }
        public List<CategoryTotalResponse> ByCategory { get; set; } = new();
        public decimal? UsdRateUsed { get; set; }
        public bool HasUnconvertedUsd { get; set; }
    }
}
