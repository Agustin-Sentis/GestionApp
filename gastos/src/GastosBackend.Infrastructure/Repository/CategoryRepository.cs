using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure.Repository
{
    public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
    {
        public CategoryRepository(GastosDbContext context) : base(context)
        {

        }

        public async Task<List<Category>> GetAllByUser(Guid userId)
        {
            return await _dbSet
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<bool> ExistsByName(Guid userId, string name)
        {
            return await _dbSet.AnyAsync(c => c.UserId == userId && c.Name == name && !c.IsDeleted);
        }
    }
}
