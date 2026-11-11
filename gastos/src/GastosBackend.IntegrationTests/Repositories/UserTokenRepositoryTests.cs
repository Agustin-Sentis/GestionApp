using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.IntegrationTests.Support;

namespace GastosBackend.IntegrationTests.Repositories;

[Collection(PostgresCollection.Name)]
public class UserTokenRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public UserTokenRepositoryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<(TestDatabase Database, User User)> Setup()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();
        return (database, await database.AddUserAsync());
    }

    private static async Task<UserToken> Save(
        TestDatabase database, Guid userId, string hash, TimeSpan? expiresIn = null, bool used = false, bool deleted = false,
        DateTimeOffset? created = null)
    {
        await using var context = database.CreateContext();
        var token = new UserToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = UserTokenPurpose.EmailConfirmation,
            TokenHash = hash,
            ExpiresAt = Now + (expiresIn ?? TimeSpan.FromHours(24)),
            UsedAt = used ? Now : null,
            IsDeleted = deleted,
            CreatedDate = created ?? Now
        };
        context.UserTokens.Add(token);
        await context.SaveChangesAsync();
        return token;
    }

    [Fact]
    public async Task GetOpen_FindsAValidToken()
    {
        var (database, user) = await Setup();
        var saved = await Save(database, user.Id, "abc");

        await using var context = database.CreateContext();
        var found = await new UserTokenRepository(context).GetOpen(UserTokenPurpose.EmailConfirmation, "abc", Now);

        Assert.Equal(saved.Id, found?.Id);
    }

    [Fact]
    public async Task GetOpen_IgnoresUsedExpiredDeletedAndUnknownTokens()
    {
        var (database, user) = await Setup();
        await Save(database, user.Id, "used", used: true);
        await Save(database, user.Id, "expired", expiresIn: TimeSpan.FromSeconds(-1));
        await Save(database, user.Id, "deleted", deleted: true);

        await using var context = database.CreateContext();
        var repository = new UserTokenRepository(context);

        foreach (var hash in new[] { "used", "expired", "deleted", "unknown" })
        {
            Assert.Null(await repository.GetOpen(UserTokenPurpose.EmailConfirmation, hash, Now));
        }
    }

    [Fact]
    public async Task GetOpenByUser_ReturnsOnlyThatUsersOpenTokens_NewestFirst()
    {
        var (database, user) = await Setup();
        var other = await database.AddUserAsync();
        var older = await Save(database, user.Id, "older", created: Now.AddMinutes(-5));
        var newer = await Save(database, user.Id, "newer", created: Now);
        await Save(database, user.Id, "used", used: true);
        await Save(database, other.Id, "foreign");

        await using var context = database.CreateContext();
        var open = await new UserTokenRepository(context).GetOpenByUser(user.Id, UserTokenPurpose.EmailConfirmation, Now);

        Assert.Equal(new[] { newer.Id, older.Id }, open.Select(t => t.Id));
    }
}
