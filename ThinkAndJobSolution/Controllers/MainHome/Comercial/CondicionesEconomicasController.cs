using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers._Model.Facturacion;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/condiciones-economicas")]
    [ApiController]
    [Authorize]
    public class CondicionesEconomicasController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "list/puesto/{puestoId}/")]
        public IActionResult ListByPuesto(string puestoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CondicionesEconomicas.ListByPuesto", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                result = new
                {
                    error = false,
                    ccees = ListCCEEsByPuesto(puestoId, conn)
                };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-client/puesto/{puestoId}")]
        public IActionResult ListByPuestoForClient(string puestoId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                if (ClientHasPermission(clientToken, PuestoId2CompanyId(puestoId, conn), null, CL_PERMISSION_CONDICIONESECONOMICAS, conn) == null)
                {
                    return Ok(new
                    {
                        error = "Error 1002, No se disponen de los privilegios suficientes."
                    });
                }
                result = new
                {
                    error = false,
                    ccees = ListCCEEsByPuesto(puestoId, conn)
                };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list/company/{companyId}")]
        public IActionResult ListByCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CondicionesEconomicas.ListByCompany", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                result = new
                {
                    error = false,
                    ccees = ListCCEEsByCompany(companyId, conn)
                };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-client/company/{companyId}/")]
        public IActionResult ListForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_CONDICIONESECONOMICAS, conn) == null)
                {
                    return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
                }
                result = new
                {
                    error = false,
                    ccees = ListCCEEsByCompany(companyId, conn)
                };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "{cceeId}")]
        public IActionResult Get(string cceeId)
        {
            object result;
            CondicionesEconomicas ccee = GetCCEE(cceeId);
            if (ccee == null)
            {
                result = new
                {
                    error = "Error 4811, condiciones económicas no encontradas"
                };
            }
            else
            {
                result = new
                {
                    error = false,
                    ccee
                };
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "/create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            if (!HasPermission("CondicionesEconomicas.Create", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }

            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            try
            {
                CondicionesEconomicas ccee = new CondicionesEconomicas(data);

                if (ccee.CompanyId == null && ccee.PuestoId != null)
                    ccee.CompanyId = PuestoId2CompanyId(ccee.PuestoId);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    if (TryCreateCCEE(ccee, conn, null, out string msg))
                    {
                        result = new { error = false, id = msg };
                    }
                    else
                    {
                        result = new { error = msg };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "5810, no se han podido crear las condiciones económicas." };
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "create-for-client/")]
        public async Task<IActionResult> CreateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result;
            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            try
            {
                CondicionesEconomicas ccee = new CondicionesEconomicas(data);
                if (ccee.CompanyId == null && ccee.PuestoId != null)
                    ccee.CompanyId = PuestoId2CompanyId(ccee.PuestoId);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    if (ClientHasPermission(clientToken, ccee.CompanyId, null, CL_PERMISSION_CONDICIONESECONOMICAS, conn) == null)
                    {
                        return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
                    }
                    if (TryCreateCCEE(ccee, conn, null, out string msg))
                    {
                        result = new { error = false, id = msg };
                    }
                    else
                    {
                        result = new { error = msg };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "5810, no se han podido crear las condiciones económicas." };
            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "update/")]
        public async Task<IActionResult> Update()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            if (!HasPermission("CondicionesEconomicas.Update", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            try
            {
                CondicionesEconomicas ccee = new CondicionesEconomicas(data);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    if (TryUpdateCCEE(ccee, conn, null, out string msg))
                    {
                        result = new { error = false };
                    }
                    else
                    {
                        result = new { error = msg };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "5801, no se han podido actualizar las condiciones económicas." };
            }
            return Ok(result);
        }

        [HttpPut]
        [Route(template: "update-for-client/")]
        public async Task<IActionResult> UpdateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result;
            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            try
            {
                CondicionesEconomicas ccee = new CondicionesEconomicas(data);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    if (ClientHasPermission(clientToken, CCEEId2CompanyId(ccee.Id, conn), null, CL_PERMISSION_CONDICIONESECONOMICAS, conn) == null)
                    {
                        return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
                    }

                    if (TryUpdateCCEE(ccee, conn, null, out string msg))
                    {
                        result = new { error = false };
                    }
                    else
                    {
                        result = new { error = msg };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "5801, no se han podido actualizar las condiciones económicas." };
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "delete/{cceeId}")]
        public IActionResult Delete(string cceeId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            if (!HasPermission("CondicionesEconomicas.Delete", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                if (TryDeleteCCEE(cceeId, conn, null, out string msg))
                {
                    result = new { error = false, id = msg };
                }
                else
                {
                    result = new { error = msg };
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "delete-for-client/{cceeId}/")]
        public IActionResult DeleteForClient(string cceeId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                string companyId = CCEEId2CompanyId(cceeId, conn);
                if (companyId == null)
                {
                    return Ok(new { error = "Error 4811, condiciones económicas no encontradas" });
                }

                if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_CONDICIONESECONOMICAS, conn) == null)
                {
                    return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
                }
                if (TryDeleteCCEE(cceeId, conn, null, out string msg))
                {
                    result = new { error = false, id = msg };
                }
                else
                {
                    result = new { error = msg };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "clone/{cceeId}/{puestoId}/")]
        public async Task<IActionResult> Clone(string cceeId, string puestoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CondicionesEconomicas.Clone", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            if (puestoId == "null")
                puestoId = null;

            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("nombre", out string nombre))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        CondicionesEconomicas ccee = GetCCEE(cceeId, conn);

                        if (ccee == null)
                        {
                            return Ok(new { error = "Error 4811, condiciones económicas no encontradas" });
                        }
                        ccee.Nombre = nombre;
                        ccee.PuestoId = puestoId;
                        if (TryCreateCCEE(ccee, conn, null, out string msg))
                        {
                            result = new { error = false, id = msg };
                        }
                        else
                        {
                            result = new { error = msg };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "5812, no se han podido clonar las condiciones económicas." };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "clone-for-client/{cceeId}/{puestoId}/")]
        public async Task<IActionResult> CloneForClient(string cceeId, string puestoId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (puestoId == "null")
                puestoId = null;
            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetString("nombre", out string nombre))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        CondicionesEconomicas ccee = GetCCEE(cceeId, conn);
                        if (ccee == null)
                        {
                            return Ok(new { error = "Error 4811, condiciones económicas no encontradas" });
                        }
                        ccee.Nombre = nombre;
                        ccee.PuestoId = puestoId;
                        if (ClientHasPermission(clientToken, ccee.CompanyId, null, CL_PERMISSION_CONDICIONESECONOMICAS, conn) == null)
                        {
                            return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
                        }
                        if (TryCreateCCEE(ccee, conn, null, out string msg))
                        {
                            result = new { error = false, id = msg };
                        }
                        else
                        {
                            result = new { error = msg };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "5812, no se han podido clonar las condiciones económicas." };
                }
            }
            return Ok(result);
        }




        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static bool TryCreateCCEE(CondicionesEconomicas ccee, SqlConnection conn, SqlTransaction transaction, out string msg)
        {
            if (!ccee.IsComplete())
            {
                msg = "Error 4812, condiciones económicas incompletas";
                return false;
            }

            if (ccee.CompanyId == null)
            {
                msg = "Error 4814, no se ha podido determinar la autoria de las condiciones";
                return false;
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT COUNT(*) " +
                    "FROM condiciones_economicas WHERE " +
                    "((puestoId IS NULL AND @PUESTO IS NULL) OR puestoId = @PUESTO) AND " +
                    "companyId = @COMPANY AND " +
                    "nombre = @NOMBRE";
                command.Parameters.AddWithValue("@COMPANY", ccee.CompanyId);
                command.Parameters.AddWithValue("@PUESTO", (object)ccee.PuestoId ?? DBNull.Value);
                command.Parameters.AddWithValue("@NOMBRE", ccee.Nombre);
                if ((int)command.ExecuteScalar() > 0)
                {
                    msg = "Error 4810, ya existen otras condiciones económicas con el mismo nombre";
                    return false;
                }
            }

            ccee.Id = ComputeStringHash(ccee.PuestoId + ccee.Nombre + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "INSERT INTO condiciones_economicas (id, companyId, puestoId, nombre, condiciones) VALUES(@ID, @COMPANY, @PUESTO, @NOMBRE, @CONDICIONES)";
                command.Parameters.AddWithValue("@ID", ccee.Id);
                command.Parameters.AddWithValue("@COMPANY", ccee.CompanyId);
                command.Parameters.AddWithValue("@PUESTO", (object)ccee.PuestoId ?? DBNull.Value);
                command.Parameters.AddWithValue("@NOMBRE", ccee.Nombre);
                command.Parameters.AddWithValue("@CONDICIONES", ccee.Serialize());
                command.ExecuteNonQuery();
            }

            msg = ccee.Id;
            return true;
        }
        public static bool TryUpdateCCEE(CondicionesEconomicas ccee, SqlConnection conn, SqlTransaction transaction, out string msg)
        {
            if (!ccee.IsComplete())
            {
                msg = "Error 4812, condiciones económicas incompletas";
                return false;
            }

            CondicionesEconomicas cceeOriginal = GetCCEE(ccee.Id, conn);
            if (cceeOriginal == null)
            {
                msg = "Error 4811, condiciones económicas no encontradas";
                return false;
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT COUNT(*) " +
                    "FROM condiciones_economicas WHERE " +
                    "((puestoId IS NULL AND @PUESTO IS NULL) OR puestoId = @PUESTO) AND " +
                    "companyId = @COMPANY AND " +
                    "nombre = @NOMBRE AND " +
                    "id <> @ID";
                command.Parameters.AddWithValue("@COMPANY", cceeOriginal.CompanyId);
                command.Parameters.AddWithValue("@PUESTO", (object)cceeOriginal.PuestoId ?? DBNull.Value);
                command.Parameters.AddWithValue("@NOMBRE", ccee.Nombre);
                command.Parameters.AddWithValue("@ID", ccee.Id);
                if ((int)command.ExecuteScalar() > 0)
                {
                    msg = "Error 4810, ya existen otras condiciones económicas con el mismo nombre";
                    return false;
                }
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "UPDATE condiciones_economicas SET nombre = @NOMBRE, condiciones = @CONDICIONES WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", ccee.Id);
                command.Parameters.AddWithValue("@NOMBRE", ccee.Nombre);
                command.Parameters.AddWithValue("@CONDICIONES", ccee.Serialize());
                command.ExecuteNonQuery();
            }

            msg = default;
            return true;
        }
        public static bool TryDeleteCCEE(string cceeId, SqlConnection conn, SqlTransaction transaction, out string msg)
        {
            if (!CheckCCEEexists(cceeId, conn))
            {
                msg = "Error 4811, condiciones económicas no encontradas";
                return false;
            }

            //TODO: Desvincular de las solicitudes en las que aparezca

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM condiciones_economicas WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", cceeId);
                command.ExecuteNonQuery();
            }

            msg = default;
            return true;
        }
        public static CondicionesEconomicas GetCCEE(string cceeId, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            CondicionesEconomicas ccee = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CCEE.* " +
                                      "FROM condiciones_economicas CCEE " +
                                      "WHERE CCEE.id = @ID";
                command.Parameters.AddWithValue("@ID", cceeId);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    if (reader.Read())
                    {
                        ccee = new(reader.GetString(reader.GetOrdinal("condiciones")), true)
                        {
                            Id = reader.GetString(reader.GetOrdinal("id")),
                            Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                            CompanyId = reader.GetString(reader.GetOrdinal("companyId")),
                            PuestoId = reader.IsDBNull(reader.GetOrdinal("puestoId")) ? null : reader.GetString(reader.GetOrdinal("puestoId"))
                        };
                    }
                }
            }

            if (lastConn == null) conn.Close();
            return ccee;
        }
        public static List<CondicionesEconomicas> ListCCEEsByPuesto(string puestoId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<CondicionesEconomicas> ccees = new();
            string companyId = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CE.companyId FROM trabajos T INNER JOIN centros CE ON(T.centroId = CE.id) WHERE T.id = @PUESTO";
                command.Parameters.AddWithValue("@PUESTO", puestoId);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        companyId = reader.GetString(reader.GetOrdinal("companyId"));
            }

            if (companyId == null)
                return ccees;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CCEE.* " +
                                      "FROM condiciones_economicas CCEE " +
                                      "WHERE CCEE.companyId = @COMPANY AND" +
                                      "(CCEE.puestoId IS NULL OR CCEE.puestoId = @PUESTO) " +
                                      "ORDER BY CCEE.puestoId DESC, CCEE.date DESC";
                command.Parameters.AddWithValue("@COMPANY", companyId);
                command.Parameters.AddWithValue("@PUESTO", puestoId);

                using (SqlDataReader reader = command.ExecuteReader())
                    ReadCCEEs(reader, ccees);
            }

            return ccees;
        }
        public static List<CondicionesEconomicas> ListCCEEsByCompany(string companyId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<CondicionesEconomicas> ccees = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CCEE.* " +
                                      "FROM condiciones_economicas CCEE " +
                                      "WHERE CCEE.companyId = @COMPANY AND CCEE.puestoId IS NULL " +
                                      "ORDER BY date DESC";
                command.Parameters.AddWithValue("@COMPANY", companyId);

                using (SqlDataReader reader = command.ExecuteReader())
                    ReadCCEEs(reader, ccees);
            }

            return ccees;
        }
        private static void ReadCCEEs(SqlDataReader reader, List<CondicionesEconomicas> ccees)
        {
            while (reader.Read())
            {
                ccees.Add(new CondicionesEconomicas()
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                    CompanyId = reader.GetString(reader.GetOrdinal("companyId")),
                    PuestoId = reader.IsDBNull(reader.GetOrdinal("puestoId")) ? null : reader.GetString(reader.GetOrdinal("puestoId"))
                });
            }
        }
        public static bool CheckCCEEexists(string cceeId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM condiciones_economicas WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", cceeId);
                return (int)command.ExecuteScalar() > 0;
            }
        }
        public static string PuestoId2CompanyId(string puestoId, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            string companyId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CE.companyId FROM trabajos T INNER JOIN centros CE ON(T.centroId = CE.id) WHERE T.id = @PUESTO";
                command.Parameters.AddWithValue("@PUESTO", puestoId);

                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        companyId = reader.GetString(reader.GetOrdinal("companyId"));
            }

            if (lastConn == null) conn.Close();
            return companyId;
        }
        public static string CCEEId2CompanyId(string cceeId, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            string companyId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CCEE.companyId FROM condiciones_economicas CCEE WHERE CCEE.id = @CCEEID";
                command.Parameters.AddWithValue("@CCEEID", cceeId);

                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        companyId = reader.GetString(reader.GetOrdinal("companyId"));
            }

            if (lastConn == null) conn.Close();
            return companyId;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
