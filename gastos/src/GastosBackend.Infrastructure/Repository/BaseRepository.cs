using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure.Repository
{
    public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
    {
        protected readonly GastosDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public BaseRepository(GastosDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<List<T>> GetAll()
        {
            return await _dbSet.Where(a => !a.IsDeleted).ToListAsync();
        }

        public virtual async Task<T?> Get(Guid IdEntity)
        {
            return await _dbSet.FirstOrDefaultAsync(a => a.Id == IdEntity && !a.IsDeleted);
        }

        public virtual async Task<T> Add(T entity)
        {
            _dbSet.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task Update(T entity)
        {
            entity.UpdateDate = DateTimeOffset.UtcNow;
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task Delete(Guid IdEntity)
        {
            var entity = await Get(IdEntity);
            if (entity != null)
            {
                entity.DeleteDate = DateTimeOffset.UtcNow;
                entity.IsDeleted = true;

                _dbSet.Update(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
