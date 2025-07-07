using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Controllers
{
    [ApiController]
    [Route("api/v1/city/")]
    public class CiudadesController : ControllerBase
    {
        private readonly ICiudadService _ciudadService;

        public CiudadesController(ICiudadService ciudadService)
        {
            _ciudadService = ciudadService;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> LeerCiudades()
        {
            try
            {
                var ciudades = await _ciudadService.ListarCiudadesAsync();
                return Ok(new { error = false, ciudades });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las ciudades" });
            }

        }

        [AllowAnonymous]
        [HttpGet("listByProvince")]
        public async Task<IActionResult> LeerCiudadesPorProvincia(int provinciaId)
        {
            try
            {
                var ciudades = await _ciudadService.ListarCiudadesPorProvinciaAsync(provinciaId);
                return Ok(new { error = false, ciudades });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las ciudades" });
            }
        }
    }
}