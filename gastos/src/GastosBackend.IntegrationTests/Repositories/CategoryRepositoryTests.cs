using GastosBackend.Application.Exceptions;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.IntegrationTests.Repositories;

[Collection(PostgresCollection.Name)]
public class CategoryRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public CategoryRepositoryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<TestDatabase> MigratedDatabase()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();
        return database;
    }

    private static Category NewCategory(Guid userId, string name)
        => new Category { Id = Guid.NewGuid(), UserId = userId, Name = name, Type = TransactionType.Expense };

    [Fact]
    public async Task Add_ThrowsDuplicateException_WhenTheNameOnlyDiffersInCase()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        await database.AddCategoryAsync(user.Id, "Comida");

        await using var context = database.CreateContext();
        await Assert.ThrowsAsync<DuplicateException>(
            () => new CategoryRepository(context).Add(NewCategory(user.Id, "COMIDA")));
    }

    [Fact]
    public async Task Add_Works_ForTheSameNameInAnotherUser()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var otherUser = await database.AddUserAsync();
        await database.AddCategoryAsync(user.Id, "Comida");

        await using var context = database.CreateContext();
        await new CategoryRepository(context).Add(NewCategory(otherUser.Id, "comida"));

        await using var check = database.CreateContext();
        Assert.Equal(2, await check.Categories.CountAsync());
    }

    [Fact]
    public async Task Add_Works_WhenTheSameNameBelongsToADeletedCategory()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        await database.AddCategoryAsync(user.Id, "Comida", deleted: true);

        await using var context = database.CreateContext();
        await new CategoryRepository(context).Add(NewCategory(user.Id, "Comida"));

        await using var check = database.CreateContext();
        Assert.Equal(2, await check.Categories.CountAsync());
    }

    [Theory]
    [InlineData("Comida", true)]
    [InlineData("comida", true)]
    [InlineData("COMIDA", true)]
    [InlineData("Comidas", false)]
    [InlineData("Otra", false)]
    public async Task ExistsByName_IgnoresCase_ButNotTheRestOfTheName(string name, bool expected)
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        await database.AddCategoryAsync(user.Id, "Comida");

        await using var context = database.CreateContext();

        Assert.Equal(expected, await new CategoryRepository(context).ExistsByName(user.Id, name));
    }

    [Fact]
    public async Task ExistsByName_IsFalse_ForAnotherUsersCategoryAndForDeletedOnes()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var otherUser = await database.AddUserAsync();
        await database.AddCategoryAsync(otherUser.Id, "Ajena");
        await database.AddCategoryAsync(user.Id, "Borrada", deleted: true);

        await using var context = database.CreateContext();
        var repository = new CategoryRepository(context);

        Assert.False(await repository.ExistsByName(user.Id, "Ajena"));
        Assert.False(await repository.ExistsByName(user.Id, "Borrada"));
    }

    [Fact]
    public async Task GetAllByUser_ReturnsOnlyTheUsersActiveCategories_OrderedByName()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var otherUser = await database.AddUserAsync();
        await database.AddCategoryAsync(user.Id, "Gamma");
        await database.AddCategoryAsync(user.Id, "Alpha");
        await database.AddCategoryAsync(user.Id, "Beta");
        await database.AddCategoryAsync(user.Id, "Deleted", deleted: true);
        await database.AddCategoryAsync(otherUser.Id, "Foreign");

        await using var context = database.CreateContext();
        var categories = await new CategoryRepository(context).GetAllByUser(user.Id);

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, categories.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task MonthlyBudget_IsNullByDefault_AndCanBeSetAndRemoved()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var category = await database.AddCategoryAsync(user.Id, "Comida");

        await using (var check = database.CreateContext())
        {
            Assert.Null((await new CategoryRepository(check).Get(category.Id))!.MonthlyBudget);
        }

        await using (var set = database.CreateContext())
        {
            var repository = new CategoryRepository(set);
            var loaded = (await repository.Get(category.Id))!;
            loaded.MonthlyBudget = 300000.5m;
            await repository.Update(loaded);
        }

        await using (var afterSet = database.CreateContext())
        {
            var repository = new CategoryRepository(afterSet);
            var loaded = (await repository.Get(category.Id))!;
            Assert.Equal(300000.5m, loaded.MonthlyBudget);

            loaded.MonthlyBudget = null;
            await repository.Update(loaded);
        }

        await using var afterRemove = database.CreateContext();
        Assert.Null((await new CategoryRepository(afterRemove).Get(category.Id))!.MonthlyBudget);
    }

    [Fact]
    public async Task Delete_IsSoft_AndFreesTheName()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync();
        var category = await database.AddCategoryAsync(user.Id, "Comida");

        await using (var context = database.CreateContext())
        {
            await new CategoryRepository(context).Delete(category.Id);
        }

        await using var next = database.CreateContext();
        var repository = new CategoryRepository(next);
        Assert.False(await repository.ExistsByName(user.Id, "Comida"));
        await repository.Add(NewCategory(user.Id, "Comida")); // the name can be used again
        Assert.Equal(2, await next.Categories.CountAsync()); // the deleted row is still in the table
    }
}
