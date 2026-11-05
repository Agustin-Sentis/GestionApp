using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/auth")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// This endpoint registers a new user and returns a JWT so the user is signed in right away.
        /// The password must have at least 8 characters, an uppercase letter, a lowercase letter, a number and a special character.
        /// </summary>
        [HttpPost("signup")]
        public async Task<ActionResult<AuthResponse>> SignUp([FromBody] SignUpRequest request)
        {
            return Ok(await _authService.SignUp(request));
        }

        /// <summary>
        /// This endpoint signs in an existing user and returns a JWT.
        /// </summary>
        [HttpPost("signin")]
        public async Task<ActionResult<AuthResponse>> SignIn([FromBody] SignInRequest request)
        {
            return Ok(await _authService.SignIn(request));
        }
    }
}
