using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> SignUp(SignUpRequest request);

        /// <summary>The session, or (with two-step verification on) a challenge to finish with <see cref="VerifyTwoFactor"/>.</summary>
        Task<SignInResponse> SignIn(SignInRequest request);

        /// <summary>Exchanges the challenge of a sign in and a valid code for the session.</summary>
        Task<AuthResponse> VerifyTwoFactor(VerifyTwoFactorRequest request);
    }
}
