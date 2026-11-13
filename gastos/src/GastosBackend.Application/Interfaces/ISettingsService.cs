using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface ISettingsService
    {
        Task<SettingsResponse> Get(Guid userId);
        Task<SettingsResponse> Update(SettingsRequest request, Guid userId);

        /// <summary>
        /// Turns the budget alert mails on or off. Turning them on with an address that is not confirmed sends the confirmation mail first.
        /// </summary>
        Task<SettingsResponse> SetBudgetAlerts(BudgetAlertsRequest request, Guid userId);

        /// <summary>
        /// Mails a new confirmation link to an address that is not confirmed yet (nothing happens if it is). At most one per minute;
        /// if the mail cannot be sent the error goes up and nothing is saved. The sign up queues it too, so the first mail and the
        /// "Reenviar" button follow the same rules.
        /// </summary>
        Task ResendConfirmation(Guid userId);

        /// <summary>
        /// Replaces the address of an account whose address is not confirmed yet (a typo at the sign up), after checking the
        /// password. The new address starts unconfirmed and gets the confirmation mail; every link sent to the old one stops working.
        /// </summary>
        Task<SettingsResponse> ChangeEmail(ChangeEmailRequest request, Guid userId);

        /// <summary>Confirms the address behind a confirmation token. No session needed: the token is the proof.</summary>
        Task ConfirmEmail(ConfirmEmailRequest request);
    }
}
