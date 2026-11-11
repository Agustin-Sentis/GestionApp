using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<List<CategoryResponse>> GetAll(Guid userId);
        Task<CategoryResponse> Create(CategoryRequest request, Guid userId);
        Task<CategoryResponse> SetBudget(Guid id, CategoryBudgetRequest request, Guid userId);
        Task Delete(Guid id, Guid userId);
    }
}
