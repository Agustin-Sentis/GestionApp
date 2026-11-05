namespace GastosBackend.Application.Configuration
{
    public class DolarApiOptions
    {
        public string BaseUrl { get; set; } = "https://dolarapi.com/";

        /// <summary>Tipo de cotización: oficial, blue, bolsa, etc.</summary>
        public string Casa { get; set; } = "oficial";

        public int CacheMinutes { get; set; } = 15;
    }
}
