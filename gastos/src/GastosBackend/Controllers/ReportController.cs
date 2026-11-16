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

        /// <summary>
        /// This endpoint returns, in ARS, the income, expenses and balance of each month from "from" to "to" (yyyy-MM, both
        /// included, at most 24 months) plus the totals of the period. Months with no movements come with zeros. USD
        /// transactions are converted like in the monthly report: with the rate stored with each one.
        /// </summary>
        [HttpGet("evolution")]
        public async Task<ActionResult<EvolutionReportResponse>> GetEvolution([FromQuery] string from, [FromQuery] string to)
        {
            return Ok(await _reportService.GetEvolution(from, to, UserId));
        }
    }
}
