using System.Security.Claims;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        private string Sub => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        /// <summary>
        /// This endpoint retrieves the transactions of the authenticated user (up to 200, newest first).
        /// It can be filtered by year and month (both are required together) and by category.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<TransactionResponse>>> GetAll([FromQuery] TransactionFilterRequest filter)
        {
            return Ok(await _transactionService.GetAll(filter, Sub));
        }

        /// <summary>
        /// This endpoint creates a new transaction in ARS or USD for one of the authenticated user's categories.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TransactionResponse>> Create([FromBody] TransactionRequest request)
        {
            var created = await _transactionService.Create(request, Sub);
            return CreatedAtAction(nameof(GetAll), created);
        }

        /// <summary>
        /// This endpoint updates an existing transaction by its unique identifier (id).
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult> Update([FromBody] TransactionRequest request, [FromRoute] Guid id)
        {
            await _transactionService.Update(request, id, Sub);
            return NoContent();
        }

        /// <summary>
        /// This endpoint deletes a transaction by its unique identifier (id).
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete([FromRoute] Guid id)
        {
            await _transactionService.Delete(id, Sub);
            return NoContent();
        }
    }
}
