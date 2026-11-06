using GastosBackend.IntegrationTests.Support;
using Npgsql;

namespace GastosBackend.IntegrationTests.Migrations;

/// <summary>
/// The B2 migration on categories that only differ in letter case. Data is inserted before the migration (when the unique
/// index was still case-sensitive and accepted it), then the database is migrated forward.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CaseInsensitiveCategoryNamesMigrationTests
{
    private const string Noon = "00:00:00+00";

    private readonly PostgresFixture _postgres;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();

    public CaseInsensitiveCategoryNamesMigrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<TestDatabase> DatabaseBeforeTheMigration()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync(MigrationIds.AddExchangeRateAndCreatedDate);

        await database.ExecuteAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "Password", "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate") VALUES
              ('{_user}',      'a@x.com', 'hash', false, now(), now(), now()),
              ('{_otherUser}', 'b@x.com', 'hash', false, now(), now(), now());
            """);
        return database;
    }

    private static Task Category(TestDatabase database, Guid user, string name, string createdOn, bool deleted = false)
        => database.ExecuteAsync(
            $"""
            INSERT INTO "Categories" ("Id", "Name", "Type", "UserId", "IsDeleted", "CreatedDate", "UpdateDate", "DeleteDate")
            VALUES (@id, @name, 1, @user, {(deleted ? "true" : "false")}, '{createdOn} {Noon}', now(), now())
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("name", name),
            new NpgsqlParameter("user", user));

    private static async Task<List<string>> ActiveNames(TestDatabase database, Guid user)
    {
        var rows = await database.QueryAsync(
            """SELECT "Name" FROM "Categories" WHERE "UserId" = @user AND NOT "IsDeleted" ORDER BY "CreatedDate", "Name" """,
            new NpgsqlParameter("user", user));
        return rows.Select(r => (string)r[0]).ToList();
    }

    [Fact]
    public async Task TheOldestCategory_KeepsItsName_AndTheOthersGetANumberedSuffix()
    {
        var database = await DatabaseBeforeTheMigration();
        await Category(database, _user, "B2Test", "2026-01-01");
        await Category(database, _user, "b2test", "2026-01-02");
        await Category(database, _user, "B2TEST", "2026-01-03");
        await Category(database, _user, "Unique", "2026-01-04");

        await database.MigrateAsync();

        Assert.Equal(new[] { "B2Test", "b2test (2)", "B2TEST (3)", "Unique" }, await ActiveNames(database, _user));
    }

    [Fact]
    public async Task ADeletedCategory_IsNotRenamed_AndDoesNotCountAsADuplicate()
    {
        var database = await DatabaseBeforeTheMigration();
        await Category(database, _user, "Food", "2026-01-01", deleted: true);
        await Category(database, _user, "food", "2026-01-02");

        await database.MigrateAsync();

        Assert.Equal(new[] { "food" }, await ActiveNames(database, _user));
        var deleted = await database.QueryAsync(
            """SELECT "Name" FROM "Categories" WHERE "UserId" = @user AND "IsDeleted" """, new NpgsqlParameter("user", _user));
        Assert.Equal("Food", (string)deleted.Single()[0]);
    }

    [Fact]
    public async Task TheSameNameOfAnotherUser_IsNotADuplicate()
    {
        var database = await DatabaseBeforeTheMigration();
        await Category(database, _user, "Food", "2026-01-01");
        await Category(database, _otherUser, "food", "2026-01-02");

        await database.MigrateAsync();

        Assert.Equal(new[] { "Food" }, await ActiveNames(database, _user));
        Assert.Equal(new[] { "food" }, await ActiveNames(database, _otherUser));
    }

    [Fact]
    public async Task ARenamedLongName_StillFitsInTheSixtyCharactersOfTheColumn()
    {
        var database = await DatabaseBeforeTheMigration();
        await Category(database, _user, new string('X', 60), "2026-01-01");
        await Category(database, _user, new string('x', 60), "2026-01-02");

        await database.MigrateAsync();

        var names = await ActiveNames(database, _user);
        Assert.Equal(new[] { new string('X', 60), new string('x', 56) + " (2)" }, names);
        Assert.All(names, name => Assert.True(name.Length <= 60));
    }

    [Fact]
    public async Task AfterTheMigration_TheDatabaseRejectsNamesThatOnlyDifferInCase()
    {
        var database = await DatabaseBeforeTheMigration();
        await Category(database, _user, "Comida", "2026-01-01");
        await database.MigrateAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => Category(database, _user, "COMIDA", "2026-01-02"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        Assert.Equal("IX_Categories_UserId_LowerName", ex.ConstraintName);
    }

    [Fact]
    public async Task AfterTheMigration_ADeletedCategoryDoesNotBlockTheSameName()
    {
        var database = await DatabaseBeforeTheMigration();
        await Category(database, _user, "Comida", "2026-01-01", deleted: true);
        await database.MigrateAsync();

        await Category(database, _user, "comida", "2026-01-02"); // would throw if the index ignored IsDeleted

        Assert.Equal(new[] { "comida" }, await ActiveNames(database, _user));
    }
}
