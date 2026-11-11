namespace GastosBackend.Application.dtos.Requests
{
    public class ConfirmEmailRequest
    {
        /// <summary>The token from the link of the confirmation mail.</summary>
        public string? Token { get; set; }
    }
}
