using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Controllers
{
    [ApiController]
    [Route("api/v1/province/")]
    public class ProvinciasController : ControllerBase
    {
        private readonly IProvinciaService _provinciaService;

        public ProvinciasController(IProvinciaService provinciaService)
        {
            _provinciaService = provinciaService;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> LeerProvincias()
        {
            try
            {
                var provincias = await _provinciaService.ListarProvinciasAsync();
                return Ok(new { error = false, provincias });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las provincias" });
            }

        }

        [AllowAnonymous]
        [HttpGet("listByRegion")]
        public async Task<IActionResult> LeerProvinciasPorRegion(int regionId)
        {
            try
            {
                var provincias = await _provinciaService.ListarProvinciasPorRegionAsync(regionId);
                return Ok(new { error = false, provincias });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las provincias" });
            }
        }
    }
}