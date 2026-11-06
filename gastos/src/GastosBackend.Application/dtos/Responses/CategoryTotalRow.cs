using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    /// <summary>Row returned by the repository: totals per category and currency.</summary>
    public class CategoryTotalRow
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public Currency Currency { get; set; }

        /// <summary>Total already expressed in ARS: ARS amounts as they are, USD amounts multiplied by their stored rate.</summary>
        public decimal Total { get; set; }

        /// <summary>Sum in USD of the transactions that have no stored rate yet. They still need to be converted.</summary>
        public decimal PendingUsd { get; set; }
    }
}
