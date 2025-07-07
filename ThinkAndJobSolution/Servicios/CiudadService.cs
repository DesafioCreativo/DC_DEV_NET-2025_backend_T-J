using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Servicios
{
    public class CiudadService : ICiudadService
    {
        private readonly string _connectionString;

        public CiudadService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<List<Ciudad>> ListarCiudadesAsync()
        {
            const string query = "SELECT * FROM const_cities ORDER BY name";
            return await ObtenerCiudadsAsync(query, null);
        }

        public async Task<List<Ciudad>> ListarCiudadesPorProvinciaAsync(int provinciaId)
        {
            const string query = "SELECT * FROM const_cities WHERE parent = @provinciaId ORDER BY name";
            var parametros = new Dictionary<string, object> { { "@provinciaId", provinciaId } };
            return await ObtenerCiudadsAsync(query, parametros);
        }

        private async Task<List<Ciudad>> ObtenerCiudadsAsync(string query, Dictionary<string, object> parametros)
        {
            List<Ciudad> ciudades = new();

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
                ciudades.Add(new Ciudad
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

            return ciudades;
        }
    }
}
