using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Mapper;
using GastosBackend.Application.Validations;

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

        public async Task<List<CategoryResponse>> GetAll(string sub)
        {
            var userId = ParseUserId(sub);
            var categories = await _categoryRepository.GetAllByUser(userId);
            return categories.Select(c => c.ToCategoryResponse()).ToList();
        }

        public async Task<CategoryResponse> Create(CategoryRequest request, string sub)
        {
            var userId = ParseUserId(sub);

            var validations = new CategoryRequestValidations();
            var result = validations.Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            if (await _categoryRepository.ExistsByName(userId, request.Name.Trim()))
            {
                throw new ConflictException($"Ya existe una categoría con el nombre '{request.Name.Trim()}'.");
            }

            var newCategory = request.ToCategory(userId);
            await _categoryRepository.Add(newCategory);
            return newCategory.ToCategoryResponse();
        }

        public async Task Delete(Guid id, string sub)
        {
            var userId = ParseUserId(sub);
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

        private static Guid ParseUserId(string sub)
        {
            if (!Guid.TryParse(sub, out Guid userId))
            {
                throw new ValidationException("El ID enviado no es válido");
            }
            return userId;
        }
    }
}
