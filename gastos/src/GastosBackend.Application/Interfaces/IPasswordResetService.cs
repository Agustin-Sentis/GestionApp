using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Application.Interfaces
{
    public interface IPasswordResetService
    {
        /// <summary>
        /// Starts the recovery of the account with this email. Answers the same whether the email is registered or not, and
        /// does not wait for the mail (it is sent in the background), so nothing tells the two cases apart.
        /// </summary>
        /// <exception cref="Exceptions.ValidationException">The email is empty or has no valid format.</exception>
        Task RequestReset(ForgotPasswordRequest request);

        /// <summary>
        /// Sends the recovery mail to the user (the background half of <see cref="RequestReset"/>). Does nothing if one was
        /// requested less than a minute ago. If the mail cannot be sent, nothing is saved and the exception goes up.
        /// </summary>
        Task SendResetEmail(Guid userId);

        /// <summary>
        /// Sets the new password with the token of the mail: the token is used up, the address counts as confirmed and every
        /// session issued before now stops working. It does not sign anyone in.
        /// </summary>
        /// <exception cref="Exceptions.ValidationException">The password does not meet the rules (the token is not used up),
        /// or the link expired, was used, or is not a link of this app.</exception>
        Task ResetPassword(ResetPasswordRequest request);
    }
}
