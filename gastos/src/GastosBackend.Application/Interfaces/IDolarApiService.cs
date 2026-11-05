namespace GastosBackend.Application.Interfaces
{
    public interface IDolarApiService
    {
        /// <summary>Cotización de venta de 1 USD expresada en pesos argentinos.</summary>
        Task<decimal> GetUsdSellRate();
    }
}
