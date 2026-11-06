using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GastosBackend.Presentation.Middlewares;

namespace GastosBackend.Tests.Integration;

public class ApiErrorFormatTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public ApiErrorFormatTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestApiFactory.CreateToken());
    }

    private static async Task<Dictionary<string, string>> ReadBody(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        // Only the { "error": "..." } shape: no ProblemDetails fields such as "title" or "errors"
        Assert.Equal(new[] { "error" }, body!.Keys.ToArray());
        return body.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()!);
    }

    private static StringContent Json(string json) => new StringContent(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task AMalformedJsonBody_ReturnsTheStandardError()
    {
        var response = await _client.PostAsync("/api/auth/signin", Json("{ this is not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ModelStateErrorMessage.BodyMessage, (await ReadBody(response))["error"]);
    }

    [Fact]
    public async Task AnInvalidEnumValueInTheBody_NamesTheField()
    {
        var response = await _client.PostAsync("/api/category", Json("""{ "name": "Comida", "type": "Foo" }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Just the field: no extra "invalid body" sentence next to it
        Assert.Equal("El valor del campo 'type' no es válido.", (await ReadBody(response))["error"]);
    }

    [Fact]
    public async Task UnparseableQueryValues_NameEachField()
    {
        var response = await _client.GetAsync("/api/transaction/paged?type=Foo&page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await ReadBody(response))["error"];
        Assert.Contains("'Type'", error);
        Assert.Contains("'Page'", error);
    }

    [Fact]
    public async Task AnInvalidGuidInTheQuery_ReturnsTheStandardError()
    {
        var response = await _client.GetAsync("/api/transaction/paged?categoryId=xyz");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("'CategoryId'", (await ReadBody(response))["error"]);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ATransactionIdThatIsNotAGuid_DoesNotMatchAnyRoute(string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), "/api/transaction/not-a-guid");
        if (method == "PUT") request.Content = Json("{}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheHealthEndpoint_Returns503_WhenTheDatabaseIsNotReachable()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("unhealthy", body!["status"]);
    }
}
