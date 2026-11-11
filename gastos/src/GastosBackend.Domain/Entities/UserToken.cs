using GastosBackend.Domain.Enums;

namespace GastosBackend.Domain.Entities
{
    /// <summary>
    /// A one-time token sent to a user by mail (a link). Only its hash is stored, so a leaked table cannot be used to
    /// confirm anything. It stops working when it is used or when it expires.
    /// </summary>
    public class UserToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public UserTokenPurpose Purpose { get; set; }

        /// <summary>SHA-256 of the token, in lower case hex.</summary>
        public string TokenHash { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>When it was used (or replaced by a newer one). Null while it can still be used.</summary>
        public DateTimeOffset? UsedAt { get; set; }
    }
}
