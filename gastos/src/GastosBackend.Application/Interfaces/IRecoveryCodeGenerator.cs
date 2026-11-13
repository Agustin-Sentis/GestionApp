namespace GastosBackend.Application.Interfaces
{
    public interface IRecoveryCodeGenerator
    {
        /// <summary>Ten new codes with the format XXXXX-XXXXX, without characters that look alike (0/O, 1/I).</summary>
        IReadOnlyList<string> Generate();

        /// <summary>Upper case, without dashes or spaces: what is hashed and compared, so the user can type it any way.</summary>
        string Normalize(string code);
    }
}
