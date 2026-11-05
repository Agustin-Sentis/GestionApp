using GastosBackend.Domain.Enums;

namespace GastosBackend.Domain.Entities
{
    public class Category : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public Guid UserId { get; set; }
    }
}
