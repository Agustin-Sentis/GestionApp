using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> SignUp(SignUpRequest request);
        Task<AuthResponse> SignIn(SignInRequest request);
    }
}
