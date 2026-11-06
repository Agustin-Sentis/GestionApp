using GastosBackend.IntegrationTests.Support;

namespace GastosBackend.IntegrationTests.Migrations;

/// <summary>
/// The migration that gives each transaction its own dollar, on data created before it existed. The point is that every
/// existing transaction is Official, which is the rate it stored, so no total changes just because the migration ran.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AddTransactionDollarTypeMigrationTests
{
    private const string User = "11111111-0000-0000-0000-000000000001";
    private const string Category = "22222222-0000-0000-0000-000000000001";
    private const string Created = "2026-03-01 10:00:00+00";
    private const string NoDate = "0001-01-01 00:00:00+00";

    private readonly PostgresFixture _postgres;

    public AddTransactionDollarTypeMigrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<TestDatabase> MigrateDataCreatedBeforeTheMigration()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync(MigrationIds.AddDollarTypes);

        // The shape of the database after the previous migration: three rates per transaction, and a dollar on the user
        await database.ExecuteAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "Password", "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate", "DollarType")
              VALUES ('{User}', 'a@x.com', 'hash', false, '{Created}', '{NoDate}', '{NoDate}', 1);

            INSERT INTO "Categories" ("Id", "Name", "Type", "UserId", "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
              VALUES ('{Category}', 'Food', 1, '{User}', false, '{Created}', '{NoDate}', '{NoDate}');

            INSERT INTO "Transactions"
              ("Id", "Amount", "Currency", "ExchangeRate", "BlueExchangeRate", "MepExchangeRate", "Date", "CategoryId", "UserId",
               "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
              VALUES
              ('33333333-0000-0000-0000-000000000001', 10, 2, 1570.1234, NULL,      NULL,   '2026-03-01', '{Category}', '{User}', false, '{Created}', '{NoDate}', '{NoDate}'),
              ('33333333-0000-0000-0000-000000000002', 20, 2, 1500,      1400.5,    1600.25, '2026-03-02', '{Category}', '{User}', false, '{Created}', '{NoDate}', '{NoDate}'),
              ('33333333-0000-0000-0000-000000000003', 30, 1, NULL,      NULL,      NULL,   '2026-03-03', '{Category}', '{User}', false, '{Created}', '{NoDate}', '{NoDate}');
            """);

        await database.MigrateAsync();
        return database;
    }

    [Fact]
    public async Task EveryExistingTransaction_IsOfficial_BecauseThatIsTheRateItStored()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        var dollarTypes = await database.QueryAsync("""SELECT "DollarType" FROM "Transactions" """);

        Assert.Equal(3, dollarTypes.Count);
        Assert.All(dollarTypes, row => Assert.Equal(0, (int)row[0]));
    }

    [Fact]
    public async Task ExistingTransactions_KeepAllTheirRatesAndData()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        var rows = await database.QueryAsync(
            """SELECT "Amount", "ExchangeRate", "BlueExchangeRate", "MepExchangeRate" FROM "Transactions" ORDER BY "Amount" """);

        Assert.Equal(3, rows.Count);
        Assert.Equal((1570.1234m, DBNull.Value, DBNull.Value), ((decimal)rows[0][1], rows[0][2], rows[0][3]));
        Assert.Equal((1500m, 1400.5m, 1600.25m), ((decimal)rows[1][1], (decimal)rows[1][2], (decimal)rows[1][3]));
        Assert.Equal((DBNull.Value, DBNull.Value, DBNull.Value), (rows[2][1], rows[2][2], rows[2][3]));
    }

    [Fact]
    public async Task TheUsersChoice_IsNotAppliedToTheirExistingTransactions()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        // The user had chosen Blue (1) when the previous migration was the latest. Their old transactions do not become blue.
        var userDollar = await database.ScalarAsync<int>($"""SELECT "DollarType" FROM "Users" WHERE "Id" = '{User}'""");
        var blueTransactions = await database.ScalarAsync<long>("""SELECT count(*) FROM "Transactions" WHERE "DollarType" <> 0""");

        Assert.Equal(1, userDollar); // the choice itself is kept
        Assert.Equal(0, blueTransactions);
    }

    [Fact]
    public async Task NewTransactions_CanBeSavedWithAnyOfTheThreeDollars()
    {
        var database = await MigrateDataCreatedBeforeTheMigration();

        await database.ExecuteAsync(
            $"""
            INSERT INTO "Transactions" ("Id", "Amount", "Currency", "DollarType", "Date", "CategoryId", "UserId",
                                        "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
              VALUES ('33333333-0000-0000-0000-000000000009', 5, 2, 2, '2026-04-01', '{Category}', '{User}', false, '{Created}', '{NoDate}', '{NoDate}');
            """);

        Assert.Equal(2, await database.ScalarAsync<int>("""SELECT "DollarType" FROM "Transactions" WHERE "Amount" = 5"""));
    }
}
