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
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _controller = new AuthController(_authServiceMock.Object);
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
