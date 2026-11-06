using GastosBackend.Domain.Enums;

namespace GastosBackend.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        public decimal Amount { get; set; }
        public Currency Currency { get; set; }

        /// <summary>
        /// OFFICIAL USD sell rate (in ARS) in effect when the transaction was recorded.
        /// Null for ARS transactions and for USD transactions whose rate could not be obtained yet.
        /// (The column is called ExchangeRate because it was the only rate before the blue and MEP ones existed:
        /// renaming it would break the previous version of the API while a deploy is in progress.)
        /// </summary>
        public decimal? ExchangeRate { get; set; }

        /// <summary>
        /// Blue USD sell rate (in ARS) in effect when the transaction was recorded.
        /// Null for ARS transactions, when it could not be obtained, and for transactions recorded before it was stored.
        /// </summary>
        public decimal? BlueExchangeRate { get; set; }

        /// <summary>MEP USD sell rate (in ARS) in effect when the transaction was recorded. Null in the same cases as the blue one.</summary>
        public decimal? MepExchangeRate { get; set; }

        /// <summary>
        /// The dollar this transaction is converted with: the one the user had chosen when it was saved (or, when the rates
        /// were taken again after an edit, when that happened). Choosing another dollar later does not change it.
        /// Only means something for USD transactions; a transaction in pesos keeps Official.
        /// Existing rows are Official, which is the rate they stored.
        /// </summary>
        public DollarType DollarType { get; set; } = DollarType.Official;

        public DateOnly Date { get; set; }
        public string? Note { get; set; }
        public Guid CategoryId { get; set; }
        public Category? Category { get; set; }
        public Guid UserId { get; set; }
    }
}
