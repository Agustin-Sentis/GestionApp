using GastosBackend.Domain.Enums;

namespace GastosBackend.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;

        /// <summary>Hash BCrypt de la contraseña (nunca el texto plano).</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// The dollar new USD transactions are saved with. Changing it does not touch the transactions already saved:
        /// each one keeps the dollar it was saved with (see Transaction.DollarType).
        /// </summary>
        public DollarType DollarType { get; set; } = DollarType.Official;

        /// <summary>The user asked to get a mail when a budget reaches 80% or is exceeded. Off until they turn it on.</summary>
        public bool BudgetEmailAlerts { get; set; }

        /// <summary>When the user clicked the link of the confirmation mail. Null: the address is not confirmed, so no alert is sent.</summary>
        public DateTimeOffset? EmailConfirmedAt { get; set; }

        /// <summary>
        /// When the password was last reset. Sessions (JWTs) issued before this moment stop working: restoring an account must
        /// also lock out whoever had been using it. Null while the password was never reset.
        /// </summary>
        public DateTimeOffset? PasswordChangedAt { get; set; }
    }
}
