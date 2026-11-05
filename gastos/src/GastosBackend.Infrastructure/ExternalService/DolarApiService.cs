using System.Net.Http.Json;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Infrastructure.ExternalDTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GastosBackend.Infrastructure.ExternalService
{
    /// <summary>Adapter sobre dolarapi.com. Cachea la cotización para no consultar la API en cada reporte.</summary>
    public class DolarApiService : IDolarApiService
    {
        public const string HttpClientName = "dolarHttpClient";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly DolarApiOptions _options;

        public DolarApiService(IHttpClientFactory httpClientFactory, IMemoryCache cache, IOptions<DolarApiOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _options = options.Value;
        }

        public async Task<decimal> GetUsdSellRate()
        {
            var cacheKey = $"usd-sell-rate:{_options.Casa}";

            if (_cache.TryGetValue(cacheKey, out decimal cachedRate))
            {
                return cachedRate;
            }

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var data = await client.GetFromJsonAsync<ExternalDolarDTO>($"v1/dolares/{_options.Casa}");

                if (data == null || data.Venta <= 0)
                {
                    throw new ExternalServiceException("La cotización del dólar llegó vacía.");
                }

                _cache.Set(cacheKey, data.Venta, TimeSpan.FromMinutes(_options.CacheMinutes));
                return data.Venta;
            }
            catch (Exception ex) when (ex is not ExternalServiceException)
            {
                // Incluye fallos de red, timeouts y circuit breaker abierto (Polly)
                throw new ExternalServiceException("No se pudo obtener la cotización del dólar.", ex);
            }
        }
    }
}
