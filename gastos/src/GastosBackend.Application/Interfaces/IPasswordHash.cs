namespace GastosBackend.Application.Interfaces
{
    public interface IPasswordHash
    {
        string Hash(string password);
    }
}
