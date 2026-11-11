using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Mapper;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IDolarApiService _dolarApiService;
        private readonly IUserRepository _userRepository;
        private readonly IBudgetAlertDispatcher _budgetAlerts;

        public TransactionService(
            ITransactionRepository transactionRepo,
            ICategoryRepository categoryRepo,
            IDolarApiService dolarApiService,
            IUserRepository userRepo,
            IBudgetAlertDispatcher budgetAlerts)
        {
            _transactionRepository = transactionRepo;
            _categoryRepository = categoryRepo;
            _dolarApiService = dolarApiService;
            _userRepository = userRepo;
            _budgetAlerts = budgetAlerts;
        }

        /// <summary>
        /// An expense was saved: ask for the budget alert check to run in the background. Income has no budget. Nothing that goes
        /// wrong here may undo or fail the save, which already happened.
        /// </summary>
        private void CheckBudgetAlert(Category category, DateOnly date, Guid userId)
        {
            if (category.Type != TransactionType.Expense)
            {
                return;
            }

            try
            {
                _budgetAlerts.Dispatch(userId, category.Id, date);
            }
            catch
            {
                // The dispatcher logs its own failures; this is only the last safety net around a finished save
            }
        }

        /// <summary>
        /// The dollar the user has chosen for new USD transactions. An unknown user (which a valid token should not produce)
        /// gets the official one instead of an error.
        /// </summary>
        private async Task<DollarType> GetDollarType(Guid userId)
        {
            return (await _userRepository.Get(userId))?.DollarType ?? DollarType.Official;
        }

        /// <summary>The most transactions one export can hold. More than that is refused instead of cut off silently.</summary>
        public const int MaxExportRows = 10_000;

        public async Task<PagedResponse<TransactionResponse>> GetPage(TransactionPageRequest request, Guid userId)
        {
            ValidatePageRequest(request);

            var (from, to) = ResolveDateRange(request);

            // long math: a huge page number must not overflow into a negative offset
            var skip = (int)Math.Min((long)(request.Page - 1) * request.PageSize, int.MaxValue);
            var page = await _transactionRepository.GetPageByUser(
                userId, from, to, request.CategoryId, request.Type, NormalizeSearch(request.Search), skip, request.PageSize);

            return new PagedResponse<TransactionResponse>
            {
                Items = page.Items.Select(t => t.ToTransactionResponse()).ToList(),
                Total = page.Total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        public async Task<List<TransactionResponse>> GetForExport(TransactionPageRequest request, Guid userId)
        {
            // Only the filters count for an export: whatever page or page size came along is ignored
            var filters = new TransactionPageRequest
            {
                Year = request.Year,
                Month = request.Month,
                From = request.From,
                To = request.To,
                CategoryId = request.CategoryId,
                Type = request.Type,
                Search = request.Search
            };
            ValidatePageRequest(filters);

            var (from, to) = ResolveDateRange(filters);
            var result = await _transactionRepository.GetPageByUser(
                userId, from, to, filters.CategoryId, filters.Type, NormalizeSearch(filters.Search), 0, MaxExportRows);

            if (result.Total > MaxExportRows)
            {
                throw new ValidationException(
                    $"Hay más de {MaxExportRows} movimientos para exportar. Acotá los filtros (mes, categoría o rango de fechas).");
            }

            return result.Items.Select(t => t.ToTransactionResponse()).ToList();
        }

        private static void ValidatePageRequest(TransactionPageRequest request)
        {
            var result = new TransactionPageRequestValidations().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }
        }

        /// <summary>A month is just a date range: first day to last day (both included).</summary>
        private static (DateOnly? From, DateOnly? To) ResolveDateRange(TransactionPageRequest request)
        {
            if (request.Year != null && request.Month != null)
            {
                var from = new DateOnly(request.Year.Value, request.Month.Value, 1);
                return (from, from.AddMonths(1).AddDays(-1));
            }

            return (request.From, request.To);
        }

        /// <summary>The text without the spaces around it; blank text means "no search".</summary>
        private static string? NormalizeSearch(string? search)
        {
            return string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        }

        public async Task<TransactionResponse> GetById(Guid id, Guid userId)
        {
            var transaction = await _transactionRepository.GetByIdWithCategory(id, userId);

            if (transaction == null)
            {
                throw new NotFoundException($"No se encontró ningún movimiento con el id '{id}'.");
            }

            return transaction.ToTransactionResponse();
        }

        public async Task<TransactionResponse> Create(TransactionRequest request, Guid userId)
        {
            ValidateRequest(request);

            var category = await _categoryRepository.Get(request.CategoryId);
            if (category == null || category.UserId != userId)
            {
                throw new ValidationException("La categoría indicada no existe.");
            }

            var newTransaction = request.ToTransaction(category, userId);
            // The transaction stays with the dollar the user has chosen right now, whatever they choose later
            if (request.Currency == Currency.USD)
            {
                newTransaction.DollarType = await GetDollarType(userId);
            }

            newTransaction.ApplyRates(await ResolveExchangeRates(request.Currency));
            await _transactionRepository.Add(newTransaction);
            CheckBudgetAlert(category, request.Date, userId);
            return newTransaction.ToTransactionResponse();
        }

        public async Task Update(TransactionRequest request, Guid id, Guid userId)
        {
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

            // New rates are only needed when what they apply to changed, or when a previous attempt failed (no rate of its dollar).
            // A transaction recorded before the blue and MEP rates were stored keeps them empty: they are not made up now.
            var needsNewRates = request.Currency == Currency.USD
                && (transactionToUpdate.Currency != Currency.USD
                    || transactionToUpdate.Amount != request.Amount
                    || transactionToUpdate.Date != request.Date
                    || transactionToUpdate.RateOf(transactionToUpdate.DollarType) == null);

            var updated = transactionToUpdate.ToTransactionUpdate(request);

            if (request.Currency == Currency.ARS)
            {
                updated.ApplyRates(UsdRates.None);
                updated.DollarType = DollarType.Official;
            }
            else if (needsNewRates)
            {
                // Taking the rates again is saving again: it is done with the dollar the user has chosen now.
                // An edit that does not need new rates (only the note, the category) leaves the dollar it had.
                updated.DollarType = await GetDollarType(userId);
                updated.ApplyRates(await ResolveExchangeRates(request.Currency));
            }

            await _transactionRepository.Update(updated);
            CheckBudgetAlert(category, request.Date, userId);
        }

        public async Task Delete(Guid id, Guid userId)
        {
            var transaction = await _transactionRepository.Get(id);

            if (transaction == null || transaction.UserId != userId)
            {
                throw new NotFoundException($"No se encontró ningún movimiento con el id '{id}'.");
            }

            await _transactionRepository.Delete(id);
        }

        /// <summary>
        /// Today's official, blue and MEP USD sell rates for a USD transaction; none for ARS.
        /// If the rate provider fails the transaction is still saved with whatever was obtained (nothing, at worst),
        /// and the report resolves the missing ones later.
        /// </summary>
        private async Task<UsdRates> ResolveExchangeRates(Currency currency)
        {
            if (currency != Currency.USD)
            {
                return UsdRates.None;
            }

            try
            {
                return await _dolarApiService.GetUsdSellRates();
            }
            catch (ExternalServiceException)
            {
                return UsdRates.None;
            }
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
    }
}
