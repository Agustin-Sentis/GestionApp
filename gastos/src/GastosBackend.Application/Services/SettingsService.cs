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
        private const string AddressTakenMessage = "Ya existe un usuario con ese email.";

        private readonly IUserRepository _userRepository;
        private readonly IUserTokenRepository _tokenRepository;
        private readonly IEmailSender _emailSender;
        private readonly IPasswordHash _passwordHash;
        private readonly EmailOptions _emailOptions;
        private readonly TimeProvider _time;

        public SettingsService(
            IUserRepository userRepo,
            IUserTokenRepository tokenRepo,
            IEmailSender emailSender,
            IPasswordHash passwordHash,
            EmailOptions emailOptions,
            TimeProvider time)
        {
            _userRepository = userRepo;
            _tokenRepository = tokenRepo;
            _emailSender = emailSender;
            _passwordHash = passwordHash;
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

        public async Task ResendConfirmation(Guid userId)
        {
            var user = await GetUser(userId);
            if (user.EmailConfirmedAt != null)
            {
                return;
            }

            await SendConfirmation(user);
        }

        public async Task<SettingsResponse> ChangeEmail(ChangeEmailRequest request, Guid userId)
        {
            var result = new ChangeEmailRequestValidations().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var user = await GetUser(userId);
            if (user.EmailConfirmedAt != null)
            {
                // Someone who proved the address is theirs does not change it with just a password: a different piece of work
                throw new ConflictException("Tu correo ya está confirmado.");
            }

            // A 400 and not a 401: the app signs the user out on a 401, and a typo in the password is not that
            if (!_passwordHash.Verify(request.Password, user.Password))
            {
                throw new ValidationException("La contraseña no es correcta.");
            }

            var email = request.Email.Trim().ToLowerInvariant();
            if (email == user.Email)
            {
                // Nothing to replace: the same address with other letter case is not "taken", it is a "Reenviar"
                await SendConfirmation(user);
                return ToResponse(user);
            }

            var owner = await _userRepository.GetByEmail(email);
            if (owner != null && owner.Id != user.Id)
            {
                throw new ConflictException(AddressTakenMessage);
            }

            // No minute between mails here: the one sent at the sign up went to the address being left behind, and the typical
            // user is correcting a typo seconds after signing up. The rate limit of the endpoint is what stops a flood.
            var now = _time.GetUtcNow();
            var openConfirmations = await _tokenRepository.GetOpenByUser(user.Id, UserTokenPurpose.EmailConfirmation, now);
            var openResets = await _tokenRepository.GetOpenByUser(user.Id, UserTokenPurpose.PasswordReset, now);

            // The mail first: if it cannot be sent nothing is changed. Its token is stored only after the address is saved,
            // so a race lost to another account leaves no link that works.
            var hash = await MailConfirmation(email);

            user.Email = email;
            user.EmailConfirmedAt = null;
            try
            {
                await _userRepository.Update(user);
            }
            catch (DuplicateException)
            {
                // Another account took the address between the check above and this update
                throw new ConflictException(AddressTakenMessage);
            }

            // Whatever was mailed to the old address must not work from now on: a confirmation link would confirm the new one,
            // and a recovery link would let whoever reads the old mailbox reset the password.
            await Close(openResets, now);
            await StoreConfirmation(user.Id, hash, openConfirmations, now);
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

            var hash = await MailConfirmation(user.Email);
            await StoreConfirmation(user.Id, hash, open, now);
        }

        /// <summary>Mails a new confirmation link to <paramref name="to"/> and returns the hash of its token, which is what gets stored.</summary>
        private async Task<string> MailConfirmation(string to)
        {
            var (token, hash) = SecureToken.Generate();
            var url = $"{_emailOptions.AppBaseUrl.TrimEnd('/')}/confirmar-correo?token={Uri.EscapeDataString(token)}";
            await _emailSender.Send(EmailTemplates.Confirmation(to, url));
            return hash;
        }

        /// <summary>Stores the token of a mail that is on its way. Only the newest link works: the ones in <paramref name="replaced"/> are closed.</summary>
        private async Task StoreConfirmation(Guid userId, string hash, IEnumerable<UserToken> replaced, DateTimeOffset now)
        {
            await Close(replaced, now);

            await _tokenRepository.Add(new UserToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Purpose = UserTokenPurpose.EmailConfirmation,
                TokenHash = hash,
                ExpiresAt = now + ConfirmationValidity
            });
        }

        private async Task Close(IEnumerable<UserToken> tokens, DateTimeOffset now)
        {
            foreach (var token in tokens)
            {
                token.UsedAt = now;
                await _tokenRepository.Update(token);
            }
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
            EmailConfirmed = user.EmailConfirmedAt != null,
            TwoFactorEnabled = user.TwoFactorEnabled
        };
    }
}
