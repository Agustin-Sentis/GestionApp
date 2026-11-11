using GastosBackend.Domain.Enums;

namespace GastosBackend.Domain.Entities
{
    /// <summary>
    /// The mark that a budget alert mail was sent: one per user, category, month and level. A unique index makes it
    /// impossible to send the same alert twice, even when two requests cross the same threshold at the same time.
    /// If the mail could not be sent the mark is deleted, so the next expense tries again.
    /// </summary>
    public class BudgetAlert : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid CategoryId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }

        /// <summary>Warning (80% or more) or Exceeded (over the budget). Never Ok.</summary>
        public BudgetStatus Level { get; set; }
    }
}
