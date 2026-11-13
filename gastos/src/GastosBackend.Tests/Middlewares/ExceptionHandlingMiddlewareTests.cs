using System.Text.Json;
using GastosBackend.Application.Exceptions;
using GastosBackend.Presentation.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastosBackend.Tests.Middlewares;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(DefaultHttpContext Context, JsonElement Body)> Run(Exception thrown)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw thrown, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        return (context, body);
    }

    [Fact]
    public async Task TooManyRequests_Is429_WithTheMessage_TheRetryHeader_AndTheSecondsInTheBody()
    {
        var (context, body) = await Run(new TooManyRequestsException(754));

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("754", context.Response.Headers.RetryAfter.ToString());
        Assert.Equal(754, body.GetProperty("retryAfterSeconds").GetInt32());
        Assert.Contains("13 minutos", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ValidationErrors_StillHaveOnlyTheErrorField()
    {
        var (context, body) = await Run(new ValidationException("mal"));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("mal", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("retryAfterSeconds", out _));
    }
}
