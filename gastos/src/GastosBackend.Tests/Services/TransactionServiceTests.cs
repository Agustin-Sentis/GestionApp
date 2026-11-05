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
    private readonly TransactionService _transactionService;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Category _category;

    public TransactionServiceTests()
    {
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _transactionService = new TransactionService(_transactionRepositoryMock.Object, _categoryRepositoryMock.Object);

        _category = new Category { Id = Guid.NewGuid(), Name = "Comida", Type = TransactionType.Expense, UserId = _userId };
        _categoryRepositoryMock.Setup(r => r.Get(_category.Id)).ReturnsAsync(_category);
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
        var result = await _transactionService.Create(ValidRequest(), _userId.ToString());

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

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.Create(request, _userId.ToString()));
        _transactionRepositoryMock.Verify(r => r.Add(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task Create_Throws_WhenCurrencyIsNotSupported()
    {
        var request = ValidRequest();
        request.Currency = (Currency)99;

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.Create(request, _userId.ToString()));
    }

    [Fact]
    public async Task Create_Throws_WhenCategoryBelongsToAnotherUser()
    {
        var request = ValidRequest();

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.Create(request, Guid.NewGuid().ToString()));
        _transactionRepositoryMock.Verify(r => r.Add(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task Create_Throws_WhenSubIsNotAGuid()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.Create(ValidRequest(), "abc"));
    }

    [Fact]
    public async Task GetAll_Throws_WhenOnlyYearIsProvided()
    {
        var filter = new TransactionFilterRequest { Year = 2026 };

        await Assert.ThrowsAsync<ValidationException>(() => _transactionService.GetAll(filter, _userId.ToString()));
    }

    [Fact]
    public async Task GetAll_FiltersByMonthRange()
    {
        _transactionRepositoryMock
            .Setup(r => r.GetAllByUser(_userId, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1), null, It.IsAny<int>()))
            .ReturnsAsync(new List<Transaction>());

        var result = await _transactionService.GetAll(
            new TransactionFilterRequest { Year = 2026, Month = 11 }, _userId.ToString());

        Assert.Empty(result);
        _transactionRepositoryMock.Verify(
            r => r.GetAllByUser(_userId, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1), null, It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_Throws_WhenTransactionBelongsToAnotherUser()
    {
        var transactionId = Guid.NewGuid();
        _transactionRepositoryMock.Setup(r => r.Get(transactionId))
            .ReturnsAsync(new Transaction { Id = transactionId, UserId = Guid.NewGuid() });

        await Assert.ThrowsAsync<NotFoundException>(
            () => _transactionService.Update(ValidRequest(), transactionId, _userId.ToString()));
        _transactionRepositoryMock.Verify(r => r.Update(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task Update_AppliesChanges_WhenTransactionBelongsToUser()
    {
        var transactionId = Guid.NewGuid();
        var existing = new Transaction { Id = transactionId, UserId = _userId, Amount = 1, Currency = Currency.ARS };
        _transactionRepositoryMock.Setup(r => r.Get(transactionId)).ReturnsAsync(existing);

        await _transactionService.Update(ValidRequest(), transactionId, _userId.ToString());

        Assert.Equal(15000, existing.Amount);
        _transactionRepositoryMock.Verify(r => r.Update(existing), Times.Once);
    }

    [Fact]
    public async Task Delete_Throws_WhenTransactionDoesNotExist()
    {
        _transactionRepositoryMock.Setup(r => r.Get(It.IsAny<Guid>())).ReturnsAsync((Transaction?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _transactionService.Delete(Guid.NewGuid(), _userId.ToString()));
    }

    [Fact]
    public async Task Delete_Succeeds_WhenTransactionBelongsToUser()
    {
        var transactionId = Guid.NewGuid();
        _transactionRepositoryMock.Setup(r => r.Get(transactionId))
            .ReturnsAsync(new Transaction { Id = transactionId, UserId = _userId });

        await _transactionService.Delete(transactionId, _userId.ToString());

        _transactionRepositoryMock.Verify(r => r.Delete(transactionId), Times.Once);
    }
}
