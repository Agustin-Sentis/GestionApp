using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Requests
{
    public class TransactionRequest
    {
        public decimal Amount { get; set; }
        public Currency Currency { get; set; }
        public DateOnly Date { get; set; }
        public string? Note { get; set; }
        public Guid CategoryId { get; set; }
    }
}
