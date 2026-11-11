using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Mapper;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITransactionRepository _transactionRepository;

        public CategoryService(ICategoryRepository categoryRepo, ITransactionRepository transactionRepo)
        {
            _categoryRepository = categoryRepo;
            _transactionRepository = transactionRepo;
        }

        public async Task<List<CategoryResponse>> GetAll(Guid userId)
        {
            var categories = await _categoryRepository.GetAllByUser(userId);
            return categories.Select(c => c.ToCategoryResponse()).ToList();
        }

        public async Task<CategoryResponse> Create(CategoryRequest request, Guid userId)
        {
            var validations = new CategoryRequestValidations();
            var result = validations.Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var name = request.Name.Trim();
            var duplicateMessage = $"Ya existe una categoría con el nombre '{name}'.";

            if (await _categoryRepository.ExistsByName(userId, name))
            {
                throw new ConflictException(duplicateMessage);
            }

            var newCategory = request.ToCategory(userId);
            try
            {
                await _categoryRepository.Add(newCategory);
            }
            catch (DuplicateException)
            {
                // Another request created the same category between the check above and the insert
                throw new ConflictException(duplicateMessage);
            }

            return newCategory.ToCategoryResponse();
        }

        public async Task<CategoryResponse> SetBudget(Guid id, CategoryBudgetRequest request, Guid userId)
        {
            var result = new CategoryBudgetRequestValidations().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var category = await _categoryRepository.Get(id);

            if (category == null || category.UserId != userId)
            {
                throw new NotFoundException($"No se encontró ninguna categoría con el id '{id}'.");
            }

            // Removing a budget is always fine; setting one only makes sense for what is spent
            if (request.MonthlyBudget != null && category.Type != TransactionType.Expense)
            {
                throw new ValidationException("Solo las categorías de gasto pueden tener presupuesto.");
            }

            category.MonthlyBudget = request.MonthlyBudget == null ? null : Math.Round(request.MonthlyBudget.Value, 2);
            await _categoryRepository.Update(category);

            return category.ToCategoryResponse();
        }

        public async Task Delete(Guid id, Guid userId)
        {
            var category = await _categoryRepository.Get(id);

            if (category == null || category.UserId != userId)
            {
                throw new NotFoundException($"No se encontró ninguna categoría con el id '{id}'.");
            }

            if (await _transactionRepository.ExistsByCategory(id))
            {
                throw new ConflictException("La categoría tiene movimientos asociados y no se puede eliminar.");
            }

            await _categoryRepository.Delete(id);
        }
    }
}
