using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure.Repository
{
    public class TransactionRepository : BaseRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(GastosDbContext context) : base(context)
        {

        }

        public async Task<PagedResult<Transaction>> GetPageByUser(
            Guid userId, DateOnly? from, DateOnly? toInclusive, Guid? categoryId, TransactionType? type, string? search,
            int skip, int take)
        {
            var query = _dbSet
                .Where(t => t.UserId == userId && !t.IsDeleted);

            if (from != null) query = query.Where(t => t.Date >= from.Value);
            if (toInclusive != null) query = query.Where(t => t.Date <= toInclusive.Value);
            if (categoryId != null) query = query.Where(t => t.CategoryId == categoryId.Value);
            if (type != null) query = query.Where(t => t.Category!.Type == type.Value);
            if (!string.IsNullOrEmpty(search))
            {
                // "contains, ignoring case". The text is data, not a pattern: %, _ and \ are escaped so they match themselves.
                var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                query = query.Where(t => t.Note != null && EF.Functions.ILike(t.Note, pattern, "\\"));
            }

            var total = await query.CountAsync();

            var items = await query
                .Include(t => t.Category)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.CreatedDate)
                .ThenByDescending(t => t.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return new PagedResult<Transaction>(items, total);
        }

        public async Task<Transaction?> GetByIdWithCategory(Guid id, Guid userId)
        {
            return await _dbSet
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);
        }

        public async Task<bool> ExistsByCategory(Guid categoryId)
        {
            return await _dbSet.AnyAsync(t => t.CategoryId == categoryId && !t.IsDeleted);
        }

        public async Task<List<CategoryTotalRow>> GetMonthlyTotals(Guid userId, DateOnly from, DateOnly toExclusive)
        {
            return await _dbSet
                .Where(t => t.UserId == userId && !t.IsDeleted && t.Date >= from && t.Date < toExclusive)
                .Select(t => new
                {
                    t.Category!.Name,
                    t.Category.Type,
                    t.Currency,
                    t.Amount,
                    // Only dollars have a dollar type: a movement in pesos must not split its category into several rows
                    DollarType = t.Currency == Currency.USD ? t.DollarType : DollarType.Official,
                    // Each transaction is converted with the rate of the dollar IT was saved with, whatever the user chose since
                    Rate = t.DollarType == DollarType.Blue ? t.BlueExchangeRate
                        : t.DollarType == DollarType.Mep ? t.MepExchangeRate
                        : t.ExchangeRate
                })
                .GroupBy(t => new { t.Name, t.Type, t.Currency, t.DollarType })
                .Select(g => new CategoryTotalRow
                {
                    Name = g.Key.Name,
                    Type = g.Key.Type,
                    Currency = g.Key.Currency,
                    DollarType = g.Key.DollarType,
                    // ARS as is; USD converted with the rate stored on each transaction (of its own dollar).
                    Total = g.Sum(t => t.Currency == Currency.ARS
                        ? t.Amount
                        : (t.Rate != null ? t.Amount * t.Rate.Value : 0m)),
                    // USD transactions that have no stored rate of their dollar yet; the service converts them.
                    PendingUsd = g.Sum(t => t.Currency == Currency.USD && t.Rate == null ? t.Amount : 0m)
                })
                .ToListAsync();
        }
    }
}
