using System.Net.Http.Json;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Infrastructure.ExternalDTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<DolarApiService> _logger;

        public DolarApiService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            IOptions<DolarApiOptions> options,
            ILogger<DolarApiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _options = options.Value;
            _logger = logger;
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
                    _logger.LogWarning("dolarapi.com returned an empty or invalid rate for '{Casa}'.", _options.Casa);
                    throw new ExternalServiceException("La cotización del dólar llegó vacía.");
                }

                _cache.Set(cacheKey, data.Venta, TimeSpan.FromMinutes(_options.CacheMinutes));
                return data.Venta;
            }
            catch (Exception ex) when (ex is not ExternalServiceException)
            {
                // Includes network failures, timeouts and an open circuit breaker (Polly)
                _logger.LogWarning(ex, "Could not get the USD rate from dolarapi.com for '{Casa}'.", _options.Casa);
                throw new ExternalServiceException("No se pudo obtener la cotización del dólar.", ex);
            }
        }
    }
}
