using Microsoft.Data.SqlClient;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.ClientUserController;
using ThinkAndJobSolution.Controllers._Helper;

namespace ThinkAndJobSolution.Controllers._Model.Client
{
    public class ExtendedCompanyData : CompanyData
    {
        public string cp { get; set; }
        public string direccion { get; set; }
        public string nombreRRHH { get; set; }
        public string apellido1RRHH { get; set; }
        public string apellido2RRHH { get; set; }
        public string telefonoRRHH { get; set; }
        public string emailRRHH { get; set; }
        public string web { get; set; }
        public string formaDePago { get; set; }
        public int? diaCobro { get; set; }
        public string cuentaBancaria { get; set; }
        public string cuentaContable { get; set; }
        public string tva { get; set; }
        public bool? vies { get; set; }
        public string indemnizacion { get; set; }
        public string creador { get; set; }
        public string firmanteNombre { get; set; }
        public string firmanteApellido1 { get; set; }
        public string firmanteApellido2 { get; set; }
        public string firmanteDni { get; set; }
        public string firmanteCargo { get; set; }
        public string firmanteEmail { get; set; }
        public string firmanteTelefono { get; set; }

        //Blobs
        public string fotoCif { get; set; }
        public string fotoDniAdministradorAnverso { get; set; }
        public string fotoDniAdministradorReverso { get; set; }

        //Has
        public bool hasFotoCif { get; set; }
        public bool hasFotoDniAdministrador { get; set; }

        //Otros ocasionales
        public bool? test { get; set; }
        public bool adminUserShouldBeCreated { get; set; }

        public new void Read(SqlDataReader reader, bool withIcon = true, bool companyGroup = true)
        {
            base.Read(reader, withIcon, companyGroup);

            cp = reader.IsDBNull(reader.GetOrdinal("cp")) ? null : reader.GetString(reader.GetOrdinal("cp"));
            direccion = reader.IsDBNull(reader.GetOrdinal("direccion")) ? null : reader.GetString(reader.GetOrdinal("direccion"));
            nombreRRHH = reader.IsDBNull(reader.GetOrdinal("nombreRRHH")) ? null : reader.GetString(reader.GetOrdinal("nombreRRHH"));
            apellido1RRHH = reader.IsDBNull(reader.GetOrdinal("apellido1RRHH")) ? null : reader.GetString(reader.GetOrdinal("apellido1RRHH"));
            apellido2RRHH = reader.IsDBNull(reader.GetOrdinal("apellido2RRHH")) ? null : reader.GetString(reader.GetOrdinal("apellido2RRHH"));
            telefonoRRHH = reader.IsDBNull(reader.GetOrdinal("telefonoRRHH")) ? null : reader.GetString(reader.GetOrdinal("telefonoRRHH"));
            emailRRHH = reader.IsDBNull(reader.GetOrdinal("emailRRHH")) ? null : reader.GetString(reader.GetOrdinal("emailRRHH"));
            web = reader.IsDBNull(reader.GetOrdinal("web")) ? null : reader.GetString(reader.GetOrdinal("web"));
            formaDePago = reader.IsDBNull(reader.GetOrdinal("formaDePago")) ? null : reader.GetString(reader.GetOrdinal("formaDePago"));
            diaCobro = reader.IsDBNull(reader.GetOrdinal("diaCobro")) ? null : reader.GetInt32(reader.GetOrdinal("diaCobro"));
            cuentaBancaria = reader.IsDBNull(reader.GetOrdinal("cuentaBancaria")) ? null : reader.GetString(reader.GetOrdinal("cuentaBancaria"));
            cuentaContable = reader.IsDBNull(reader.GetOrdinal("cuentaContable")) ? null : reader.GetString(reader.GetOrdinal("cuentaContable"));
            vies = reader.IsDBNull(reader.GetOrdinal("vies")) ? null : (reader.GetInt32(reader.GetOrdinal("vies")) == 1);
            tva = reader.IsDBNull(reader.GetOrdinal("tva")) ? null : reader.GetString(reader.GetOrdinal("tva"));
            indemnizacion = reader.IsDBNull(reader.GetOrdinal("indemnizacion")) ? null : reader.GetString(reader.GetOrdinal("indemnizacion"));
            creador = reader.IsDBNull(reader.GetOrdinal("creador")) ? null : reader.GetString(reader.GetOrdinal("creador"));
            firmanteNombre = reader.IsDBNull(reader.GetOrdinal("firmanteNombre")) ? null : reader.GetString(reader.GetOrdinal("firmanteNombre"));
            firmanteApellido1 = reader.IsDBNull(reader.GetOrdinal("firmanteApellido1")) ? null : reader.GetString(reader.GetOrdinal("firmanteApellido1"));
            firmanteApellido2 = reader.IsDBNull(reader.GetOrdinal("firmanteApellido2")) ? null : reader.GetString(reader.GetOrdinal("firmanteApellido2"));
            firmanteDni = reader.IsDBNull(reader.GetOrdinal("firmanteDni")) ? null : reader.GetString(reader.GetOrdinal("firmanteDni"));
            firmanteCargo = reader.IsDBNull(reader.GetOrdinal("firmanteCargo")) ? null : reader.GetString(reader.GetOrdinal("firmanteCargo"));
            firmanteEmail = reader.IsDBNull(reader.GetOrdinal("firmanteEmail")) ? null : reader.GetString(reader.GetOrdinal("firmanteEmail"));
            firmanteTelefono = reader.IsDBNull(reader.GetOrdinal("firmanteTelefono")) ? null : reader.GetString(reader.GetOrdinal("firmanteTelefono"));
            test = reader.GetInt32(reader.GetOrdinal("test")) == 1;
            companyGroupName = reader.IsDBNull(reader.GetOrdinal("companyGroupName")) ? null : reader.GetString(reader.GetOrdinal("companyGroupName"));

            hasFotoCif = HelperMethods.ExistsFile(new[] { "companies", id, "foto_cif" });
            hasFotoDniAdministrador = HelperMethods.ExistsFile(new[] { "companies", id, "foto_dni_administrador" }) && HelperMethods.ExistsFile(new[] { "companies", id, "foto_dni_administrador_reverso" });

            centros = new List<CentroData>();
        }

        public void CalculateAdminUserShouldBeCreated(SqlConnection conn, SqlTransaction transaction = null)
        {
            //Comprobar que tenga todos los datos
            if (String.IsNullOrEmpty(firmanteNombre) ||
                            String.IsNullOrEmpty(firmanteApellido1) ||
                            String.IsNullOrEmpty(firmanteCargo) ||
                            String.IsNullOrEmpty(firmanteEmail) ||
                            String.IsNullOrEmpty(firmanteDni) ||
                            String.IsNullOrEmpty(firmanteTelefono))
            {
                adminUserShouldBeCreated = false;
                return;
            }

            //Comprobar que no exista ya
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM client_users WHERE email = @EMAIL";
                command.Parameters.AddWithValue("@EMAIL", firmanteEmail);
                if ((int)command.ExecuteScalar() > 0)
                {
                    adminUserShouldBeCreated = false;
                    return;
                }
            }

            adminUserShouldBeCreated = true;
        }
    }
}
