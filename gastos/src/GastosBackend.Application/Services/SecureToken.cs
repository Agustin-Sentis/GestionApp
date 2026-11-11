using System.Security.Cryptography;
using System.Text;

namespace GastosBackend.Application.Services
{
    /// <summary>
    /// One-time tokens for links sent by mail. The token goes in the mail; only its SHA-256 is stored, so reading the
    /// table is not enough to use a token. They are random and long, which is why a plain hash (no salt, no stretching) is enough.
    /// </summary>
    public static class SecureToken
    {
        /// <summary>A new random token (32 bytes, safe to put in a URL) and the hash to store.</summary>
        public static (string Token, string Hash) Generate()
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            return (token, Hash(token));
        }

        /// <summary>The hash to look a token up with: SHA-256 in lower case hex (64 characters).</summary>
        public static string Hash(string token)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
    }
}
