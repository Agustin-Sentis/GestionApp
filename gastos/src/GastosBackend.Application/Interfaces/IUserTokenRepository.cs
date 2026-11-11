using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Interfaces
{
    public interface IUserTokenRepository : IBaseRepository<UserToken>
    {
        /// <summary>The token with this hash that can still be used: not used, not expired and not deleted. Null if there is none.</summary>
        Task<UserToken?> GetOpen(UserTokenPurpose purpose, string tokenHash, DateTimeOffset now);

        /// <summary>The tokens of the user for this purpose that can still be used.</summary>
        Task<List<UserToken>> GetOpenByUser(Guid userId, UserTokenPurpose purpose, DateTimeOffset now);
    }
}
