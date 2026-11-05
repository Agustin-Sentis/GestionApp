using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class TransactionResponse
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public Currency Currency { get; set; }
        public DateOnly Date { get; set; }
        public string? Note { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
    }
}
