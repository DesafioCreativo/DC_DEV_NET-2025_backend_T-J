using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Servicios
{
    public class RegionService : IRegionService
    {
        private readonly string _connectionString;

        public RegionService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<List<Region>> ListarRegionesAsync()
        {
            const string query = "SELECT * FROM const_regions ORDER BY name";
            var regiones = new List<Region>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var command = conn.CreateCommand();
            command.CommandText = query;

            using var reader = await command.ExecuteReaderAsync();

            int idxId = reader.GetOrdinal("id");
            int idxName = reader.GetOrdinal("name");
            int idxIntegrationId = reader.GetOrdinal("integration_id");
            int idxApiId = reader.GetOrdinal("api_id");
            int idxCode = reader.GetOrdinal("code");
            int idxTimezone = reader.GetOrdinal("timezone");
            int idxNameDt = reader.GetOrdinal("name_dt");
            int idxParent = reader.GetOrdinal("parent");
            int idxStatus = reader.GetOrdinal("status");

            while (await reader.ReadAsync())
            {
                regiones.Add(new Region
                {
                    id = reader.IsDBNull(idxId) ? 0 : reader.GetInt32(idxId),
                    name = reader.IsDBNull(idxName) ? "" : reader.GetString(idxName),
                    integration_id = reader.IsDBNull(idxIntegrationId) ? "" : reader.GetString(idxIntegrationId),
                    api_id = reader.IsDBNull(idxApiId) ? 0 : reader.GetInt32(idxApiId),
                    code = reader.IsDBNull(idxCode) ? "" : reader.GetString(idxCode),
                    timezone = reader.IsDBNull(idxTimezone) ? "" : reader.GetString(idxTimezone),
                    name_dt = reader.IsDBNull(idxNameDt) ? "" : reader.GetString(idxNameDt),
                    parent = reader.IsDBNull(idxParent) ? 0 : reader.GetInt32(idxParent),
                    status = reader.IsDBNull(idxStatus) ? 0 : (int)reader.GetByte(idxStatus),
                });
            }

            return regiones;
        }
    }
}
