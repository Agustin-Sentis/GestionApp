using Polly;
using Polly.Extensions.Http;

namespace GastosBackend.Infrastructure.HttpResiliencePolicies
{
    public static class HttpPollyPolicies
    {
        public static class PollyResiliencePolicies
        {
            public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ApiClientConfigurationDTO config)
            {
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(
                        config.RetryCount,
                        retryAttempt => TimeSpan.FromSeconds(config.RetryAttemptInSeconds)
                    );
            }

            public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ApiClientConfigurationDTO config)
            {
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(
                        config.HandledEventsAllowedBeforeBreaking,
                        TimeSpan.FromSeconds(config.DurationOfBreakInSeconds)
                    );
            }
        }
    }
}
