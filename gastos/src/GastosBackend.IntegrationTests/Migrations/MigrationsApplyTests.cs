using GastosBackend.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.IntegrationTests.Migrations;

[Collection(PostgresCollection.Name)]
public class MigrationsApplyTests
{
    private readonly PostgresFixture _postgres;

    public MigrationsApplyTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task AllMigrations_ApplyOnAnEmptyDatabase()
    {
        var database = await _postgres.CreateDatabaseAsync();

        await database.MigrateAsync();

        await using var context = database.CreateContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(context.Database.GetMigrations().Count(), (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(0, await context.Users.CountAsync());
    }
}
