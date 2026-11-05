using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure.Repository
{
    public class TransactionRepository : BaseRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(GastosDbContext context) : base(context)
        {

        }

        public async Task<List<Transaction>> GetAllByUser(
            Guid userId, DateOnly? from, DateOnly? toExclusive, Guid? categoryId, int take)
        {
            var query = _dbSet
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && !t.IsDeleted);

            if (from != null) query = query.Where(t => t.Date >= from.Value);
            if (toExclusive != null) query = query.Where(t => t.Date < toExclusive.Value);
            if (categoryId != null) query = query.Where(t => t.CategoryId == categoryId.Value);

            return await query
                .OrderByDescending(t => t.Date)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> ExistsByCategory(Guid categoryId)
        {
            return await _dbSet.AnyAsync(t => t.CategoryId == categoryId && !t.IsDeleted);
        }

        public async Task<List<CategoryTotalRow>> GetMonthlyTotals(Guid userId, DateOnly from, DateOnly toExclusive)
        {
            return await _dbSet
                .Where(t => t.UserId == userId && !t.IsDeleted && t.Date >= from && t.Date < toExclusive)
                .GroupBy(t => new { t.Category!.Name, t.Category.Type, t.Currency })
                .Select(g => new CategoryTotalRow
                {
                    Name = g.Key.Name,
                    Type = g.Key.Type,
                    Currency = g.Key.Currency,
                    Total = g.Sum(t => t.Amount)
                })
                .ToListAsync();
        }
    }
}
