namespace GastosBackend.Application.Interfaces
{
    /// <summary>The time-based one-time codes (RFC 6238) an authenticator app such as Google Authenticator shows.</summary>
    public interface ITotpService
    {
        /// <summary>A new random secret, in Base32 (what the authenticator app stores).</summary>
        string GenerateSecret();

        /// <summary>The otpauth:// URI the QR code carries, so the app can add the account by scanning it.</summary>
        string BuildUri(string email, string secret);

        /// <summary>
        /// Whether <paramref name="code"/> is the 6-digit code of the current 30-second step, or of the step before or after
        /// (a phone clock that is a little off).
        /// </summary>
        /// <param name="lastUsedStep">The step of the last accepted code. A code of that step or an older one is rejected.</param>
        /// <param name="matchedStep">The step the code belongs to, when it is accepted.</param>
        bool Verify(string secret, string code, long? lastUsedStep, out long matchedStep);
    }
}
