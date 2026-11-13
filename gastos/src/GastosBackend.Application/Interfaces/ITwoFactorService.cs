using GastosBackend.Application.dtos.Responses;
using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Interfaces
{
    public interface ITwoFactorService
    {
        /// <summary>Saves a new secret (encrypted, not active yet) and returns what the QR code needs.</summary>
        Task<TwoFactorSetupResponse> Setup(Guid userId, string password);

        /// <summary>Checks the first code, turns the 2FA on and returns the recovery codes (the only time they are shown).</summary>
        Task<RecoveryCodesResponse> Enable(Guid userId, string code);

        /// <summary>Turns the 2FA off and removes the secret and the recovery codes.</summary>
        Task Disable(Guid userId, string password, string code);

        /// <summary>Replaces the recovery codes with new ones.</summary>
        Task<RecoveryCodesResponse> RegenerateRecoveryCodes(Guid userId, string password, string code);

        /// <summary>
        /// Checks a code of the app (6 digits) or a recovery code, applying the lock. Returns normally when it is valid.
        /// Throws TooManyRequestsException while the user is locked, and ValidationException when the code is wrong.
        /// </summary>
        Task VerifyCode(User user, string code);
    }
}
