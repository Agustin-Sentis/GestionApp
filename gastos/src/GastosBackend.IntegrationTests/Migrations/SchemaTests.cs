using GastosBackend.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.IntegrationTests.Migrations;

/// <summary>The shape of the database after every migration: model in sync, columns and indexes as the code expects.</summary>
[Collection(PostgresCollection.Name)]
public class SchemaTests
{
    private readonly PostgresFixture _postgres;

    public SchemaTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<TestDatabase> MigratedDatabase()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();
        return database;
    }

    private static async Task<Dictionary<string, string>> IndexDefinitions(TestDatabase database)
    {
        var rows = await database.QueryAsync("SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public'");
        return rows.ToDictionary(r => (string)r[0], r => (string)r[1]);
    }

    [Fact]
    public async Task TheModel_HasNoChangesMissingFromTheMigrations()
    {
        // If someone changes an entity or the DbContext and forgets "dotnet ef migrations add", this fails
        var database = await MigratedDatabase();
        await using var context = database.CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task TheUsersEmail_IsUnique_AmongTheUsersThatAreNotDeleted()
    {
        var indexes = await IndexDefinitions(await MigratedDatabase());

        var definition = indexes["IX_Users_Email"];
        Assert.Contains("UNIQUE", definition);
        Assert.Contains("(\"IsDeleted\" = false)", definition);
    }

    [Fact]
    public async Task TheCategoryName_IsUniquePerUser_IgnoringCaseAndDeletedRows()
    {
        var indexes = await IndexDefinitions(await MigratedDatabase());

        var definition = indexes["IX_Categories_UserId_LowerName"];
        Assert.Contains("UNIQUE", definition);
        Assert.Contains("lower(", definition);
        Assert.Contains("(\"IsDeleted\" = false)", definition);
        // The old case-sensitive index must be gone, or "Comida" and "comida" would still be accepted
        Assert.DoesNotContain("IX_Categories_UserId_Name", indexes.Keys);
    }

    [Fact]
    public async Task TheLookupIndexes_ThePagedListRelyOn_Exist()
    {
        var indexes = await IndexDefinitions(await MigratedDatabase());

        Assert.Contains("(\"UserId\", \"Date\")", indexes["IX_Transactions_UserId_Date"]);
        Assert.Contains("IX_Transactions_CategoryId", indexes.Keys);
        Assert.Contains("IX_Categories_UserId", indexes.Keys);
    }

    [Fact]
    public async Task TheExchangeRate_IsNullable_WithFourDecimals_AndTheAmountHasTwo()
    {
        var database = await MigratedDatabase();

        var columns = await database.QueryAsync(
            """
            SELECT column_name, numeric_precision, numeric_scale, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Transactions' AND column_name IN ('ExchangeRate', 'Amount')
            """);
        var byName = columns.ToDictionary(r => (string)r[0]);

        Assert.Equal((18, 4, "YES"), ((int)byName["ExchangeRate"][1], (int)byName["ExchangeRate"][2], (string)byName["ExchangeRate"][3]));
        Assert.Equal((18, 2, "NO"), ((int)byName["Amount"][1], (int)byName["Amount"][2], (string)byName["Amount"][3]));
    }

    [Fact]
    public async Task EveryTable_HasACreatedDateColumn()
    {
        var database = await MigratedDatabase();

        var tables = await database.QueryAsync(
            """
            SELECT table_name FROM information_schema.columns
            WHERE table_schema = 'public' AND column_name = 'CreatedDate' ORDER BY table_name
            """);

        Assert.Equal(
            new[] { "BudgetAlerts", "Categories", "RecoveryCodes", "Transactions", "UserTokens", "Users" },
            tables.Select(r => (string)r[0]).ToArray());
    }
}
