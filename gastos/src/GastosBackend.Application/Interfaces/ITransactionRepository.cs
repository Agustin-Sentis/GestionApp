using GastosBackend.Application.dtos.Responses;
using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Interfaces
{
    public interface ITransactionRepository : IBaseRepository<Transaction>
    {
        /// <summary>Movimientos del usuario (con su categoría), filtrados en SQL. "toExclusive" es el límite superior sin incluir.</summary>
        Task<List<Transaction>> GetAllByUser(Guid userId, DateOnly? from, DateOnly? toExclusive, Guid? categoryId, int take);

        Task<bool> ExistsByCategory(Guid categoryId);

        /// <summary>Totales agrupados por categoría y moneda (GROUP BY + SUM en la base).</summary>
        Task<List<CategoryTotalRow>> GetMonthlyTotals(Guid userId, DateOnly from, DateOnly toExclusive);
    }
}
