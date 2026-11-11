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
    public class CategoryController : ApiControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// This endpoint retrieves all the categories of the authenticated user, ordered by name.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<CategoryResponse>>> GetAll()
        {
            return Ok(await _categoryService.GetAll(UserId));
        }

        /// <summary>
        /// This endpoint creates a new category (Expense or Income) for the authenticated user.
        /// Category names must be unique per user.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<CategoryResponse>> Create([FromBody] CategoryRequest request)
        {
            var created = await _categoryService.Create(request, UserId);
            return CreatedAtAction(nameof(GetAll), created);
        }

        /// <summary>
        /// This endpoint sets the monthly budget (in ARS) of an expense category, or removes it when MonthlyBudget is null.
        /// </summary>
        [HttpPut("{id}/budget")]
        public async Task<ActionResult<CategoryResponse>> SetBudget([FromRoute] Guid id, [FromBody] CategoryBudgetRequest request)
        {
            return Ok(await _categoryService.SetBudget(id, request, UserId));
        }

        /// <summary>
        /// This endpoint deletes a category by its unique identifier (id).
        /// A category that still has transactions cannot be deleted.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete([FromRoute] Guid id)
        {
            await _categoryService.Delete(id, UserId);
            return NoContent();
        }
    }
}
