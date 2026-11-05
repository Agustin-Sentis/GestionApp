using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Interfaces
{
    public interface ICategoryRepository : IBaseRepository<Category>
    {
        Task<List<Category>> GetAllByUser(Guid userId);
        Task<bool> ExistsByName(Guid userId, string name);
    }
}
