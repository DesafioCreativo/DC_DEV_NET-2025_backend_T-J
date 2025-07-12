using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;
using ThinkAndJobSolution.Utils;

namespace ThinkAndJobSolution.Controllers
{
    [ApiController]
    //[Authorize]
    [Route("api/v1/category/")]
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaService _categoriaService;

        public CategoriasController(ICategoriaService categoriaService)
        {
            _categoriaService = categoriaService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CrearCategoria([FromBody] Categoria categoria)
        {
            if (categoria == null)
            {
                return BadRequest(new { error = "Error 4001, la información de la categoría es inválida o nula." });
            }

            try
            {
                return await _categoriaService.CrearCategoriaAsync(categoria);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Error 5811, no se ha podido crear la categoría." });
            }
        }


        [HttpPost("update")]
        public async Task<IActionResult> EditarCategoria([FromBody] Categoria categoria)
        {
            if (categoria == null)
            {
                return BadRequest(new { error = "Error 4001, la información de la categoría es inválida o nula." });
            }

            try
            {
                return await _categoriaService.EditarCategoriaAsync(categoria);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Error 5811, no se ha podido editar la categoría." });
            }
        }


        [HttpDelete("delete")]
        public async Task<IActionResult> EliminarCategoria(int categoriaId)
        {
            try
            {
                return await _categoriaService.EliminarCategoriaAsync(categoriaId);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Error 5811, no se ha podido eliminar la categoría." });
            }
        }


        [HttpGet]
        [Route("getById")]
        public async Task<IActionResult> LeerCategoriaPorId(string categoriaId)
        {
            try
            {
                var categoria = await _categoriaService.LeerCategoriaPorIdAsync(categoriaId);
                return Ok(new { error = false, categoria });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se ha podido leer la categoria" });
            }
        }


        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> LeerCategorias()
        {
            try
            {
                var categorias = await _categoriaService.ListarCategoriasAsync();
                return Ok(new { error = false, categorias });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se han podido listar las categorias" });
            }
        }
    }
}