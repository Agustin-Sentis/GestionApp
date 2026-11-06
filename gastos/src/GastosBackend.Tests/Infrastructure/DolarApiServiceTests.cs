using System.Collections.Concurrent;
using System.Net;
using System.Text;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.Exceptions;
using GastosBackend.Domain.Enums;
using GastosBackend.Infrastructure.ExternalService;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GastosBackend.Tests.Infrastructure;

public class DolarApiServiceTests
{
    /// <summary>A fake dolarapi.com: answers each "casa" with the body the test gives it, and remembers what was asked.</summary>
    private sealed class FakeDolarApi : HttpMessageHandler
    {
        private readonly Func<string, HttpResponseMessage> _answer;
        public ConcurrentQueue<string> Paths { get; } = new();

        public FakeDolarApi(Func<string, HttpResponseMessage> answer)
        {
            _answer = answer;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Enqueue(path);
            return Task.FromResult(_answer(path));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Rate(decimal sell) => Json($$"""{ "casa": "x", "compra": 1, "venta": {{sell.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }""");

    private static (DolarApiService Service, FakeDolarApi Api) Create(Func<string, HttpResponseMessage> answer)
    {
        var api = new FakeDolarApi(answer);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(DolarApiService.HttpClientName))
            .Returns(() => new HttpClient(api) { BaseAddress = new Uri("https://dolarapi.test/") });

        var service = new DolarApiService(
            factory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new DolarApiOptions { CacheMinutes = 15 }),
            NullLogger<DolarApiService>.Instance);
        return (service, api);
    }

    /// <summary>The usual answers: oficial 1570, blue 1555, bolsa (MEP) 1582,2.</summary>
    private static HttpResponseMessage Usual(string path) => path switch
    {
        "/v1/dolares/oficial" => Rate(1570m),
        "/v1/dolares/blue" => Rate(1555m),
        "/v1/dolares/bolsa" => Rate(1582.2m),
        _ => Json("{}", HttpStatusCode.NotFound)
    };

    [Theory]
    [InlineData(DollarType.Official, "oficial")]
    [InlineData(DollarType.Blue, "blue")]
    [InlineData(DollarType.Mep, "bolsa")] // dolarapi.com calls the MEP dollar "bolsa"
    public void NamesEachDollarTheWayDolarApiDoes(DollarType type, string casa)
    {
        Assert.Equal(casa, DolarApiService.CasaOf(type));
    }

    [Theory]
    [InlineData(DollarType.Official, "/v1/dolares/oficial", 1570)]
    [InlineData(DollarType.Blue, "/v1/dolares/blue", 1555)]
    [InlineData(DollarType.Mep, "/v1/dolares/bolsa", 1582.2)]
    public async Task GetUsdSellRate_AsksForTheRightDollar_AndReturnsItsSellRate(DollarType type, string path, double expected)
    {
        var (service, api) = Create(Usual);

        var rate = await service.GetUsdSellRate(type);

        Assert.Equal((decimal)expected, rate);
        Assert.Equal(new[] { path }, api.Paths.ToArray());
    }

    [Fact]
    public async Task GetUsdSellRate_RemembersTheAnswerOfEachDollarSeparately()
    {
        var (service, api) = Create(Usual);

        await service.GetUsdSellRate(DollarType.Blue);
        await service.GetUsdSellRate(DollarType.Blue); // from the cache
        await service.GetUsdSellRate(DollarType.Official); // another dollar: not the blue one's answer
        var blueAgain = await service.GetUsdSellRate(DollarType.Blue);

        Assert.Equal(1555m, blueAgain);
        Assert.Equal(new[] { "/v1/dolares/blue", "/v1/dolares/oficial" }, api.Paths.ToArray());
    }

    [Theory]
    [InlineData("""{ "venta": 0 }""")]
    [InlineData("""{ "venta": -5 }""")]
    [InlineData("{}")]
    public async Task GetUsdSellRate_Throws_WhenTheRateComesEmptyOrInvalid(string body)
    {
        var (service, _) = Create(_ => Json(body));

        await Assert.ThrowsAsync<ExternalServiceException>(() => service.GetUsdSellRate(DollarType.Blue));
    }

    [Fact]
    public async Task GetUsdSellRate_Throws_WhenTheServiceAnswersWithAnError()
    {
        var (service, _) = Create(_ => Json("{}", HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<ExternalServiceException>(() => service.GetUsdSellRate(DollarType.Mep));
    }

    [Fact]
    public async Task GetUsdSellRate_DoesNotRemember_ARateThatFailed()
    {
        var calls = 0;
        var (service, api) = Create(path =>
        {
            calls++;
            return calls == 1 ? Json("{}", HttpStatusCode.ServiceUnavailable) : Usual(path);
        });

        await Assert.ThrowsAsync<ExternalServiceException>(() => service.GetUsdSellRate(DollarType.Blue));
        var second = await service.GetUsdSellRate(DollarType.Blue);

        Assert.Equal(1555m, second); // a failure is not cached: the next request tries again
        Assert.Equal(2, api.Paths.Count);
    }

    [Fact]
    public async Task GetUsdSellRates_ReturnsTheThreeDollars_WithOneRequestEach()
    {
        var (service, api) = Create(Usual);

        var rates = await service.GetUsdSellRates();

        Assert.Equal(1570m, rates.Official);
        Assert.Equal(1555m, rates.Blue);
        Assert.Equal(1582.2m, rates.Mep);
        // One request per dollar. They run at the same time, so the order they arrive in is not part of the promise
        var expected = new[] { "/v1/dolares/blue", "/v1/dolares/bolsa", "/v1/dolares/oficial" };
        Assert.Equal(expected, api.Paths.OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task GetUsdSellRates_ReturnsNullForTheOneThatFails_AndKeepsTheOthers()
    {
        var (service, _) = Create(path => path.EndsWith("/blue") ? Json("{}", HttpStatusCode.BadGateway) : Usual(path));

        var rates = await service.GetUsdSellRates();

        Assert.Equal(1570m, rates.Official);
        Assert.Null(rates.Blue); // one failure must not take the other two down with it
        Assert.Equal(1582.2m, rates.Mep);
    }

    [Fact]
    public async Task GetUsdSellRates_ReturnsNothing_WithoutThrowing_WhenEverythingFails()
    {
        var (service, _) = Create(_ => Json("{}", HttpStatusCode.ServiceUnavailable));

        var rates = await service.GetUsdSellRates();

        Assert.Null(rates.Official);
        Assert.Null(rates.Blue);
        Assert.Null(rates.Mep);
    }
}
