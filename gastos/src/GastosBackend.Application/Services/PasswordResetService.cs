using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        /// <summary>How long the link works. Short on purpose: a link that changes the password must not stay around.</summary>
        public static readonly TimeSpan TokenValidity = TimeSpan.FromHours(1);

        /// <summary>The least time between two recovery mails to the same account.</summary>
        public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

        /// <summary>A real token is 43 characters; anything much longer is not one and is not even looked up.</summary>
        private const int MaxTokenLength = 200;

        private const string InvalidLinkMessage = "El enlace venció o ya se usó.";

        private readonly IUserRepository _userRepository;
        private readonly IUserTokenRepository _tokenRepository;
        private readonly IEmailSender _emailSender;
        private readonly IPasswordHash _passwordHash;
        private readonly IBackgroundRunner _background;
        private readonly EmailOptions _emailOptions;
        private readonly TimeProvider _time;

        public PasswordResetService(
            IUserRepository userRepo,
            IUserTokenRepository tokenRepo,
            IEmailSender emailSender,
            IPasswordHash passwordHash,
            IBackgroundRunner background,
            EmailOptions emailOptions,
            TimeProvider time)
        {
            _userRepository = userRepo;
            _tokenRepository = tokenRepo;
            _emailSender = emailSender;
            _passwordHash = passwordHash;
            _background = background;
            _emailOptions = emailOptions;
            _time = time;
        }

        public async Task RequestReset(ForgotPasswordRequest request)
        {
            var result = new ForgotPasswordRequestValidations().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var user = await _userRepository.GetByEmail(request.Email.Trim().ToLowerInvariant());
            if (user == null)
            {
                // Looks exactly like the other case from outside: the same answer, and in both nothing waits for a mail
                return;
            }

            _background.Run<IPasswordResetService>(service => service.SendResetEmail(user.Id));
        }

        public async Task SendResetEmail(Guid userId)
        {
            var user = await _userRepository.Get(userId);
            if (user == null || user.IsDeleted)
            {
                return;
            }

            var now = _time.GetUtcNow();
            var open = await _tokenRepository.GetOpenByUser(userId, UserTokenPurpose.PasswordReset, now);
            if (open.Any(t => now - t.CreatedDate < ResendCooldown))
            {
                return;
            }

            var (token, hash) = SecureToken.Generate();
            var url = $"{_emailOptions.AppBaseUrl.TrimEnd('/')}/restablecer-contrasena?token={Uri.EscapeDataString(token)}";
            await _emailSender.Send(EmailTemplates.PasswordReset(user.Email, url));

            // Only once the mail is on its way: the newest link is the only one that works
            await Invalidate(open, now);
            await _tokenRepository.Add(new UserToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Purpose = UserTokenPurpose.PasswordReset,
                TokenHash = hash,
                ExpiresAt = now + TokenValidity
            });
        }

        public async Task ResetPassword(ResetPasswordRequest request)
        {
            // The password first: a wrong one must not use up the link, the user has to be able to try again with the same one
            var result = new ResetPasswordRequestValidations().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var token = request.Token?.Trim();
            if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
            {
                throw new ValidationException(InvalidLinkMessage);
            }

            var now = _time.GetUtcNow();
            var stored = await _tokenRepository.GetOpen(UserTokenPurpose.PasswordReset, SecureToken.Hash(token), now);
            var user = stored == null ? null : await _userRepository.Get(stored.UserId);
            if (stored == null || user == null || user.IsDeleted)
            {
                // Unknown, expired, used and "not a reset link" look the same from outside
                throw new ValidationException(InvalidLinkMessage);
            }

            // The link first, then the password: if the second step fails the user asks for a new link, instead of being left
            // with a link that still works after something went wrong.
            stored.UsedAt = now;
            await _tokenRepository.Update(stored);

            user.Password = _passwordHash.Hash(request.NewPassword);
            user.PasswordChangedAt = now; // closes the sessions issued before this moment
            user.EmailConfirmedAt ??= now; // the link only reached the owner of the address
            await _userRepository.Update(user);

            await Invalidate(await _tokenRepository.GetOpenByUser(user.Id, UserTokenPurpose.PasswordReset, now), now);

            // The user is told what happened to their account. If this mail fails the password was changed all the same.
            try
            {
                await _emailSender.Send(EmailTemplates.PasswordChanged(user.Email, _emailOptions.AppBaseUrl));
            }
            catch (ExternalServiceException)
            {
            }
        }

        private async Task Invalidate(IEnumerable<UserToken> tokens, DateTimeOffset now)
        {
            foreach (var token in tokens)
            {
                token.UsedAt = now;
                await _tokenRepository.Update(token);
            }
        }
    }
}
