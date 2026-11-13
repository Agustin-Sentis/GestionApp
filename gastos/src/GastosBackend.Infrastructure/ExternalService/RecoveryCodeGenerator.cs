using System.Security.Cryptography;
using GastosBackend.Application.Interfaces;

namespace GastosBackend.Infrastructure.ExternalService
{
    public class RecoveryCodeGenerator : IRecoveryCodeGenerator
    {
        private const int Count = 10;
        private const int HalfLength = 5;

        // 32 characters, without 0/O and 1/I: a code that is copied by hand or read from paper is not mistyped
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public IReadOnlyList<string> Generate()
        {
            var codes = new HashSet<string>();
            while (codes.Count < Count)
            {
                codes.Add($"{RandomPart()}-{RandomPart()}");
            }

            return codes.ToList();
        }

        public string Normalize(string code) =>
            new string(code.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        private static string RandomPart() =>
            string.Create(HalfLength, 0, (span, _) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
                }
            });
    }
}
