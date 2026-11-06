using GastosBackend.IntegrationTests.Support;

namespace GastosBackend.IntegrationTests.Migrations;

/// <summary>
/// The B1 migration on data created before it existed. Rows are inserted with the OLD schema, then the database is migrated forward.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AddExchangeRateAndCreatedDateMigrationTests
{
    private const string UserWithRealUpdate = "11111111-0000-0000-0000-000000000001";
    private const string UserNeverUpdated = "11111111-0000-0000-0000-000000000002";
    private const string Category = "22222222-0000-0000-0000-000000000001";
    private const string TransactionWithRealUpdate = "33333333-0000-0000-0000-000000000001";
    private const string TransactionNeverUpdated = "33333333-0000-0000-0000-000000000002";

    private const string RealUpdate = "2026-03-01 10:00:00+00";
    private const string NeverUpdated = "0001-01-01 00:00:00+00"; // what BaseRepository.Add used to leave in UpdateDate

    private readonly PostgresFixture _postgres;

    public AddExchangeRateAndCreatedDateMigrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<(TestDatabase Database, DateTime Before, DateTime After)> MigrateDataCreatedBeforeTheMigration()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync(MigrationIds.Init);

        await database.ExecuteAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "Password", "IsDeleted", "UpdateDate", "DeleteDate") VALUES
              ('{UserWithRealUpdate}', 'a@x.com', 'hash', false, '{RealUpdate}', '{NeverUpdated}'),
              ('{UserNeverUpdated}',   'b@x.com', 'hash', false, '{NeverUpdated}', '{NeverUpdated}');

            INSERT INTO "Categories" ("Id", "Name", "Type", "UserId", "IsDeleted", "UpdateDate", "DeleteDate") VALUES
              ('{Category}', 'Food', 1, '{UserWithRealUpdate}', false, '{NeverUpdated}', '{NeverUpdated}');

            INSERT INTO "Transactions" ("Id", "Amount", "Currency", "Date", "Note", "CategoryId", "UserId", "IsDeleted", "UpdateDate", "DeleteDate") VALUES
              ('{TransactionWithRealUpdate}', 10, 2, '2026-03-01', NULL, '{Category}', '{UserWithRealUpdate}', false, '{RealUpdate}', '{NeverUpdated}'),
              ('{TransactionNeverUpdated}',   20, 1, '2026-03-02', NULL, '{Category}', '{UserWithRealUpdate}', false, '{NeverUpdated}', '{NeverUpdated}');
            """);

        var before = await database.ScalarAsync<DateTime>("SELECT now()");
        await database.MigrateAsync();
        var after = await database.ScalarAsync<DateTime>("SELECT clock_timestamp()");

        return (database, before, after);
    }

    [Fact]
    public async Task CreatedDate_TakesTheUpdateDate_WhenTheRowWasEverUpdated()
    {
        var (database, _, _) = await MigrateDataCreatedBeforeTheMigration();

        var user = await database.ScalarAsync<DateTime>($"""SELECT "CreatedDate" FROM "Users" WHERE "Id" = '{UserWithRealUpdate}'""");
        var transaction = await database.ScalarAsync<DateTime>(
            $"""SELECT "CreatedDate" FROM "Transactions" WHERE "Id" = '{TransactionWithRealUpdate}'""");

        Assert.Equal(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), user);
        Assert.Equal(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), transaction);
    }

    [Fact]
    public async Task CreatedDate_IsTheMigrationTime_WhenTheRowWasNeverUpdated()
    {
        var (database, before, after) = await MigrateDataCreatedBeforeTheMigration();

        // Most rows are like this: Add never set UpdateDate. They must not stay at year 0001.
        var user = await database.ScalarAsync<DateTime>($"""SELECT "CreatedDate" FROM "Users" WHERE "Id" = '{UserNeverUpdated}'""");
        var category = await database.ScalarAsync<DateTime>($"""SELECT "CreatedDate" FROM "Categories" WHERE "Id" = '{Category}'""");
        var transaction = await database.ScalarAsync<DateTime>(
            $"""SELECT "CreatedDate" FROM "Transactions" WHERE "Id" = '{TransactionNeverUpdated}'""");

        foreach (var createdDate in new[] { user, category, transaction })
        {
            Assert.InRange(createdDate, before, after);
        }
    }

    [Fact]
    public async Task ExistingTransactions_HaveNoExchangeRateYet_AndKeepTheirData()
    {
        var (database, _, _) = await MigrateDataCreatedBeforeTheMigration();

        var rows = await database.QueryAsync(
            """SELECT "Amount", "Currency", "ExchangeRate" FROM "Transactions" ORDER BY "Amount" """);

        Assert.Equal(2, rows.Count);
        Assert.Equal((10m, 2), ((decimal)rows[0][0], (int)rows[0][1]));
        Assert.Equal((20m, 1), ((decimal)rows[1][0], (int)rows[1][1]));
        Assert.All(rows, row => Assert.Equal(DBNull.Value, row[2]));
    }
}
