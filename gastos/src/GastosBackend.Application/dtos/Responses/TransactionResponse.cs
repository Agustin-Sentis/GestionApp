using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public Currency Currency { get; set; }

        /// <summary>USD sell rate stored with the transaction. Null for ARS and for USD transactions still waiting for a rate.</summary>
        public decimal? ExchangeRate { get; set; }

        /// <summary>Amount expressed in ARS. Null only for USD transactions that have no stored rate yet.</summary>
        public decimal? ArsAmount { get; set; }
        public DateOnly Date { get; set; }
        public string? Note { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
    }
}
