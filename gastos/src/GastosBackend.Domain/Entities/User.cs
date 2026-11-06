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
    }
}
