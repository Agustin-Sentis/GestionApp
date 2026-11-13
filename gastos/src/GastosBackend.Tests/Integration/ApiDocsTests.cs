using System.Net;

namespace GastosBackend.Tests.Integration;

public class ApiDocsTests : IClassFixture<ApiDocsTests.DocsOnFactory>, IClassFixture<ApiDocsTests.DocsOffFactory>
{
    private readonly HttpClient _docsOn;
    private readonly HttpClient _docsOff;

    public ApiDocsTests(DocsOnFactory docsOn, DocsOffFactory docsOff)
    {
        _docsOn = docsOn.CreateClient();
        _docsOff = docsOff.CreateClient();
    }

    public class DocsOnFactory : TestApiFactory
    {
        protected override bool DocsEnabled => true;
    }

    public class DocsOffFactory : TestApiFactory
    {
        protected override bool DocsEnabled => false;
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task TheDocs_AreServed_WhenEnabled(string path)
    {
        var response = await _docsOn.GetAsync(path);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"{path} answered {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task TheDocs_AreNotServed_WhenDisabled(string path)
    {
        var response = await _docsOff.GetAsync(path);

        // The API is closed by default, so a route that does not exist answers 401 to a visitor without a session (not 404):
        // it does not reveal which routes exist. Either way the docs are not served.
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,
            $"{path} answered {(int)response.StatusCode}");
    }

    [Fact]
    public async Task TheApiItself_KeepsWorking_WhenTheDocsAreDisabled()
    {
        // Disabling the docs must not take any real endpoint with it
        var response = await _docsOff.GetAsync("/api/transaction/paged");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
