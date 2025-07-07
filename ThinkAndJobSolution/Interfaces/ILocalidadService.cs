using ThinkAndJobSolution.Controllers._Model;

namespace ThinkAndJobSolution.Interfaces
{
    public interface ILocalidadService
    {
        Task<List<Localidad>> ListarLocalidadesAsync();
        Task<List<Localidad>> ListarLocalidadesPorCiudadAsync(int ciudadId);
        Task<List<Localidad>> ListarLocalidadesPorProvinciaAsync(int provinciaId);
        Task<List<Localidad>> ListarLocalidadesPorRegionAsync(int regionId);
    }
}
