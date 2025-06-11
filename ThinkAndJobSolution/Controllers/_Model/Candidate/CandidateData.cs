using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers._Model.Candidate
{
    public class CandidateData
    {
        public string id { get; set; }

        //Datos minimos de registro
        public string name { get; set; }
        public string surname { get; set; }
        public string dni { get; set; }
        public string phone { get; set; }
        public string email { get; set; }
        public string direccion { get; set; }
        public string cp { get; set; }
        public string provincia { get; set; }
        public string localidad { get; set; }

        //Informacion auxiliar
        public DateTime date { get; set; }
        public DateTime lastAccess { get; set; }
        public bool banned { get; set; }
        public bool termsAccepted { get; set; }

        public void Read(SqlDataReader reader)
        {
            id = reader.GetString(reader.GetOrdinal("id"));

            name = reader.GetString(reader.GetOrdinal("nombre"));
            surname = reader.GetString(reader.GetOrdinal("apellidos"));
            dni = reader.GetString(reader.GetOrdinal("dni"));
            phone = reader.GetString(reader.GetOrdinal("telefono"));
            email = reader.GetString(reader.GetOrdinal("email"));
            direccion = reader.IsDBNull(reader.GetOrdinal("direccion")) ? null : reader.GetString(reader.GetOrdinal("direccion"));
            cp = reader.IsDBNull(reader.GetOrdinal("cp")) ? null : reader.GetString(reader.GetOrdinal("cp"));
            provincia = reader.IsDBNull(reader.GetOrdinal("provincia")) ? null : reader.GetString(reader.GetOrdinal("provincia"));
            localidad = reader.IsDBNull(reader.GetOrdinal("localidad")) ? null : reader.GetString(reader.GetOrdinal("localidad"));

            date = reader.GetDateTime(reader.GetOrdinal("date"));
            lastAccess = reader.GetDateTime(reader.GetOrdinal("ultimoAcceso"));
            banned = reader.GetInt32(reader.GetOrdinal("banned")) == 1;
            termsAccepted = reader.GetInt32(reader.GetOrdinal("terminosAceptados")) == 1;
        }
    }
}
