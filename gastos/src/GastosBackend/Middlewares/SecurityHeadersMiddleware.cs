namespace GastosBackend.Presentation.Middlewares
{
    /// <summary>
    /// Adds the security headers to every response of the API (<c>/api/*</c>): the browser must not guess content types, must not
    /// send the address of the app as a referrer, and nothing private (a session token, the data of a user) may stay in a cache.
    /// The API docs page is left alone: it brings its own scripts and styles.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                // Set when the response starts, so it also covers the answers of the middleware that run after this one
                // (a 401, a 429 from the rate limiter, an error)
                context.Response.OnStarting(() =>
                {
                    var headers = context.Response.Headers;
                    headers["X-Content-Type-Options"] = "nosniff";
                    headers["Referrer-Policy"] = "no-referrer";
                    headers["Cache-Control"] = "no-store";
                    return Task.CompletedTask;
                });
            }

            return _next(context);
        }
    }
}
