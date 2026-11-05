using GastosBackend.Application.dtos.Requests;
using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Mapper
{
    public static class UserMapper
    {
        public static User ToUser(this SignUpRequest request, string passwordHash)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.Trim().ToLowerInvariant(),
                Password = passwordHash
            };
        }
    }
}
