using System.Text.Json.Serialization;

namespace GastosBackend.Application.dtos.Responses
{
    /// <summary>
    /// What signing in answers. Without two-step verification it is the same as before (token, user, expiry) plus
    /// <c>requiresTwoFactor: false</c>. With it, there is no session yet: only a short-lived challenge token to send,
    /// with the code, to <c>POST /api/auth/2fa/verify</c>.
    /// </summary>
    public class SignInResponse
    {
        public bool RequiresTwoFactor { get; set; }

        /// <summary>Only when <see cref="RequiresTwoFactor"/> is true. It is not a session: no other endpoint accepts it.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ChallengeToken { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Token { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? UserId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Email { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? ExpiresAtUtc { get; set; }

        public static SignInResponse FromSession(AuthResponse session) => new()
        {
            Token = session.Token,
            UserId = session.UserId,
            Email = session.Email,
            ExpiresAtUtc = session.ExpiresAtUtc
        };
    }
}
