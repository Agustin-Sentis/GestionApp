namespace GastosBackend.Application.dtos.Responses
{
    /// <summary>One month of the evolution report: what came in, what went out and what was left, in ARS.</summary>
    public class MonthEvolutionResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance { get; set; }

        /// <summary>True when some USD transactions of this month were left out because no rate was available: the figures are incomplete.</summary>
        public bool HasUnconvertedUsd { get; set; }
    }

    /// <summary>The sum of every month of the period.</summary>
    public class PeriodTotalsResponse
    {
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance { get; set; }
    }

    /// <summary>
    /// Income, expenses and balance of each month from <c>From</c> to <c>To</c> (both included, formatted yyyy-MM), in ARS.
    /// A month with no movements is in the list with zeros. Same conversion rules as the monthly report: each USD
    /// transaction uses the rate stored with it, of the dollar it was saved with; PendingConversions lists the dollars whose
    /// current rate was needed for transactions that had none, and HasUnconvertedUsd is true when some were left out.
    /// </summary>
    public class EvolutionReportResponse
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Currency { get; set; } = "ARS";
        public List<MonthEvolutionResponse> Months { get; set; } = new();
        public PeriodTotalsResponse Totals { get; set; } = new();
        public List<PendingConversion> PendingConversions { get; set; } = new();
        public bool HasUnconvertedUsd { get; set; }
    }
}
