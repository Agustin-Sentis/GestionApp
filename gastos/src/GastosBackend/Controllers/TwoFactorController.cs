using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GastosBackend.Presentation.Controllers
{
    /// <summary>
    /// The settings of the two-step verification (an authenticator app such as Google Authenticator, plus recovery codes).
    /// Every action asks for the current password and/or a code, and the controller is rate limited like the sign in because
    /// they check one. A wrong password or code is a 400, not a 401: the app signs the user out on a 401. After 5 wrong
    /// codes in a row the answer is 429 for 15 minutes.
    /// </summary>
    [Route("api/settings/2fa")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting(Program.AuthRateLimitPolicy)]
    public class TwoFactorController : ApiControllerBase
    {
        private readonly ITwoFactorService _twoFactorService;

        public TwoFactorController(ITwoFactorService twoFactorService)
        {
            _twoFactorService = twoFactorService;
        }

        /// <summary>
        /// This endpoint starts turning the two-step verification on. It asks for the password and returns the otpauth:// URI
        /// (to draw as a QR code) and the secret as text. Nothing is active until <c>enable</c> confirms a code. Calling it
        /// again replaces the secret of an activation left halfway.
        /// </summary>
        [HttpPost("setup")]
        public async Task<ActionResult<TwoFactorSetupResponse>> Setup([FromBody] TwoFactorSetupRequest request)
        {
            return Ok(await _twoFactorService.Setup(UserId, request.Password));
        }

        /// <summary>
        /// This endpoint turns the two-step verification on with the first code of the app, and returns the 10 recovery codes.
        /// It is the only time they are shown.
        /// </summary>
        [HttpPost("enable")]
        public async Task<ActionResult<RecoveryCodesResponse>> Enable([FromBody] TwoFactorEnableRequest request)
        {
            return Ok(await _twoFactorService.Enable(UserId, request.Code));
        }

        /// <summary>
        /// This endpoint turns the two-step verification off. It asks for the password and a code (of the app or a recovery one),
        /// and removes the secret and the recovery codes.
        /// </summary>
        [HttpPost("disable")]
        public async Task<ActionResult> Disable([FromBody] TwoFactorPasswordCodeRequest request)
        {
            await _twoFactorService.Disable(UserId, request.Password, request.Code);
            return NoContent();
        }

        /// <summary>
        /// This endpoint replaces the recovery codes with 10 new ones (the old ones stop working). It asks for the password and a
        /// code, and it is the only time the new ones are shown.
        /// </summary>
        [HttpPost("recovery-codes")]
        public async Task<ActionResult<RecoveryCodesResponse>> RegenerateRecoveryCodes([FromBody] TwoFactorPasswordCodeRequest request)
        {
            return Ok(await _twoFactorService.RegenerateRecoveryCodes(UserId, request.Password, request.Code));
        }
    }
}
