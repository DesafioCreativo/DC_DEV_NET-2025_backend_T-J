using System.Data;

namespace ThinkAndJobSolution.AccesoDato
{
    public interface IDataAccess
    {
        string QueryReturnJSON(string nombre_sp);
        string QueryExecute(string nombre_sp);
        DataTable QueryReturnDatable(string nombre_sp);
        DataSet QueryReturnDataset(string nombre_sp);
        string QueryReturnDatasetasJSON(string nombre_sp);
        //---
        string QueryReturnJSON(string connectionString, string nombre_sp);
        DataTable QueryReturnDatable(string connectionString, string nombre_sp);
        DataSet QueryReturnDataset(string connectionString, string nombre_sp);
    }
}
