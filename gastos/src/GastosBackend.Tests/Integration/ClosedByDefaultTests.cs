using System.Net;
using System.Net.Http.Json;

namespace GastosBackend.Tests.Integration;

/// <summary>What a visitor without a session sees from outside: the private routes are closed, the public ones are not.</summary>
public class ClosedByDefaultTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public ClosedByDefaultTests(TestApiFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("GET", "/api/category")]
    [InlineData("GET", "/api/transaction")]
    [InlineData("GET", "/api/report/monthly")]
    [InlineData("GET", "/api/settings")]
    [InlineData("PUT", "/api/settings/budget-alerts")]
    [InlineData("POST", "/api/settings/resend-confirmation")]
    [InlineData("POST", "/api/settings/2fa/setup")]
    [InlineData("POST", "/api/settings/2fa/disable")]
    public async Task APrivateRoute_WithoutASession_Gets401(string method, string path)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { })
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheHealthCheck_StaysOpen_ForTheHostingPlatform()
    {
        var response = await _client.GetAsync("/api/health");

        // 200 or 503 (this factory has no database): never 401, or the platform would restart a healthy API
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SigningIn_StaysOpen()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/signin", new { email = "a@x.com", password = "Demo1234!" });

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
