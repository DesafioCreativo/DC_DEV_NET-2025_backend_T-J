using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Controllers._Model;

namespace ThinkAndJobSolution.Interfaces
{
    public interface ICategoriaService
    {
        Task<IActionResult> CrearCategoriaAsync(Categoria categoria);
        Task<IActionResult> EditarCategoriaAsync(Categoria categoria);
        Task<IActionResult> EliminarCategoriaAsync(int categoryId);
        Task<Categoria?> LeerCategoriaPorIdAsync(string categoriaId);
        Task<List<Categoria>> ListarCategoriasAsync();
    }
}
