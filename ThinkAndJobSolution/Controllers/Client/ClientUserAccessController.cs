using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.Client
{
    [Route("api/v1/client-user-access")]
    [ApiController]
    [Authorize]
    public class ClientUserAccessController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        //Listado

        [HttpGet]
        [Route(template: "list-levels/")]
        public IActionResult ListLevels()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("ClientUserAccess.ListLevels", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new { error = false, levels = listAccessLevels(conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5930, No se han podido listar los niveles de acceso" };
                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-levels-for-client/")]
        public IActionResult ListLevelsForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS) == null)
                return Ok(new { error = "Error 1002, permisos insuficientes" });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new { error = false, levels = listAccessLevels(conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5931, No se han podido listar los niveles de acceso" };
                }

            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------

        //Ayuda

        public struct ClientAccessLevel
        {
            public string id { get; set; }
            public string description { get; set; }
            public int ordering { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static List<ClientAccessLevel> listAccessLevels(SqlConnection conn, SqlTransaction transaction = null)
        {
            List<ClientAccessLevel> levels = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT * FROM client_access_levels ORDER BY ordering ASC";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        levels.Add(new ClientAccessLevel()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            description = reader.GetString(reader.GetOrdinal("description")),
                            ordering = reader.GetInt32(reader.GetOrdinal("ordering")),
                        });
                    }
                }
            }

            return levels;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }



}
