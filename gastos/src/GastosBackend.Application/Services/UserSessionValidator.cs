using GastosBackend.Application.Interfaces;

namespace GastosBackend.Application.Services
{
    /// <summary>
    /// The JWT is stateless, so by itself it cannot be revoked. This is what closes sessions: resetting a password stores the
    /// moment in <c>User.PasswordChangedAt</c>, and a session issued before it is refused. Costs one primary key lookup per request.
    /// </summary>
    public class UserSessionValidator : IUserSessionValidator
    {
        private readonly IUserRepository _userRepository;

        public UserSessionValidator(IUserRepository userRepo)
        {
            _userRepository = userRepo;
        }

        public async Task<bool> IsValid(Guid userId, long? issuedAtUnixSeconds)
        {
            var user = await _userRepository.Get(userId);
            if (user == null || user.IsDeleted)
            {
                return false;
            }

            // Nobody reset their password: every session is fine, also those issued before tokens carried "iat"
            if (user.PasswordChangedAt == null)
            {
                return true;
            }

            if (issuedAtUnixSeconds == null)
            {
                return false;
            }

            // "iat" has whole seconds and the reset has fractions: compare in seconds, or someone signing in right after the
            // reset (same second) would be refused. A session started in the very second of the reset still counts as new.
            return issuedAtUnixSeconds.Value >= user.PasswordChangedAt.Value.ToUnixTimeSeconds();
        }
    }
}
