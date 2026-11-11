using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Mapper
{
    public static class CategoryMapper
    {
        public static CategoryResponse ToCategoryResponse(this Category category)
        {
            return new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Type = category.Type,
                MonthlyBudget = category.MonthlyBudget
            };
        }

        public static Category ToCategory(this CategoryRequest request, Guid userId)
        {
            return new Category
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Type = request.Type,
                UserId = userId
            };
        }
    }
}
