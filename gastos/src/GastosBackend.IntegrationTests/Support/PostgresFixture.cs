using Npgsql;
using Testcontainers.PostgreSql;

namespace GastosBackend.IntegrationTests.Support;

/// <summary>
/// Starts one PostgreSQL container for the whole test run (starting it takes seconds) and hands every test its own
/// empty database inside it, so tests never see each other's data.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a new empty database with a unique name. Migrations are NOT applied here.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = name,
            // Each test opens a handful of connections; pooling them across hundreds of throwaway databases only wastes slots
            Pooling = false
        }.ConnectionString;

        return new TestDatabase(connectionString);
    }
}

/// <summary>All tests that need PostgreSQL share one container through this collection (its tests run one after another).</summary>
[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
