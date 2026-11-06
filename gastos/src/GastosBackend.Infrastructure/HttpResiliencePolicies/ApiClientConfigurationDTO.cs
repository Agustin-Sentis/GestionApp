namespace GastosBackend.Infrastructure.HttpResiliencePolicies
{
    public class ApiClientConfigurationDTO
    {
        /// <summary>How many times a failed call is retried (the first attempt is not counted).</summary>
        public int RetryCount;

        /// <summary>Wait before the first retry; it doubles on every following retry (exponential backoff).</summary>
        public int RetryBaseDelayInMilliseconds;

        /// <summary>Maximum time a single attempt may take before it is cancelled and counted as a failure.</summary>
        public int TimeoutInMilliseconds;

        /// <summary>Consecutive failures that open the circuit.</summary>
        public int HandledEventsAllowedBeforeBreaking;

        /// <summary>How long the circuit stays open, answering immediately instead of calling the API.</summary>
        public int DurationOfBreakInSeconds;
    }
}
