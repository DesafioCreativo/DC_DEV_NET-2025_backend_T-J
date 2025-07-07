using ThinkAndJobSolution.Controllers._Model;

namespace ThinkAndJobSolution.Interfaces
{
    public interface IRegionService
    {
        Task<List<Region>> ListarRegionesAsync();
    }
}
