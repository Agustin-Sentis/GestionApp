using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public Currency Currency { get; set; }

        /// <summary>
        /// USD sell rate stored with the transaction, of the dollar it was saved with (see DollarType).
        /// Null for ARS and for USD transactions that have no stored rate of that dollar yet.
        /// </summary>
        public decimal? ExchangeRate { get; set; }

        /// <summary>Amount expressed in ARS with that rate. Null only for USD transactions that have no stored rate of their dollar yet.</summary>
        public decimal? ArsAmount { get; set; }

        /// <summary>
        /// The dollar ExchangeRate and ArsAmount are expressed with: the one the user had chosen when the transaction was saved.
        /// It does not change when the user chooses another one later.
        /// </summary>
        public DollarType DollarType { get; set; }
        public DateOnly Date { get; set; }
        public string? Note { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
    }
}
