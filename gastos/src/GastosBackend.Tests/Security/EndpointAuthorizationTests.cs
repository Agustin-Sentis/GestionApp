using System.Reflection;
using GastosBackend.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GastosBackend.Tests.Security;

/// <summary>
/// The API is closed by default (a fallback policy asks for a signed-in user). On top of that, every endpoint must SAY whether it
/// is private or public, so a new controller never ends up open (or closed) just because nobody thought about it.
/// </summary>
public class EndpointAuthorizationTests
{
    private static IEnumerable<Type> Controllers() =>
        typeof(HealthController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    private static bool IsAction(MethodInfo method) =>
        method.IsPublic && !method.IsStatic && method.DeclaringType != typeof(object)
        && method.GetCustomAttributes<HttpMethodAttribute>().Any();

    private static bool Declares(MemberInfo member) =>
        member.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
        || member.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    [Fact]
    public void ThereAreControllersToCheck()
    {
        // Guards the test itself: a wrong assembly would make the checks below pass on nothing
        Assert.NotEmpty(Controllers());
    }

    [Fact]
    public void EveryAction_DeclaresAuthorizeOrAllowAnonymous()
    {
        var undeclared = Controllers()
            .SelectMany(c => c.GetMethods().Where(IsAction).Select(m => (Controller: c, Action: m)))
            .Where(x => !Declares(x.Controller) && !Declares(x.Action))
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
            .ToList();

        Assert.True(undeclared.Count == 0,
            "These endpoints do not say if they are private or public: " + string.Join(", ", undeclared));
    }

    [Fact]
    public void PublicEndpoints_AreExactlyTheKnownOnes()
    {
        // A new public endpoint has to be added here on purpose: it is the list a reviewer reads to know what is open
        var expected = new[]
        {
            "AuthController.SignUp", "AuthController.SignIn", "AuthController.VerifyTwoFactor",
            "AuthController.ForgotPassword", "AuthController.ResetPassword",
            "HealthController.Get", "SettingsController.ConfirmEmail"
        };

        var open = Controllers()
            .SelectMany(c => c.GetMethods().Where(IsAction).Select(m => (Controller: c, Action: m)))
            .Where(x => x.Action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
                        || (x.Controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
                            && !x.Action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()))
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(expected.OrderBy(n => n), open);
    }
}
