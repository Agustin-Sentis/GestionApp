using System.Net;
using System.Net.Http.Json;

namespace GastosBackend.Tests.Integration;

/// <summary>The headers the browser gets from the API: no content sniffing, no referrer, and nothing private kept in a cache.</summary>
public class SecurityHeadersTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(TestApiFactory factory) => _client = factory.CreateClient();

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : "";

    [Theory]
    [InlineData("/api/category")]            // closed: a 401
    [InlineData("/api/health")]              // open
    [InlineData("/api/does-not-exist")]      // a route that is not there
    public async Task EveryApiResponse_CarriesTheSecurityHeaders(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AResponseOfTheAuthEndpoints_IsNeverCached()
    {
        // The session token travels in these responses
        var response = await _client.PostAsJsonAsync("/api/auth/signin", new { email = "a@x.com", password = "Demo1234!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task TheApiDocs_AreLeftAlone_SoTheirOwnPageStillWorks()
    {
        var response = await _client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.CacheControl?.NoStore ?? false);
    }
}
