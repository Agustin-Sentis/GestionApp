using GastosBackend.Application.Interfaces;

namespace GastosBackend.Infrastructure.ExternalService
{
    public class PasswordHash : IPasswordHash
    {
        public string Hash(string password)
        {
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            return hashedPassword;
        }
    }
}
