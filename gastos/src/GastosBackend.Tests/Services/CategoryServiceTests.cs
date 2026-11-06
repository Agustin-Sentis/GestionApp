using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using Moq;

namespace GastosBackend.Tests.Services;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly CategoryService _categoryService;
    private readonly Guid _userId = Guid.NewGuid();

    public CategoryServiceTests()
    {
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _categoryService = new CategoryService(_categoryRepositoryMock.Object, _transactionRepositoryMock.Object);
    }

    [Fact]
    public async Task Create_Succeeds_AndTrimsTheName()
    {
        var request = new CategoryRequest { Name = "  Comida ", Type = TransactionType.Expense };

        var result = await _categoryService.Create(request, _userId);

        Assert.Equal("Comida", result.Name);
        _categoryRepositoryMock.Verify(
            r => r.Add(It.Is<Category>(c => c.UserId == _userId && c.Name == "Comida")), Times.Once);
    }

    [Fact]
    public async Task Create_Throws_WhenNameIsEmpty()
    {
        var request = new CategoryRequest { Name = "   ", Type = TransactionType.Expense };

        await Assert.ThrowsAsync<ValidationException>(() => _categoryService.Create(request, _userId));
        _categoryRepositoryMock.Verify(r => r.Add(It.IsAny<Category>()), Times.Never);
    }

    [Fact]
    public async Task Create_Throws_WhenNameAlreadyExists()
    {
        _categoryRepositoryMock.Setup(r => r.ExistsByName(_userId, "Comida")).ReturnsAsync(true);
        var request = new CategoryRequest { Name = "Comida", Type = TransactionType.Expense };

        await Assert.ThrowsAsync<ConflictException>(() => _categoryService.Create(request, _userId));
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenAnotherRequestCreatedTheSameNameFirst()
    {
        // The name looked free, but the unique index rejected the insert (two requests racing)
        _categoryRepositoryMock.Setup(r => r.ExistsByName(_userId, "Comida")).ReturnsAsync(false);
        _categoryRepositoryMock.Setup(r => r.Add(It.IsAny<Category>())).ThrowsAsync(new DuplicateException("duplicate"));
        var request = new CategoryRequest { Name = " Comida ", Type = TransactionType.Expense };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _categoryService.Create(request, _userId));

        Assert.Equal("Ya existe una categoría con el nombre 'Comida'.", ex.Message);
    }

    [Fact]
    public async Task Delete_Throws_WhenCategoryBelongsToAnotherUser()
    {
        var category = new Category { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Name = "Comida" };
        _categoryRepositoryMock.Setup(r => r.Get(category.Id)).ReturnsAsync(category);

        await Assert.ThrowsAsync<NotFoundException>(() => _categoryService.Delete(category.Id, _userId));
        _categoryRepositoryMock.Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Delete_Throws_WhenCategoryHasTransactions()
    {
        var category = new Category { Id = Guid.NewGuid(), UserId = _userId, Name = "Comida" };
        _categoryRepositoryMock.Setup(r => r.Get(category.Id)).ReturnsAsync(category);
        _transactionRepositoryMock.Setup(r => r.ExistsByCategory(category.Id)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => _categoryService.Delete(category.Id, _userId));
        _categoryRepositoryMock.Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Delete_Succeeds_WhenCategoryHasNoTransactions()
    {
        var category = new Category { Id = Guid.NewGuid(), UserId = _userId, Name = "Comida" };
        _categoryRepositoryMock.Setup(r => r.Get(category.Id)).ReturnsAsync(category);
        _transactionRepositoryMock.Setup(r => r.ExistsByCategory(category.Id)).ReturnsAsync(false);

        await _categoryService.Delete(category.Id, _userId);

        _categoryRepositoryMock.Verify(r => r.Delete(category.Id), Times.Once);
    }
}
