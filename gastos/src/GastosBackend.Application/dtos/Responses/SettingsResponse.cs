using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class SettingsResponse
    {
        /// <summary>The dollar the user's USD amounts are converted with.</summary>
        public DollarType DollarType { get; set; }
    }
}
