using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Mapper;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GastosBackend.Infrastructure.ExternalService
{
    public class AuthService : IAuthService
    {
        /// <summary>Valid BCrypt hash (default work factor) of a throwaway password; computed once, never matches a real password.</summary>
        private static readonly string UnknownUserHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

        private readonly IUserRepository _userRepository;
        private readonly IPasswordHash _passwordHash;
        private readonly IBackgroundRunner _background;
        private readonly JwtOptions _jwtOptions;
        private readonly ITwoFactorService _twoFactor;
        private readonly TimeProvider _time;

        /// <summary>How long the user has, after the password, to give the code of the second step.</summary>
        public static readonly TimeSpan ChallengeValidity = TimeSpan.FromMinutes(5);

        private const string PurposeClaim = "purpose";
        private const string TwoFactorPurpose = "2fa";
        private const string ExpiredChallengeMessage = "Tu verificación venció, ingresá de nuevo.";

        public AuthService(
            IUserRepository userRepository,
            IPasswordHash passwordHash,
            IBackgroundRunner background,
            IOptions<JwtOptions> jwtOptions,
            ITwoFactorService twoFactor,
            TimeProvider time)
        {
            _userRepository = userRepository;
            _passwordHash = passwordHash;
            _background = background;
            _jwtOptions = jwtOptions.Value;
            _twoFactor = twoFactor;
            _time = time;
        }

        public async Task<AuthResponse> SignUp(SignUpRequest request)
        {
            var validations = new SignUpValidator();
            var result = validations.Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var email = request.Email.Trim().ToLowerInvariant();
            if (await _userRepository.GetByEmail(email) != null)
            {
                throw new ConflictException("Ya existe un usuario con ese email.");
            }

            try
            {
                var user = await _userRepository.Add(request.ToUser(_passwordHash.Hash(request.Password)));

                // The mail does not hold the sign up back, and if it cannot be sent the account is created all the same: the
                // user asks for another one from the notice in the app.
                _background.Run<ISettingsService>(settings => settings.ResendConfirmation(user.Id));

                return BuildAuthResponse(user);
            }
            catch (DuplicateException)
            {
                // Another request registered the same email between the check above and the insert
                throw new ConflictException("Ya existe un usuario con ese email.");
            }
        }

        public async Task<SignInResponse> SignIn(SignInRequest request)
        {
            var result = new SignInValidator().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmail(email);

            // Always run BCrypt, even when the user does not exist, so the response time does not reveal which emails are registered
            var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user?.Password ?? UnknownUserHash);

            if (user == null || !passwordMatches)
            {
                throw new UnauthorizedException("Credenciales inválidas.");
            }

            // With the second step on, the password alone is not a session: only a short challenge that is worth nothing else
            if (user.TwoFactorEnabled)
            {
                return new SignInResponse { RequiresTwoFactor = true, ChallengeToken = BuildChallengeToken(user.Id) };
            }

            return SignInResponse.FromSession(BuildAuthResponse(user));
        }

        public async Task<AuthResponse> VerifyTwoFactor(VerifyTwoFactorRequest request)
        {
            var result = new VerifyTwoFactorRequestValidations().Validate(request);
            if (!result.IsValid)
            {
                throw new ValidationException(result.ToString("~"));
            }

            var (userId, issuedAt) = ReadChallenge(request.ChallengeToken);

            var user = await _userRepository.Get(userId);
            if (user == null || !user.TwoFactorEnabled)
            {
                throw new UnauthorizedException(ExpiredChallengeMessage);
            }

            // Same rule as the sessions (see UserSessionValidator): a password change closes the challenges issued before it
            if (user.PasswordChangedAt is { } changedAt && issuedAt < changedAt.ToUnixTimeSeconds())
            {
                throw new UnauthorizedException(ExpiredChallengeMessage);
            }

            // Throws when the code is wrong or the user is locked
            await _twoFactor.VerifyCode(user, request.Code);

            return BuildAuthResponse(user);
        }

        private AuthResponse BuildAuthResponse(User user)
        {
            var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);

            return new AuthResponse
            {
                Token = GenerateToken(user.Id, user.Email, expires),
                UserId = user.Id,
                Email = user.Email,
                ExpiresAtUtc = expires
            };
        }

        /// <summary>
        /// The key of the challenges is derived from the one of the sessions, so it is another key: a session token does not
        /// pass as a challenge and a challenge does not pass as a session (the JWT middleware cannot verify its signature).
        /// </summary>
        private SymmetricSecurityKey ChallengeKey() =>
            new(System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(_jwtOptions.Key), Encoding.UTF8.GetBytes("2fa-challenge")));

        private string BuildChallengeToken(Guid userId)
        {
            var now = _time.GetUtcNow();
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(PurposeClaim, TwoFactorPurpose),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: (now + ChallengeValidity).UtcDateTime,
                signingCredentials: new SigningCredentials(ChallengeKey(), SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>The user and the moment of issue of a valid challenge. Anything else (garbage, expired, another purpose) is refused.</summary>
        private (Guid UserId, long IssuedAt) ReadChallenge(string challengeToken)
        {
            try
            {
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = ChallengeKey(),
                    // The expiry is checked below against the app clock, not the machine one
                    ValidateLifetime = false
                };

                new JwtSecurityTokenHandler().ValidateToken(challengeToken, parameters, out var validated);
                var jwt = (JwtSecurityToken)validated;

                var purpose = jwt.Claims.FirstOrDefault(c => c.Type == PurposeClaim)?.Value;
                var issuedAt = long.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Iat).Value);
                if (purpose != TwoFactorPurpose || jwt.ValidTo <= _time.GetUtcNow().UtcDateTime)
                {
                    throw new UnauthorizedException(ExpiredChallengeMessage);
                }

                return (Guid.Parse(jwt.Subject), issuedAt);
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException or InvalidOperationException)
            {
                throw new UnauthorizedException(ExpiredChallengeMessage);
            }
        }

        private string GenerateToken(Guid userId, string email, DateTime expires)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                // When it was issued: a password reset closes every session issued before it (see UserSessionValidator)
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
