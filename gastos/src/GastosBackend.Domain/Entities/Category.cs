using GastosBackend.Domain.Enums;

namespace GastosBackend.Domain.Entities
{
    public class Category : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public Guid UserId { get; set; }

        /// <summary>
        /// What the user plans to spend on this category each month, in ARS. Null means no budget.
        /// Only expense categories can have one. It is a single amount for every month: changing it applies to past months too.
        /// </summary>
        public decimal? MonthlyBudget { get; set; }
    }
}
