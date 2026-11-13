namespace GastosBackend.Application.dtos.Responses
{
    public class TwoFactorSetupResponse
    {
        /// <summary>The otpauth:// URI the frontend draws as a QR code.</summary>
        public string Uri { get; set; } = string.Empty;

        /// <summary>The same secret as text, for whoever cannot scan the QR.</summary>
        public string Secret { get; set; } = string.Empty;
    }

    /// <summary>The recovery codes in plain text. This is the only time they are shown.</summary>
    public class RecoveryCodesResponse
    {
        public IReadOnlyList<string> Codes { get; set; } = Array.Empty<string>();
    }
}
