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

        // A month nobody set up has no movements. The specific setups below take precedence over this one.
        _transactionRepositoryMock
            .Setup(r => r.GetMonthlyTotals(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CategoryTotalRow>());
    }

    private void SetupRows(params CategoryTotalRow[] rows) => SetupMonth(new DateOnly(2026, 11, 1), rows);

    /// <summary>The rows of the month that starts on <paramref name="first"/>.</summary>
    private void SetupMonth(DateOnly first, params CategoryTotalRow[] rows)
    {
        _transactionRepositoryMock
            .Setup(r => r.GetMonthlyTotals(_userId, first, first.AddMonths(1)))
            .ReturnsAsync(rows.ToList());
    }

    private void SetupCurrentRate(DollarType type, decimal rate) =>
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRate(type)).ReturnsAsync(rate);

    private void SetupRateDown(DollarType type) =>
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRate(type)).ThrowsAsync(new ExternalServiceException("down"));

    /// <param name="total">Total already in ARS (USD converted with the stored rate).</param>
    /// <param name="pendingUsd">USD amount without a stored rate yet.</param>
    /// <param name="dollar">For USD rows, the dollar the transactions of the row were saved with.</param>
    private static CategoryTotalRow Row(
        string name, TransactionType type, Currency currency, decimal total, decimal pendingUsd = 0m,
        DollarType dollar = DollarType.Official)
        => new CategoryTotalRow
        {
            Name = name, Type = type, Currency = currency, Total = total, PendingUsd = pendingUsd, DollarType = dollar
        };

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 5)]
    public async Task GetMonthly_Throws_WhenDateIsInvalid(int year, int month)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _reportService.GetMonthly(year, month, _userId));
    }

    [Fact]
    public async Task GetMonthly_DoesNotAskForAnyRate_WhenThereAreNoUsdTransactions()
    {
        SetupRows(
            Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m),
            Row("Comida", TransactionType.Expense, Currency.ARS, 400m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.Equal(1000m, report.Income);
        Assert.Equal(400m, report.Expense);
        Assert.Equal(600m, report.Balance);
        Assert.Empty(report.PendingConversions);
        Assert.False(report.HasUnconvertedUsd);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(It.IsAny<DollarType>()), Times.Never);
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
        Assert.Empty(report.PendingConversions);
        Assert.False(report.HasUnconvertedUsd);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(It.IsAny<DollarType>()), Times.Never);
    }

    [Fact]
    public async Task GetMonthly_AddsUpTheTransactionsOfOneCategorySavedWithDifferentDollars()
    {
        // The user chose another dollar in between: the same category has USD rows of three dollars, each already in ARS
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.USD, 1000m, dollar: DollarType.Official),
            Row("Comida", TransactionType.Expense, Currency.USD, 2000m, dollar: DollarType.Blue),
            Row("Comida", TransactionType.Expense, Currency.USD, 4000m, dollar: DollarType.Mep));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var comida = Assert.Single(report.ByCategory);
        Assert.Equal(7000m, comida.Total); // one line per category, not one per dollar
        Assert.Equal(7000m, report.Expense);
    }

    [Fact]
    public async Task GetMonthly_ConvertsPendingUsdWithTheCurrentRateOfItsOwnDollar()
    {
        SetupCurrentRate(DollarType.Official, 1000m);
        SetupCurrentRate(DollarType.Blue, 1200m);
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.ARS, 5000m),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 2000m, pendingUsd: 10m, dollar: DollarType.Official),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 0m, pendingUsd: 5m, dollar: DollarType.Blue),
            Row("Sueldo", TransactionType.Income, Currency.ARS, 100000m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var comida = Assert.Single(report.ByCategory, c => c.Name == "Comida");
        // 5.000 ARS + 2.000 stored + 10 USD * 1.000 (official) + 5 USD * 1.200 (blue)
        Assert.Equal(23000m, comida.Total);
        Assert.Collection(
            report.PendingConversions,
            p => Assert.Equal((DollarType.Official, (decimal?)1000m), (p.DollarType, p.Rate)),
            p => Assert.Equal((DollarType.Blue, (decimal?)1200m), (p.DollarType, p.Rate)));
        Assert.False(report.HasUnconvertedUsd);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(DollarType.Mep), Times.Never); // only the dollars that needed it
    }

    [Fact]
    public async Task GetMonthly_LeavesPendingUsdOut_AndFlagsIt_WhenItsRateIsNotAvailable()
    {
        SetupRateDown(DollarType.Official);
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.ARS, 5000m),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 2000m, pendingUsd: 10m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var comida = Assert.Single(report.ByCategory);
        Assert.Equal(7000m, comida.Total); // pending USD left out
        Assert.True(report.HasUnconvertedUsd);
        var pending = Assert.Single(report.PendingConversions);
        Assert.Equal(DollarType.Official, pending.DollarType);
        Assert.Null(pending.Rate); // the report says which dollar is missing
    }

    [Fact]
    public async Task GetMonthly_ConvertsTheDollarThatHasARate_AndLeavesOutTheOneThatDoesNot()
    {
        SetupCurrentRate(DollarType.Blue, 1100m);
        SetupRateDown(DollarType.Mep);
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.USD, total: 0m, pendingUsd: 10m, dollar: DollarType.Blue),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 0m, pendingUsd: 10m, dollar: DollarType.Mep));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.Equal(11000m, report.Expense); // only the blue ones; the MEP ones are left out
        Assert.True(report.HasUnconvertedUsd);
        Assert.Collection(
            report.PendingConversions,
            p => Assert.Equal((DollarType.Blue, (decimal?)1100m), (p.DollarType, p.Rate)),
            p => Assert.Equal((DollarType.Mep, (decimal?)null), (p.DollarType, p.Rate)));
    }

    // ---------- Previous month ----------

    [Fact]
    public async Task GetMonthly_IncludesTheTotalsOfThePreviousMonth()
    {
        SetupMonth(new DateOnly(2026, 11, 1), Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m));
        SetupMonth(
            new DateOnly(2026, 10, 1),
            Row("Sueldo", TransactionType.Income, Currency.ARS, 800m),
            Row("Comida", TransactionType.Expense, Currency.ARS, 500m),
            Row("Comida", TransactionType.Expense, Currency.ARS, 100m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.NotNull(report.Previous);
        Assert.Equal(2026, report.Previous!.Year);
        Assert.Equal(10, report.Previous.Month);
        Assert.Equal(800m, report.Previous.Income);
        Assert.Equal(600m, report.Previous.Expense);
        Assert.Equal(200m, report.Previous.Balance);
        Assert.False(report.Previous.HasUnconvertedUsd);
        Assert.Equal(1000m, report.Income); // the month itself is not affected
    }

    [Fact]
    public async Task GetMonthly_ReportsZeros_WhenThePreviousMonthHasNoMovements()
    {
        SetupRows(Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.NotNull(report.Previous);
        Assert.Equal(0m, report.Previous!.Income);
        Assert.Equal(0m, report.Previous.Expense);
        Assert.Equal(0m, report.Previous.Balance);
    }

    [Fact]
    public async Task GetMonthly_ComparesJanuaryWithDecemberOfThePreviousYear()
    {
        SetupMonth(new DateOnly(2025, 12, 1), Row("Sueldo", TransactionType.Income, Currency.ARS, 700m));

        var report = await _reportService.GetMonthly(2026, 1, _userId);

        Assert.Equal(2025, report.Previous!.Year);
        Assert.Equal(12, report.Previous.Month);
        Assert.Equal(700m, report.Previous.Income);
    }

    [Fact]
    public async Task GetMonthly_HasNoPreviousMonth_ForJanuaryOfTheFirstYear_AndDoesNotQueryIt()
    {
        var report = await _reportService.GetMonthly(2000, 1, _userId);

        Assert.Null(report.Previous);
        _transactionRepositoryMock.Verify(
            r => r.GetMonthlyTotals(_userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Once);
    }

    [Fact]
    public async Task GetMonthly_ConvertsThePendingUsdOfThePreviousMonth_WithoutListingItAsAConversionOfThisOne()
    {
        SetupCurrentRate(DollarType.Official, 1000m);
        // This month has nothing pending: only the previous one needs a current rate
        SetupRows(Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m));
        SetupMonth(
            new DateOnly(2026, 10, 1),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 2000m, pendingUsd: 10m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.Equal(12000m, report.Previous!.Expense); // 2.000 stored + 10 USD * 1.000
        Assert.Empty(report.PendingConversions); // that list is about the month on screen
        Assert.False(report.HasUnconvertedUsd);
    }

    [Fact]
    public async Task GetMonthly_AsksForTheRateOfADollarOnlyOnce_WhenBothMonthsNeedIt()
    {
        SetupCurrentRate(DollarType.Official, 1000m);
        SetupRows(Row("Comida", TransactionType.Expense, Currency.USD, total: 0m, pendingUsd: 5m));
        SetupMonth(new DateOnly(2026, 10, 1), Row("Comida", TransactionType.Expense, Currency.USD, total: 0m, pendingUsd: 10m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.Equal(5000m, report.Expense);
        Assert.Equal(10000m, report.Previous!.Expense);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(DollarType.Official), Times.Once);
    }

    [Fact]
    public async Task GetMonthly_FlagsThePreviousMonthAsIncomplete_WhenTheRateIsNotAvailable_ButNotThisOne()
    {
        SetupRateDown(DollarType.Official);
        SetupRows(Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m)); // nothing pending here
        SetupMonth(
            new DateOnly(2026, 10, 1),
            Row("Comida", TransactionType.Expense, Currency.USD, total: 2000m, pendingUsd: 10m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.False(report.HasUnconvertedUsd);
        Assert.True(report.Previous!.HasUnconvertedUsd);
        Assert.Equal(2000m, report.Previous.Expense); // the pending USD is left out, as in the report itself
    }
}
