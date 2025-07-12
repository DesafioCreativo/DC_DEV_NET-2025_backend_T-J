using ThinkAndJobSolution.Controllers._Model;

namespace ThinkAndJobSolution.Interfaces
{
    public interface IPaisService
    {
        Task<List<Pais>> ListarPaisesAsync();
    }
}
