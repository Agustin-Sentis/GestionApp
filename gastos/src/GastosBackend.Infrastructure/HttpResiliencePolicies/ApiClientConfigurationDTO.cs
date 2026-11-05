namespace GastosBackend.Infrastructure.HttpResiliencePolicies
{
    public class ApiClientConfigurationDTO
    {
        public int RetryCount;
        public int RetryAttemptInSeconds;
        public int HandledEventsAllowedBeforeBreaking;
        public int DurationOfBreakInSeconds;
    }
}
