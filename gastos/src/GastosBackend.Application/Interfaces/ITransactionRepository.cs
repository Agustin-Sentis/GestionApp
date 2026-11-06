using GastosBackend.Application.dtos.Responses;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Interfaces
{
    public interface ITransactionRepository : IBaseRepository<Transaction>
    {
        /// <summary>
        /// One page of the user's transactions (with their category) plus the total that match the filters.
        /// Newest first; ties on the date are broken by creation date and id so pages never overlap or skip rows.
        /// "toInclusive" is the last day to include. "search" is text to find inside the note, ignoring case (null: no filter).
        /// </summary>
        Task<PagedResult<Transaction>> GetPageByUser(
            Guid userId, DateOnly? from, DateOnly? toInclusive, Guid? categoryId, TransactionType? type, string? search,
            int skip, int take);

        /// <summary>A transaction of the user with its category loaded, or null if it does not exist or belongs to someone else.</summary>
        Task<Transaction?> GetByIdWithCategory(Guid id, Guid userId);

        Task<bool> ExistsByCategory(Guid categoryId);

        /// <summary>Totales agrupados por categoría y moneda (GROUP BY + SUM en la base).</summary>
        Task<List<CategoryTotalRow>> GetMonthlyTotals(Guid userId, DateOnly from, DateOnly toExclusive);
    }
}
