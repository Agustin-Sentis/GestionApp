using GastosBackend.Domain.Enums;

namespace GastosBackend.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        public decimal Amount { get; set; }
        public Currency Currency { get; set; }

        /// <summary>
        /// USD sell rate (in ARS) in effect when the transaction was recorded.
        /// Null for ARS transactions and for USD transactions whose rate could not be obtained yet.
        /// </summary>
        public decimal? ExchangeRate { get; set; }
        public DateOnly Date { get; set; }
        public string? Note { get; set; }
        public Guid CategoryId { get; set; }
        public Category? Category { get; set; }
        public Guid UserId { get; set; }
    }
}
