namespace GastosBackend.Application.Interfaces
{
    /// <summary>Decides, for every authenticated request, whether the session behind a valid JWT is still allowed.</summary>
    public interface IUserSessionValidator
    {
        /// <param name="userId">The user the token was issued to.</param>
        /// <param name="issuedAtUnixSeconds">When the token was issued (its "iat" claim, in seconds), or null if it has none.</param>
        /// <returns>False when the user no longer exists, or when the session is older than their last password reset.</returns>
        Task<bool> IsValid(Guid userId, long? issuedAtUnixSeconds);
    }
}
