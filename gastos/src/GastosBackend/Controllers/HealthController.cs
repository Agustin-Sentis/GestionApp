using GastosBackend.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// Default for how long the database may take to answer before the API is reported as unhealthy.
        /// It must leave room for opening a brand-new connection: the check cancels the attempt when the time is up, so a
        /// limit shorter than a slow connection setup (for example right after the database restarted) would never succeed.
        /// </summary>
        private const int DefaultDatabaseTimeoutSeconds = 8;

        private readonly GastosDbContext _context;
        private readonly TimeSpan _databaseTimeout;

        public HealthController(GastosDbContext context, IConfiguration configuration)
        {
            _context = context;
            _databaseTimeout = TimeSpan.FromSeconds(
                configuration.GetValue("Health:DatabaseTimeoutSeconds", DefaultDatabaseTimeoutSeconds));
        }

        /// <summary>
        /// This endpoint is used to check the health of the Gastos API. It returns 200 when the API can reach its database
        /// and 503 when it cannot, so a hosting platform can restart or stop routing traffic to an instance that is not working.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_databaseTimeout);

            bool databaseIsUp;
            try
            {
                databaseIsUp = await _context.Database.CanConnectAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Our own time limit expired: the database is too slow to count as healthy
                databaseIsUp = false;
            }

            return databaseIsUp
                ? Ok(new { status = "healthy" })
                : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unhealthy" });
        }
    }
}
