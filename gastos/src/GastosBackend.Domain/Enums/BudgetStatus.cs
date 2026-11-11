namespace GastosBackend.Domain.Enums
{
    /// <summary>How much of a category's monthly budget has been spent.</summary>
    public enum BudgetStatus
    {
        /// <summary>Under 80% of the budget.</summary>
        Ok = 0,

        /// <summary>80% of the budget or more, but not over it yet (spending exactly the whole budget is still this).</summary>
        Warning = 1,

        /// <summary>More than the budget.</summary>
        Exceeded = 2
    }
}
