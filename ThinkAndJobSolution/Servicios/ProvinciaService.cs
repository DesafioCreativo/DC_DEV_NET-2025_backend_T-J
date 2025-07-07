using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Servicios
{
    public class ProvinciaService : IProvinciaService
    {
        private readonly string _connectionString;

        public ProvinciaService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<List<Provincia>> ListarProvinciasAsync()
        {
            const string query = "SELECT * FROM const_provincias ORDER BY nombre";
            return await ObtenerProvinciasAsync(query, null);
        }

        public async Task<List<Provincia>> ListarProvinciasPorRegionAsync(int regionId)
        {
            const string query = "SELECT * FROM const_provincias WHERE parent = @regionId ORDER BY nombre";
            var parametros = new Dictionary<string, object> { { "@regionId", regionId } };
            return await ObtenerProvinciasAsync(query, parametros);
        }

        private async Task<List<Provincia>> ObtenerProvinciasAsync(string query, Dictionary<string, object> parametros)
        {
            List<Provincia> provincias = new();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var command = conn.CreateCommand();
            command.CommandText = query;

            if (parametros != null)
            {
                foreach (var param in parametros)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            using var reader = await command.ExecuteReaderAsync();

            int idxRef = reader.GetOrdinal("ref");
            int idxNombre = reader.GetOrdinal("nombre");
            int idxIntegrationId = reader.GetOrdinal("integration_id");
            int idxApiId = reader.GetOrdinal("api_id");
            int idxCode = reader.GetOrdinal("code");
            int idxTimezone = reader.GetOrdinal("timezone");
            int idxNameDt = reader.GetOrdinal("name_dt");
            int idxParent = reader.GetOrdinal("parent");
            int idxStatus = reader.GetOrdinal("status");

            while (await reader.ReadAsync())
            {
                provincias.Add(new Provincia
                {
                    id = reader.IsDBNull(idxRef) ? 0 : reader.GetInt32(idxRef),
                    nombre = reader.IsDBNull(idxNombre) ? "" : reader.GetString(idxNombre),
                    integration_id = reader.IsDBNull(idxIntegrationId) ? "" : reader.GetString(idxIntegrationId),
                    api_id = reader.IsDBNull(idxApiId) ? 0 : reader.GetInt32(idxApiId),
                    code = reader.IsDBNull(idxCode) ? "" : reader.GetString(idxCode),
                    timezone = reader.IsDBNull(idxTimezone) ? "" : reader.GetString(idxTimezone),
                    name_dt = reader.IsDBNull(idxNameDt) ? "" : reader.GetString(idxNameDt),
                    parent = reader.IsDBNull(idxParent) ? 0 : reader.GetInt32(idxParent),
                    status = reader.IsDBNull(idxStatus) ? 0 : (int)reader.GetByte(idxStatus),
                });
            }

            return provincias;
        }
    }
}
