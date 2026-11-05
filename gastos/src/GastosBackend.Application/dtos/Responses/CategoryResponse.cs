using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class CategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
    }
}
