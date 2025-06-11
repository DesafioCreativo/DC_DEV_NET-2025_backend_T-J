using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ThinkAndJobSolution.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {

        [HttpGet("clima")]
        public IActionResult Clima()
        {
            var weatherData = new[]
            {
                new { Date = "2024-09-16", TemperatureC = 25, Summary = "Warm" },
                new { Date = "2024-09-17", TemperatureC = 20, Summary = "Cool" }
            };
            return Ok(weatherData);
        }
    }
}
