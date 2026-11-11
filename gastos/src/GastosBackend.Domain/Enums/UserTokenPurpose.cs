namespace GastosBackend.Domain.Enums
{
    /// <summary>What a one-time token sent to a user is for. New purposes are added at the end (password recovery will be one).</summary>
    public enum UserTokenPurpose
    {
        EmailConfirmation = 1
    }
}
