namespace GastosBackend.Application.Configuration
{
    public class DolarApiOptions
    {
        public string BaseUrl { get; set; } = "https://dolarapi.com/";

        public int CacheMinutes { get; set; } = 15;
    }
}
