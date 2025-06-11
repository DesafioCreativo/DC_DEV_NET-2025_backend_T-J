using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.RRHH
{
    [Route("api/v1/guardias")]
    [ApiController]
    [Authorize]
    public class GuardiasController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        //Listado

        [HttpGet]
        [Route(template: "list/{userId}/{year}/{month}/{day}/")]
        public IActionResult List(string userId, int? year, int? month, int? day)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Guardias.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            if (userId == "null")
                userId = null;

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    result = new
                    {
                        error = false,
                        guardias = listGuardias(conn, null, userId, assambleDate(year, month, day))
                    };
                }
            }
            catch
            {
                result = new { error = "Error 5110, no se han podido obtener las guardias" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "check/")]
        public IActionResult CheckGuardias()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    string userId = FindUserIdBySecurityToken(securityToken, conn);

                    if (userId == null)
                    {
                        return Ok(new
                        {
                            error = "Error 1001, No se disponen de los privilegios suficientes."
                        });
                    }

                    result = new
                    {
                        error = false,
                        guardias = listGuardias(conn, null, userId, DateTime.Now)
                    };
                }
            }
            catch
            {
                result = new { error = "Error 5111, no se han podido obtener las guardias" };
            }

            return Ok(result);
        }

        //Creacion

        [HttpPost]
        [Route(template: "create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Guardias.Create", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.ValueKind != JsonValueKind.Array)
                return Ok(result);

            List<Guardia> guardias = new();
            foreach (JsonElement guardiaJson in json.EnumerateArray())
            {
                if (!tryGetGuardia(guardiaJson, out Guardia guardia))
                    return Ok(result);
                guardias.Add(guardia);
            }


            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        List<string> ids = new();

                        foreach (Guardia guardia in guardias)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM guardias_rrhh WHERE userId = @USER AND firstDay = @FIRST AND lastDay = @LAST";
                                command.Parameters.AddWithValue("@USER", guardia.userId);
                                command.Parameters.AddWithValue("@FIRST", guardia.firstDay);
                                command.Parameters.AddWithValue("@LAST", guardia.lastDay);
                                if ((int)command.ExecuteScalar() > 0)
                                    continue;
                            }

                            string id = ComputeStringHash(guardia.id + guardia.startTime + "guardia" + guardia.userId + DateTime.Now.Millisecond);

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "INSERT INTO guardias_rrhh " +
                                                        "(id, userId, startTime, endTime, firstDay, lastDay) VALUES " +
                                                        "(@ID, @USER, @START, @END, @FIRST, @LAST)";

                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@USER", guardia.userId);
                                command.Parameters.AddWithValue("@START", guardia.startTime);
                                command.Parameters.AddWithValue("@END", guardia.endTime);
                                command.Parameters.AddWithValue("@FIRST", guardia.firstDay);
                                command.Parameters.AddWithValue("@LAST", guardia.lastDay);
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        result = new
                        {
                            error = false,
                            ids
                        };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "5114, no se han podido crear las guardias." };
                    }
                }
            }

            return Ok(result);
        }

        //Eliminacion

        [HttpDelete]
        [Route(template: "delete/{guardiaId}/")]
        public IActionResult Delete(string guardiaId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Guardias.Delete", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.CommandText = "DELETE FROM guardias_rrhh WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", guardiaId);
                        command.ExecuteNonQuery();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        result = new
                        {
                            error = "Error 5113, no se ha podido eliminar la guardia"
                        };
                    }
                }
            }

            return Ok(result);
        }

        // Otros

        [HttpGet]
        [Route(template: "send-by-mail/")]
        public IActionResult SendByMail()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Guardias.SendByMail", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Obtener todas las guardias 
                    IEnumerable<IGrouping<string, Guardia>> guardiasByUser = listGuardias(conn, null, null, DateTime.Now.Date).GroupBy(g => g.userId);

                    int nSend = 0;
                    foreach (IGrouping<string, Guardia> guardias in guardiasByUser)
                    {
                        //Obtener los datos del usuario
                        string email = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT email FROM users WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", guardias.Key);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    email = reader.GetString(reader.GetOrdinal("email"));
                        }
                        if (email == null) continue;

                        //Formar el correo
                        List<string> lines = new();
                        foreach (Guardia guardia in guardias.ToList())
                        {
                            lines.Add($"{FormatPairOfDates(guardia.firstDay, guardia.lastDay)}<br/><span style='font-size:80%; opacity: 0.8;'>Duración de los superpoderes: {FormatPairOfDates(guardia.startTime, guardia.endTime, withHour: true)}</span>");
                        }

                        StringBuilder sb = new();
                        bool darken = false;
                        foreach (string line in lines)
                        {
                            string bg = darken ? "#B3B3B3" : "#E6E6E6";
                            sb.Append("<tr style='background-color:");
                            sb.Append(bg);
                            sb.Append(";'><td style='text-align:left; width: 100%;'><div style='padding: 5px;'>");
                            sb.Append(line);
                            sb.Append("</div></td></tr>");
                            darken = !darken;
                        }

                        //Enviar el correo
                        EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "guardiasList",
                            inserts = new() {
                                { "guardias", sb.ToString() }
                            },
                            toEmail = email,
                            subject = "[Think&Job] Próximas guardias",
                            priority = EventMailer.EmailPriority.MODERATE
                        });
                        nSend++;
                    }

                    result = new { error = false, nSend };
                }
            }
            catch
            {
                result = new { error = "Error 5116, no se han podido enviar las guardias por correo." };
            }

            return Ok(result);
        }


        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        public struct Guardia
        {
            public string id { get; set; }
            public string userId { get; set; }
            public bool name { get; set; }
            public DateTime startTime { get; set; }
            public DateTime endTime { get; set; }
            public DateTime firstDay { get; set; }
            public DateTime lastDay { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static DateTime? assambleDate(int? year, int? month, int? day)
        {
            if (year.HasValue && month.HasValue && day.HasValue)
                return new DateTime(year.Value, month.Value, day.Value);
            else
                return null;
        }

        public static bool tryGetGuardia(JsonElement json, out Guardia guardia)
        {
            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("startTime", out JsonElement startTimeJson) &&
                json.TryGetProperty("endTime", out JsonElement endTimeJson) &&
                json.TryGetProperty("firstDay", out JsonElement firstDayJson) &&
                json.TryGetProperty("lastDay", out JsonElement lastDayJson))
            {
                DateTime? startTime = GetJsonDateTime(startTimeJson);
                DateTime? endTime = GetJsonDateTime(endTimeJson);
                DateTime? firstDay = GetJsonDate(firstDayJson);
                DateTime? lastDay = GetJsonDate(lastDayJson);

                if (startTime.HasValue && endTime.HasValue && firstDay.HasValue && lastDay.HasValue)
                {
                    guardia = new Guardia()
                    {
                        userId = userIdJson.GetString(),
                        startTime = startTime.Value,
                        endTime = endTime.Value,
                        firstDay = firstDay.Value,
                        lastDay = lastDay.Value
                    };
                    return true;
                }
            }

            guardia = default;
            return false;
        }

        public static List<Guardia> listGuardias(SqlConnection conn, SqlTransaction transaction, string userId = null, DateTime? startDay = null)
        {
            List<Guardia> guardias = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT G.* " +
                                      "FROM guardias_rrhh G " +
                                      "WHERE (@USER IS NULL OR @USER = userId) AND " +
                                      "(@START IS NULL OR @START <= lastDay) " +
                                      "ORDER BY firstDay DESC, lastDay DESC";
                command.Parameters.AddWithValue("@USER", (object)userId ?? DBNull.Value);
                command.Parameters.AddWithValue("@START", startDay == null ? DBNull.Value : startDay.Value.Date);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        guardias.Add(new Guardia()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            userId = reader.GetString(reader.GetOrdinal("userId")),
                            startTime = reader.GetDateTime(reader.GetOrdinal("startTime")),
                            endTime = reader.GetDateTime(reader.GetOrdinal("endTime")),
                            firstDay = reader.GetDateTime(reader.GetOrdinal("firstDay")),
                            lastDay = reader.GetDateTime(reader.GetOrdinal("lastDay"))
                        });
                    }
                }
            }

            return guardias;
        }

        public static string getUserIdFilter(string userId, bool esJefe, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            if (esJefe) return null;

            bool guardiaOAdministracion = false;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                //Comprobar si su departamento es Administracion
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT COUNT(*) FROM users U WHERE U.id = @ID AND U.department = 'Administración'";

                    command.Parameters.AddWithValue("@ID", userId);

                    if ((int)command.ExecuteScalar() > 0)
                        guardiaOAdministracion = true;
                }

                //Si no es de Administracion, comprobar si tiene guardia
                if (!guardiaOAdministracion)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }

                        command.CommandText =
                            "SELECT COUNT(*) FROM guardias_rrhh G INNER JOIN users U ON(G.userId = U.id) WHERE U.id = @ID AND @TIME BETWEEN startTime AND endTime";

                        command.Parameters.AddWithValue("@ID", userId);
                        command.Parameters.AddWithValue("@TIME", DateTime.Now);

                        if ((int)command.ExecuteScalar() > 0)
                            guardiaOAdministracion = true;
                    }
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception) { }

            return guardiaOAdministracion ? null : userId;
        }

        public static string getSecurityTokenFilter(string securityToken, bool esJefe, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            if (esJefe) return null;

            bool guardiaOAdministracion = false;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                //Comprobar si su departamento es Administracion
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT COUNT(*) FROM users U WHERE U.securityToken = @TOKEN AND U.department = 'Administración'";

                    command.Parameters.AddWithValue("@TOKEN", securityToken);

                    if ((int)command.ExecuteScalar() > 0)
                        guardiaOAdministracion = true;
                }

                //Si no es de Administracion, comprobar si tiene guardia
                if (!guardiaOAdministracion)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }

                        command.CommandText =
                            "SELECT COUNT(*) FROM guardias_rrhh G INNER JOIN users U ON(G.userId = U.id) WHERE U.securityToken = @TOKEN AND @TIME BETWEEN startTime AND endTime";

                        command.Parameters.AddWithValue("@TOKEN", securityToken);
                        command.Parameters.AddWithValue("@TIME", DateTime.Now);

                        if ((int)command.ExecuteScalar() > 0)
                            guardiaOAdministracion = true;
                    }
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception) { }

            return guardiaOAdministracion ? null : securityToken;
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------



    }
}
