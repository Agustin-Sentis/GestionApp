using GastosBackend.Application.Exceptions;
using GastosBackend.Domain.Entities;
using GastosBackend.Infrastructure.Repository;
using GastosBackend.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.IntegrationTests.Repositories;

/// <summary>UserRepository, and through it the behavior every repository inherits from BaseRepository.</summary>
[Collection(PostgresCollection.Name)]
public class UserRepositoryTests
{
    private readonly PostgresFixture _postgres;

    public UserRepositoryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<TestDatabase> MigratedDatabase()
    {
        var database = await _postgres.CreateDatabaseAsync();
        await database.MigrateAsync();
        return database;
    }

    private static User NewUser(string email) => new User { Id = Guid.NewGuid(), Email = email, Password = "hash" };

    [Fact]
    public async Task Add_StoresTheUser_AndSetsTheCreatedDate()
    {
        var database = await MigratedDatabase();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await using (var context = database.CreateContext())
        {
            await new UserRepository(context).Add(NewUser("a@x.com"));
        }

        await using var check = database.CreateContext();
        var stored = await check.Users.SingleAsync();
        Assert.Equal("a@x.com", stored.Email);
        Assert.InRange(stored.CreatedDate, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Add_ThrowsDuplicateException_WhenTheEmailIsAlreadyTaken()
    {
        // This is the real "two signups raced" case: the check passed, the unique index rejected the insert
        var database = await MigratedDatabase();
        await database.AddUserAsync("same@x.com");

        await using var context = database.CreateContext();
        await Assert.ThrowsAsync<DuplicateException>(() => new UserRepository(context).Add(NewUser("same@x.com")));

        await using var check = database.CreateContext();
        Assert.Equal(1, await check.Users.CountAsync());
    }

    [Fact]
    public async Task Add_Works_WhenTheOnlyUserWithThatEmailWasDeleted()
    {
        var database = await MigratedDatabase();
        await database.AddUserAsync("old@x.com", deleted: true);

        await using var context = database.CreateContext();
        await new UserRepository(context).Add(NewUser("old@x.com"));

        await using var check = database.CreateContext();
        Assert.Equal(2, await check.Users.CountAsync());
    }

    [Fact]
    public async Task GetByEmail_FindsTheActiveUser_AndIgnoresDeletedOnes()
    {
        var database = await MigratedDatabase();
        await database.AddUserAsync("gone@x.com", deleted: true);
        var active = await database.AddUserAsync("here@x.com");

        await using var context = database.CreateContext();
        var repository = new UserRepository(context);

        Assert.Equal(active.Id, (await repository.GetByEmail("here@x.com"))!.Id);
        Assert.Null(await repository.GetByEmail("gone@x.com"));
        Assert.Null(await repository.GetByEmail("nobody@x.com"));
    }

    [Fact]
    public async Task Update_SetsTheUpdateDate()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync("a@x.com");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await using (var context = database.CreateContext())
        {
            var repository = new UserRepository(context);
            var loaded = (await repository.Get(user.Id))!;
            loaded.Password = "new-hash";
            await repository.Update(loaded);
        }

        await using var check = database.CreateContext();
        var stored = await check.Users.SingleAsync();
        Assert.Equal("new-hash", stored.Password);
        Assert.InRange(stored.UpdateDate, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Delete_IsSoft_TheRowStaysButIsHidden()
    {
        var database = await MigratedDatabase();
        var user = await database.AddUserAsync("a@x.com");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await using (var context = database.CreateContext())
        {
            var repository = new UserRepository(context);
            await repository.Delete(user.Id);
            Assert.Null(await repository.Get(user.Id));
            Assert.Empty(await repository.GetAll());
        }

        await using var check = database.CreateContext();
        var stored = await check.Users.SingleAsync(); // the row is still in the table
        Assert.True(stored.IsDeleted);
        Assert.InRange(stored.DeleteDate, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Delete_DoesNothing_WhenTheUserDoesNotExist()
    {
        var database = await MigratedDatabase();

        await using var context = database.CreateContext();
        await new UserRepository(context).Delete(Guid.NewGuid());

        Assert.Equal(0, await context.Users.CountAsync());
    }
}
