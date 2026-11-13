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

        public bool Verify(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}
