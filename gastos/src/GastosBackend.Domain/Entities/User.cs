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

        /// <summary>
        /// Two-step verification is on: signing in also asks for a code from the authenticator app (or a recovery code).
        /// Off by default; the user turns it on from their settings.
        /// </summary>
        public bool TwoFactorEnabled { get; set; }

        /// <summary>
        /// The TOTP secret, encrypted with Data Protection (it has to be readable to check the codes, so it cannot be a hash).
        /// It is saved when the user starts turning the 2FA on, but it only counts once TwoFactorEnabled is true.
        /// </summary>
        public string? TwoFactorSecret { get; set; }

        /// <summary>The last 30-second step of an accepted code, so the same code cannot be used twice.</summary>
        public long? TwoFactorLastStep { get; set; }

        /// <summary>Wrong codes (or passwords, in the 2FA settings) in a row. A correct code sets it back to zero.</summary>
        public int TwoFactorFailedAttempts { get; set; }

        /// <summary>While this is in the future, no code is accepted, not even a correct one. Null when not locked.</summary>
        public DateTimeOffset? TwoFactorLockedUntil { get; set; }
    }
}
