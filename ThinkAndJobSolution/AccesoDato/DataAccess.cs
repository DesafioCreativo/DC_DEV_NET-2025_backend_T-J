using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;
using ThinkAndJobSolution.Security;

namespace ThinkAndJobSolution.AccesoDato
{
    public class DataAccess : IDataAccess
    {
        private readonly string _connectionString;
        private readonly ICl_Encryption _cl_encryption;

        public DataAccess(IConfiguration configuration, ICl_Encryption cl_encryption)
        {
            var a = configuration["ConnectionStrings:sqlcon"];
            _cl_encryption = cl_encryption;
            _connectionString = _cl_encryption.RijndaelDecryptString(configuration["ConnectionStrings:sqlcon"]);            
        }        
        public DataAccess()
        {

        }
        public string QueryReturnJSON(string nombre_sp)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    DataSet result = new DataSet();
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandText = nombre_sp;
                    cmd.CommandType = CommandType.Text;
                    cmd.Connection = connection;

                    connection.Open();
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = cmd;

                    da.Fill(result);
                    return JsonConvert.SerializeObject(result.Tables[0]);
                }
            }
            catch (Exception)
            {
                throw;                
            }


        }

        public string QueryExecute(string nombre_sp)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    DataSet result = new DataSet();
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandText = nombre_sp;
                    cmd.CommandType = CommandType.Text;
                    cmd.Connection = connection;

                    connection.Open();
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = cmd;

                    da.Fill(result);
                    return "OK";
                }
            }
            catch (Exception)
            {

                throw;
            }


        }
        public DataTable QueryReturnDatable(string nombre_sp)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                DataSet result = new DataSet();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = nombre_sp;
                cmd.CommandType = CommandType.Text;
                cmd.Connection = connection;

                connection.Open();
                SqlDataAdapter da = new SqlDataAdapter();
                da.SelectCommand = cmd;

                da.Fill(result);
                return result.Tables[0];
            }

        }

        public DataSet QueryReturnDataset(string nombre_sp)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                DataSet result = new DataSet();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = nombre_sp;
                cmd.CommandType = CommandType.Text;
                cmd.Connection = connection;

                connection.Open();
                SqlDataAdapter da = new SqlDataAdapter();
                da.SelectCommand = cmd;

                da.Fill(result);                
                return result;
            }
        }

        public string QueryReturnDatasetasJSON(string nombre_sp)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                DataSet result = new DataSet();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = nombre_sp;
                cmd.CommandType = CommandType.Text;
                cmd.Connection = connection;

                connection.Open();
                SqlDataAdapter da = new SqlDataAdapter();
                da.SelectCommand = cmd;

                da.Fill(result);
                return JsonConvert.SerializeObject(result);
            }
        }

        /***/
        public string QueryReturnJSON(string connectionString, string nombre_sp)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    DataSet result = new DataSet();
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandText = nombre_sp;
                    cmd.CommandType = CommandType.Text;
                    cmd.Connection = connection;

                    connection.Open();
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = cmd;
                    da.Fill(result);
                    return JsonConvert.SerializeObject(result.Tables[0]);
                }
            }
            catch (Exception ex)
            {
                throw;                
            }


        }
        public DataTable QueryReturnDatable(string connectionString, string nombre_sp)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    DataSet result = new DataSet();
                    SqlCommand cmd = new SqlCommand();
                    cmd.CommandText = nombre_sp;
                    cmd.CommandType = CommandType.Text;
                    cmd.Connection = connection;
                    cmd.CommandTimeout = 0;

                    connection.Open();
                    SqlDataAdapter da = new SqlDataAdapter();
                    da.SelectCommand = cmd;

                    da.Fill(result);
                    return result.Tables[0];
                }
            }
            catch (Exception ex)
            {
                throw;                
            }


        }
        public DataSet QueryReturnDataset(string connectionString, string nombre_sp)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                DataSet result = new DataSet();
                SqlCommand cmd = new SqlCommand();
                cmd.CommandText = nombre_sp;
                cmd.CommandType = CommandType.Text;
                cmd.Connection = connection;

                connection.Open();
                SqlDataAdapter da = new SqlDataAdapter();
                da.SelectCommand = cmd;

                da.Fill(result);                
                return result;
            }
        }
    }
}
