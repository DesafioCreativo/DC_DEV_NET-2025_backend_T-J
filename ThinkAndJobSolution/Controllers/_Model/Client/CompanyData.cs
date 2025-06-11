using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Helper;

namespace ThinkAndJobSolution.Controllers._Model.Client
{
    public class CompanyData
    {
        public string id { get; set; }
        public string nombre { get; set; }
        public string cif { get; set; }
        public string icon { get; set; }
        public string companyGroupId { get; set; }
        public string companyGroupName { get; set; }

        //Relaciones
        public List<CentroData> centros { get; set; }

        //Adicional
        public string comercialResponsable { get; set; }
        public int estadoContrato { get; set; }

        public void Read(SqlDataReader reader, bool withIcon = true, bool companyGroup = false)
        {
            id = reader.GetString(reader.GetOrdinal("id"));
            nombre = reader.GetString(reader.GetOrdinal("nombre"));
            cif = reader.GetString(reader.GetOrdinal("cif"));
            icon = withIcon ? HelperMethods.ReadFile(new[] { "companies", id, "icon" }) : null;
            centros = new List<CentroData>();
            if (companyGroup)
            {
                companyGroupId = reader.IsDBNull(reader.GetOrdinal("grupoId")) ? null : reader.GetString(reader.GetOrdinal("grupoId"));
                companyGroupName = reader.IsDBNull(reader.GetOrdinal("companyGroupName")) ? null : reader.GetString(reader.GetOrdinal("companyGroupName"));
            }
            estadoContrato = 0;
        }
        public void ReadEstadoContrato(SqlDataReader reader)
        {
            estadoContrato = reader.GetInt32(reader.GetOrdinal("estadoContrato"));
        }

        public void ReadCentro(SqlDataReader reader)
        {
            CentroData centro = new CentroData();
            centro.Read(reader);
            centros.Add(centro);
        }
    }
}
