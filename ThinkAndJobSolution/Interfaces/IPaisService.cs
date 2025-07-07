using ThinkAndJobSolution.Controllers._Model;

namespace ThinkAndJobSolution.Interfaces
{
    public interface IProvinciaService
    {
        Task<List<Provincia>> ListarProvinciasAsync();
        Task<List<Provincia>> ListarProvinciasPorRegionAsync(int regionId);
    }
}
