using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;

namespace GastosBackend.IntegrationTests.Support;

/// <summary>
/// Puts rows straight into the database through the context, bypassing the repositories under test.
/// Dates are UTC (PostgreSQL's timestamptz only accepts offset 0) and can be set so ordering tests control them exactly.
/// </summary>
public static class Seeder
{
    public static async Task<User> AddUserAsync(this TestDatabase database, string? email = null, bool deleted = false)
    {
        await using var context = database.CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email ?? $"{Guid.NewGuid():N}@example.com",
            Password = "hash",
            CreatedDate = DateTimeOffset.UtcNow,
            IsDeleted = deleted
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<Category> AddCategoryAsync(
        this TestDatabase database, Guid userId, string name, TransactionType type = TransactionType.Expense, bool deleted = false)
    {
        await using var context = database.CreateContext();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            UserId = userId,
            CreatedDate = DateTimeOffset.UtcNow,
            IsDeleted = deleted
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    public static async Task<Transaction> AddTransactionAsync(
        this TestDatabase database,
        Guid userId,
        Guid categoryId,
        string date,
        decimal amount = 100m,
        Currency currency = Currency.ARS,
        decimal? exchangeRate = null,
        string? note = null,
        bool deleted = false,
        DateTimeOffset? createdDate = null)
    {
        await using var context = database.CreateContext();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = categoryId,
            Date = DateOnly.Parse(date),
            Amount = amount,
            Currency = currency,
            ExchangeRate = exchangeRate,
            Note = note,
            CreatedDate = createdDate ?? DateTimeOffset.UtcNow,
            IsDeleted = deleted
        };
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction;
    }
}
