using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReportController : ApiControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// This endpoint returns the monthly summary of the authenticated user in ARS: income, expenses, balance and totals by category.
        /// Transactions in USD are converted with the official sell rate from dolarapi.com.
        /// </summary>
        [HttpGet("monthly")]
        public async Task<ActionResult<MonthlyReportResponse>> GetMonthly([FromQuery] int year, [FromQuery] int month)
        {
            return Ok(await _reportService.GetMonthly(year, month, UserId));
        }
    }
}
