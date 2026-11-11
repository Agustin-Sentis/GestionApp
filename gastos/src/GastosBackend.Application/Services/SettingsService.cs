using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    public class SettingsService : ISettingsService
    {
        /// <summary>How long the link of a confirmation mail works.</summary>
        public static readonly TimeSpan ConfirmationValidity = TimeSpan.FromHours(24);

        /// <summary>The least time between two confirmation mails to the same user: stops the button from being used to flood an inbox.</summary>
        public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

        /// <summary>A real token is 43 characters; anything much longer is not one and is not even looked up.</summary>
        private const int MaxTokenLength = 200;

        private const string InvalidLinkMessage = "El enlace venció o ya se usó.";

        private readonly IUserRepository _userRepository;
        private readonly IUserTokenRepository _tokenRepository;
        private readonly IEmailSender _emailSender;
        private readonly EmailOptions _emailOptions;
        private readonly TimeProvider _time;

        public SettingsService(
            IUserRepository userRepo,
            IUserTokenRepository tokenRepo,
            IEmailSender emailSender,
            EmailOptions emailOptions,
            TimeProvider time)
        {
            _userRepository = userRepo;
            _tokenRepository = tokenRepo;
            _emailSender = emailSender;
            _emailOptions = emailOptions;
            _time = time;
        }

        public async Task<SettingsResponse> Get(Guid userId)
        {
            return ToResponse(await GetUser(userId));
        }

        public async Task<SettingsResponse> Update(SettingsRequest request, Guid userId)
        {
            var result = new SettingsRequestValidations().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var user = await GetUser(userId);

            // Only how the stored rates are read changes: no transaction is touched
            user.DollarType = request.DollarType;
            await _userRepository.Update(user);

            return ToResponse(user);
        }

        public async Task<SettingsResponse> SetBudgetAlerts(BudgetAlertsRequest request, Guid userId)
        {
            var user = await GetUser(userId);

            if (!request.Enabled)
            {
                user.BudgetEmailAlerts = false;
                await _userRepository.Update(user);
                return ToResponse(user);
            }

            if (user.EmailConfirmedAt == null)
            {
                await SendConfirmation(user);
            }

            user.BudgetEmailAlerts = true;
            await _userRepository.Update(user);
            return ToResponse(user);
        }

        public async Task ConfirmEmail(ConfirmEmailRequest request)
        {
            var token = request.Token?.Trim();
            if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
            {
                throw new ValidationException(InvalidLinkMessage);
            }

            var now = _time.GetUtcNow();
            var stored = await _tokenRepository.GetOpen(UserTokenPurpose.EmailConfirmation, SecureToken.Hash(token), now);
            var user = stored == null ? null : await _userRepository.Get(stored.UserId);
            if (stored == null || user == null || user.IsDeleted)
            {
                // Unknown, expired and used look the same from outside
                throw new ValidationException(InvalidLinkMessage);
            }

            stored.UsedAt = now;
            await _tokenRepository.Update(stored);

            user.EmailConfirmedAt ??= now;
            await _userRepository.Update(user);
        }

        /// <summary>
        /// Mails a new confirmation link, and only when that worked it replaces the ones sent before. If the mail cannot be sent
        /// the exception goes up (503) and nothing is saved: the alerts are never left "on" without a mail on its way.
        /// </summary>
        private async Task SendConfirmation(User user)
        {
            var now = _time.GetUtcNow();
            var open = await _tokenRepository.GetOpenByUser(user.Id, UserTokenPurpose.EmailConfirmation, now);
            if (open.Any(t => now - t.CreatedDate < ResendCooldown))
            {
                throw new ValidationException("Esperá un minuto antes de pedir otro correo.");
            }

            var (token, hash) = SecureToken.Generate();
            var url = $"{_emailOptions.AppBaseUrl.TrimEnd('/')}/confirmar-correo?token={Uri.EscapeDataString(token)}";
            await _emailSender.Send(EmailTemplates.Confirmation(user.Email, url));

            // Only the newest link works
            foreach (var old in open)
            {
                old.UsedAt = now;
                await _tokenRepository.Update(old);
            }

            await _tokenRepository.Add(new UserToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Purpose = UserTokenPurpose.EmailConfirmation,
                TokenHash = hash,
                ExpiresAt = now + ConfirmationValidity
            });
        }

        private async Task<User> GetUser(Guid userId)
        {
            var user = await _userRepository.Get(userId);
            if (user == null || user.IsDeleted)
            {
                throw new NotFoundException("No se encontró el usuario.");
            }

            return user;
        }

        private static SettingsResponse ToResponse(User user) => new SettingsResponse
        {
            DollarType = user.DollarType,
            Email = user.Email,
            BudgetEmailAlerts = user.BudgetEmailAlerts,
            EmailConfirmed = user.EmailConfirmedAt != null
        };
    }
}
