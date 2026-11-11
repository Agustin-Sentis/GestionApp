namespace GastosBackend.Application.dtos.Requests
{
    public class BudgetAlertsRequest
    {
        /// <summary>True to get a mail when a budget reaches 80% or is exceeded; false to stop getting them.</summary>
        public bool Enabled { get; set; }
    }
}
