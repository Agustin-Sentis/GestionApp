using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    /// <summary>
    /// One month's total of one kind of movement, as the database computes it: the evolution report adds these up per month.
    /// Same meaning of Total and PendingUsd as <see cref="CategoryTotalRow"/>, but grouped by month instead of by category.
    /// </summary>
    public class MonthTotalRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public TransactionType Type { get; set; }
        public Currency Currency { get; set; }

        /// <summary>
        /// For USD rows, the dollar the transactions of the row were saved with (each transaction is converted with its own).
        /// Always Official for rows in pesos, so they are not split by something that does not apply to them.
        /// </summary>
        public DollarType DollarType { get; set; }

        /// <summary>Total already expressed in ARS: ARS amounts as they are, USD amounts multiplied by their stored rate.</summary>
        public decimal Total { get; set; }

        /// <summary>Sum in USD of the transactions that have no stored rate yet (of their own dollar). They still need to be converted.</summary>
        public decimal PendingUsd { get; set; }
    }
}
