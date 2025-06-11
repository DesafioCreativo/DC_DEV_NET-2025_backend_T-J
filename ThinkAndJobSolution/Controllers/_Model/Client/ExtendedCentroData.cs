using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers._Model.Client
{
    public class ExtendedCentroData : CentroData
    {
        public string sociedad { get; set; }
        public string regimen { get; set; }
        public string domicilio { get; set; }
        public string cp { get; set; }
        public string poblacion { get; set; }
        public string provincia { get; set; }
        public string contactoNombre { get; set; }
        public string contactoApellido1 { get; set; }
        public string contactoApellido2 { get; set; }
        public string telefono { get; set; }
        public string email { get; set; }
        public DateTime? fechaAlta { get; set; }
        public int? servicioPrevencion { get; set; }
        public string servicioPrevencionNombre { get; set; }
        public string convenio { get; set; }
        public string ccc { get; set; }
        public string cnae { get; set; }
        public bool? workshiftRequiereFoto { get; set; }
        public bool? workshiftRequiereUbicacion { get; set; }
        public string referenciaExterna { get; set; }

        //Otros
        public string companyCif { get; set; }

        public new void Read(SqlDataReader reader)
        {
            base.Read(reader);

            sociedad = reader.GetString(reader.GetOrdinal("sociedad"));
            regimen = reader.IsDBNull(reader.GetOrdinal("regimen")) ? null : reader.GetString(reader.GetOrdinal("regimen"));
            domicilio = reader.IsDBNull(reader.GetOrdinal("domicilio")) ? null : reader.GetString(reader.GetOrdinal("domicilio"));
            cp = reader.IsDBNull(reader.GetOrdinal("cp")) ? null : reader.GetString(reader.GetOrdinal("cp"));
            poblacion = reader.IsDBNull(reader.GetOrdinal("poblacion")) ? null : reader.GetString(reader.GetOrdinal("poblacion"));
            provincia = reader.IsDBNull(reader.GetOrdinal("provincia")) ? null : reader.GetString(reader.GetOrdinal("provincia"));
            contactoNombre = reader.IsDBNull(reader.GetOrdinal("contactoNombre")) ? null : reader.GetString(reader.GetOrdinal("contactoNombre"));
            contactoApellido1 = reader.IsDBNull(reader.GetOrdinal("contactoApellido1")) ? null : reader.GetString(reader.GetOrdinal("contactoApellido1"));
            contactoApellido2 = reader.IsDBNull(reader.GetOrdinal("contactoApellido2")) ? null : reader.GetString(reader.GetOrdinal("contactoApellido2"));
            telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString(reader.GetOrdinal("telefono"));
            email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email"));
            fechaAlta = reader.GetDateTime(reader.GetOrdinal("fechaAlta"));
            servicioPrevencion = reader.IsDBNull(reader.GetOrdinal("servicioPrevencion")) ? null : reader.GetInt32(reader.GetOrdinal("servicioPrevencion"));
            servicioPrevencionNombre = reader.IsDBNull(reader.GetOrdinal("servicioPrevencionNombre")) ? null : reader.GetString(reader.GetOrdinal("servicioPrevencionNombre"));
            convenio = reader.IsDBNull(reader.GetOrdinal("convenio")) ? null : reader.GetString(reader.GetOrdinal("convenio"));
            ccc = reader.IsDBNull(reader.GetOrdinal("ccc")) ? null : reader.GetString(reader.GetOrdinal("ccc"));
            cnae = reader.IsDBNull(reader.GetOrdinal("cnae")) ? null : reader.GetString(reader.GetOrdinal("cnae"));
            workshiftRequiereFoto = reader.GetInt32(reader.GetOrdinal("workshiftRequiereFoto")) == 1;
            workshiftRequiereUbicacion = reader.GetInt32(reader.GetOrdinal("workshiftRequiereUbicacion")) == 1;
            referenciaExterna = reader.IsDBNull(reader.GetOrdinal("referenciaExterna")) ? null : reader.GetString(reader.GetOrdinal("referenciaExterna"));
        }
    }
}
