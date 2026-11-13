namespace GastosBackend.Application.Interfaces
{
    public interface IPasswordHash
    {
        string Hash(string password);

        /// <summary>Whether <paramref name="password"/> is the one behind <paramref name="hash"/>.</summary>
        bool Verify(string password, string hash);
    }
}
