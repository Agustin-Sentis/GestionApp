namespace GastosBackend.Application.Exceptions
{
    /// <summary>Too many wrong codes in a row: the second step is locked for a while. It becomes a 429.</summary>
    public class TooManyRequestsException : Exception
    {
        /// <summary>How long until it can be tried again.</summary>
        public int RetryAfterSeconds { get; }

        public TooManyRequestsException(int retryAfterSeconds)
            : base(BuildMessage(retryAfterSeconds))
        {
            RetryAfterSeconds = retryAfterSeconds;
        }

        private static string BuildMessage(int seconds)
        {
            var minutes = (int)Math.Ceiling(seconds / 60.0);
            return minutes <= 1
                ? "Demasiados intentos. Probá de nuevo en un minuto."
                : $"Demasiados intentos. Probá de nuevo en {minutes} minutos.";
        }
    }
}
