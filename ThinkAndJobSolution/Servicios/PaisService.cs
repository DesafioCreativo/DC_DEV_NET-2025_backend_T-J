using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Servicios
{
    public class PaisService : IPaisService
    {
        private readonly string _connectionString;

        public PaisService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<List<Pais>> ListarPaisesAsync()
        {
            const string query = "SELECT * FROM const_paises ORDER BY nombre";
            var paises = new List<Pais>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var command = conn.CreateCommand();
            command.CommandText = query;

            using var reader = await command.ExecuteReaderAsync();

            int idxIso3 = reader.GetOrdinal("iso3");
            int idxIso2 = reader.GetOrdinal("iso2");
            int idxCodigo = reader.GetOrdinal("codigo");
            int idxNombre = reader.GetOrdinal("nombre");
            int idxSchengen = reader.GetOrdinal("schengen");

            while (await reader.ReadAsync())
            {
                paises.Add(new Pais
                {
                    iso3 = reader.IsDBNull(idxIso3) ? "" : reader.GetString(idxIso3),
                    iso2 = reader.IsDBNull(idxIso2) ? "" : reader.GetString(idxIso2),
                    codigo = reader.IsDBNull(idxCodigo) ? 0 : reader.GetInt32(idxCodigo),
                    nombre = reader.IsDBNull(idxNombre) ? "" : reader.GetString(idxNombre),
                    schengen = !reader.IsDBNull(idxSchengen) && reader.GetBoolean(idxSchengen)
                });
            }

            return paises;
        }
    }
}
