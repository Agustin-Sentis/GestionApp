using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.IntegrationTests.Support;

namespace GastosBackend.IntegrationTests.Repositories;

/// <summary>The SQL behind the transaction list, the paged list and the monthly report, against a real PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public class TransactionRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public TransactionRepositoryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<TestDatabase> MigratedDatabase()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();
        return database;
    }

    private static DateTimeOffset At(int day) => new DateTimeOffset(2026, 1, day, 12, 0, 0, TimeSpan.Zero);

    // ---------- GetPageByUser ----------

    [Fact]
    public async Task Page_IsOrderedByDate_ThenByCreationTime_Newest_First()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        var oldest = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01", createdDate: At(9));
        var sameDayFirst = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", createdDate: At(1));
        var sameDayLast = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", createdDate: At(3));
        var sameDayMiddle = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", createdDate: At(2));
        var newest = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-20", createdDate: At(1));

        await using var context = database.CreateContext();
        var page = await new TransactionRepository(context).GetPageByUser(user.Id, null, null, null, null, null, 0, 50);

        Assert.Equal(
            new[] { newest.Id, sameDayLast.Id, sameDayMiddle.Id, sameDayFirst.Id, oldest.Id },
            page.Items.Select(t => t.Id).ToArray());
    }

    [Fact]
    public async Task Page_BreaksTiesByIdDescending_WhenDateAndCreationTimeAreEqual()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        for (var i = 0; i < 6; i++)
        {
            await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", createdDate: At(1));
        }

        await using var context = database.CreateContext();
        var page = await new TransactionRepository(context).GetPageByUser(user.Id, null, null, null, null, null, 0, 50);

        // PostgreSQL sorts uuids by their bytes, which is the same as sorting their canonical text
        var expected = page.Items.Select(t => t.Id.ToString("D")).OrderByDescending(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, page.Items.Select(t => t.Id.ToString("D")).ToArray());
    }

    [Fact]
    public async Task Pages_CoverEveryRowExactlyOnce_AndMatchOneBigPage()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        for (var i = 0; i < 25; i++)
        {
            // Few distinct dates on purpose, so the tie-breakers decide most of the order
            await database.AddTransactionAsync(user.Id, food.Id, $"2026-11-{1 + i % 4:00}", createdDate: At(1 + i % 7));
        }

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);
        var all = (await repository.GetPageByUser(user.Id, null, null, null, null, null, 0, 100)).Items.Select(t => t.Id).ToList();

        var paged = new List<Guid>();
        for (var skip = 0; skip < 25; skip += 10)
        {
            var page = await repository.GetPageByUser(user.Id, null, null, null, null, null, skip, 10);
            Assert.Equal(25, page.Total);
            paged.AddRange(page.Items.Select(t => t.Id));
        }

        Assert.Equal(25, paged.Distinct().Count()); // no row repeated
        Assert.Equal(all, paged);                   // none skipped, and the same order as the single query
    }

    [Fact]
    public async Task Page_TotalCountsEverythingThatMatches_NotJustThePage()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        for (var i = 0; i < 12; i++)
        {
            await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05");
        }

        await using var context = database.CreateContext();
        var page = await new TransactionRepository(context).GetPageByUser(user.Id, null, null, null, null, null, 0, 5);

        Assert.Equal(5, page.Items.Count);
        Assert.Equal(12, page.Total);
    }

    [Fact]
    public async Task Page_DateRange_IncludesBothEnds()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        foreach (var date in new[] { "2026-10-31", "2026-11-01", "2026-11-15", "2026-11-30", "2026-12-01" })
        {
            await database.AddTransactionAsync(user.Id, food.Id, date);
        }

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        var november = await repository.GetPageByUser(
            user.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, null, null, 0, 50);
        Assert.Equal(
            new[] { "2026-11-30", "2026-11-15", "2026-11-01" },
            november.Items.Select(t => t.Date.ToString("yyyy-MM-dd")).ToArray());

        Assert.Equal(2, (await repository.GetPageByUser(user.Id, new DateOnly(2026, 11, 30), null, null, null, null, 0, 50)).Total);
        Assert.Equal(2, (await repository.GetPageByUser(user.Id, null, new DateOnly(2026, 11, 1), null, null, null, 0, 50)).Total);
    }

    [Fact]
    public async Task Page_FiltersByCategory_AndByTheTypeOfTheCategory()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food", TransactionType.Expense);
        var rent = await database.AddCategoryAsync(user.Id, "Rent", TransactionType.Expense);
        var salary = await database.AddCategoryAsync(user.Id, "Salary", TransactionType.Income);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-02");
        await database.AddTransactionAsync(user.Id, rent.Id, "2026-11-03");
        await database.AddTransactionAsync(user.Id, salary.Id, "2026-11-04");

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        var byCategory = await repository.GetPageByUser(user.Id, null, null, food.Id, null, null, 0, 50);
        Assert.Equal(2, byCategory.Total);
        Assert.All(byCategory.Items, t => Assert.Equal(food.Id, t.CategoryId));

        var expenses = await repository.GetPageByUser(user.Id, null, null, null, TransactionType.Expense, null, 0, 50);
        Assert.Equal(3, expenses.Total);
        Assert.All(expenses.Items, t => Assert.Equal(TransactionType.Expense, t.Category!.Type));

        var incomes = await repository.GetPageByUser(user.Id, null, null, null, TransactionType.Income, null, 0, 50);
        Assert.Equal(salary.Id, incomes.Items.Single().CategoryId);

        var none = await repository.GetPageByUser(user.Id, null, null, salary.Id, TransactionType.Expense, null, 0, 50);
        Assert.Equal(0, none.Total); // the filters combine: Salary is not an expense
    }

    [Fact]
    public async Task Page_ExcludesOtherUsers_AndDeletedTransactions()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var otherUser = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        var otherFood = await database.AddCategoryAsync(otherUser.Id, "Food");
        var mine = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-02", deleted: true);
        await database.AddTransactionAsync(otherUser.Id, otherFood.Id, "2026-11-03");

        await using var context = database.CreateContext();
        var page = await new TransactionRepository(context).GetPageByUser(user.Id, null, null, null, null, null, 0, 50);

        Assert.Equal(mine.Id, page.Items.Single().Id);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task Page_LoadsTheCategoryOfEveryItem()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01");

        await using var context = database.CreateContext();
        var item = (await new TransactionRepository(context).GetPageByUser(user.Id, null, null, null, null, null, 0, 50)).Items.Single();

        Assert.Equal("Food", item.Category!.Name);
    }

    [Fact]
    public async Task Page_PastTheEnd_IsEmpty_ButKeepsTheTotal_EvenWithAHugeOffset()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-02");

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        var pastTheEnd = await repository.GetPageByUser(user.Id, null, null, null, null, null, 100, 10);
        Assert.Empty(pastTheEnd.Items);
        Assert.Equal(2, pastTheEnd.Total);

        var huge = await repository.GetPageByUser(user.Id, null, null, null, null, null, int.MaxValue, 100);
        Assert.Empty(huge.Items);
        Assert.Equal(2, huge.Total);
    }

    [Fact]
    public async Task Page_Search_FindsPartOfTheNote_IgnoringCase_AndLeavesOutTransactionsWithoutANote()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var otherUser = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        var otherFood = await database.AddCategoryAsync(otherUser.Id, "Food");
        var lunch = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01", note: "Almuerzo con Juan");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-02", note: "Cena");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-03", note: null);
        await database.AddTransactionAsync(otherUser.Id, otherFood.Id, "2026-11-04", note: "almuerzo de otro");

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        var found = await repository.GetPageByUser(user.Id, null, null, null, null, "ALMUERZO", 0, 50);
        Assert.Equal(lunch.Id, found.Items.Single().Id);
        Assert.Equal(1, found.Total);

        var middle = await repository.GetPageByUser(user.Id, null, null, null, null, "uerzo con", 0, 50);
        Assert.Equal(1, middle.Total);

        Assert.Equal(0, (await repository.GetPageByUser(user.Id, null, null, null, null, "pizza", 0, 50)).Total);
    }

    [Fact]
    public async Task Page_Search_TreatsPercentUnderscoreAndBackslashAsPlainText()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        var discount = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01", note: "descuento 50% off");
        var underscore = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-02", note: "a_b");
        var backslash = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-03", note: @"ruta c:\temp");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-04", note: "aXb");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", note: "cualquier cosa");

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        // "%" alone would match every note if it were a wildcard; "_" would match "aXb" too
        Assert.Equal(discount.Id, (await repository.GetPageByUser(user.Id, null, null, null, null, "%", 0, 50)).Items.Single().Id);
        Assert.Equal(underscore.Id, (await repository.GetPageByUser(user.Id, null, null, null, null, "a_b", 0, 50)).Items.Single().Id);
        Assert.Equal(backslash.Id, (await repository.GetPageByUser(user.Id, null, null, null, null, @"c:\t", 0, 50)).Items.Single().Id);
    }

    [Fact]
    public async Task Page_Search_CombinesWithTheOtherFilters_AndTheTotalFollowsIt()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food", TransactionType.Expense);
        var salary = await database.AddCategoryAsync(user.Id, "Salary", TransactionType.Income);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01", note: "cena");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-02", note: "cena");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-12-02", note: "cena");
        await database.AddTransactionAsync(user.Id, salary.Id, "2026-11-03", note: "cena de empresa");

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        var page = await repository.GetPageByUser(
            user.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, TransactionType.Expense, "cena", 0, 1);

        Assert.Single(page.Items);
        Assert.Equal(2, page.Total); // two November expenses match, one of them is on this page
    }

    // ---------- GetMonthlyTotals ----------

    [Fact]
    public async Task MonthlyTotals_ConvertUsdInSql_AndReportThePendingOnesSeparately()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food", TransactionType.Expense);
        var salary = await database.AddCategoryAsync(user.Id, "Salary", TransactionType.Income);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-03", amount: 1000m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-04", amount: 500m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", amount: 10m, currency: Currency.USD, exchangeRate: 1500m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-06", amount: 2m, currency: Currency.USD, exchangeRate: 1600m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-07", amount: 5m, currency: Currency.USD, exchangeRate: null);
        await database.AddTransactionAsync(user.Id, salary.Id, "2026-11-08", amount: 100000m);

        await using var context = database.CreateContext();
        var rows = await new TransactionRepository(context).GetMonthlyTotals(user.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));

        var foodArs = rows.Single(r => r.Name == "Food" && r.Currency == Currency.ARS);
        Assert.Equal((1500m, 0m), (foodArs.Total, foodArs.PendingUsd));

        // Each USD transaction is converted with its own rate: 10 * 1500 + 2 * 1600 = 18200. The 5 USD without a rate is pending.
        var foodUsd = rows.Single(r => r.Name == "Food" && r.Currency == Currency.USD);
        Assert.Equal((18200m, 5m), (foodUsd.Total, foodUsd.PendingUsd));

        var income = rows.Single(r => r.Name == "Salary");
        Assert.Equal((TransactionType.Income, 100000m, 0m), (income.Type, income.Total, income.PendingUsd));

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task MonthlyTotals_KeepTheExactDecimalsOfTheConversion()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", amount: 10.5m, currency: Currency.USD, exchangeRate: 1570.1234m);

        await using var context = database.CreateContext();
        var rows = await new TransactionRepository(context).GetMonthlyTotals(user.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));

        Assert.Equal(16486.2957m, rows.Single().Total); // 10.5 * 1570.1234, no rounding in SQL
    }

    [Fact]
    public async Task MonthlyTotals_IncludeBothEdgesOfTheMonth_AndNothingOutside()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-10-31", amount: 1m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01", amount: 10m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-30", amount: 100m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-12-01", amount: 1000m);

        await using var context = database.CreateContext();
        var rows = await new TransactionRepository(context).GetMonthlyTotals(user.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));

        Assert.Equal(110m, rows.Single().Total);
    }

    [Fact]
    public async Task MonthlyTotals_IgnoreOtherUsers_AndDeletedTransactions()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var otherUser = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        var otherFood = await database.AddCategoryAsync(otherUser.Id, "Food");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", amount: 10m);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-06", amount: 999m, deleted: true);
        await database.AddTransactionAsync(otherUser.Id, otherFood.Id, "2026-11-07", amount: 777m);

        await using var context = database.CreateContext();
        var rows = await new TransactionRepository(context).GetMonthlyTotals(user.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));

        Assert.Equal(10m, rows.Single().Total);
    }

    [Fact]
    public async Task MonthlyTotals_AreEmpty_WhenTheMonthHasNoTransactions()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();

        await using var context = database.CreateContext();
        var rows = await new TransactionRepository(context).GetMonthlyTotals(user.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));

        Assert.Empty(rows);
    }

    // ---------- GetMonthlyTotals: each transaction with the dollar it was saved with ----------

    private static readonly DateOnly November = new(2026, 11, 1);
    private static readonly DateOnly December = new(2026, 12, 1);

    [Theory]
    [InlineData(DollarType.Official, 15000)] // 10 USD * 1500
    [InlineData(DollarType.Blue, 14000)] // 10 USD * 1400
    [InlineData(DollarType.Mep, 16000)] // 10 USD * 1600
    public async Task MonthlyTotals_ConvertATransactionWithTheStoredRateOfTheDollarItWasSavedWith(DollarType saved, double expected)
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-05", amount: 10m, currency: Currency.USD, exchangeRate: 1500m,
            blueExchangeRate: 1400m, mepExchangeRate: 1600m, dollarType: saved);

        await using var context = database.CreateContext();
        var row = (await new TransactionRepository(context).GetMonthlyTotals(user.Id, November, December)).Single();

        Assert.Equal(((decimal)expected, 0m, saved), (row.Total, row.PendingUsd, row.DollarType));
    }

    [Fact]
    public async Task MonthlyTotals_KeepTransactionsSavedWithDifferentDollarsInDifferentRows_EachWithItsOwnRate()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        // The same category, three transactions of 10 USD saved on different days with three different dollars
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-03", amount: 10m, currency: Currency.USD, exchangeRate: 1500m, dollarType: DollarType.Official);
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-04", amount: 10m, currency: Currency.USD, exchangeRate: 1500m,
            blueExchangeRate: 1400m, mepExchangeRate: 1600m, dollarType: DollarType.Blue);
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-05", amount: 10m, currency: Currency.USD, exchangeRate: 1500m,
            blueExchangeRate: 1400m, mepExchangeRate: 1600m, dollarType: DollarType.Mep);

        await using var context = database.CreateContext();
        var rows = await new TransactionRepository(context).GetMonthlyTotals(user.Id, November, December);

        Assert.Equal(3, rows.Count);
        Assert.Equal(15000m, rows.Single(r => r.DollarType == DollarType.Official).Total);
        Assert.Equal(14000m, rows.Single(r => r.DollarType == DollarType.Blue).Total);
        Assert.Equal(16000m, rows.Single(r => r.DollarType == DollarType.Mep).Total);
    }

    [Fact]
    public async Task MonthlyTotals_DoNotDependOnWhatTheUserHasChosenSince_OnlyOnWhatEachTransactionStored()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync(); // the user's own dollar is Official, the default
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-05", amount: 10m, currency: Currency.USD, exchangeRate: 1500m,
            blueExchangeRate: 1400m, dollarType: DollarType.Blue);

        await using var context = database.CreateContext();
        var row = (await new TransactionRepository(context).GetMonthlyTotals(user.Id, November, December)).Single();

        Assert.Equal(14000m, row.Total); // blue, as saved, although the user's setting says Official
    }

    [Theory]
    [InlineData(DollarType.Official, 15000)] // has its official rate: 10 USD * 1500
    [InlineData(DollarType.Blue, 0)] // saved with blue, which has no rate: pending
    [InlineData(DollarType.Mep, 0)]
    public async Task MonthlyTotals_TreatATransactionWithoutTheRateOfItsOwnDollarAsPending(DollarType saved, double stored)
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        // Only the official rate was obtained (or the others failed): the transaction was saved with a dollar that has none
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-05", amount: 10m, currency: Currency.USD, exchangeRate: 1500m, dollarType: saved);

        await using var context = database.CreateContext();
        var row = (await new TransactionRepository(context).GetMonthlyTotals(user.Id, November, December)).Single();

        Assert.Equal(((decimal)stored, saved == DollarType.Official ? 0m : 10m), (row.Total, row.PendingUsd));
        Assert.Equal(saved, row.DollarType); // the service needs to know whose current rate converts the pending ones
    }

    [Fact]
    public async Task MonthlyTotals_AddUpTransactionsOfOneDollarWithAndWithoutTheRate_Separately()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-03", amount: 2m, currency: Currency.USD, blueExchangeRate: 1400m, dollarType: DollarType.Blue);
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-04", amount: 5m, currency: Currency.USD, dollarType: DollarType.Blue); // no rate at all

        await using var context = database.CreateContext();
        var blue = (await new TransactionRepository(context).GetMonthlyTotals(user.Id, November, December)).Single();

        Assert.Equal((2800m, 5m), (blue.Total, blue.PendingUsd)); // 2 * 1400 converted; the 5 USD without a rate pending
    }

    [Theory]
    [InlineData(DollarType.Official)]
    [InlineData(DollarType.Blue)]
    [InlineData(DollarType.Mep)]
    public async Task MonthlyTotals_DoNotSplitTransactionsInPesos_WhateverDollarTheyCarry(DollarType carried)
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-05", amount: 1234.5m, dollarType: carried);
        await database.AddTransactionAsync(user.Id, food.Id, "2026-11-06", amount: 100m, dollarType: DollarType.Mep);

        await using var context = database.CreateContext();
        var row = (await new TransactionRepository(context).GetMonthlyTotals(user.Id, November, December)).Single();

        // One row: a dollar type means nothing for pesos, so it must not turn one category into several lines
        Assert.Equal((1334.5m, 0m, DollarType.Official), (row.Total, row.PendingUsd, row.DollarType));
    }

    [Fact]
    public async Task MonthlyTotals_KeepTheExactDecimalsOfTheBlueAndMepRates()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-05", amount: 10.5m, currency: Currency.USD, blueExchangeRate: 1555.5555m, dollarType: DollarType.Blue);
        await database.AddTransactionAsync(
            user.Id, food.Id, "2026-11-06", amount: 10.5m, currency: Currency.USD, mepExchangeRate: 1582.2m, dollarType: DollarType.Mep);

        await using var context = database.CreateContext();
        var rows = await new TransactionRepository(context).GetMonthlyTotals(user.Id, November, December);

        Assert.Equal(16333.33275m, rows.Single(r => r.DollarType == DollarType.Blue).Total); // 10.5 * 1555.5555, exact: SQL does not round
        Assert.Equal(16613.1m, rows.Single(r => r.DollarType == DollarType.Mep).Total); // 10.5 * 1582.2
    }

    // ---------- GetByIdWithCategory / ExistsByCategory ----------

    [Fact]
    public async Task GetByIdWithCategory_ReturnsTheOwnTransactionWithItsCategory()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food", TransactionType.Expense);
        var transaction = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01", note: "lunch");

        await using var context = database.CreateContext();
        var found = await new TransactionRepository(context).GetByIdWithCategory(transaction.Id, user.Id);

        Assert.Equal("lunch", found!.Note);
        Assert.Equal("Food", found.Category!.Name);
        Assert.Equal(TransactionType.Expense, found.Category.Type);
    }

    [Fact]
    public async Task GetByIdWithCategory_ReturnsNull_ForAnotherUsersDeletedOrUnknownTransaction()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var otherUser = await database.AddUserAsync();
        var food = await database.AddCategoryAsync(user.Id, "Food");
        var mine = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-01");
        var deleted = await database.AddTransactionAsync(user.Id, food.Id, "2026-11-02", deleted: true);

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        Assert.Null(await repository.GetByIdWithCategory(mine.Id, otherUser.Id)); // somebody else's: indistinguishable from missing
        Assert.Null(await repository.GetByIdWithCategory(deleted.Id, user.Id));
        Assert.Null(await repository.GetByIdWithCategory(Guid.NewGuid(), user.Id));
    }

    [Fact]
    public async Task ExistsByCategory_IsTrueOnlyWhileThereAreActiveTransactions()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var used = await database.AddCategoryAsync(user.Id, "Used");
        var onlyDeleted = await database.AddCategoryAsync(user.Id, "OnlyDeleted");
        var empty = await database.AddCategoryAsync(user.Id, "Empty");
        await database.AddTransactionAsync(user.Id, used.Id, "2026-11-01");
        await database.AddTransactionAsync(user.Id, onlyDeleted.Id, "2026-11-01", deleted: true);

        await using var context = database.CreateContext();
        var repository = new TransactionRepository(context);

        Assert.True(await repository.ExistsByCategory(used.Id));
        Assert.False(await repository.ExistsByCategory(onlyDeleted.Id));
        Assert.False(await repository.ExistsByCategory(empty.Id));
    }
}
