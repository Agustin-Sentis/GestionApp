using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SettingsController : ApiControllerBase
    {
        private readonly ISettingsService _settingsService;

        public SettingsController(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// This endpoint retrieves the settings of the authenticated user: which dollar (Official, Blue or Mep)
        /// their USD amounts are converted with.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<SettingsResponse>> Get()
        {
            return Ok(await _settingsService.Get(UserId));
        }

        /// <summary>
        /// This endpoint changes the settings of the authenticated user. Choosing another dollar does not modify any
        /// transaction: it only changes which of the rates stored with each one is used in the lists, the reports and the export.
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<SettingsResponse>> Update([FromBody] SettingsRequest request)
        {
            return Ok(await _settingsService.Update(request, UserId));
        }

        /// <summary>
        /// This endpoint turns the budget alert mails on or off. Turning them on with an address that has not been confirmed
        /// sends a confirmation mail (at most one per minute); the alerts start once its link is opened.
        /// </summary>
        [HttpPut("budget-alerts")]
        public async Task<ActionResult<SettingsResponse>> SetBudgetAlerts([FromBody] BudgetAlertsRequest request)
        {
            return Ok(await _settingsService.SetBudgetAlerts(request, UserId));
        }

        /// <summary>
        /// This endpoint confirms the address of an account with the token of the confirmation mail. It needs no session,
        /// because the link may be opened on another device; it is rate limited like the sign in.
        /// </summary>
        [HttpPost("confirm-email")]
        [AllowAnonymous]
        [EnableRateLimiting(Program.AuthRateLimitPolicy)]
        public async Task<ActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            await _settingsService.ConfirmEmail(request);
            return NoContent();
        }
    }
}
