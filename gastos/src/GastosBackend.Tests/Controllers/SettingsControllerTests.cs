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

    [Fact]
    public async Task SetBudgetAlerts_PassesTheSignedInUser_AndReturnsTheResult()
    {
        var request = new BudgetAlertsRequest { Enabled = true };
        _settingsServiceMock.Setup(s => s.SetBudgetAlerts(request, UserId))
            .ReturnsAsync(new SettingsResponse { BudgetEmailAlerts = true, EmailConfirmed = false, Email = "a@x.com" });

        var result = await _controller.SetBudgetAlerts(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var settings = Assert.IsType<SettingsResponse>(ok.Value);
        Assert.True(settings.BudgetEmailAlerts);
        Assert.False(settings.EmailConfirmed);
    }

    [Fact]
    public async Task ResendConfirmation_ReturnsNoContent_AndAsksForTheSignedInUser()
    {
        var result = await _controller.ResendConfirmation();

        Assert.IsType<NoContentResult>(result);
        _settingsServiceMock.Verify(s => s.ResendConfirmation(UserId), Times.Once);
    }

    [Fact]
    public async Task ChangeEmail_PassesTheSignedInUser_AndReturnsTheResult()
    {
        var request = new ChangeEmailRequest { Email = "nuevo@x.com", Password = "Demo1234!" };
        _settingsServiceMock.Setup(s => s.ChangeEmail(request, UserId))
            .ReturnsAsync(new SettingsResponse { Email = "nuevo@x.com", EmailConfirmed = false });

        var result = await _controller.ChangeEmail(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("nuevo@x.com", Assert.IsType<SettingsResponse>(ok.Value).Email);
    }

    [Fact]
    public void ChangeEmail_ChecksAPassword_SoItIsRateLimitedLikeTheSignIn()
    {
        var change = typeof(SettingsController).GetMethod(nameof(SettingsController.ChangeEmail))!;

        Assert.NotNull(change.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false).SingleOrDefault());
        Assert.Empty(change.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false));
    }

    [Fact]
    public async Task ConfirmEmail_ReturnsNoContent_WithoutNeedingASignedInUser()
    {
        // A controller with no user claim at all: the link of the mail is opened without a session
        var anonymous = new SettingsController(_settingsServiceMock.Object);
        var request = new ConfirmEmailRequest { Token = "abc" };

        var result = await anonymous.ConfirmEmail(request);

        Assert.IsType<NoContentResult>(result);
        _settingsServiceMock.Verify(s => s.ConfirmEmail(request), Times.Once);
    }

    [Fact]
    public void ConfirmEmail_IsAnonymous_AndRateLimited_ButTheOtherActionsNeedASession()
    {
        var confirm = typeof(SettingsController).GetMethod(nameof(SettingsController.ConfirmEmail))!;
        var setAlerts = typeof(SettingsController).GetMethod(nameof(SettingsController.SetBudgetAlerts))!;

        Assert.NotNull(confirm.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false).SingleOrDefault());
        Assert.NotNull(confirm.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false).SingleOrDefault());
        Assert.Empty(setAlerts.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false));
        Assert.NotNull(typeof(SettingsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).SingleOrDefault());
    }
}
