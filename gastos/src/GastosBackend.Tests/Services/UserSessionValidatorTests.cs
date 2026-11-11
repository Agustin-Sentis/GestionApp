using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using Moq;

namespace GastosBackend.Tests.Services;

public class UserSessionValidatorTests
{
    // A reset at 15:00:00.700 UTC: its second is 15:00:00
    private static readonly DateTimeOffset ResetAt = new(2026, 11, 10, 15, 0, 0, 700, TimeSpan.Zero);
    private static readonly long ResetSecond = new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly UserSessionValidator _validator;

    public UserSessionValidatorTests()
    {
        _validator = new UserSessionValidator(_userRepositoryMock.Object);
    }

    private User Known(DateTimeOffset? passwordChangedAt = null, bool deleted = false)
    {
        var user = new User { Id = Guid.NewGuid(), PasswordChangedAt = passwordChangedAt, IsDeleted = deleted };
        _userRepositoryMock.Setup(r => r.Get(user.Id)).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task IsInvalid_WhenTheUserDoesNotExist()
    {
        Assert.False(await _validator.IsValid(Guid.NewGuid(), ResetSecond));
    }

    [Fact]
    public async Task IsInvalid_WhenTheUserWasDeleted()
    {
        var user = Known(deleted: true);

        Assert.False(await _validator.IsValid(user.Id, ResetSecond));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1L)]
    [InlineData(1_800_000_000L)]
    public async Task IsValid_WhenThePasswordWasNeverReset_WhateverTheTokenSays(long? issuedAt)
    {
        // Includes the tokens issued before they carried "iat" (null) and the ones that are very old
        var user = Known();

        Assert.True(await _validator.IsValid(user.Id, issuedAt));
    }

    [Fact]
    public async Task IsValid_ForASessionIssuedAfterTheReset()
    {
        var user = Known(ResetAt);

        Assert.True(await _validator.IsValid(user.Id, ResetSecond + 60));
    }

    [Fact]
    public async Task IsValid_ForASessionIssuedInTheSameSecondAsTheReset()
    {
        // Signing in right after the reset truncates to that same second: it must not be refused
        var user = Known(ResetAt);

        Assert.True(await _validator.IsValid(user.Id, ResetSecond));
    }

    [Fact]
    public async Task IsInvalid_ForASessionIssuedBeforeTheReset()
    {
        var user = Known(ResetAt);

        Assert.False(await _validator.IsValid(user.Id, ResetSecond - 1));
        Assert.False(await _validator.IsValid(user.Id, ResetSecond - 3600));
    }

    [Fact]
    public async Task IsInvalid_ForATokenWithoutIssuedAt_WhenThePasswordWasReset()
    {
        var user = Known(ResetAt);

        Assert.False(await _validator.IsValid(user.Id, null));
    }

    [Fact]
    public async Task AResetOfAnotherUser_DoesNotAffectThisOne()
    {
        var other = Known(ResetAt);
        var mine = Known();

        Assert.False(await _validator.IsValid(other.Id, ResetSecond - 10));
        Assert.True(await _validator.IsValid(mine.Id, ResetSecond - 10));
    }
}
