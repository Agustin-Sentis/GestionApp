namespace GastosBackend.Application.dtos.Requests
{
    public class ChangeEmailRequest
    {
        /// <summary>The address the account should have from now on.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>The current password: the address is the key to the account (recovery mails go there), so changing it asks for it.</summary>
        public string Password { get; set; } = string.Empty;
    }
}
