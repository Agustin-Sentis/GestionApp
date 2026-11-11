using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class SettingsResponse
    {
        /// <summary>The dollar the user's USD amounts are converted with.</summary>
        public DollarType DollarType { get; set; }

        /// <summary>The address of the account: where the budget alerts and the confirmation mail go.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>The user asked to get a mail when a budget reaches 80% or is exceeded.</summary>
        public bool BudgetEmailAlerts { get; set; }

        /// <summary>The user confirmed the address with the link of the mail. Without it no alert is sent, even if they are turned on.</summary>
        public bool EmailConfirmed { get; set; }
    }
}
