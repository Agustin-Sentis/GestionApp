using GastosBackend.Application.dtos.Responses;

namespace GastosBackend.Application.Interfaces
{
    public interface IReportService
    {
        Task<MonthlyReportResponse> GetMonthly(int year, int month, Guid userId);
    }
}
