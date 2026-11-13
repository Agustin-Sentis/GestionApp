using GastosBackend.Application.Interfaces;
using OtpNet;

namespace GastosBackend.Infrastructure.ExternalService
{
    public class TotpService : ITotpService
    {
        private const string Issuer = "DualBalance";
        private const int SecretBytes = 20;
        private const int Digits = 6;

        // One step of tolerance on each side: a phone clock that is up to 30 seconds off still works
        private static readonly VerificationWindow Window = new(previous: 1, future: 1);

        private readonly TimeProvider _clock;

        public TotpService(TimeProvider clock)
        {
            _clock = clock;
        }

        public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretBytes));

        public string BuildUri(string email, string secret) =>
            $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Issuer}";

        public bool Verify(string secret, string code, long? lastUsedStep, out long matchedStep)
        {
            matchedStep = 0;

            if (code is not { Length: Digits } || !code.All(char.IsAsciiDigit))
            {
                return false;
            }

            var totp = new Totp(Base32Encoding.ToBytes(secret), mode: OtpHashMode.Sha1, totpSize: Digits);
            if (!totp.VerifyTotp(_clock.GetUtcNow().UtcDateTime, code, out matchedStep, Window))
            {
                return false;
            }

            // The same code (or an older one) cannot be used twice
            return lastUsedStep is null || matchedStep > lastUsedStep;
        }
    }
}
