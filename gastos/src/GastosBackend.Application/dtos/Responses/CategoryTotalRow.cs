using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    /// <summary>Fila que devuelve el repositorio: total por categoría y moneda, antes de convertir a pesos.</summary>
    public class CategoryTotalRow
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public Currency Currency { get; set; }
        public decimal Total { get; set; }
    }
}
