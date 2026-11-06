using System.Security.Claims;
using GastosBackend.Application.Exceptions;
using GastosBackend.Presentation.Controllers;
using GastosBackend.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Tests.Controllers;

public class ApiControllerBaseTests
{
    /// <summary>ApiControllerBase.UserId is protected: this exposes it so the tests can read it.</summary>
    private class ProbeController : ApiControllerBase
    {
        public Guid ReadUserId() => UserId;
    }

    [Fact]
    public void UserId_IsTheGuidInTheNameIdentifierClaim()
    {
        var userId = Guid.NewGuid();
        var controller = new ProbeController();
        ControllerTestHelper.SetUserClaim(controller, userId.ToString());

        Assert.Equal(userId, controller.ReadUserId());
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("12345")]
    public void UserId_Throws_WhenTheClaimIsNotAGuid(string claim)
    {
        var controller = new ProbeController();
        ControllerTestHelper.SetUserClaim(controller, claim);

        Assert.Throws<UnauthorizedException>(() => controller.ReadUserId());
    }

    [Fact]
    public void UserId_Throws_WhenThereIsNoClaim()
    {
        var controller = new ProbeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
            }
        };

        Assert.Throws<UnauthorizedException>(() => controller.ReadUserId());
    }

    [Fact]
    public void UserId_Throws_WhenTheUserIsNotAuthenticated()
    {
        var controller = new ProbeController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.Throws<UnauthorizedException>(() => controller.ReadUserId());
    }
}
