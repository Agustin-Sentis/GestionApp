using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Mapper;
using GastosBackend.Application.Validations;

namespace GastosBackend.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private const int MaxResults = 200;

        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;

        public TransactionService(ITransactionRepository transactionRepo, ICategoryRepository categoryRepo)
        {
            _transactionRepository = transactionRepo;
            _categoryRepository = categoryRepo;
        }

        public async Task<List<TransactionResponse>> GetAll(TransactionFilterRequest filter, string sub)
        {
            var userId = ParseUserId(sub);

            DateOnly? from = null;
            DateOnly? to = null;

            if (filter.Year != null || filter.Month != null)
            {
                if (filter.Year == null || filter.Month == null
                    || filter.Month < 1 || filter.Month > 12
                    || filter.Year < 2000 || filter.Year > 2100)
                {
                    throw new ValidationException("El año y el mes deben indicarse juntos y ser válidos.");
                }

                from = new DateOnly(filter.Year.Value, filter.Month.Value, 1);
                to = from.Value.AddMonths(1);
            }

            var transactions = await _transactionRepository.GetAllByUser(userId, from, to, filter.CategoryId, MaxResults);
            return transactions.Select(t => t.ToTransactionResponse()).ToList();
        }

        public async Task<TransactionResponse> Create(TransactionRequest request, string sub)
        {
            var userId = ParseUserId(sub);
            ValidateRequest(request);

            var category = await _categoryRepository.Get(request.CategoryId);
            if (category == null || category.UserId != userId)
            {
                throw new ValidationException("La categoría indicada no existe.");
            }

            var newTransaction = request.ToTransaction(category, userId);
            await _transactionRepository.Add(newTransaction);
            return newTransaction.ToTransactionResponse();
        }

        public async Task Update(TransactionRequest request, Guid id, string sub)
        {
            var userId = ParseUserId(sub);
            var transactionToUpdate = await _transactionRepository.Get(id);

            if (transactionToUpdate == null || transactionToUpdate.UserId != userId)
            {
                throw new NotFoundException($"No se encontró ningún movimiento con el id '{id}'.");
            }

            ValidateRequest(request);

            var category = await _categoryRepository.Get(request.CategoryId);
            if (category == null || category.UserId != userId)
            {
                throw new ValidationException("La categoría indicada no existe.");
            }

            await _transactionRepository.Update(transactionToUpdate.ToTransactionUpdate(request));
        }

        public async Task Delete(Guid id, string sub)
        {
            var userId = ParseUserId(sub);
            var transaction = await _transactionRepository.Get(id);

            if (transaction == null || transaction.UserId != userId)
            {
                throw new NotFoundException($"No se encontró ningún movimiento con el id '{id}'.");
            }

            await _transactionRepository.Delete(id);
        }

        private static void ValidateRequest(TransactionRequest request)
        {
            var validations = new TransactionRequestValidations();
            var result = validations.Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }
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
