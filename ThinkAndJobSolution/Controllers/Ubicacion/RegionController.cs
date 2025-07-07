using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Controllers
{
    [ApiController]
    [Route("api/v1/region/")]
    public class RegionController : ControllerBase
    {
        private readonly IRegionService _regionService;

        public RegionController(IRegionService regionService)
        {
            _regionService = regionService;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> LeerRegiones()
        {
            try
            {
                var regiones = await _regionService.ListarRegionesAsync();
                return Ok(new { error = false, regiones });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las regiones" });
            }
        }
    }
}