using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/auth")]
    [ApiController]
    [AllowAnonymous]
    [EnableRateLimiting(Program.AuthRateLimitPolicy)]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPasswordResetService _passwordResetService;

        public AuthController(IAuthService authService, IPasswordResetService passwordResetService)
        {
            _authService = authService;
            _passwordResetService = passwordResetService;
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

        /// <summary>
        /// This endpoint starts the recovery of a password: if the email has an account, a mail with a link (valid for one hour)
        /// is sent to it. It answers 204 in every case, registered or not, so it cannot be used to find out which emails have
        /// an account. At most one mail per minute is sent to the same account.
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await _passwordResetService.RequestReset(request);
            return NoContent();
        }

        /// <summary>
        /// This endpoint sets a new password with the token of the recovery mail. The token works once. It closes every session
        /// opened before, confirms the email address, and does not sign anyone in. The password follows the same rules as when signing up.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            await _passwordResetService.ResetPassword(request);
            return NoContent();
        }
    }
}
