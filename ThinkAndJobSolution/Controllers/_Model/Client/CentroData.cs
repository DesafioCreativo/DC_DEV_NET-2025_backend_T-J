using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers._Model.Client
{
    public class CentroData
    {
        public string id { get; set; }
        public string alias { get; set; }
        public List<WorkData> works { get; set; }

        public void Read(SqlDataReader reader)
        {
            id = reader.GetString(reader.GetOrdinal("id"));
            alias = reader.GetString(reader.GetOrdinal("alias"));
            works = new List<WorkData>();
        }

        public void ReadWork(SqlDataReader reader)
        {
            WorkData work = new WorkData();
            work.Read(reader);
            works.Add(work);
        }
    }
}
