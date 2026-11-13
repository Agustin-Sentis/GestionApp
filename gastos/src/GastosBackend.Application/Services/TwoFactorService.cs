using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Services
{
    public class TwoFactorService : ITwoFactorService
    {
        /// <summary>Wrong codes (or passwords) in a row before the second step is locked.</summary>
        public const int MaxFailedAttempts = 5;

        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

        private const string WrongCodeMessage = "Código incorrecto o vencido.";
        private const string WrongPasswordMessage = "Contraseña incorrecta.";
        private const int AppCodeLength = 6;

        private readonly IUserRepository _userRepository;
        private readonly IRecoveryCodeRepository _recoveryCodeRepository;
        private readonly ITotpService _totp;
        private readonly ISecretProtector _secretProtector;
        private readonly IRecoveryCodeGenerator _recoveryCodeGenerator;
        private readonly IPasswordHash _passwordHash;
        private readonly TimeProvider _time;

        public TwoFactorService(
            IUserRepository userRepository,
            IRecoveryCodeRepository recoveryCodeRepository,
            ITotpService totp,
            ISecretProtector secretProtector,
            IRecoveryCodeGenerator recoveryCodeGenerator,
            IPasswordHash passwordHash,
            TimeProvider time)
        {
            _userRepository = userRepository;
            _recoveryCodeRepository = recoveryCodeRepository;
            _totp = totp;
            _secretProtector = secretProtector;
            _recoveryCodeGenerator = recoveryCodeGenerator;
            _passwordHash = passwordHash;
            _time = time;
        }

        public async Task<TwoFactorSetupResponse> Setup(Guid userId, string password)
        {
            Validate(new TwoFactorSetupRequestValidations().Validate(new TwoFactorSetupRequest { Password = password }));

            var user = await GetUser(userId);
            EnsureNotLocked(user);
            if (user.TwoFactorEnabled)
            {
                throw new ConflictException("La verificación en dos pasos ya está activada.");
            }

            await CheckPassword(user, password);

            // Replaces the secret of an activation that was left halfway. It does not count until Enable confirms a code.
            var secret = _totp.GenerateSecret();
            user.TwoFactorSecret = _secretProtector.Protect(secret);
            user.TwoFactorLastStep = null;
            await _userRepository.Update(user);

            return new TwoFactorSetupResponse { Secret = secret, Uri = _totp.BuildUri(user.Email, secret) };
        }

        public async Task<RecoveryCodesResponse> Enable(Guid userId, string code)
        {
            Validate(new TwoFactorEnableRequestValidations().Validate(new TwoFactorEnableRequest { Code = code }));

            var user = await GetUser(userId);
            EnsureNotLocked(user);
            if (user.TwoFactorEnabled)
            {
                throw new ConflictException("La verificación en dos pasos ya está activada.");
            }

            if (user.TwoFactorSecret == null)
            {
                throw new ConflictException("Primero iniciá la activación.");
            }

            // Only the app code: there are no recovery codes yet, and this proves the app was set up correctly
            if (!IsValidAppCode(user, code))
            {
                await RegisterFailure(user);
                throw new ValidationException(WrongCodeMessage);
            }

            user.TwoFactorEnabled = true;
            await ClearFailures(user);

            return await ReplaceRecoveryCodes(user.Id);
        }

        public async Task Disable(Guid userId, string password, string code)
        {
            var user = await CheckSensitiveAction(userId, password, code);

            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.TwoFactorLastStep = null;
            await _userRepository.Update(user);
            await _recoveryCodeRepository.DeleteAllByUser(user.Id);
        }

        public async Task<RecoveryCodesResponse> RegenerateRecoveryCodes(Guid userId, string password, string code)
        {
            var user = await CheckSensitiveAction(userId, password, code);

            return await ReplaceRecoveryCodes(user.Id);
        }

        public async Task VerifyCode(User user, string code)
        {
            EnsureNotLocked(user);

            if (await IsValidAppOrRecoveryCode(user, code))
            {
                await ClearFailures(user);
                return;
            }

            await RegisterFailure(user);
            throw new ValidationException(WrongCodeMessage);
        }

        /// <summary>The checks of turning the 2FA off or replacing the recovery codes: not locked, enabled, password and code.</summary>
        private async Task<User> CheckSensitiveAction(Guid userId, string password, string code)
        {
            Validate(new TwoFactorPasswordCodeRequestValidations().Validate(new TwoFactorPasswordCodeRequest { Password = password, Code = code }));

            var user = await GetUser(userId);
            EnsureNotLocked(user);
            if (!user.TwoFactorEnabled)
            {
                throw new ConflictException("La verificación en dos pasos no está activada.");
            }

            await CheckPassword(user, password);
            await VerifyCode(user, code);
            return user;
        }

        private async Task<RecoveryCodesResponse> ReplaceRecoveryCodes(Guid userId)
        {
            var codes = _recoveryCodeGenerator.Generate();
            var hashes = codes.Select(c => _passwordHash.Hash(_recoveryCodeGenerator.Normalize(c))).ToList();
            await _recoveryCodeRepository.ReplaceAll(userId, hashes);

            return new RecoveryCodesResponse { Codes = codes };
        }

        private async Task<bool> IsValidAppOrRecoveryCode(User user, string code)
        {
            var normalized = _recoveryCodeGenerator.Normalize(code ?? string.Empty);

            // Six digits: the code of the app. Anything else is tried as a recovery code.
            if (normalized.Length == AppCodeLength && normalized.All(char.IsAsciiDigit))
            {
                return IsValidAppCode(user, normalized);
            }

            foreach (var recovery in await _recoveryCodeRepository.GetUnusedByUser(user.Id))
            {
                if (_passwordHash.Verify(normalized, recovery.CodeHash))
                {
                    recovery.UsedAt = _time.GetUtcNow();
                    await _recoveryCodeRepository.Update(recovery);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Checks the code against the secret and remembers its step (saved with the user by the caller's next update).</summary>
        private bool IsValidAppCode(User user, string code)
        {
            if (user.TwoFactorSecret == null)
            {
                return false;
            }

            var normalized = _recoveryCodeGenerator.Normalize(code ?? string.Empty);
            if (!_totp.Verify(_secretProtector.Unprotect(user.TwoFactorSecret), normalized, user.TwoFactorLastStep, out var step))
            {
                return false;
            }

            user.TwoFactorLastStep = step;
            return true;
        }

        private async Task CheckPassword(User user, string password)
        {
            if (!_passwordHash.Verify(password, user.Password))
            {
                await RegisterFailure(user);
                throw new ValidationException(WrongPasswordMessage);
            }
        }

        private void EnsureNotLocked(User user)
        {
            var now = _time.GetUtcNow();
            if (user.TwoFactorLockedUntil is { } until && until > now)
            {
                throw new TooManyRequestsException((int)Math.Ceiling((until - now).TotalSeconds));
            }
        }

        private async Task RegisterFailure(User user)
        {
            user.TwoFactorFailedAttempts++;
            if (user.TwoFactorFailedAttempts >= MaxFailedAttempts)
            {
                // Locks from the next attempt on, and the count starts over once the lock is over
                user.TwoFactorLockedUntil = _time.GetUtcNow() + LockDuration;
                user.TwoFactorFailedAttempts = 0;
            }

            await _userRepository.Update(user);
        }

        private async Task ClearFailures(User user)
        {
            user.TwoFactorFailedAttempts = 0;
            user.TwoFactorLockedUntil = null;
            await _userRepository.Update(user);
        }

        private async Task<User> GetUser(Guid userId)
        {
            return await _userRepository.Get(userId) ?? throw new NotFoundException("No se encontró el usuario.");
        }

        private static void Validate(FluentValidation.Results.ValidationResult result)
        {
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }
        }
    }
}
