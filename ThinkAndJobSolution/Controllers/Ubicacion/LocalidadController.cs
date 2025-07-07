using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Controllers
{
    [ApiController]
    [Route("api/v1/locality/")]
    public class LocalidadesController : ControllerBase
    {
        private readonly ILocalidadService _localidadService;

        public LocalidadesController(ILocalidadService localidadService)
        {
            _localidadService = localidadService;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> LeerLocalidades()
        {
            try
            {
                var localidades = await _localidadService.ListarLocalidadesAsync();
                return Ok(new { error = false, localidades });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las localidades" });
            }

        }

        [AllowAnonymous]
        [HttpGet("listByCity")]
        public async Task<IActionResult> LeerLocalidadesPorCiudad(int ciudadId)
        {
            try
            {
                var localidades = await _localidadService.ListarLocalidadesPorCiudadAsync(ciudadId);
                return Ok(new { error = false, localidades });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las localidades" });
            }
        }

        [AllowAnonymous]
        [HttpGet("listByProvince")]
        public async Task<IActionResult> LeerLocalidadesPorProvincia(int provinciaId)
        {
            try
            {
                var localidades = await _localidadService.ListarLocalidadesPorProvinciaAsync(provinciaId);
                return Ok(new { error = false, localidades });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las localidades" });
            }
        }

        [AllowAnonymous]
        [HttpGet("listByRegion")]
        public async Task<IActionResult> LeerLocalidadesPorRegion(int regionId)
        {
            try
            {
                var localidades = await _localidadService.ListarLocalidadesPorRegionAsync(regionId);
                return Ok(new { error = false, localidades });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las localidades" });
            }
        }
    }
}
