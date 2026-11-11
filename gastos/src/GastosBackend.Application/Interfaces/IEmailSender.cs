using GastosBackend.Application.dtos;

namespace GastosBackend.Application.Interfaces
{
    /// <summary>Sends a mail. Behind this interface the provider can change (Brevo today) without touching the services.</summary>
    public interface IEmailSender
    {
        /// <exception cref="Exceptions.ExternalServiceException">The mail could not be sent.</exception>
        Task Send(EmailMessage message);
    }
}
