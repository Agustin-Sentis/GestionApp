using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GastosBackend.Tests.Integration;

/// <summary>HSTS and AllowedHosts: the two production settings that depend on the request's scheme and Host header.</summary>
public class ProductionHardeningTests :
    IClassFixture<ProductionHardeningTests.HstsOnFactory>,
    IClassFixture<ProductionHardeningTests.HstsOffFactory>,
    IClassFixture<ProductionHardeningTests.RestrictedHostsFactory>
{
    private const string ApiHost = "gestionapp-j0j4.onrender.com";

    public class HstsOnFactory : TestApiFactory
    {
        protected override IReadOnlyDictionary<string, string> ExtraSettings => new Dictionary<string, string>
        {
            ["Https:Hsts:Enabled"] = "true",
            ["Https:Hsts:MaxAgeDays"] = "45"
        };
    }

    public class HstsOffFactory : TestApiFactory
    {
    }

    public class RestrictedHostsFactory : TestApiFactory
    {
        protected override IReadOnlyDictionary<string, string> ExtraSettings => new Dictionary<string, string>
        {
            ["AllowedHosts"] = ApiHost
        };
    }

    private readonly HstsOnFactory _hstsOn;
    private readonly HstsOffFactory _hstsOff;
    private readonly RestrictedHostsFactory _restricted;

    public ProductionHardeningTests(HstsOnFactory hstsOn, HstsOffFactory hstsOff, RestrictedHostsFactory restricted)
    {
        _hstsOn = hstsOn;
        _hstsOff = hstsOff;
        _restricted = restricted;
    }

    private static HttpClient ClientFor(TestApiFactory factory, string baseAddress)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseAddress) });

    private static bool TryGetHsts(HttpResponseMessage response, out string? value)
    {
        var found = response.Headers.TryGetValues("Strict-Transport-Security", out var values);
        value = values?.SingleOrDefault();
        return found;
    }

    [Fact]
    public async Task Hsts_IsSentOverHttps_WhenEnabled_WithTheConfiguredMaxAge()
    {
        var response = await ClientFor(_hstsOn, $"https://{ApiHost}").GetAsync("/api/transaction/paged");

        Assert.True(TryGetHsts(response, out var hsts), "no Strict-Transport-Security header");
        Assert.Equal("max-age=3888000", hsts); // 45 days
    }

    [Fact]
    public async Task Hsts_IsNotSentOverPlainHttp_EvenWhenEnabled()
    {
        var response = await ClientFor(_hstsOn, $"http://{ApiHost}").GetAsync("/api/transaction/paged");

        Assert.False(TryGetHsts(response, out _));
    }

    [Fact]
    public async Task Hsts_IsNotSentForLocalhost_EvenWhenEnabled()
    {
        // So a developer's browser is never locked into HTTPS for localhost
        var response = await ClientFor(_hstsOn, "https://localhost").GetAsync("/api/transaction/paged");

        Assert.False(TryGetHsts(response, out _));
    }

    [Fact]
    public async Task Hsts_IsNotSent_WhenDisabled()
    {
        var response = await ClientFor(_hstsOff, $"https://{ApiHost}").GetAsync("/api/transaction/paged");

        Assert.False(TryGetHsts(response, out _));
    }

    [Fact]
    public async Task ARequestForTheApiDomain_IsAnswered_WhenHostsAreRestricted()
    {
        var response = await ClientFor(_restricted, $"https://{ApiHost}").GetAsync("/api/transaction/paged");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); // reached the API: only the token is missing
    }

    [Theory]
    [InlineData("https://gestionapp-frontend-delta.vercel.app")]
    [InlineData("https://evil.example.com")]
    public async Task ARequestForAnyOtherHost_IsRejected_WhenHostsAreRestricted(string baseAddress)
    {
        var response = await ClientFor(_restricted, baseAddress).GetAsync("/api/transaction/paged");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnyHost_IsAccepted_ByDefault()
    {
        var response = await ClientFor(_hstsOff, "https://evil.example.com").GetAsync("/api/transaction/paged");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
