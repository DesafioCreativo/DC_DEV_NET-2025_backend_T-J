using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Controllers
{
    [ApiController]
    [Route("api/v1/country/")]
    public class PaisesController : ControllerBase
    {
        private readonly IPaisService _paisService;

        public PaisesController(IPaisService paisService)
        {
            _paisService = paisService;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> LeerPaises()
        {
            try
            {
                var paises = await _paisService.ListarPaisesAsync();
                return Ok(new { error = false, paises });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar los países" });
            }
        }
    }
}
