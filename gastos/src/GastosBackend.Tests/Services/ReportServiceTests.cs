using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Enums;
using Moq;

namespace GastosBackend.Tests.Services;

public class ReportServiceTests
{
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<IDolarApiService> _dolarApiServiceMock;
    private readonly ReportService _reportService;
    private readonly Guid _userId = Guid.NewGuid();

    public ReportServiceTests()
    {
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _dolarApiServiceMock = new Mock<IDolarApiService>();
        _reportService = new ReportService(_transactionRepositoryMock.Object, _dolarApiServiceMock.Object);
    }

    private void SetupRows(params CategoryTotalRow[] rows)
    {
        _transactionRepositoryMock
            .Setup(r => r.GetMonthlyTotals(_userId, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1)))
            .ReturnsAsync(rows.ToList());
    }

    private static CategoryTotalRow Row(string name, TransactionType type, Currency currency, decimal total)
        => new CategoryTotalRow { Name = name, Type = type, Currency = currency, Total = total };

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 5)]
    public async Task GetMonthly_Throws_WhenDateIsInvalid(int year, int month)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _reportService.GetMonthly(year, month, _userId.ToString()));
    }

    [Fact]
    public async Task GetMonthly_DoesNotAskForTheRate_WhenThereAreNoUsdTransactions()
    {
        SetupRows(
            Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m),
            Row("Comida", TransactionType.Expense, Currency.ARS, 400m));

        var report = await _reportService.GetMonthly(2026, 11, _userId.ToString());

        Assert.Equal(1000m, report.Income);
        Assert.Equal(400m, report.Expense);
        Assert.Equal(600m, report.Balance);
        Assert.Null(report.UsdRateUsed);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(), Times.Never);
    }

    [Fact]
    public async Task GetMonthly_ConvertsUsdToArs_AndGroupsByCategory()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRate()).ReturnsAsync(1000m);
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.ARS, 5000m),
            Row("Comida", TransactionType.Expense, Currency.USD, 10m),
            Row("Sueldo", TransactionType.Income, Currency.ARS, 100000m));

        var report = await _reportService.GetMonthly(2026, 11, _userId.ToString());

        var comida = Assert.Single(report.ByCategory, c => c.Name == "Comida");
        Assert.Equal(15000m, comida.Total); // 5.000 ARS + 10 USD * 1.000
        Assert.Equal(15000m, report.Expense);
        Assert.Equal(85000m, report.Balance);
        Assert.Equal(1000m, report.UsdRateUsed);
        Assert.Equal("ARS", report.Currency);
    }
}
