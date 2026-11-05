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
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHash _passwordHash;
        private readonly JwtOptions _jwtOptions;

        public AuthService(IUserRepository userRepository, IPasswordHash passwordHash, IOptions<JwtOptions> jwtOptions)
        {
            _userRepository = userRepository;
            _passwordHash = passwordHash;
            _jwtOptions = jwtOptions.Value;
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

            var user = await _userRepository.Add(request.ToUser(_passwordHash.Hash(request.Password)));
            return BuildAuthResponse(user);
        }

        public async Task<AuthResponse> SignIn(SignInRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmail(email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new UnauthorizedException("Credenciales inválidas.");
            }

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

        private string GenerateToken(Guid userId, string email, DateTime expires)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
