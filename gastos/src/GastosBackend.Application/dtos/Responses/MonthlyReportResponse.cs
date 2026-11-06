using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class CategoryTotalResponse
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>The totals of the month before the one reported, to compare against. Same rules as the report itself.</summary>
    public class PreviousMonthSummary
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance { get; set; }

        /// <summary>True when some USD transactions of that month were left out because no rate was available: the figures are incomplete.</summary>
        public bool HasUnconvertedUsd { get; set; }
    }

    /// <summary>
    /// A dollar that had USD transactions without a stored rate of their own, and the current rate that stood in for it.
    /// Rate is null when that rate could not be obtained either: those transactions are then left out of the totals.
    /// </summary>
    public class PendingConversion
    {
        public DollarType DollarType { get; set; }
        public decimal? Rate { get; set; }
    }

    /// <summary>
    /// Monthly report expressed in ARS. Each USD transaction is converted with the rate stored on it, of the dollar it was
    /// saved with, so a month can mix dollars (the user may have chosen another one in between).
    /// PendingConversions lists the dollars whose current rate was needed for transactions that had none stored.
    /// HasUnconvertedUsd is true when some USD transactions were left out because no rate was available.
    /// Previous is the month before, for comparing; null when that month would be before the first one the app accepts (2000).
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
        public List<PendingConversion> PendingConversions { get; set; } = new();
        public bool HasUnconvertedUsd { get; set; }
        public PreviousMonthSummary? Previous { get; set; }
    }
}
