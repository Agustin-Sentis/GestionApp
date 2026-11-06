using GastosBackend.IntegrationTests.Support;

namespace GastosBackend.IntegrationTests.Migrations;

/// <summary>
/// The migration that adds the blue and MEP rates and the user's dollar, on data created before it existed.
/// Rows are inserted with the OLD schema, then the database is migrated forward. The point is that nothing already there is lost
/// or changed: the migration only adds.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AddDollarTypesMigrationTests
{
    private const string User = "11111111-0000-0000-0000-000000000001";
    private const string Category = "22222222-0000-0000-0000-000000000001";
    private const string UsdTransaction = "33333333-0000-0000-0000-000000000001";
    private const string ArsTransaction = "33333333-0000-0000-0000-000000000002";
    private const string Created = "2026-03-01 10:00:00+00";
    private const string NoDate = "0001-01-01 00:00:00+00";

    private readonly PostgresFixture _postgres;

    public AddDollarTypesMigrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<TestDatabase> MigrateDataCreatedBeforeTheMigration()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync(MigrationIds.CaseInsensitiveCategoryNames);

        await database.ExecuteAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "Password", "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
              VALUES ('{User}', 'a@x.com', 'hash', false, '{Created}', '{NoDate}', '{NoDate}');

            INSERT INTO "Categories" ("Id", "Name", "Type", "UserId", "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
              VALUES ('{Category}', 'Food', 1, '{User}', false, '{Created}', '{NoDate}', '{NoDate}');

            INSERT INTO "Transactions"
              ("Id", "Amount", "Currency", "ExchangeRate", "Date", "Note", "CategoryId", "UserId", "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
              VALUES
              ('{UsdTransaction}', 10, 2, 1570.1234, '2026-03-01', 'dollars', '{Category}', '{User}', false, '{Created}', '{NoDate}', '{NoDate}'),
              ('{ArsTransaction}',  20, 1, NULL,      '2026-03-02', NULL,      '{Category}', '{User}', false, '{Created}', '{NoDate}', '{NoDate}');
            """);

        await database.MigrateAsync();
        return database;
    }

    [Fact]
    public async Task ExistingUsers_GetTheOfficialDollar()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        var dollarType = await database.ScalarAsync<int>($"""SELECT "DollarType" FROM "Users" WHERE "Id" = '{User}'""");

        Assert.Equal(0, dollarType); // 0 is Official: nobody's totals change just because the migration ran
    }

    [Fact]
    public async Task ExistingTransactions_KeepTheirOfficialRate_AndTheirData()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        var rows = await database.QueryAsync(
            """SELECT "Amount", "Currency", "ExchangeRate", "Note" FROM "Transactions" ORDER BY "Amount" """);

        Assert.Equal(2, rows.Count);
        Assert.Equal((10m, 2, 1570.1234m, "dollars"), ((decimal)rows[0][0], (int)rows[0][1], (decimal)rows[0][2], (string)rows[0][3]));
        Assert.Equal((20m, 1), ((decimal)rows[1][0], (int)rows[1][1]));
        Assert.Equal(DBNull.Value, rows[1][2]);
    }

    [Fact]
    public async Task ExistingTransactions_HaveNoBlueOrMepRate_BecauseNoneWasStoredThen()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        var rows = await database.QueryAsync("""SELECT "BlueExchangeRate", "MepExchangeRate" FROM "Transactions" """);

        Assert.Equal(2, rows.Count);
        // Not filled in with today's rate: that would pass for the rate of the day each one was recorded
        Assert.All(rows, row => Assert.All(row, value => Assert.Equal(DBNull.Value, value)));
    }

    [Fact]
    public async Task NewTransactions_CanStoreAllThreeRates_WithFourDecimals()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        await database.ExecuteAsync(
            $"""
            INSERT INTO "Transactions"
              ("Id", "Amount", "Currency", "ExchangeRate", "BlueExchangeRate", "MepExchangeRate", "Date", "CategoryId", "UserId",
               "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
              VALUES ('33333333-0000-0000-0000-000000000009', 5, 2, 1570.1234, 1555.5555, 1582.2, '2026-04-01',
                      '{Category}', '{User}', false, '{Created}', '{NoDate}', '{NoDate}');
            """);

        var row = (await database.QueryAsync(
            """SELECT "ExchangeRate", "BlueExchangeRate", "MepExchangeRate" FROM "Transactions" WHERE "Amount" = 5""")).Single();

        Assert.Equal((1570.1234m, 1555.5555m, 1582.2m), ((decimal)row[0], (decimal)row[1], (decimal)row[2]));
    }
}
