using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Domain.Enums;
using GastosBackend.Presentation.Controllers;
using GastosBackend.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GastosBackend.Tests.Controllers;

public class SettingsControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Mock<ISettingsService> _settingsServiceMock = new();
    private readonly SettingsController _controller;

    public SettingsControllerTests()
    {
        _controller = new SettingsController(_settingsServiceMock.Object);
        ControllerTestHelper.SetUserClaim(_controller, UserId.ToString());
    }

    [Fact]
    public async Task Get_ReturnsTheSettingsOfTheSignedInUser()
    {
        _settingsServiceMock.Setup(s => s.Get(UserId)).ReturnsAsync(new SettingsResponse { DollarType = DollarType.Blue });

        var result = await _controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(DollarType.Blue, Assert.IsType<SettingsResponse>(ok.Value).DollarType);
    }

    [Fact]
    public async Task Update_SavesForTheSignedInUser_AndReturnsTheResult()
    {
        var request = new SettingsRequest { DollarType = DollarType.Mep };
        _settingsServiceMock.Setup(s => s.Update(request, UserId)).ReturnsAsync(new SettingsResponse { DollarType = DollarType.Mep });

        var result = await _controller.Update(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(DollarType.Mep, Assert.IsType<SettingsResponse>(ok.Value).DollarType);
        _settingsServiceMock.Verify(s => s.Update(request, UserId), Times.Once);
    }
}
