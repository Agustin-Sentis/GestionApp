using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure.Repository
{
    public class BudgetAlertRepository : BaseRepository<BudgetAlert>, IBudgetAlertRepository
    {
        public BudgetAlertRepository(GastosDbContext context) : base(context)
        {

        }

        public async Task<bool> Exists(Guid userId, Guid categoryId, int year, int month, BudgetStatus level)
        {
            return await _dbSet.AnyAsync(a =>
                a.UserId == userId && a.CategoryId == categoryId && a.Year == year && a.Month == month && a.Level == level && !a.IsDeleted);
        }

        public async Task<bool> TryAdd(BudgetAlert alert)
        {
            try
            {
                await Add(alert);
                return true;
            }
            catch (DuplicateException)
            {
                // Another request marked the same alert between the caller's check and this insert. Forget the failed row
                // so this context can be used again.
                _context.Entry(alert).State = EntityState.Detached;
                return false;
            }
        }
    }
}
