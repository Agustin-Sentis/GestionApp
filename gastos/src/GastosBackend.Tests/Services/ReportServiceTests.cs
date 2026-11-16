using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using Moq;

namespace GastosBackend.Tests.Services;

public class ReportServiceTests
{
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IDolarApiService> _dolarApiServiceMock;
    private readonly ReportService _reportService;
    private readonly Guid _userId = Guid.NewGuid();

    public ReportServiceTests()
    {
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _dolarApiServiceMock = new Mock<IDolarApiService>();
        _reportService = new ReportService(
            _transactionRepositoryMock.Object, _categoryRepositoryMock.Object, _dolarApiServiceMock.Object);

        // Nobody has budgets unless a test says so
        _categoryRepositoryMock.Setup(r => r.GetAllByUser(_userId)).ReturnsAsync(new List<Category>());

        // A month nobody set up has no movements. The specific setups below take precedence over this one.
        _transactionRepositoryMock
            .Setup(r => r.GetMonthlyTotals(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CategoryTotalRow>());

        // A range nobody set up has no movements either
        _transactionRepositoryMock
            .Setup(r => r.GetTotalsByMonth(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<MonthTotalRow>());
    }

    private void SetupTotalsByMonth(params MonthTotalRow[] rows) =>
        _transactionRepositoryMock
            .Setup(r => r.GetTotalsByMonth(_userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(rows.ToList());

    private static MonthTotalRow MonthRow(
        int year, int month, TransactionType type, Currency currency, decimal total, decimal pendingUsd = 0m,
        DollarType dollar = DollarType.Official)
        => new MonthTotalRow
        {
            Year = year, Month = month, Type = type, Currency = currency, Total = total, PendingUsd = pendingUsd, DollarType = dollar
        };

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

    private void SetupCategories(params Category[] categories) =>
        _categoryRepositoryMock.Setup(r => r.GetAllByUser(_userId)).ReturnsAsync(categories.ToList());

    private static Category Budgeted(string name, decimal? budget, TransactionType type = TransactionType.Expense)
        => new Category { Id = Guid.NewGuid(), Name = name, Type = type, MonthlyBudget = budget };

    [Theory]
    [InlineData(79, 79, BudgetStatus.Ok)]
    [InlineData(79.99, 79.99, BudgetStatus.Ok)]
    [InlineData(80, 80, BudgetStatus.Warning)]
    [InlineData(99, 99, BudgetStatus.Warning)]
    [InlineData(100, 100, BudgetStatus.Warning)] // spending exactly the whole budget is not over it yet
    [InlineData(100.01, 100.01, BudgetStatus.Exceeded)]
    [InlineData(101, 101, BudgetStatus.Exceeded)]
    [InlineData(250, 250, BudgetStatus.Exceeded)]
    public async Task GetMonthly_MeasuresTheBudgetAgainstWhatWasSpent(double spent, double expectedPercent, BudgetStatus expected)
    {
        SetupCategories(Budgeted("Comida", 100m));
        SetupRows(Row("Comida", TransactionType.Expense, Currency.ARS, (decimal)spent));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var budget = Assert.Single(report.Budgets);
        Assert.Equal("Comida", budget.Name);
        Assert.Equal(100m, budget.MonthlyBudget);
        Assert.Equal((decimal)spent, budget.Spent);
        Assert.Equal(Math.Round((decimal)expectedPercent, 1), budget.UsedPercent);
        Assert.Equal(expected, budget.Status);
    }

    [Fact]
    public async Task GetMonthly_ShowsABudgetedCategoryWithNoSpending_AsZero()
    {
        SetupCategories(Budgeted("Viajes", 50000m));
        SetupRows(Row("Sueldo", TransactionType.Income, Currency.ARS, 1000m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var budget = Assert.Single(report.Budgets);
        Assert.Equal(0m, budget.Spent);
        Assert.Equal(0m, budget.UsedPercent);
        Assert.Equal(BudgetStatus.Ok, budget.Status);
        // The empty category does not turn into a slice of the charts
        Assert.DoesNotContain(report.ByCategory, c => c.Name == "Viajes");
    }

    [Fact]
    public async Task GetMonthly_LeavesOutCategoriesWithoutABudget_AndIncomeCategories()
    {
        SetupCategories(Budgeted("Comida", null), Budgeted("Sueldo", 1000m, TransactionType.Income), Budgeted("Gym", 10m));
        SetupRows(Row("Comida", TransactionType.Expense, Currency.ARS, 400m));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.Equal("Gym", Assert.Single(report.Budgets).Name);
    }

    [Fact]
    public async Task GetMonthly_CountsUsdSpendingConvertedToPesos_AgainstTheBudget()
    {
        // 90.000 of the USD row were already converted with the rate stored on each transaction
        SetupCategories(Budgeted("Comida", 100000m));
        SetupRows(
            Row("Comida", TransactionType.Expense, Currency.ARS, 5000m),
            Row("Comida", TransactionType.Expense, Currency.USD, 90000m, dollar: DollarType.Blue));

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        var budget = Assert.Single(report.Budgets);
        Assert.Equal(95000m, budget.Spent);
        Assert.Equal(95m, budget.UsedPercent);
        Assert.Equal(BudgetStatus.Warning, budget.Status);
    }

    [Fact]
    public async Task GetMonthly_MeasuresWithWhatIsAvailable_WhenSomeUsdCouldNotBeConverted()
    {
        SetupCategories(Budgeted("Comida", 1000m));
        SetupRows(Row("Comida", TransactionType.Expense, Currency.USD, 100m, pendingUsd: 50m));
        SetupRateDown(DollarType.Official);

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.True(report.HasUnconvertedUsd);
        var budget = Assert.Single(report.Budgets);
        Assert.Equal(100m, budget.Spent);
        Assert.Equal(BudgetStatus.Ok, budget.Status);
    }

    [Fact]
    public async Task GetMonthly_SortsTheBudgetsWithTheMostUrgentFirst()
    {
        SetupCategories(Budgeted("Ocio", 100m), Budgeted("Comida", 100m), Budgeted("Gym", 100m), Budgeted("Auto", 100m));
        SetupRows(
            Row("Ocio", TransactionType.Expense, Currency.ARS, 30m),     // ok
            Row("Comida", TransactionType.Expense, Currency.ARS, 150m),  // exceeded
            Row("Gym", TransactionType.Expense, Currency.ARS, 85m),      // warning
            Row("Auto", TransactionType.Expense, Currency.ARS, 120m));   // exceeded, less than Comida

        var report = await _reportService.GetMonthly(2026, 11, _userId);

        Assert.Equal(new[] { "Comida", "Auto", "Gym", "Ocio" }, report.Budgets.Select(b => b.Name));
    }

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

    // ---------- GetEvolution ----------

    [Fact]
    public async Task GetEvolution_ReturnsOneEntryPerMonthOfTheRange_WithEmptyMonthsInZero()
    {
        SetupTotalsByMonth(
            MonthRow(2026, 9, TransactionType.Income, Currency.ARS, 1000m),
            MonthRow(2026, 9, TransactionType.Expense, Currency.ARS, 400m),
            MonthRow(2026, 11, TransactionType.Expense, Currency.ARS, 250m));

        var report = await _reportService.GetEvolution("2026-09", "2026-11", _userId);

        Assert.Equal(("2026-09", "2026-11", "ARS"), (report.From, report.To, report.Currency));
        Assert.Equal(new[] { 9, 10, 11 }, report.Months.Select(m => m.Month));
        Assert.All(report.Months, m => Assert.Equal(2026, m.Year));

        var september = report.Months[0];
        Assert.Equal((1000m, 400m, 600m), (september.Income, september.Expense, september.Balance));

        var october = report.Months[1]; // no movements: a month in zero, not a hole
        Assert.Equal((0m, 0m, 0m, false), (october.Income, october.Expense, october.Balance, october.HasUnconvertedUsd));

        var november = report.Months[2];
        Assert.Equal((0m, 250m, -250m), (november.Income, november.Expense, november.Balance));
    }

    [Fact]
    public async Task GetEvolution_CrossesYears_InOrder()
    {
        SetupTotalsByMonth(
            MonthRow(2026, 1, TransactionType.Expense, Currency.ARS, 20m),
            MonthRow(2025, 12, TransactionType.Expense, Currency.ARS, 10m));

        var report = await _reportService.GetEvolution("2025-11", "2026-01", _userId);

        Assert.Equal(new[] { (2025, 11), (2025, 12), (2026, 1) }, report.Months.Select(m => (m.Year, m.Month)));
        Assert.Equal(new[] { 0m, 10m, 20m }, report.Months.Select(m => m.Expense));
    }

    [Fact]
    public async Task GetEvolution_AddsUpTheTotalsOfThePeriod()
    {
        SetupTotalsByMonth(
            MonthRow(2026, 10, TransactionType.Income, Currency.ARS, 1000m),
            MonthRow(2026, 10, TransactionType.Expense, Currency.ARS, 300m),
            MonthRow(2026, 11, TransactionType.Income, Currency.ARS, 500m),
            MonthRow(2026, 11, TransactionType.Expense, Currency.ARS, 900m));

        var report = await _reportService.GetEvolution("2026-10", "2026-11", _userId);

        Assert.Equal((1500m, 1200m, 300m), (report.Totals.Income, report.Totals.Expense, report.Totals.Balance));
    }

    [Fact]
    public async Task GetEvolution_SumsPesosAndUsdOfTheSameMonth_AndKindOfMovement()
    {
        SetupTotalsByMonth(
            MonthRow(2026, 11, TransactionType.Expense, Currency.ARS, 1500m),
            MonthRow(2026, 11, TransactionType.Expense, Currency.USD, 18200m, dollar: DollarType.Blue));

        var report = await _reportService.GetEvolution("2026-11", "2026-11", _userId);

        Assert.Equal(19700m, Assert.Single(report.Months).Expense);
    }

    [Fact]
    public async Task GetEvolution_ConvertsPendingUsdWithTheCurrentRate_AskingEachDollarOnce()
    {
        SetupCurrentRate(DollarType.Blue, 1000m);
        SetupTotalsByMonth(
            MonthRow(2026, 10, TransactionType.Expense, Currency.USD, 0m, pendingUsd: 5m, dollar: DollarType.Blue),
            MonthRow(2026, 11, TransactionType.Expense, Currency.USD, 2000m, pendingUsd: 10m, dollar: DollarType.Blue));

        var report = await _reportService.GetEvolution("2026-10", "2026-11", _userId);

        Assert.Equal(5000m, report.Months[0].Expense);
        Assert.Equal(12000m, report.Months[1].Expense); // 2000 already converted + 10 USD * 1000
        Assert.Equal(17000m, report.Totals.Expense);

        var pending = Assert.Single(report.PendingConversions);
        Assert.Equal((DollarType.Blue, (decimal?)1000m), (pending.DollarType, pending.Rate));
        Assert.False(report.HasUnconvertedUsd);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(DollarType.Blue), Times.Once);
    }

    [Fact]
    public async Task GetEvolution_DoesNotAskForAnyRate_WhenNothingIsPending()
    {
        SetupTotalsByMonth(MonthRow(2026, 11, TransactionType.Expense, Currency.USD, 15000m));

        var report = await _reportService.GetEvolution("2026-11", "2026-11", _userId);

        Assert.Empty(report.PendingConversions);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRate(It.IsAny<DollarType>()), Times.Never);
    }

    [Fact]
    public async Task GetEvolution_WhenARateIsDown_LeavesThoseOutAndFlagsOnlyTheirMonth()
    {
        SetupRateDown(DollarType.Official);
        SetupTotalsByMonth(
            MonthRow(2026, 10, TransactionType.Expense, Currency.ARS, 100m),
            MonthRow(2026, 11, TransactionType.Expense, Currency.USD, 2000m, pendingUsd: 10m));

        var report = await _reportService.GetEvolution("2026-10", "2026-11", _userId);

        Assert.False(report.Months[0].HasUnconvertedUsd);
        Assert.True(report.Months[1].HasUnconvertedUsd);
        Assert.Equal(2000m, report.Months[1].Expense); // the pending USD is left out, as in the monthly report
        Assert.True(report.HasUnconvertedUsd);

        var pending = Assert.Single(report.PendingConversions);
        Assert.Null(pending.Rate);
    }

    [Fact]
    public async Task GetEvolution_AsksTheRepositoryOnceForTheWholeRange()
    {
        await _reportService.GetEvolution("2026-06", "2026-11", _userId);

        _transactionRepositoryMock.Verify(
            r => r.GetTotalsByMonth(_userId, new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 1)), Times.Once);
        _transactionRepositoryMock.Verify(
            r => r.GetMonthlyTotals(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task GetEvolution_AcceptsASingleMonth_AndExactly24Months()
    {
        var single = await _reportService.GetEvolution("2026-11", "2026-11", _userId);
        var twoYears = await _reportService.GetEvolution("2025-01", "2026-12", _userId);

        Assert.Single(single.Months);
        Assert.Equal(24, twoYears.Months.Count);
    }

    [Theory]
    [InlineData("2026-6", "2026-11", "El rango debe tener el formato AAAA-MM.")]
    [InlineData("2026-13", "2026-11", "El rango debe tener el formato AAAA-MM.")]
    [InlineData("2026-00", "2026-11", "El rango debe tener el formato AAAA-MM.")]
    [InlineData("abc", "2026-11", "El rango debe tener el formato AAAA-MM.")]
    [InlineData("2026-06", "", "El rango debe tener el formato AAAA-MM.")]
    [InlineData("2026-06-01", "2026-11", "El rango debe tener el formato AAAA-MM.")]
    [InlineData("2026-11", "2026-10", "El mes inicial no puede ser posterior al final.")]
    [InlineData("2024-12", "2026-12", "El rango no puede superar los 24 meses.")] // 25 months
    [InlineData("1999-12", "2000-03", "El año o el mes no son válidos.")]
    [InlineData("2100-11", "2101-01", "El año o el mes no son válidos.")]
    public async Task GetEvolution_RejectsInvalidRanges(string from, string to, string message)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => _reportService.GetEvolution(from, to, _userId));

        Assert.Equal(message, error.Message);
        _transactionRepositoryMock.Verify(
            r => r.GetTotalsByMonth(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
    }
}
