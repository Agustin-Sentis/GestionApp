using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            entity.CreatedDate = DateTimeOffset.UtcNow;
            _dbSet.Add(entity);
            await SaveChangesTranslatingDuplicates();
            return entity;
        }

        public virtual async Task Update(T entity)
        {
            entity.UpdateDate = DateTimeOffset.UtcNow;
            _dbSet.Update(entity);
            await SaveChangesTranslatingDuplicates();
        }

        /// <summary>
        /// Saves the pending changes. A unique constraint violation (two requests racing past the "already exists" check)
        /// becomes a <see cref="DuplicateException"/> instead of an unhandled database error.
        /// </summary>
        protected async Task SaveChangesTranslatingDuplicates()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                throw new DuplicateException("A record with the same unique values already exists.", ex);
            }
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
