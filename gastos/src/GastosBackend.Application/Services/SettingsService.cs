using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Validations;
using GastosBackend.Domain.Entities;

namespace GastosBackend.Application.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IUserRepository _userRepository;

        public SettingsService(IUserRepository userRepo)
        {
            _userRepository = userRepo;
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

        private async Task<User> GetUser(Guid userId)
        {
            var user = await _userRepository.Get(userId);
            if (user == null || user.IsDeleted)
            {
                throw new NotFoundException("No se encontró el usuario.");
            }

            return user;
        }

        private static SettingsResponse ToResponse(User user) => new SettingsResponse { DollarType = user.DollarType };
    }
}
