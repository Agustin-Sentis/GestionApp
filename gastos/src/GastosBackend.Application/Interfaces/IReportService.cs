using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface IReportService
    {
        Task<MonthlyReportResponse> GetMonthly(int year, int month, Guid userId);

        /// <summary>Month by month totals from <paramref name="from"/> to <paramref name="to"/> (yyyy-MM, both included, at most 24 months).</summary>
        Task<EvolutionReportResponse> GetEvolution(string from, string to, Guid userId);
    }
}
