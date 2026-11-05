namespace GastosBackend.Infrastructure.ExternalDTO
{
    /// <summary>Respuesta de https://dolarapi.com/v1/dolares/{casa} (solo el campo que usamos).</summary>
    public class ExternalDolarDTO
    {
        public decimal Venta { get; set; }
    }
}
