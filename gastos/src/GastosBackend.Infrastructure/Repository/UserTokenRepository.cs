using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure.Repository
{
    public class UserTokenRepository : BaseRepository<UserToken>, IUserTokenRepository
    {
        public UserTokenRepository(GastosDbContext context) : base(context)
        {

        }

        public async Task<UserToken?> GetOpen(UserTokenPurpose purpose, string tokenHash, DateTimeOffset now)
        {
            return await _dbSet.FirstOrDefaultAsync(t =>
                t.Purpose == purpose && t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > now && !t.IsDeleted);
        }

        public async Task<List<UserToken>> GetOpenByUser(Guid userId, UserTokenPurpose purpose, DateTimeOffset now)
        {
            return await _dbSet
                .Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();
        }
    }
}
