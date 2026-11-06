using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.Exceptions;
using GastosBackend.Application.Interfaces;
using GastosBackend.Application.Services;
using GastosBackend.Domain.Entities;
using GastosBackend.Domain.Enums;
using Moq;

namespace GastosBackend.Tests.Services;

public class SettingsServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly SettingsService _service;
    private readonly User _user = new User { Id = Guid.NewGuid(), Email = "a@x.com", DollarType = DollarType.Official };

    public SettingsServiceTests()
    {
        _service = new SettingsService(_userRepositoryMock.Object);
        _userRepositoryMock.Setup(r => r.Get(_user.Id)).ReturnsAsync(_user);
    }

    [Theory]
    [InlineData(DollarType.Official)]
    [InlineData(DollarType.Blue)]
    [InlineData(DollarType.Mep)]
    public async Task Get_ReturnsTheDollarTheUserChose(DollarType chosen)
    {
        _user.DollarType = chosen;

        var settings = await _service.Get(_user.Id);

        Assert.Equal(chosen, settings.DollarType);
    }

    [Fact]
    public async Task Get_Throws_WhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.Get(Guid.NewGuid()));
    }

    [Fact]
    public async Task Get_Throws_WhenTheUserWasDeleted()
    {
        _user.IsDeleted = true;

        await Assert.ThrowsAsync<NotFoundException>(() => _service.Get(_user.Id));
    }

    [Theory]
    [InlineData(DollarType.Blue)]
    [InlineData(DollarType.Mep)]
    [InlineData(DollarType.Official)]
    public async Task Update_SavesTheNewDollar_AndReturnsIt(DollarType chosen)
    {
        _user.DollarType = chosen == DollarType.Official ? DollarType.Blue : DollarType.Official;

        var settings = await _service.Update(new SettingsRequest { DollarType = chosen }, _user.Id);

        Assert.Equal(chosen, settings.DollarType);
        Assert.Equal(chosen, _user.DollarType);
        _userRepositoryMock.Verify(r => r.Update(_user), Times.Once);
    }

    [Fact]
    public async Task Update_Throws_AndSavesNothing_WhenTheDollarIsNotOneOfTheThree()
    {
        var request = new SettingsRequest { DollarType = (DollarType)7 };

        await Assert.ThrowsAsync<ValidationException>(() => _service.Update(request, _user.Id));

        Assert.Equal(DollarType.Official, _user.DollarType);
        _userRepositoryMock.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Update_Throws_WhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.Update(new SettingsRequest { DollarType = DollarType.Blue }, Guid.NewGuid()));
    }

    [Fact]
    public async Task Update_OnlyTouchesTheDollar_NotTheEmailOrThePassword()
    {
        _user.Password = "hash";

        await _service.Update(new SettingsRequest { DollarType = DollarType.Mep }, _user.Id);

        Assert.Equal("a@x.com", _user.Email);
        Assert.Equal("hash", _user.Password);
    }
}
