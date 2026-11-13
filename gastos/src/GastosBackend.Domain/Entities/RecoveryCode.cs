namespace GastosBackend.Domain.Entities
{
    /// <summary>
    /// A one-time code the user can use instead of the authenticator code (a lost phone). Only its hash is stored, so a
    /// leaked table cannot be used to sign in. The plain codes are shown once, when they are generated.
    /// </summary>
    public class RecoveryCode : BaseEntity
    {
        public Guid UserId { get; set; }

        /// <summary>BCrypt hash of the normalized code (see IRecoveryCodeGenerator.Normalize).</summary>
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>When it was used. Null while it can still be used.</summary>
        public DateTimeOffset? UsedAt { get; set; }
    }
}
