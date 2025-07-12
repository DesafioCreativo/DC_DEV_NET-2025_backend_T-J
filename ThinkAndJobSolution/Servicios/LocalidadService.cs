using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;

namespace ThinkAndJobSolution.Servicios
{
    public class LocalidadService : ILocalidadService
    {
        private readonly string _connectionString;

        public LocalidadService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<List<Localidad>> ListarLocalidadesAsync()
        {
            const string query = "SELECT * FROM const_localidades ORDER BY nombre";
            return await ObtenerLocalidadesAsync(query, null);
        }

        public async Task<List<Localidad>> ListarLocalidadesPorProvinciaAsync(int provinciaId)
        {
            const string query = @"SELECT * FROM const_localidades
                WHERE const_localidades.parent = @provinciaId ORDER BY const_localidades.nombre";
            var parametros = new Dictionary<string, object> { { "@provinciaId", provinciaId } };
            return await ObtenerLocalidadesAsync(query, parametros);
        }

        public async Task<List<Localidad>> ListarLocalidadesPorRegionAsync(int regionId)
        {
            const string query = @"SELECT * FROM const_localidades
                left join const_provincias on const_localidades.parent = const_provincias.api_id
                WHERE const_provincias.parent = @regionId ORDER BY const_localidades.nombre";
            var parametros = new Dictionary<string, object> { { "@regionId", regionId } };
            return await ObtenerLocalidadesAsync(query, parametros);
        }

        private async Task<List<Localidad>> ObtenerLocalidadesAsync(string query, Dictionary<string, object> parametros)
        {
            List<Localidad> localidades = new();

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

            int idxId = reader.GetOrdinal("ref");
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
                localidades.Add(new Localidad
                {

                    id = reader.IsDBNull(idxId) ? 0 : reader.GetInt32(idxId),
                    nombre = reader.IsDBNull(idxNombre) ? "" : reader.GetString(idxNombre),
                    integration_id = reader.IsDBNull(idxIntegrationId) ? 0 : reader.GetInt32(idxIntegrationId),
                    api_id = reader.IsDBNull(idxApiId) ? 0 : reader.GetInt32(idxApiId),
                    code = reader.IsDBNull(idxCode) ? "" : reader.GetString(idxCode),
                    timezone = reader.IsDBNull(idxTimezone) ? "" : reader.GetString(idxTimezone),
                    name_dt = reader.IsDBNull(idxNameDt) ? "" : reader.GetString(idxNameDt),
                    parent = reader.IsDBNull(idxParent) ? 0 : reader.GetInt32(idxParent),
                    status = reader.IsDBNull(idxStatus) ? 0 : reader.GetInt32(idxStatus),
                });
            }

            return localidades;
        }
    }
}
