using GastosBackend.Domain.Enums;

namespace GastosBackend.Application.dtos.Responses
{
    public class CategoryTotalResponse
    {
        public string Name { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>Reporte mensual expresado en ARS. Si hubo movimientos en USD, UsdRateUsed trae la cotización aplicada.</summary>
    public class MonthlyReportResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Currency { get; set; } = "ARS";
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance { get; set; }
        public List<CategoryTotalResponse> ByCategory { get; set; } = new();
        public decimal? UsdRateUsed { get; set; }
    }
}
