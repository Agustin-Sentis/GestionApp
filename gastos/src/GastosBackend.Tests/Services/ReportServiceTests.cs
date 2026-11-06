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

    /// <param name="total">Total already in ARS (USD converted with the stored rate).</param>
    /// <param name="pendingUsd">USD amount without a stored rate yet.</param>
    private static CategoryTotalRow Row(
        string name, TransactionType type, Currency currency, decimal total, decimal pendingUsd = 0m)
        => new CategoryTotalRow { Name = name, Type = type, Currency = currency, Total = total, PendingUsd = pendingUsd };

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 5)]
    public async Task GetMonthly_Throws_WhenDateIsInvalid(int year, int month)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _reportService.GetMonthly(year, month, _userId));
    }

    [Fact]
    public async Task GetMonthly_DoesNotAskForTheRate_WhenThereAreNoUsdTransactions()
    {
        SetupRows(
            Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m),
            Row("Comida", TransactionType.Expense, Currency.ARS, 400m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.Equal(1000m, report.Income);
        Assert.Equal(400m, report.Expense);
        Assert.Equal(600m, report.Balance);
        Assert.Null(report.UsdRateUsed);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(), Times.Never);
    }

    [Fact]
    public async Task GetMonthly_UsesStoredRates_WithoutAskingForTheCurrentRate()
    {
        // The USD row total (10.000) was already converted in SQL with the rate stored on each transaction.
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.ARS, 5000m),
            Row("Comida", TransactionType.Expense, Currency.USD, 10000m),
            Row("Sueldo", TransactionType.Income, Currency.ARS, 100000m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var comida = Assert.Single(report.ByCategory, c => c.Name == "Comida");
        Assert.Equal(15000m, comida.Total);
        Assert.Equal(15000m, report.Expense);
        Assert.Equal(85000m, report.Balance);
        Assert.Equal("ARS", report.Currency);
        Assert.Null(report.UsdRateUsed);
        Assert.False(report.HasUnconvertedUsd);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(), Times.Never);
    }

    [Fact]
    public async Task GetMonthly_ConvertsPendingUsdWithTheCurrentRate()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRate()).ReturnsAsync(1000m);
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.ARS, 5000m),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 2000m, pendingUsd: 10m),
            Row("Sueldo", TransactionType.Income, Currency.ARS, 100000m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var comida = Assert.Single(report.ByCategory, c => c.Name == "Comida");
        Assert.Equal(17000m, comida.Total); // 5.000 ARS + 2.000 stored + 10 USD * 1.000
        Assert.Equal(1000m, report.UsdRateUsed);
        Assert.False(report.HasUnconvertedUsd);
    }

    [Fact]
    public async Task GetMonthly_LeavesPendingUsdOut_AndFlagsIt_WhenTheRateIsNotAvailable()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRate()).ThrowsAsync(new ExternalServiceException("down"));
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.ARS, 5000m),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 2000m, pendingUsd: 10m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var comida = Assert.Single(report.ByCategory);
        Assert.Equal(7000m, comida.Total); // pending USD left out
        Assert.True(report.HasUnconvertedUsd);
        Assert.Null(report.UsdRateUsed);
    }
}
