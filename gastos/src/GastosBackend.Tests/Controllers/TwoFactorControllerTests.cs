using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Presentation.Controllers;
using GastosBackend.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;

namespace GastosBackend.Tests.Controllers;

public class TwoFactorControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Mock<ITwoFactorService> _serviceMock = new();
    private readonly TwoFactorController _controller;

    public TwoFactorControllerTests()
    {
        _controller = new TwoFactorController(_serviceMock.Object);
        ControllerTestHelper.SetUserClaim(_controller, UserId.ToString());
    }

    [Fact]
    public async Task Setup_PassesTheSignedInUserAndPassword_AndReturnsTheQrData()
    {
        _serviceMock.Setup(s => s.Setup(UserId, "Demo1234!")).ReturnsAsync(new TwoFactorSetupResponse { Uri = "otpauth://x", Secret = "ABC" });

        var result = await _controller.Setup(new TwoFactorSetupRequest { Password = "Demo1234!" });

        var body = Assert.IsType<TwoFactorSetupResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("ABC", body.Secret);
    }

    [Fact]
    public async Task Enable_PassesTheCode_AndReturnsTheRecoveryCodes()
    {
        _serviceMock.Setup(s => s.Enable(UserId, "123456")).ReturnsAsync(new RecoveryCodesResponse { Codes = new[] { "AAAAA-BBBBB" } });

        var result = await _controller.Enable(new TwoFactorEnableRequest { Code = "123456" });

        var body = Assert.IsType<RecoveryCodesResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new[] { "AAAAA-BBBBB" }, body.Codes);
    }

    [Fact]
    public async Task Disable_PassesPasswordAndCode_AndReturnsNoContent()
    {
        var result = await _controller.Disable(new TwoFactorPasswordCodeRequest { Password = "Demo1234!", Code = "123456" });

        Assert.IsType<NoContentResult>(result);
        _serviceMock.Verify(s => s.Disable(UserId, "Demo1234!", "123456"), Times.Once);
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_PassesPasswordAndCode_AndReturnsTheNewCodes()
    {
        _serviceMock.Setup(s => s.RegenerateRecoveryCodes(UserId, "Demo1234!", "123456"))
            .ReturnsAsync(new RecoveryCodesResponse { Codes = new[] { "CCCCC-DDDDD" } });

        var result = await _controller.RegenerateRecoveryCodes(new TwoFactorPasswordCodeRequest { Password = "Demo1234!", Code = "123456" });

        var body = Assert.IsType<RecoveryCodesResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new[] { "CCCCC-DDDDD" }, body.Codes);
    }

    [Fact]
    public void TheController_NeedsASession_IsRateLimitedLikeTheSignIn_AndLivesUnderSettings()
    {
        var type = typeof(TwoFactorController);

        Assert.NotNull(type.GetCustomAttributes(typeof(AuthorizeAttribute), false).SingleOrDefault());
        Assert.Empty(type.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        Assert.NotNull(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).SingleOrDefault());
        Assert.Equal("api/settings/2fa", type.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Single().Template);
    }

    [Theory]
    [InlineData(nameof(TwoFactorController.Setup), "setup")]
    [InlineData(nameof(TwoFactorController.Enable), "enable")]
    [InlineData(nameof(TwoFactorController.Disable), "disable")]
    [InlineData(nameof(TwoFactorController.RegenerateRecoveryCodes), "recovery-codes")]
    public void EveryAction_IsAPost_WithItsRoute(string action, string route)
    {
        var post = typeof(TwoFactorController).GetMethod(action)!.GetCustomAttributes(typeof(HttpPostAttribute), false)
            .Cast<HttpPostAttribute>().Single();

        Assert.Equal(route, post.Template);
    }
}
