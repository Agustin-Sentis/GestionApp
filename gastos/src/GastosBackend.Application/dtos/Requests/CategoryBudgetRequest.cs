namespace GastosBackend.Application.dtos.Requests
{
    public class CategoryBudgetRequest
    {
        /// <summary>The monthly budget in ARS, or null to remove it.</summary>
        public decimal? MonthlyBudget { get; set; }
    }
}
