using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// This endpoint is used to check the health of the Gastos API. It returns a simple message indicating that the API is running.
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Gastos API is running!");
        }
    }
}
