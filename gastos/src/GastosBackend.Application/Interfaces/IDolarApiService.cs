using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.Interfaces
{
    /// <summary>The sell rate of each dollar type, or null for one that could not be obtained.</summary>
    public record UsdRates(decimal? Official, decimal? Blue, decimal? Mep)
    {
        /// <summary>Nothing obtained: what a transaction in pesos has, and what a failed lookup leaves.</summary>
        public static readonly UsdRates None = new(null, null, null);

        public decimal? Get(DollarType type) => type switch
        {
            DollarType.Blue => Blue,
            DollarType.Mep => Mep,
            _ => Official
        };
    }

    public interface IDolarApiService
    {
        /// <summary>Cotización de venta de 1 USD expresada en pesos argentinos, del tipo de dólar pedido.</summary>
        /// <exception cref="Exceptions.ExternalServiceException">When the rate cannot be obtained.</exception>
        Task<decimal> GetUsdSellRate(DollarType type);

        /// <summary>
        /// The three rates at once. It never throws for a single one that fails: that one comes back null and the others
        /// are still returned, so a transaction can be saved with whatever was available.
        /// </summary>
        Task<UsdRates> GetUsdSellRates();
    }
}
