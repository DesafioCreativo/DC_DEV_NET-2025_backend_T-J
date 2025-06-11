using Microsoft.AspNetCore.Mvc;

namespace ThinkAndJobSolution.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {

        [HttpPost("index")]
        public IActionResult Index()
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
