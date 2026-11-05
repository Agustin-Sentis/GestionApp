using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<List<CategoryResponse>> GetAll(string sub);
        Task<CategoryResponse> Create(CategoryRequest request, string sub);
        Task Delete(Guid id, string sub);
    }
}
