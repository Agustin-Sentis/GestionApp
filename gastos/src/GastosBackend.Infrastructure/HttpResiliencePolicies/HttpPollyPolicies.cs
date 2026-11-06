using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

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
                    .Or<TimeoutRejectedException>() // not part of "transient HTTP error": handle our own timeout explicitly
                    .WaitAndRetryAsync(
                        config.RetryCount,
                        // Exponential backoff: base, base * 2, base * 4...
                        retryAttempt => TimeSpan.FromMilliseconds(
                            config.RetryBaseDelayInMilliseconds * Math.Pow(2, retryAttempt - 1))
                    );
            }

            /// <summary>
            /// Cancels a single attempt that takes too long. It must be the innermost policy, so every retry gets its own timeout.
            /// It throws TimeoutRejectedException, which the retry and circuit breaker policies handle explicitly.
            /// </summary>
            public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(ApiClientConfigurationDTO config)
            {
                return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(config.TimeoutInMilliseconds));
            }

            public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ApiClientConfigurationDTO config)
            {
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .Or<TimeoutRejectedException>() // a timed-out attempt must count towards opening the circuit
                    .CircuitBreakerAsync(
                        config.HandledEventsAllowedBeforeBreaking,
                        TimeSpan.FromSeconds(config.DurationOfBreakInSeconds)
                    );
            }
        }
    }
}
