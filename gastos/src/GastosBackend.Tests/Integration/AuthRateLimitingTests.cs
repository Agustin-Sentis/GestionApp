using System.Net;
using System.Net.Http.Json;
using GastosBackend.Application.dtos.Requests;

namespace GastosBackend.Tests.Integration;

public class AuthRateLimitingTests : IClassFixture<AuthRateLimitingTests.ApiFactory>
{
    private const int PermitLimit = 3;

    private readonly HttpClient _client;

    public AuthRateLimitingTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>The real pipeline with a tiny limit, no database and a fake auth service.</summary>
    public class ApiFactory : TestApiFactory
    {
        protected override int AuthPermitLimit => PermitLimit;
    }

    private Task<HttpResponseMessage> SignIn()
        => _client.PostAsJsonAsync("/api/auth/signin", new SignInRequest { Email = "demo@demo.com", Password = "Demo1234!" });

    [Fact]
    public async Task AuthEndpoints_Return429_AfterThePermitLimit_WithTheStandardErrorBody()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SignIn()).StatusCode);
        }

        var limited = await SignIn();

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.True(int.Parse(retryAfter!.Single()) > 0);

        var body = await limited.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("error"));
    }

    [Fact]
    public async Task OtherEndpoints_AreNotRateLimited()
    {
        // The OpenAPI document needs no database (unlike /api/health) and is outside the auth policy
        for (var i = 0; i < PermitLimit + 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/openapi/v1.json")).StatusCode);
        }
    }
}
