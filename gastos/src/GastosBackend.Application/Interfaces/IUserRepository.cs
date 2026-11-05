using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Interfaces
{
    public interface IUserRepository : IBaseRepository<User>
    {
        Task<User?> GetByEmail(string email);
    }
}
