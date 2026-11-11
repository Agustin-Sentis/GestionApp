using System.Net.Http.Json;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GastosBackend.Infrastructure.ExternalService
{
    /// <summary>Adapter over the transactional email API of Brevo (https://developers.brevo.com).</summary>
    public class BrevoEmailSender : IEmailSender
    {
        public const string HttpClientName = "brevoHttpClient";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EmailOptions _options;
        private readonly ILogger<BrevoEmailSender> _logger;

        public BrevoEmailSender(
            IHttpClientFactory httpClientFactory,
            IOptions<EmailOptions> options,
            ILogger<BrevoEmailSender> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task Send(EmailMessage message)
        {
            var payload = new
            {
                sender = new { name = _options.SenderName, email = _options.SenderEmail },
                to = new[] { new { email = message.To } },
                subject = message.Subject,
                htmlContent = message.HtmlBody,
                textContent = message.TextBody
            };

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email") { Content = JsonContent.Create(payload) };
                request.Headers.Add("api-key", _options.ApiKey);

                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    // 401 here usually means the key expired (Brevo keys expire after 90 days without use) or was revoked
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Brevo refused the email '{Subject}': {Status} {Body}",
                        message.Subject, (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
                    throw new ExternalServiceException("No se pudo enviar el correo.");
                }
            }
            catch (Exception ex) when (ex is not ExternalServiceException)
            {
                // Includes network failures, timeouts and an open circuit breaker (Polly)
                _logger.LogError(ex, "Could not send the email '{Subject}' through Brevo.", message.Subject);
                throw new ExternalServiceException("No se pudo enviar el correo.", ex);
            }
        }
    }
}
