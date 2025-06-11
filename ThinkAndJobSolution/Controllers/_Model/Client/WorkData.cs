using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers._Model.Client
{
    public class WorkData
    {
        public string id { get; set; }
        public string nombre { get; set; }
        public string centroId { get; set; }
        public string companyId { get; set; }
        public string detalles { get; set; }
        public string signLink { get; set; }
        public string categoryId { get; set; }
        public int numDoc { get; set; }

        public void Read(SqlDataReader reader)
        {
            id = reader.GetString(reader.GetOrdinal("id"));
            nombre = reader.GetString(reader.GetOrdinal("name"));
            centroId = reader.GetString(reader.GetOrdinal("centroId"));
            companyId = reader.GetString(reader.GetOrdinal("companyId"));
            detalles = reader.GetString(reader.GetOrdinal("details"));
            signLink = reader.GetString(reader.GetOrdinal("signLink"));
            categoryId = reader.GetString(reader.GetOrdinal("categoryId"));
        }

        public void Read(SqlDataReader reader, int numDoc)
        {
            Read(reader);
            this.numDoc = numDoc;

        }
    }
}
