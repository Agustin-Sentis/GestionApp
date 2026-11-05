using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Requests
{
    public class CategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
    }
}
