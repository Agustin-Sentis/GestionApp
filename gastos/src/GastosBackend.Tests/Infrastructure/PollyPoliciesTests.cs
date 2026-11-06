using System.Diagnostics;
using System.Net;
using GastosBackend.Infrastructure.HttpResiliencePolicies;
using Microsoft.Extensions.Http;
using Polly.CircuitBreaker;
using Polly.Timeout;
using static GastosBackend.Infrastructure.HttpResiliencePolicies.HttpPollyPolicies;

namespace GastosBackend.Tests.Infrastructure;

/// <summary>
/// Builds the same handler chain as Program.cs (retry > circuit breaker > timeout > server)
/// around a fake server, with short times so the tests stay fast.
/// </summary>
public class PollyPoliciesTests
{
    private class FakeServer : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public FakeServer(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public List<long> CallTimesInMs { get; } = new();
        public int Calls => CallTimesInMs.Count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallTimesInMs.Add(_clock.ElapsedMilliseconds);
            return _respond(cancellationToken);
        }
    }

    private static ApiClientConfigurationDTO Config(
        int retryCount = 1, int baseDelayMs = 10, int timeoutMs = 1000, int failuresToBreak = 100, int breakSeconds = 30)
        => new ApiClientConfigurationDTO
        {
            RetryCount = retryCount,
            RetryBaseDelayInMilliseconds = baseDelayMs,
            TimeoutInMilliseconds = timeoutMs,
            HandledEventsAllowedBeforeBreaking = failuresToBreak,
            DurationOfBreakInSeconds = breakSeconds
        };

    private static HttpClient BuildClient(FakeServer server, ApiClientConfigurationDTO config)
    {
        var timeout = new PolicyHttpMessageHandler(PollyResiliencePolicies.GetTimeoutPolicy(config)) { InnerHandler = server };
        var breaker = new PolicyHttpMessageHandler(PollyResiliencePolicies.GetCircuitBreakerPolicy(config)) { InnerHandler = timeout };
        var retry = new PolicyHttpMessageHandler(PollyResiliencePolicies.GetRetryPolicy(config)) { InnerHandler = breaker };
        return new HttpClient(retry) { BaseAddress = new Uri("http://fake.test/") };
    }

    private static Task<HttpResponseMessage> Respond(HttpStatusCode status)
        => Task.FromResult(new HttpResponseMessage(status));

    [Fact]
    public async Task ASuccessfulCall_IsNotRetried()
    {
        var server = new FakeServer(_ => Respond(HttpStatusCode.OK));

        var response = await BuildClient(server, Config()).GetAsync("rate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, server.Calls);
    }

    [Fact]
    public async Task AServerError_IsRetriedOnce_AndThenReturned()
    {
        var server = new FakeServer(_ => Respond(HttpStatusCode.InternalServerError));

        var response = await BuildClient(server, Config(retryCount: 1)).GetAsync("rate");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, server.Calls); // the first attempt + 1 retry
    }

    [Fact]
    public async Task ATransientFailure_RecoversOnTheRetry()
    {
        var attempt = 0;
        var server = new FakeServer(_ => Respond(++attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));

        var response = await BuildClient(server, Config(retryCount: 1)).GetAsync("rate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, server.Calls);
    }

    [Fact]
    public async Task TheWaitBetweenRetries_DoublesEveryTime()
    {
        var server = new FakeServer(_ => Respond(HttpStatusCode.InternalServerError));

        await BuildClient(server, Config(retryCount: 2, baseDelayMs: 100)).GetAsync("rate");

        Assert.Equal(3, server.Calls);
        var firstWait = server.CallTimesInMs[1] - server.CallTimesInMs[0];
        var secondWait = server.CallTimesInMs[2] - server.CallTimesInMs[1];
        // Lower bounds only (the timer can fire late, never early): 100 ms, then 200 ms
        Assert.True(firstWait >= 90, $"first wait was {firstWait} ms");
        Assert.True(secondWait >= 190, $"second wait was {secondWait} ms");
    }

    [Fact]
    public async Task ASlowAttempt_IsCancelledAndRetried_ThenGivesUp()
    {
        // Never answers until it is cancelled
        var server = new FakeServer(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutRejectedException>(
            () => BuildClient(server, Config(retryCount: 1, baseDelayMs: 10, timeoutMs: 100)).GetAsync("rate"));

        Assert.Equal(2, server.Calls); // each attempt got its own timeout
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task TheCircuitOpens_AfterTheConfiguredFailures_AndStopsCallingTheServer()
    {
        var server = new FakeServer(_ => Respond(HttpStatusCode.InternalServerError));
        var client = BuildClient(server, Config(retryCount: 0, failuresToBreak: 3));

        for (var i = 0; i < 3; i++)
        {
            await client.GetAsync("rate");
        }
        Assert.Equal(3, server.Calls);

        // Any: the policy is generic, so it throws BrokenCircuitException<HttpResponseMessage>
        await Assert.ThrowsAnyAsync<BrokenCircuitException>(() => client.GetAsync("rate"));
        Assert.Equal(3, server.Calls); // the open circuit answered without calling the server
    }

    [Fact]
    public async Task TimedOutAttempts_CountTowardsOpeningTheCircuit()
    {
        var server = new FakeServer(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = BuildClient(server, Config(retryCount: 0, timeoutMs: 50, failuresToBreak: 2));

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.GetAsync("rate"));
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.GetAsync("rate"));
        Assert.Equal(2, server.Calls);

        await Assert.ThrowsAnyAsync<BrokenCircuitException>(() => client.GetAsync("rate"));
        Assert.Equal(2, server.Calls); // the third call never reached the server
    }

    [Fact]
    public async Task AnOpenCircuit_AnswersImmediately()
    {
        var server = new FakeServer(_ => Respond(HttpStatusCode.InternalServerError));
        var client = BuildClient(server, Config(retryCount: 0, failuresToBreak: 1));
        await client.GetAsync("rate");

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<BrokenCircuitException>(() => client.GetAsync("rate"));

        Assert.True(stopwatch.ElapsedMilliseconds < 200, $"took {stopwatch.ElapsedMilliseconds} ms");
    }
}
