using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.IntegrationTests.Repositories;

[Collection(PostgresCollection.Name)]
public class BudgetAlertRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public BudgetAlertRepositoryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<(TestDatabase Database, User User, Category Category)> Setup()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();
        var user = await database.AddUserAsync();
        var category = await database.AddCategoryAsync(user.Id, "Comida");
        return (database, user, category);
    }

    private static BudgetAlert Alert(Guid userId, Guid categoryId, int month = 11, BudgetStatus level = BudgetStatus.Warning)
        => new BudgetAlert { Id = Guid.NewGuid(), UserId = userId, CategoryId = categoryId, Year = 2026, Month = month, Level = level };

    [Fact]
    public async Task TryAdd_ReturnsTrue_AndTheAlertExistsAfterwards()
    {
        var (database, user, category) = await Setup();

        await using (var context = database.CreateContext())
        {
            Assert.True(await new BudgetAlertRepository(context).TryAdd(Alert(user.Id, category.Id)));
        }

        await using var check = database.CreateContext();
        var repository = new BudgetAlertRepository(check);
        Assert.True(await repository.Exists(user.Id, category.Id, 2026, 11, BudgetStatus.Warning));
        Assert.False(await repository.Exists(user.Id, category.Id, 2026, 11, BudgetStatus.Exceeded));
        Assert.False(await repository.Exists(user.Id, category.Id, 2026, 10, BudgetStatus.Warning));
    }

    [Fact]
    public async Task TryAdd_ReturnsFalse_ForTheSameUserCategoryMonthAndLevel()
    {
        var (database, user, category) = await Setup();
        await using (var first = database.CreateContext())
        {
            await new BudgetAlertRepository(first).TryAdd(Alert(user.Id, category.Id));
        }

        await using var second = database.CreateContext();
        Assert.False(await new BudgetAlertRepository(second).TryAdd(Alert(user.Id, category.Id)));

        await using var check = database.CreateContext();
        Assert.Equal(1, await check.BudgetAlerts.CountAsync());
    }

    [Fact]
    public async Task TryAdd_Works_ForAnotherLevelAnotherMonthAndAnotherCategory()
    {
        var (database, user, category) = await Setup();
        var other = await database.AddCategoryAsync(user.Id, "Ocio");

        await using var context = database.CreateContext();
        var repository = new BudgetAlertRepository(context);

        Assert.True(await repository.TryAdd(Alert(user.Id, category.Id)));
        Assert.True(await repository.TryAdd(Alert(user.Id, category.Id, level: BudgetStatus.Exceeded)));
        Assert.True(await repository.TryAdd(Alert(user.Id, category.Id, month: 12)));
        Assert.True(await repository.TryAdd(Alert(user.Id, other.Id)));
    }

    [Fact]
    public async Task TryAdd_Works_AfterTheAlertWasSoftDeleted()
    {
        var (database, user, category) = await Setup();
        var alert = Alert(user.Id, category.Id);
        await using (var first = database.CreateContext())
        {
            await new BudgetAlertRepository(first).TryAdd(alert);
        }

        await using (var delete = database.CreateContext())
        {
            await new BudgetAlertRepository(delete).Delete(alert.Id);
        }

        await using var again = database.CreateContext();
        var repository = new BudgetAlertRepository(again);
        Assert.False(await repository.Exists(user.Id, category.Id, 2026, 11, BudgetStatus.Warning));
        Assert.True(await repository.TryAdd(Alert(user.Id, category.Id)));
    }

    [Fact]
    public async Task TryAdd_AllowsOnlyOne_WhenSeveralRunAtTheSameTime()
    {
        var (database, user, category) = await Setup();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = database.CreateContext();
            return await new BudgetAlertRepository(context).TryAdd(Alert(user.Id, category.Id));
        }));

        Assert.Equal(1, results.Count(r => r));
        await using var check = database.CreateContext();
        Assert.Equal(1, await check.BudgetAlerts.CountAsync());
    }
}
