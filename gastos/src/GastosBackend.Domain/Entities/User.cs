namespace GastosBackend.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;

        /// <summary>Hash BCrypt de la contraseña (nunca el texto plano).</summary>
        public string Password { get; set; } = string.Empty;
    }
}
