using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Interfaces
{
    public interface IBudgetAlertRepository : IBaseRepository<BudgetAlert>
    {
        Task<bool> Exists(Guid userId, Guid categoryId, int year, int month, BudgetStatus level);

        /// <summary>
        /// Saves the mark. Returns false, without saving, when the same alert was already marked (another request got there first):
        /// the caller must not send the mail then.
        /// </summary>
        Task<bool> TryAdd(BudgetAlert alert);
    }
}
