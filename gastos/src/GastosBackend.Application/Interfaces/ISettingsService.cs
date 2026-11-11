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

        /// <summary>Confirms the address behind a confirmation token. No session needed: the token is the proof.</summary>
        Task ConfirmEmail(ConfirmEmailRequest request);
    }
}
