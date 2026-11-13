namespace GastosBackend.Application.dtos.Requests
{
    public class VerifyTwoFactorRequest
    {
        /// <summary>The challenge token the sign in answered with.</summary>
        public string ChallengeToken { get; set; } = string.Empty;

        /// <summary>The 6-digit code of the authenticator app, or a recovery code.</summary>
        public string Code { get; set; } = string.Empty;
    }
}
