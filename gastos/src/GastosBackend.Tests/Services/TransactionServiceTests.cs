using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using Moq;

namespace GastosBackend.Tests.Services;

public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IDolarApiService> _dolarApiServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly TransactionService _transactionService;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Category _category;

    /// <summary>The user of the tests. Change its DollarType to test the other dollars.</summary>
    private readonly User _user;

    public TransactionServiceTests()
    {
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _dolarApiServiceMock = new Mock<IDolarApiService>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _transactionService = new TransactionService(
            _transactionRepositoryMock.Object, _categoryRepositoryMock.Object, _dolarApiServiceMock.Object,
            _userRepositoryMock.Object);

        _category = new Category { Id = Guid.NewGuid(), Name = "Comida", Type = TransactionType.Expense, UserId = _userId };
        _categoryRepositoryMock.Setup(r => r.Get(_category.Id)).ReturnsAsync(_category);

        _user = new User { Id = _userId, DollarType = DollarType.Official };
        _userRepositoryMock.Setup(r => r.Get(_userId)).ReturnsAsync(_user);
    }

    private TransactionRequest ValidRequest() => new TransactionRequest
    {
        Amount = 15000,
        Currency = Currency.ARS,
        Date = new DateOnly(2026, 11, 3),
        Note = "Supermercado",
        CategoryId = _category.Id
    };

    [Fact]
    public async Task Create_Succeeds_WhenCategoryBelongsToUser()
    {
        var result = await _transactionService.Create(ValidRequest(), _userId);

        Assert.Equal(15000, result.Amount);
        Assert.Equal("Comida", result.CategoryName);
        Assert.Equal(TransactionType.Expense, result.Type);
        _transactionRepositoryMock.Verify(r => r.Add(It.IsAny<Transaction>()), Times.Once);
    }

    [Fact]
    public async Task Create_Throws_WhenAmountIsNotPositive()
    {
        var request = ValidRequest();
        request.Amount = -10;

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.Create(request, _userId));
        _transactionRepositoryMock.Verify(r => r.Add(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task Create_Throws_WhenCurrencyIsNotSupported()
    {
        var request = ValidRequest();
        request.Currency = (Currency)99;

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.Create(request, _userId));
    }

    [Fact]
    public async Task Create_Throws_WhenCategoryBelongsToAnotherUser()
    {
        var request = ValidRequest();

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.Create(request, Guid.NewGuid()));
        _transactionRepositoryMock.Verify(r => r.Add(It.IsAny<Transaction>()), Times.Never);
    }

    private void SetupPage(int total = 0, params Transaction[] items)
    {
        _transactionRepositoryMock
            .Setup(r => r.GetPageByUser(
                It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<Guid?>(),
                It.IsAny<TransactionType?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PagedResult<Transaction>(items.ToList(), total));
    }

    [Theory]
    [InlineData(2026, 11, "2026-11-01", "2026-11-30")]
    [InlineData(2026, 12, "2026-12-01", "2026-12-31")]
    [InlineData(2026, 2, "2026-02-01", "2026-02-28")]
    [InlineData(2028, 2, "2028-02-01", "2028-02-29")]
    public async Task GetPage_TurnsAMonthIntoAnInclusiveDateRange(int year, int month, string from, string to)
    {
        SetupPage();

        await _transactionService.GetPage(
            new TransactionPageRequest { Year = year, Month = month }, _userId);

        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(_userId, DateOnly.Parse(from), DateOnly.Parse(to), null, null, null, 0, 20), Times.Once);
    }

    [Fact]
    public async Task GetPage_PassesTheRangeCategoryAndTypeToTheRepository()
    {
        SetupPage();
        var categoryId = Guid.NewGuid();
        var request = new TransactionPageRequest
        {
            From = new DateOnly(2026, 10, 15),
            To = new DateOnly(2026, 11, 3),
            CategoryId = categoryId,
            Type = TransactionType.Expense
        };

        await _transactionService.GetPage(request, _userId);

        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(
                _userId, new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 3), categoryId, TransactionType.Expense, null, 0, 20),
            Times.Once);
    }

    [Fact]
    public async Task GetPage_ComputesTheOffsetAndThePageCount()
    {
        SetupPage(total: 25, new Transaction { Id = Guid.NewGuid(), Category = _category });

        var result = await _transactionService.GetPage(
            new TransactionPageRequest { Page = 3, PageSize = 10 }, _userId);

        Assert.Equal(25, result.Total);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(3, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Single(result.Items);
        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(_userId, null, null, null, null, null, 20, 10), Times.Once);
    }

    [Fact]
    public async Task GetPage_ReturnsAnEmptyPage_WhenThereAreNoResults()
    {
        SetupPage();

        var result = await _transactionService.GetPage(new TransactionPageRequest(), _userId);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPage_DoesNotOverflow_WhenThePageNumberIsHuge()
    {
        SetupPage();

        await _transactionService.GetPage(
            new TransactionPageRequest { Page = int.MaxValue, PageSize = 100 }, _userId);

        // The offset is capped instead of wrapping around to a negative number
        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(_userId, null, null, null, null, null, int.MaxValue, 100), Times.Once);
    }

    [Fact]
    public async Task GetPage_Throws_AndDoesNotQuery_WhenTheRequestIsInvalid()
    {
        var request = new TransactionPageRequest { Year = 2026 };

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.GetPage(request, _userId));
        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(
                It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<Guid?>(),
                It.IsAny<TransactionType?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Theory]
    [InlineData("  almuerzo ", "almuerzo")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task GetPage_PassesTheSearchWithoutTheSurroundingSpaces_AndBlankMeansNoSearch(string? text, string? expected)
    {
        SetupPage();

        await _transactionService.GetPage(new TransactionPageRequest { Search = text }, _userId);

        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(_userId, null, null, null, null, expected, 0, 20), Times.Once);
    }

    [Fact]
    public async Task GetPage_Throws_WhenTheSearchIsTooLong()
    {
        var request = new TransactionPageRequest { Search = new string('a', TransactionPageRequest.MaxSearchLength + 1) };

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.GetPage(request, _userId));
    }

    [Fact]
    public async Task GetForExport_AsksForEveryRowFromTheStart_IgnoringThePage()
    {
        SetupPage(total: 1, new Transaction { Id = Guid.NewGuid(), Category = _category });
        var categoryId = Guid.NewGuid();

        var result = await _transactionService.GetForExport(
            new TransactionPageRequest
            {
                Year = 2026, Month = 11, CategoryId = categoryId, Type = TransactionType.Expense, Search = " cena ",
                Page = 7, PageSize = 5 // meant for the list: an export does not use them
            },
            _userId);

        Assert.Single(result);
        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(
                _userId, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), categoryId, TransactionType.Expense,
                "cena", 0, TransactionService.MaxExportRows),
            Times.Once);
    }

    [Fact]
    public async Task GetForExport_Throws_WhenMoreRowsMatchThanTheLimit()
    {
        SetupPage(total: TransactionService.MaxExportRows + 1);

        await Assert.ThrowsAsync<ValidationException>(
            () => _transactionService.GetForExport(new TransactionPageRequest(), _userId));
    }

    [Fact]
    public async Task GetForExport_Allows_ExactlyTheLimit()
    {
        SetupPage(total: TransactionService.MaxExportRows);

        var result = await _transactionService.GetForExport(new TransactionPageRequest(), _userId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetForExport_Throws_AndDoesNotQuery_WhenTheFiltersAreInvalid()
    {
        var request = new TransactionPageRequest { Year = 2026 };

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.GetForExport(request, _userId));
        _transactionRepositoryMock.Verify(
            r => r.GetPageByUser(
                It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<Guid?>(),
                It.IsAny<TransactionType?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_ReturnsTheTransactionWithItsCategory()
    {
        var id = Guid.NewGuid();
        _transactionRepositoryMock.Setup(r => r.GetByIdWithCategory(id, _userId))
            .ReturnsAsync(new Transaction
            {
                Id = id, UserId = _userId, Amount = 50, Currency = Currency.ARS, CategoryId = _category.Id, Category = _category
            });

        var result = await _transactionService.GetById(id, _userId);

        Assert.Equal(id, result.Id);
        Assert.Equal("Comida", result.CategoryName);
        Assert.Equal(TransactionType.Expense, result.Type);
    }

    [Fact]
    public async Task GetById_Throws_WhenTheTransactionDoesNotExistOrBelongsToAnotherUser()
    {
        // The repository filters by user, so another user's transaction is simply "not found"
        _transactionRepositoryMock.Setup(r => r.GetByIdWithCategory(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Transaction?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _transactionService.GetById(Guid.NewGuid(), _userId));
    }

    [Fact]
    public async Task Update_Throws_WhenTransactionBelongsToAnotherUser()
    {
        var transactionId = Guid.NewGuid();
        _transactionRepositoryMock.Setup(r => r.Get(transactionId))
            .ReturnsAsync(new Transaction { Id = transactionId, UserId = Guid.NewGuid() });

        await Assert.ThrowsAsync<NotFoundException>(
            () => _transactionService.Update(ValidRequest(), transactionId, _userId));
        _transactionRepositoryMock.Verify(r => r.Update(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task Update_AppliesChanges_WhenTransactionBelongsToUser()
    {
        var transactionId = Guid.NewGuid();
        var existing = new Transaction { Id = transactionId, UserId = _userId, Amount = 1, Currency = Currency.ARS };
        _transactionRepositoryMock.Setup(r => r.Get(transactionId)).ReturnsAsync(existing);

        await _transactionService.Update(ValidRequest(), transactionId, _userId);

        Assert.Equal(15000, existing.Amount);
        _transactionRepositoryMock.Verify(r => r.Update(existing), Times.Once);
    }

    [Fact]
    public async Task Create_StoresTheThreeCurrentRates_WhenCurrencyIsUsd()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1200m, 1100m, 1300m));
        var request = ValidRequest();
        request.Currency = Currency.USD;
        request.Amount = 10;

        var result = await _transactionService.Create(request, _userId);

        // The response speaks in the user's dollar (official by default); every rate is stored
        Assert.Equal(1200m, result.ExchangeRate);
        Assert.Equal(12000m, result.ArsAmount);
        Assert.Equal(DollarType.Official, result.DollarType);
        _transactionRepositoryMock.Verify(
            r => r.Add(It.Is<Transaction>(t => t.ExchangeRate == 1200m && t.BlueExchangeRate == 1100m && t.MepExchangeRate == 1300m)),
            Times.Once);
    }

    [Theory]
    [InlineData(DollarType.Official, 1200, 12000)]
    [InlineData(DollarType.Blue, 1100, 11000)]
    [InlineData(DollarType.Mep, 1300, 13000)]
    public async Task Create_ExpressesTheResponseWithTheDollarTheUserChose(DollarType chosen, double rate, double inPesos)
    {
        _user.DollarType = chosen;
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1200m, 1100m, 1300m));
        var request = ValidRequest();
        request.Currency = Currency.USD;
        request.Amount = 10;

        var result = await _transactionService.Create(request, _userId);

        Assert.Equal((decimal)rate, result.ExchangeRate);
        Assert.Equal((decimal)inPesos, result.ArsAmount);
        Assert.Equal(chosen, result.DollarType);
    }

    [Fact]
    public async Task Create_StoresWhatWasObtained_WhenOnlySomeRatesAreAvailable()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1200m, null, 1300m));
        var request = ValidRequest();
        request.Currency = Currency.USD;

        await _transactionService.Create(request, _userId);

        _transactionRepositoryMock.Verify(
            r => r.Add(It.Is<Transaction>(t => t.ExchangeRate == 1200m && t.BlueExchangeRate == null && t.MepExchangeRate == 1300m)),
            Times.Once);
    }

    [Fact]
    public async Task Create_SavesWithoutRate_WhenTheRateProviderFails()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ThrowsAsync(new ExternalServiceException("down"));
        var request = ValidRequest();
        request.Currency = Currency.USD;

        var result = await _transactionService.Create(request, _userId);

        Assert.Null(result.ExchangeRate);
        Assert.Null(result.ArsAmount);
        _transactionRepositoryMock.Verify(r => r.Add(It.IsAny<Transaction>()), Times.Once);
    }

    [Fact]
    public async Task Create_DoesNotAskForTheRate_WhenCurrencyIsArs()
    {
        var result = await _transactionService.Create(ValidRequest(), _userId);

        Assert.Null(result.ExchangeRate);
        Assert.Equal(15000m, result.ArsAmount);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRates(), Times.Never);
    }

    private Transaction ExistingUsdTransaction(decimal? rate)
    {
        var request = ValidRequest();
        var existing = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Amount = request.Amount,
            Currency = Currency.USD,
            Date = request.Date,
            ExchangeRate = rate,
            CategoryId = _category.Id
        };
        _transactionRepositoryMock.Setup(r => r.Get(existing.Id)).ReturnsAsync(existing);
        return existing;
    }

    [Fact]
    public async Task Update_KeepsTheStoredRate_WhenAmountCurrencyAndDateDoNotChange()
    {
        var existing = ExistingUsdTransaction(rate: 900m);
        var request = ValidRequest();
        request.Currency = Currency.USD;
        request.Note = "only the note changes";

        await _transactionService.Update(request, existing.Id, _userId);

        Assert.Equal(900m, existing.ExchangeRate);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRates(), Times.Never);
    }

    [Fact]
    public async Task Update_AsksForANewRate_WhenTheAmountChanges()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1500m, 1400m, 1600m));
        var existing = ExistingUsdTransaction(rate: 900m);
        var request = ValidRequest();
        request.Currency = Currency.USD;
        request.Amount += 1;

        await _transactionService.Update(request, existing.Id, _userId);

        Assert.Equal(1500m, existing.ExchangeRate);
        Assert.Equal(1400m, existing.BlueExchangeRate); // all three are refreshed together
        Assert.Equal(1600m, existing.MepExchangeRate);
    }

    [Fact]
    public async Task Update_LeavesTheMissingBlueAndMepRatesEmpty_OfATransactionRecordedBeforeTheyWereStored()
    {
        // Only the official rate was stored then. Changing the note must not make up a blue or MEP rate for that day.
        var existing = ExistingUsdTransaction(rate: 900m);
        var request = ValidRequest();
        request.Currency = Currency.USD;
        request.Note = "only the note changes";

        await _transactionService.Update(request, existing.Id, _userId);

        Assert.Equal(900m, existing.ExchangeRate);
        Assert.Null(existing.BlueExchangeRate);
        Assert.Null(existing.MepExchangeRate);
    }

    [Fact]
    public async Task Update_RetriesTheRate_WhenTheStoredRateIsMissing()
    {
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1500m, 1400m, 1600m));
        var existing = ExistingUsdTransaction(rate: null);
        var request = ValidRequest();
        request.Currency = Currency.USD;

        await _transactionService.Update(request, existing.Id, _userId);

        Assert.Equal(1500m, existing.ExchangeRate);
    }

    [Fact]
    public async Task Update_ClearsAllTheRates_WhenCurrencyChangesToArs()
    {
        var existing = ExistingUsdTransaction(rate: 900m);
        existing.BlueExchangeRate = 800m;
        existing.MepExchangeRate = 950m;

        await _transactionService.Update(ValidRequest(), existing.Id, _userId);

        Assert.Null(existing.ExchangeRate);
        Assert.Null(existing.BlueExchangeRate);
        Assert.Null(existing.MepExchangeRate);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRates(), Times.Never);
    }

    // ---------- Each transaction keeps the dollar it was saved with ----------

    private static Transaction UsdTransaction(Category category, DollarType dollar) => new Transaction
    {
        Id = Guid.NewGuid(),
        Amount = 10,
        Currency = Currency.USD,
        ExchangeRate = 1200m,
        BlueExchangeRate = 1100m,
        MepExchangeRate = 1300m,
        DollarType = dollar,
        Date = new DateOnly(2026, 11, 3),
        CategoryId = category.Id,
        Category = category
    };

    [Theory]
    [InlineData(DollarType.Official, 1200, 12000)]
    [InlineData(DollarType.Blue, 1100, 11000)]
    [InlineData(DollarType.Mep, 1300, 13000)]
    public async Task GetPage_ExpressesEachUsdTransactionWithTheDollarItWasSavedWith(DollarType saved, double rate, double inPesos)
    {
        SetupPage(total: 1, UsdTransaction(_category, saved));

        var result = await _transactionService.GetPage(new TransactionPageRequest(), _userId);

        var item = Assert.Single(result.Items);
        Assert.Equal((decimal)rate, item.ExchangeRate);
        Assert.Equal((decimal)inPesos, item.ArsAmount);
        Assert.Equal(saved, item.DollarType);
    }

    [Theory]
    [InlineData(DollarType.Official)]
    [InlineData(DollarType.Blue)]
    [InlineData(DollarType.Mep)]
    public async Task GetPage_DoesNotChangeATransaction_WhenTheUserChoosesAnotherDollarLater(DollarType chosenNow)
    {
        // Saved with blue; whatever the user has chosen since, it still says blue and keeps the blue rate
        _user.DollarType = chosenNow;
        SetupPage(total: 1, UsdTransaction(_category, DollarType.Blue));

        var result = await _transactionService.GetPage(new TransactionPageRequest(), _userId);

        var item = Assert.Single(result.Items);
        Assert.Equal(DollarType.Blue, item.DollarType);
        Assert.Equal(1100m, item.ExchangeRate);
        Assert.Equal(11000m, item.ArsAmount);
    }

    [Fact]
    public async Task GetPage_MixesDollarsInOneList_EachTransactionWithItsOwn()
    {
        SetupPage(total: 2, UsdTransaction(_category, DollarType.Official), UsdTransaction(_category, DollarType.Mep));

        var result = await _transactionService.GetPage(new TransactionPageRequest(), _userId);

        Assert.Equal(new[] { DollarType.Official, DollarType.Mep }, result.Items.Select(i => i.DollarType).ToArray());
        Assert.Equal(new decimal?[] { 12000m, 13000m }, result.Items.Select(i => i.ArsAmount).ToArray());
    }

    [Fact]
    public async Task GetPage_ReportsNoRate_ForATransactionWhoseOwnDollarHasNoRate()
    {
        var old = UsdTransaction(_category, DollarType.Blue);
        old.BlueExchangeRate = null; // the blue rate could not be obtained when it was saved
        SetupPage(total: 1, old);

        var result = await _transactionService.GetPage(new TransactionPageRequest(), _userId);

        var item = Assert.Single(result.Items);
        Assert.Null(item.ExchangeRate);
        Assert.Null(item.ArsAmount); // not made up with another dollar's rate: the report resolves it and says so
        Assert.Equal(DollarType.Blue, item.DollarType); // and the list can say which rate is missing
    }

    [Fact]
    public async Task GetPage_DoesNotChangeAnArsTransaction()
    {
        SetupPage(total: 1, new Transaction
        {
            Id = Guid.NewGuid(), Amount = 500, Currency = Currency.ARS, CategoryId = _category.Id, Category = _category
        });

        var result = await _transactionService.GetPage(new TransactionPageRequest(), _userId);

        Assert.Equal(500m, Assert.Single(result.Items).ArsAmount);
    }

    [Fact]
    public async Task GetPage_DoesNotEvenAskForTheUser_ThatIsNotNeededToReadATransaction()
    {
        SetupPage(total: 1, UsdTransaction(_category, DollarType.Mep));

        await _transactionService.GetPage(new TransactionPageRequest(), _userId);

        _userRepositoryMock.Verify(r => r.Get(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetById_ExpressesTheTransactionWithTheDollarItWasSavedWith()
    {
        _user.DollarType = DollarType.Official; // the user has gone back to the official one since
        var transaction = UsdTransaction(_category, DollarType.Blue);
        _transactionRepositoryMock.Setup(r => r.GetByIdWithCategory(transaction.Id, _userId)).ReturnsAsync(transaction);

        var result = await _transactionService.GetById(transaction.Id, _userId);

        Assert.Equal(11000m, result.ArsAmount);
        Assert.Equal(DollarType.Blue, result.DollarType);
    }

    [Fact]
    public async Task GetForExport_ExpressesEachRowWithTheDollarItWasSavedWith()
    {
        SetupPage(total: 2, UsdTransaction(_category, DollarType.Mep), UsdTransaction(_category, DollarType.Official));

        var result = await _transactionService.GetForExport(new TransactionPageRequest(), _userId);

        Assert.Equal(new decimal?[] { 13000m, 12000m }, result.Select(r => r.ArsAmount).ToArray());
        Assert.Equal(new[] { DollarType.Mep, DollarType.Official }, result.Select(r => r.DollarType).ToArray());
    }

    [Theory]
    [InlineData(DollarType.Official)]
    [InlineData(DollarType.Blue)]
    [InlineData(DollarType.Mep)]
    public async Task Create_SavesAUsdTransactionWithTheDollarTheUserHasChosenNow(DollarType chosen)
    {
        _user.DollarType = chosen;
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1200m, 1100m, 1300m));
        var request = ValidRequest();
        request.Currency = Currency.USD;

        await _transactionService.Create(request, _userId);

        _transactionRepositoryMock.Verify(r => r.Add(It.Is<Transaction>(t => t.DollarType == chosen)), Times.Once);
    }

    [Fact]
    public async Task Create_SavesATransactionInPesosAsOfficial_WhateverTheUserHasChosen()
    {
        _user.DollarType = DollarType.Mep;

        await _transactionService.Create(ValidRequest(), _userId);

        _transactionRepositoryMock.Verify(r => r.Add(It.Is<Transaction>(t => t.DollarType == DollarType.Official)), Times.Once);
    }

    [Fact]
    public async Task Create_SavesWithTheOfficialDollar_WhenTheUserCannotBeFound()
    {
        _userRepositoryMock.Setup(r => r.Get(_userId)).ReturnsAsync((User?)null);
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1200m, null, null));
        var request = ValidRequest();
        request.Currency = Currency.USD;

        await _transactionService.Create(request, _userId);

        _transactionRepositoryMock.Verify(r => r.Add(It.Is<Transaction>(t => t.DollarType == DollarType.Official)), Times.Once);
    }

    [Fact]
    public async Task Update_KeepsTheDollarTheTransactionWasSavedWith_WhenOnlyTheNoteChanges()
    {
        _user.DollarType = DollarType.Mep; // chosen after the transaction was saved
        var existing = ExistingUsdTransaction(rate: 900m);
        existing.DollarType = DollarType.Blue;
        existing.BlueExchangeRate = 880m;
        var request = ValidRequest();
        request.Currency = Currency.USD;
        request.Note = "only the note changes";

        await _transactionService.Update(request, existing.Id, _userId);

        Assert.Equal(DollarType.Blue, existing.DollarType);
        Assert.Equal(880m, existing.BlueExchangeRate);
        _dolarApiServiceMock.Verify(s => s.GetUsdSellRates(), Times.Never);
    }

    [Fact]
    public async Task Update_TakesTheRatesAgainWithTheDollarChosenNow_WhenTheAmountChanges()
    {
        _user.DollarType = DollarType.Mep;
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1500m, 1400m, 1600m));
        var existing = ExistingUsdTransaction(rate: 900m); // was official
        var request = ValidRequest();
        request.Currency = Currency.USD;
        request.Amount += 1;

        await _transactionService.Update(request, existing.Id, _userId);

        Assert.Equal(DollarType.Mep, existing.DollarType); // saving again is saving with what is chosen now
        Assert.Equal(1600m, existing.MepExchangeRate);
    }

    [Fact]
    public async Task Update_MovesATransactionFromPesosToUsdWithTheDollarChosenNow()
    {
        _user.DollarType = DollarType.Blue;
        _dolarApiServiceMock.Setup(s => s.GetUsdSellRates()).ReturnsAsync(new UsdRates(1500m, 1400m, 1600m));
        var existing = new Transaction
        {
            Id = Guid.NewGuid(), UserId = _userId, Amount = 15000, Currency = Currency.ARS,
            Date = new DateOnly(2026, 11, 3), CategoryId = _category.Id
        };
        _transactionRepositoryMock.Setup(r => r.Get(existing.Id)).ReturnsAsync(existing);
        var request = ValidRequest();
        request.Currency = Currency.USD;

        await _transactionService.Update(request, existing.Id, _userId);

        Assert.Equal(DollarType.Blue, existing.DollarType);
        Assert.Equal(1400m, existing.BlueExchangeRate);
    }

    [Fact]
    public async Task Update_ResetsTheDollarToOfficial_WhenATransactionBecomesPesos()
    {
        var existing = ExistingUsdTransaction(rate: 900m);
        existing.DollarType = DollarType.Blue;

        await _transactionService.Update(ValidRequest(), existing.Id, _userId); // ValidRequest is in pesos

        Assert.Equal(DollarType.Official, existing.DollarType);
    }

    [Fact]
    public async Task Delete_Throws_WhenTransactionDoesNotExist()
    {
        _transactionRepositoryMock.Setup(r => r.Get(It.IsAny<Guid>())).ReturnsAsync((Transaction?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _transactionService.Delete(Guid.NewGuid(), _userId));
    }

    [Fact]
    public async Task Delete_Succeeds_WhenTransactionBelongsToUser()
    {
        var transactionId = Guid.NewGuid();
        _transactionRepositoryMock.Setup(r => r.Get(transactionId))
            .ReturnsAsync(new Transaction { Id = transactionId, UserId = _userId });

        await _transactionService.Delete(transactionId, _userId);

        _transactionRepositoryMock.Verify(r => r.Delete(transactionId), Times.Once);
    }
}
