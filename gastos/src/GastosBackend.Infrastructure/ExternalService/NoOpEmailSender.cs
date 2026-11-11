using GastosBackend.Application.dtos;
using GastosBackend.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GastosBackend.Infrastructure.ExternalService
{
    /// <summary>Used when no mail provider is configured (local development, tests): the mail is only logged, nothing leaves the machine.</summary>
    public class NoOpEmailSender : IEmailSender
    {
        private readonly ILogger<NoOpEmailSender> _logger;

        public NoOpEmailSender(ILogger<NoOpEmailSender> logger)
        {
            _logger = logger;
        }

        public Task Send(EmailMessage message)
        {
            // The body is not logged: it carries links that confirm things
            _logger.LogInformation("Email provider not configured: not sending '{Subject}'.", message.Subject);
            return Task.CompletedTask;
        }
    }
}
