using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Requests
{
    /// <summary>What the user can change about how their data is shown.</summary>
    public class SettingsRequest
    {
        /// <summary>Official, Blue or Mep.</summary>
        public DollarType DollarType { get; set; }
    }
}
