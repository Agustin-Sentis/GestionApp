using System.Net;
using System.Text;
using System.Text.Json;
using GastosBackend.Application.Configuration;
using GastosBackend.Application.dtos;
using GastosBackend.Application.Exceptions;
using GastosBackend.Infrastructure.ExternalService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GastosBackend.Tests.Infrastructure;

public class BrevoEmailSenderTests
{
    private const string ApiKey = "xkeysib-super-secret-key";

    /// <summary>A fake Brevo: answers with what the test says and remembers the request it got.</summary>
    private sealed class FakeBrevo : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _answer;
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? ApiKeyHeader { get; private set; }
        public JsonElement Body { get; private set; }

        public FakeBrevo(Func<HttpRequestMessage, HttpResponseMessage> answer)
        {
            _answer = answer;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            ApiKeyHeader = request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null;
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
            return _answer(request);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Lines.Add(formatter(state, exception));
            if (exception != null) Lines.Add(exception.ToString());
        }
    }

    private static readonly EmailMessage Message = new("ana@example.com", "Asunto", "texto", "<p>html</p>");

    private static (BrevoEmailSender Sender, FakeBrevo Api, CapturingLogger<BrevoEmailSender> Log) Create(
        Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var api = new FakeBrevo(answer);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(BrevoEmailSender.HttpClientName))
            .Returns(() => new HttpClient(api) { BaseAddress = new Uri("https://brevo.test/") });

        var log = new CapturingLogger<BrevoEmailSender>();
        var options = Options.Create(new EmailOptions { ApiKey = ApiKey, SenderEmail = "yo@gmail.com", SenderName = "DualBalance" });
        return (new BrevoEmailSender(factory.Object, options, log), api, log);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string body = "{}") =>
        new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Send_PostsToBrevoWithTheApiKeyAndTheMessage()
    {
        var (sender, api, _) = Create(_ => Answer(HttpStatusCode.Created, """{ "messageId": "<1@x>" }"""));

        await sender.Send(Message);

        Assert.Equal(HttpMethod.Post, api.Method);
        Assert.Equal("/v3/smtp/email", api.Path);
        Assert.Equal(ApiKey, api.ApiKeyHeader);
        Assert.Equal("DualBalance", api.Body.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("yo@gmail.com", api.Body.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("ana@example.com", api.Body.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Asunto", api.Body.GetProperty("subject").GetString());
        Assert.Equal("<p>html</p>", api.Body.GetProperty("htmlContent").GetString());
        Assert.Equal("texto", api.Body.GetProperty("textContent").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Send_Throws_ExternalServiceException_WhenBrevoRefuses(HttpStatusCode status)
    {
        var (sender, _, log) = Create(_ => Answer(status, """{ "code": "unauthorized", "message": "Key not found" }"""));

        await Assert.ThrowsAsync<ExternalServiceException>(() => sender.Send(Message));

        // What went wrong is logged, so a dead key is found quickly
        Assert.Contains(log.Lines, line => line.Contains((int)status + "") && line.Contains("Asunto"));
    }

    [Fact]
    public async Task Send_Throws_ExternalServiceException_OnNetworkFailure()
    {
        var (sender, _, _) = Create(_ => throw new HttpRequestException("no route to host"));

        await Assert.ThrowsAsync<ExternalServiceException>(() => sender.Send(Message));
    }

    [Fact]
    public async Task Send_Throws_ExternalServiceException_OnTimeout()
    {
        var (sender, _, _) = Create(_ => throw new TaskCanceledException("timed out"));

        await Assert.ThrowsAsync<ExternalServiceException>(() => sender.Send(Message));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.OK)]
    public async Task Send_NeverLogsTheApiKeyOrTheBodyOfTheMail(HttpStatusCode status)
    {
        var (sender, _, log) = Create(_ => Answer(status));

        try { await sender.Send(Message); } catch (ExternalServiceException) { }

        Assert.DoesNotContain(log.Lines, line => line.Contains(ApiKey));
        Assert.DoesNotContain(log.Lines, line => line.Contains("<p>html</p>"));
    }

    [Fact]
    public async Task NoOpEmailSender_DoesNotThrow_AndLogsOnlyTheSubject()
    {
        var log = new CapturingLogger<NoOpEmailSender>();

        await new NoOpEmailSender(log).Send(new EmailMessage("ana@example.com", "Asunto", "enlace secreto", "<a>enlace secreto</a>"));

        Assert.Contains(log.Lines, line => line.Contains("Asunto"));
        Assert.DoesNotContain(log.Lines, line => line.Contains("enlace secreto"));
    }
}
