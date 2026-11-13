using GastosBackend.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace GastosBackend.Infrastructure.ExternalService
{
    public class SecretProtector : ISecretProtector
    {
        private readonly IDataProtector _protector;

        public SecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("GastosBackend.TwoFactorSecret");
        }

        public string Protect(string plainText) => _protector.Protect(plainText);

        public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
    }
}
