using GastosBackend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GastosBackend.IntegrationTests.Support;

/// <summary>A throwaway database. Create contexts on it, migrate it (all the way or up to a given migration) and run raw SQL.</summary>
public class TestDatabase
{
    public TestDatabase(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    /// <summary>A new context on this database. Use one context per unit of work, as the API does per request.</summary>
    public GastosDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GastosDbContext>().UseNpgsql(ConnectionString).Options;
        return new GastosDbContext(options);
    }

    /// <summary>Applies the migrations: all of them, or only up to <paramref name="targetMigration"/> (inclusive).</summary>
    public async Task MigrateAsync(string? targetMigration = null)
    {
        await using var context = CreateContext();
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
    }

    public async Task<int> ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a query and returns every row as an array of column values (nulls as <see cref="DBNull"/>).</summary>
    public async Task<List<object[]>> QueryAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<object[]>();
        while (await reader.ReadAsync())
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<T> ScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
    {
        var rows = await QueryAsync(sql, parameters);
        return (T)rows.Single().Single();
    }
}
