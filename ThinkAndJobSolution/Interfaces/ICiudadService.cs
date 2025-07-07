using ThinkAndJobSolution.Controllers._Model;

namespace ThinkAndJobSolution.Interfaces
{
    public interface ICiudadService
    {
        Task<List<Ciudad>> ListarCiudadesAsync();
        Task<List<Ciudad>> ListarCiudadesPorProvinciaAsync(int provinciaId);
    }
}
