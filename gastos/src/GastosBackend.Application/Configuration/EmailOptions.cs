namespace GastosBackend.Application.Configuration
{
    /// <summary>Settings of the mail provider (section "Email"). The key comes from an environment variable, never from the repo.</summary>
    public class EmailOptions
    {
        /// <summary>API key of the provider. Empty: nothing is sent (the mails are only logged).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>The address mails come from. It has to be verified in the provider.</summary>
        public string SenderEmail { get; set; } = string.Empty;

        public string SenderName { get; set; } = "DualBalance";

        /// <summary>Public address of the frontend: the links in the mails point to it.</summary>
        public string AppBaseUrl { get; set; } = "http://localhost:5173";
    }
}
