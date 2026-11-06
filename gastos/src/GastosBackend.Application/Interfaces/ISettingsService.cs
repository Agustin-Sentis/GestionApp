using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface ISettingsService
    {
        Task<SettingsResponse> Get(Guid userId);
        Task<SettingsResponse> Update(SettingsRequest request, Guid userId);
    }
}
