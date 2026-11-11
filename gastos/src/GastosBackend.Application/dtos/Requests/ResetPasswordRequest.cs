namespace GastosBackend.Application.dtos.Requests
{
    public class ResetPasswordRequest
    {
        /// <summary>The token from the link of the recovery mail.</summary>
        public string? Token { get; set; }

        /// <summary>The new password. It has to meet the same rules as when signing up.</summary>
        public string NewPassword { get; set; } = string.Empty;
    }
}
