using System.Text.Json;
using GastosBackend.Application.Exceptions;

namespace GastosBackend.Presentation.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // El cliente cortó la conexión: no hay a quién responder
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            int statusCode;
            string message;
            int? retryAfterSeconds = null;

            switch (exception)
            {
                case ValidationException:
                    statusCode = StatusCodes.Status400BadRequest;
                    message = exception.Message;
                    break;

                case UnauthorizedException:
                    statusCode = StatusCodes.Status401Unauthorized;
                    message = exception.Message;
                    break;

                case NotFoundException:
                    statusCode = StatusCodes.Status404NotFound;
                    message = exception.Message;
                    break;

                case ConflictException:
                    statusCode = StatusCodes.Status409Conflict;
                    message = exception.Message;
                    break;

                case TooManyRequestsException tooMany:
                    statusCode = StatusCodes.Status429TooManyRequests;
                    message = exception.Message;
                    retryAfterSeconds = tooMany.RetryAfterSeconds;
                    context.Response.Headers.RetryAfter = tooMany.RetryAfterSeconds.ToString();
                    break;

                case ExternalServiceException:
                    _logger.LogWarning(exception, "Falló un servicio externo");
                    statusCode = StatusCodes.Status503ServiceUnavailable;
                    message = exception.Message;
                    break;

                default:
                    _logger.LogError(exception, "Excepción no controlada");
                    statusCode = StatusCodes.Status500InternalServerError;
                    message = "Ocurrió un error inesperado.";
                    break;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            // The seconds are only in the body of a 429, so the screen can say how long to wait
            var body = retryAfterSeconds is { } seconds
                ? JsonSerializer.Serialize(new { error = message, retryAfterSeconds = seconds })
                : JsonSerializer.Serialize(new { error = message });
            return context.Response.WriteAsync(body);
        }
    }
}
