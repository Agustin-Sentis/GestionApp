using GastosBackend.Application.dtos.Requests;
using GastosBackend.Application.dtos.Responses;
using GastosBackend.Application.Interfaces;
using GastosBackend.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GastosBackend.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<IPasswordResetService> _passwordResetServiceMock = new();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _controller = new AuthController(_authServiceMock.Object, _passwordResetServiceMock.Object);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsNoContent_AndAsksTheService()
    {
        var request = new ForgotPasswordRequest { Email = "demo@demo.com" };

        var result = await _controller.ForgotPassword(request);

        Assert.IsType<NoContentResult>(result);
        _passwordResetServiceMock.Verify(s => s.RequestReset(request), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ReturnsNoContent_AndAsksTheService()
    {
        var request = new ResetPasswordRequest { Token = "abc", NewPassword = "Demo1234!" };

        var result = await _controller.ResetPassword(request);

        Assert.IsType<NoContentResult>(result);
        _passwordResetServiceMock.Verify(s => s.ResetPassword(request), Times.Once);
    }

    [Fact]
    public void TheRecoveryEndpoints_AreAnonymousAndRateLimited_LikeTheSignIn()
    {
        // The attributes are on the controller, so they cover every action of it: the two of the recovery are reachable
        // without a session and share the limit of attempts of the sign in
        var controller = typeof(AuthController);
        Assert.NotNull(controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false).SingleOrDefault());
        Assert.NotNull(controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false).SingleOrDefault());

        foreach (var (action, route) in new[] { (nameof(AuthController.ForgotPassword), "forgot-password"), (nameof(AuthController.ResetPassword), "reset-password") })
        {
            var post = controller.GetMethod(action)!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false)
                .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>().Single();
            Assert.Equal(route, post.Template);
        }
    }

    [Fact]
    public async Task SignUp_ReturnsOkWithToken()
    {
        var request = new SignUpRequest { Email = "demo@demo.com", Password = "Demo1234!" };
        _authServiceMock.Setup(s => s.SignUp(request)).ReturnsAsync(new AuthResponse { Token = "jwt" });

        var result = await _controller.SignUp(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("jwt", Assert.IsType<AuthResponse>(ok.Value).Token);
    }

    [Fact]
    public async Task SignIn_ReturnsOkWithToken()
    {
        var request = new SignInRequest { Email = "demo@demo.com", Password = "Demo1234!" };
        _authServiceMock.Setup(s => s.SignIn(request)).ReturnsAsync(new AuthResponse { Token = "jwt" });

        var result = await _controller.SignIn(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("jwt", Assert.IsType<AuthResponse>(ok.Value).Token);
    }
}
