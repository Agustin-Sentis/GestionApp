namespace GastosBackend.Application.Interfaces
{
    /// <summary>Encrypts a secret that has to be read back later (so a hash does not work), such as the TOTP secret.</summary>
    public interface ISecretProtector
    {
        string Protect(string plainText);

        string Unprotect(string protectedText);
    }
}
