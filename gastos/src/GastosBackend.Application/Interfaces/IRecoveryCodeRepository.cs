using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Interfaces
{
    public interface IRecoveryCodeRepository : IBaseRepository<RecoveryCode>
    {
        /// <summary>The codes of the user that were not used yet.</summary>
        Task<List<RecoveryCode>> GetUnusedByUser(Guid userId);

        /// <summary>Replaces every code of the user (used or not) with the new ones, in a single save.</summary>
        Task ReplaceAll(Guid userId, IEnumerable<string> codeHashes);

        /// <summary>Removes every code of the user (the 2FA was turned off).</summary>
        Task DeleteAllByUser(Guid userId);
    }
}
