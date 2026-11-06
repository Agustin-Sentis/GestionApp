using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exports;
using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionController : ApiControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        /// This endpoint retrieves one page of the authenticated user's transactions, newest first, together with the total count.
        /// Filters (all optional): year + month, or from + to (inclusive) but not both; categoryId; type (Expense or Income).
        /// page starts at 1; pageSize defaults to 20 and cannot exceed 100.
        /// </summary>
        [HttpGet("paged")]
        public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetPage([FromQuery] TransactionPageRequest request)
        {
            return Ok(await _transactionService.GetPage(request, UserId));
        }

        /// <summary>
        /// This endpoint downloads the authenticated user's transactions as a CSV file (for Excel: ";" separator, decimal comma).
        /// It takes the same filters as the paged list (page and pageSize are ignored) and refuses more than 10,000 rows.
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] TransactionPageRequest request)
        {
            var transactions = await _transactionService.GetForExport(request, UserId);
            return File(TransactionCsv.Build(transactions), "text/csv; charset=utf-8", "movimientos.csv");
        }

        /// <summary>
        /// This endpoint retrieves a transaction of the authenticated user by its unique identifier (id).
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TransactionResponse>> GetById([FromRoute] Guid id)
        {
            return Ok(await _transactionService.GetById(id, UserId));
        }

        /// <summary>
        /// This endpoint creates a new transaction in ARS or USD for one of the authenticated user's categories.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TransactionResponse>> Create([FromBody] TransactionRequest request)
        {
            var created = await _transactionService.Create(request, UserId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>
        /// This endpoint updates an existing transaction by its unique identifier (id).
        /// The transaction can be moved to a category of the other type (Expense/Income) on purpose, to fix a wrong category.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult> Update([FromBody] TransactionRequest request, [FromRoute] Guid id)
        {
            await _transactionService.Update(request, id, UserId);
            return NoContent();
        }

        /// <summary>
        /// This endpoint deletes a transaction by its unique identifier (id).
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> Delete([FromRoute] Guid id)
        {
            await _transactionService.Delete(id, UserId);
            return NoContent();
        }
    }
}
