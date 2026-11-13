using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure.Repository
{
    public class RecoveryCodeRepository : BaseRepository<RecoveryCode>, IRecoveryCodeRepository
    {
        public RecoveryCodeRepository(GastosDbContext context) : base(context)
        {

        }

        public async Task<List<RecoveryCode>> GetUnusedByUser(Guid userId)
        {
            return await _dbSet.Where(c => c.UserId == userId && c.UsedAt == null && !c.IsDeleted).ToListAsync();
        }

        public async Task ReplaceAll(Guid userId, IEnumerable<string> codeHashes)
        {
            var now = DateTimeOffset.UtcNow;

            // One save, one transaction: the old codes never stop working before the new ones exist
            await MarkDeleted(userId, now);
            _dbSet.AddRange(codeHashes.Select(hash => new RecoveryCode { UserId = userId, CodeHash = hash, CreatedDate = now }));
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllByUser(Guid userId)
        {
            await MarkDeleted(userId, DateTimeOffset.UtcNow);
            await _context.SaveChangesAsync();
        }

        private async Task MarkDeleted(Guid userId, DateTimeOffset now)
        {
            foreach (var code in await _dbSet.Where(c => c.UserId == userId && !c.IsDeleted).ToListAsync())
            {
                code.IsDeleted = true;
                code.DeleteDate = now;
            }
        }
    }
}
