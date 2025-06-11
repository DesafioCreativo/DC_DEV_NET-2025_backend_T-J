using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Model.Client;
using ThinkAndJobSolution.Controllers.Candidate;
using ThinkAndJobSolution.Controllers.MainHome.RRHH;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.Client
{
    [Route("api/v1/incidence-extrahours")]
    [ApiController]
    [Authorize]
    public class IncidenceExtraHoursController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        // Crear el reporte

        [HttpPost]
        [Route("create/{centroId}/")]
        public async Task<IActionResult> Create(string centroId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ExtraHoursIncidence.Create", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("entries", out JsonElement entriesJson) &&
                json.TryGetProperty("day", out JsonElement dayJson) &&
                json.TryGetProperty("total", out JsonElement totalJson) &&
                json.TryGetProperty("jefeDni", out JsonElement jefeDniJson) &&
                json.TryGetProperty("jefePwd", out JsonElement jefePwdJson))
            {
                DateTime day = GetJsonDate(dayJson).Value;
                bool total = totalJson.GetBoolean();
                string jefeDni = jefeDniJson.GetString();
                string jefePwd = jefePwdJson.GetString();

                if (!access.EsJefe && !HasJefePermission(null, null, "NotAttendIncidence.Create", jefeDni, jefePwd))
                {
                    result = new { error = "Error 1008, credenciales de jefe incorrectas" };
                }
                else
                {
                    result = createIncidence(parseEntriesJson(entriesJson), day, centroId, total, FindUsernameBySecurityToken(securityToken));
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("create-for-client/{centroId}/")]
        public async Task<IActionResult> CreateForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("entries", out JsonElement entriesJson) &&
                json.TryGetProperty("day", out JsonElement dayJson))
            {
                DateTime day = GetJsonDate(dayJson).Value;

                //Comprobar que el cliente tiene permiso para usar este centro
                string author = ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_EXTRAS);
                if (author == null)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                result = createIncidence(parseEntriesJson(entriesJson), day, centroId, false, author);
            }

            return Ok(result);
        }

        // Modificar manualmente un reporte para resolver un conflicto o cerrarla y abrirla

        [HttpPut]
        [Route("update/{incidenceId}/")]
        public async Task<IActionResult> Update(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("ExtraHoursIncidence.Update", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("state", out JsonElement stateJson))
            {
                string state = stateJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        bool failed = false;
                        ExtraHoursIncidence? incidence = getIncidence(conn, null, incidenceId);

                        //Comprobar que exista y que no este cerrada
                        if (incidence == null)
                        {
                            failed = true;
                            result = new { error = "Error 4721, reporte no encontrado" };
                        }

                        //Modificar el reporte e insertar el evento
                        if (!failed)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "UPDATE incidencia_horas_extra SET " +
                                    "state = @STATE " +
                                    "WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", incidenceId);
                                command.Parameters.AddWithValue("@STATE", state);
                                command.ExecuteNonQuery();
                            }

                            if (!incidence.Value.state.Equals("validada") && state.Equals("validada"))
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText =
                                        "UPDATE incidencia_horas_extra SET " +
                                        "validationDate = getdate() " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ID", incidenceId);
                                    command.ExecuteNonQuery();
                                }
                            }

                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5721, no se han podido modificar el reporte" };
                    }
                }
            }

            return Ok(result);
        }

        // Eliminar el reporte

        [HttpDelete]
        [Route("{incidenceId}/")]
        public IActionResult Delete(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("ExtraHoursIncidence.Delete", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE candidate_checks SET extraHoursReportId = NULL WHERE extraHoursReportId = @ID";
                        command.Parameters.AddWithValue("@ID", incidenceId);
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM incidencia_horas_extra_entrada WHERE incidenceId = @ID";
                        command.Parameters.AddWithValue("@ID", incidenceId);
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM incidencia_horas_extra WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", incidenceId);
                        command.ExecuteNonQuery();
                    }

                    LogToDB(LogType.DELETION, "Reporte de horas extra", FindUsernameBySecurityToken(securityToken), conn);

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5721, no se han podido borrar el reporte" };
                }
            }

            return Ok(result);
        }

        // Obtener datos del reporte

        [HttpGet]
        [Route("{incidenceId}")]
        public IActionResult Get(string incidenceId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    ExtraHoursIncidence? incidence = getIncidence(conn, null, incidenceId);

                    if (incidence == null)
                    {
                        result = new { error = "Error 4721, no se ha encontrado el reporte" };
                    }
                    else
                    {
                        result = new
                        {
                            error = false,
                            incidence = incidence.Value
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5722, no se han podido obtener el reporte" };
                }
            }

            return Ok(result);
        }

        // Listado de reportes para rrhh, clientes y candidatos
        [HttpPost]
        [Route("list/")]
        public async Task<IActionResult> List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ExtraHoursIncidence.List", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("companyKey", out JsonElement companyKeyJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("total", out JsonElement totalJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) &&
                json.TryGetProperty("perpage", out JsonElement perpageJson))
            {
                int? number = GetJsonInt(numberJson);
                string companyId = companyIdJson.GetString();
                string companyKey = companyKeyJson.GetString();
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string state = stateJson.GetString();
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? total = GetJsonBool(totalJson);
                int page = int.Parse(pageJson.GetString());
                int perpage = int.Parse(perpageJson.GetString());

                try
                {
                    result = new
                    {
                        error = false,
                        incidences = listIncidences(number, companyId, companyKey, centroId, centroKey, state, startDate, endDate, total, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe), null, page, perpage)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5723, no se han podido listar los reportes" };
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("list-count/")]
        public async Task<IActionResult> ListCount()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ExtraHoursIncidence.ListCount", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("companyKey", out JsonElement companyKeyJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("total", out JsonElement totalJson))
            {
                int? number = GetJsonInt(numberJson);
                string companyId = companyIdJson.GetString();
                string companyKey = companyKeyJson.GetString();
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string state = stateJson.GetString();
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? total = GetJsonBool(totalJson);

                try
                {
                    result = countIncidences(number, companyId, companyKey, centroId, centroKey, state, startDate, endDate, total, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe), null);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5724, no se han podido contar los reportes" };
                }
            }

            return Ok(result);
        }

        [HttpPost]        
        [Route("list-for-client/")]        
        public async Task<IActionResult> ListForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("companyKey", out JsonElement companyKeyJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("total", out JsonElement totalJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) &&
                json.TryGetProperty("perpage", out JsonElement perpageJson))
            {
                int? number = GetJsonInt(numberJson);
                string companyId = companyIdJson.GetString();
                string companyKey = companyKeyJson.GetString();
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string state = stateJson.GetString();
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? total = GetJsonBool(totalJson);
                int page = int.Parse(pageJson.GetString());
                int perpage = int.Parse(perpageJson.GetString());

                if (ClientHasPermission(clientToken, companyId, centroId, CL_PERMISSION_EXTRAS) == null)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                try
                {
                    result = new
                    {
                        error = false,
                        incidences = listIncidences(number, companyId, companyKey, centroId, centroKey, state, startDate, endDate, total, null, clientToken, page, perpage)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5723, no se han podido listar los reportes" };
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("list-count-for-client/")]
        public async Task<IActionResult> ListCountForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("companyKey", out JsonElement companyKeyJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("total", out JsonElement totalJson))
            {
                int? number = GetJsonInt(numberJson);
                string companyId = companyIdJson.GetString();
                string companyKey = companyKeyJson.GetString();
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string state = stateJson.GetString();
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? total = GetJsonBool(totalJson);


                if (ClientHasPermission(clientToken, companyId, centroId, CL_PERMISSION_EXTRAS) == null)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                try
                {
                    result = countIncidences(number, companyId, companyKey, centroId, centroKey, state, startDate, endDate, total, null, clientToken);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5724, no se han podido contar los reportes" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("list-unrevised-extras-for-candidate/")]
        public IActionResult ListUnrevisedExtrasForCandidate()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            try
            {
                Dictionary<DateTime, List<ExtraHoursEntry>> dias = new();
                ExtraHoursEntryResume resume = new() { vacaciones = new() };
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Obtener el centro del candidato
                    string centroId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT centroId FROM candidatos WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", candidateId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read() && !reader.IsDBNull(reader.GetOrdinal("centroId")))
                            {
                                centroId = reader.GetString(reader.GetOrdinal("centroId"));
                            }
                        }
                    }

                    if (centroId == null)
                    {
                        return Ok(new { error = "Error 4932, candidato no encontrado." });
                    }

                    DateTime now = DateTime.Now;
                    if (now.Day <= 5)
                        now.AddMonths(-1);

                    List<string> selectedReports = fetchReportIdsOfMonth(now, centroId, conn);

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT E.*, I.day " +
                                              "FROM incidencia_horas_extra_entrada E " +
                                              "INNER JOIN incidencia_horas_extra I ON(E.incidenceId = I.id) " +
                                              "INNER JOIN candidatos C ON (E.candidateDni = C.dni) " +
                                              "WHERE C.id = @CANDIDATE AND " +
                                              "I.id = @REPORT AND " +
                                              "E.revisadaPorCandidato = 0";
                        command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                        command.Parameters.Add("@REPORT", System.Data.SqlDbType.VarChar);
                        foreach (string reportId in selectedReports)
                        {
                            command.Parameters["@CANDIDATE"].Value = candidateId;
                            command.Parameters["@REPORT"].Value = reportId;
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ExtraHoursEntry entry = new()
                                    {
                                        id = reader.GetString(reader.GetOrdinal("id")),
                                        concepto = reader.GetString(reader.GetOrdinal("concepto")),
                                        cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                                        multiplicador = reader.GetDouble(reader.GetOrdinal("multiplicador")),
                                        vacacionesInicio = reader.IsDBNull(reader.GetOrdinal("vacacionesInicio")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesInicio")),
                                        vacacionesFin = reader.IsDBNull(reader.GetOrdinal("vacacionesFin")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesFin")),
                                        neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1,
                                        tipo = reader.GetString(reader.GetOrdinal("tipo")),
                                        preestablecida = reader.GetInt32(reader.GetOrdinal("preestablecida")) == 1
                                    };
                                    if (entry.tipo == "vacaciones")
                                    {
                                        resume.vacaciones.Add(entry);
                                    }
                                    else
                                    {
                                        DateTime day = reader.GetDateTime(reader.GetOrdinal("day"));
                                        if (!dias.ContainsKey(day))
                                            dias[day] = new();
                                        dias[day].Add(entry);
                                    }
                                }
                            }
                        }
                    }

                    //Obtener las vacaciones segun el calendario de vacaciones
                    resume.vacaciones.AddRange(CandidateVacacionesController.getVacacionesNotAccepted(candidateId, conn).Select(i => new ExtraHoursEntry()
                    {
                        id = $"vacaciones-{i.Item1:yyyy-MM-dd}-{i.Item2:yyyy-MM-dd}",
                        concepto = "Vacaciones",
                        tipo = "vacaciones",
                        vacacionesInicio = i.Item1,
                        vacacionesFin = i.Item2,
                        cantidad = 0,
                        multiplicador = 1,
                        neto = false,
                        preestablecida = false
                    }));
                }

                resume.dias = dias.Select(d => new ExtraHoursEntryResumeDay() { date = d.Key, extras = d.Value }).OrderBy(d => d.date).ToList();
                resume.vacaciones = resume.vacaciones.OrderBy(v => v.vacacionesInicio).ToList();
                resume.empty = resume.dias.Count == 0 && resume.vacaciones.Count == 0;

                result = new
                {
                    error = false,
                    resume
                };
            }
            catch (Exception)
            {
                result = new { error = "Error 5723, no se han podido listar los reportes" };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("accept-entry-for-candidate/")]
        public async Task<IActionResult> AcceptReportsForCandidate()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("ids", out JsonElement idsJson))
            {
                List<string> ids = GetJsonStringList(idsJson);
                List<string> extrasIds = new();
                List<Tuple<DateTime, DateTime>> vacacionesCalendario = new();

                try
                {
                    //Clasificar las ids
                    foreach (string id in ids)
                    {
                        if (id.StartsWith("vacaciones-"))
                        {
                            string[] parts = id.Split("-");
                            if (parts.Length == 7)
                            {
                                vacacionesCalendario.Add(new(new(Int32.Parse(parts[1]), Int32.Parse(parts[2]), Int32.Parse(parts[3])), new(Int32.Parse(parts[4]), Int32.Parse(parts[5]), Int32.Parse(parts[6]))));
                            }
                        }
                        else
                        {
                            extrasIds.Add(id);
                        }
                    }

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                //Obtner el DNI del candiato
                                string dni = null;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "SELECT dni FROM candidatos WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                        if (reader.Read())
                                            dni = reader.GetString(reader.GetOrdinal("dni"));
                                }

                                if (dni == null)
                                {
                                    return Ok(new { error = "Error 4932, candidato no encontrado." });
                                }

                                //Aceptar las IDs de extras
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE incidencia_horas_extra_entrada " +
                                                          "SET revisadaPorCandidato = 1" +
                                                          "WHERE candidateDni = @DNI AND id = @ID";
                                    command.Parameters.AddWithValue("@DNI", dni);
                                    command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                                    foreach (string id in ids)
                                    {
                                        command.Parameters["@ID"].Value = id;
                                        command.ExecuteNonQuery();
                                    }
                                }

                                //Aceptar las vacaciones del calendario
                                CandidateVacacionesController.setVacacionesAccepted(candidateId, vacacionesCalendario, conn, transaction);

                                transaction.Commit();
                                result = new { error = false };
                            }
                            catch (Exception)
                            {
                                transaction.Rollback();
                                result = new { error = "Error 5864, no se han podido actualizar los reportes" };
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5724, no se han podido actualizar los reportes" };
                }
            }

            return Ok(result);
        }

        //Validacion de totales

        [HttpGet]
        [Route("list-totals-for-client/{year}/{month}/")]
        public IActionResult ListTotalsForClient(int year, int month)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_EXTRAS) == null) //Puede requerir un nivel de acceso mas alto
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    result = new { error = false, reports = listTotals(conn, year, month, clientToken) };
                }

            }
            catch (Exception)
            {
                result = new { error = "Error 5721, no se han podido listar los reportes totales" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("list-totals/{year}/{month}/")]
        public IActionResult ListTotals(int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ExtraHoursIncidence.ListTotals", securityToken);
            if (!access.Acceso)
            {
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    result = new { error = false, reports = listTotals(conn, year, month, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn)) };
                }

            }
            catch (Exception)
            {
                result = new { error = "Error 5721, no se han podido listar los reportes totales" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("count-validated-unreaded-totals/{year}/{month}/")]
        public IActionResult CountUnreadedValidatedTotals(int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ExtraHoursIncidence.ListTotals", securityToken);
            if (!access.Acceso)
            {
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    result = new { error = false, news = listTotals(conn, year, month, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn)).Count(t => t.rrhhUnreaded) };
                }

            }
            catch (Exception)
            {
                result = new { error = "Error 5721, no se han podido listar los reportes totales" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("download-report-for-client/{incidenceId}/")]
        public IActionResult DownloadReportForClient(string incidenceId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            try
            {
                string author = ClientHasPermission(clientToken, null, null, CL_PERMISSION_EXTRAS); //Puede requerir un nivel de acceso mas alto
                if (author == null)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Recopilar todas las entradas del reporte
                    ExtraHoursIncidence? incidence = getIncidence(conn, null, incidenceId);
                    if (!incidence.HasValue) return new NoContentResult();
                    List<ExtraHoursEntry> entries = incidence.Value.entries;

                    string tmpDir = GetTemporaryDirectory();

                    return generateExcelTemplate(HttpContext,conn, incidence.Value.day, clientToken, null, incidence.Value.companyId, incidence.Value.centroId, entries, true);
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        [HttpGet]
        [Route("download-report/{incidenceId}/")]
        public IActionResult DownloadReport(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            try
            {
                ResultadoAcceso access = HasPermission("ExtraHoursIncidence.DownloadReport", securityToken);
                if (!access.Acceso)
                {
                    return Ok(new { error = "Error 1001, permisos insuficientes" });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Recopilar todas las entradas del reporte
                    ExtraHoursIncidence? incidence = getIncidence(conn, null, incidenceId);
                    if (!incidence.HasValue) return new NoContentResult();
                    List<ExtraHoursEntry> entries = incidence.Value.entries;

                    string tmpDir = GetTemporaryDirectory();

                    return generateExcelTemplate(HttpContext,conn, incidence.Value.day, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn), incidence.Value.companyId, incidence.Value.centroId, entries, true);
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        [HttpGet]
        [Route("download-month-for-client/{centroId}/{year}/{month}/")]
        public IActionResult DownloadMonthForClient(string centroId, int year, int month)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            try
            {
                string author = ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_EXTRAS); //Puede requerir un nivel de acceso mas alto
                if (author == null)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Recopilar las entradas del mes
                    DateTime firstDayOfMonth = new DateTime(year, month, 1);
                    DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                    List<ExtraHoursEntry> entries = fetchEntriesOfMonth(conn, firstDayOfMonth, centroId);

                    return generateExcelOverview(HttpContext, conn, lastDayOfMonth, clientToken, null, entries, getCompanyFromCentro(conn, null, centroId), centroId);
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        [HttpGet]
        [Route("download-month/{centroId}/{year}/{month}/")]
        public IActionResult DownloadMonth(string centroId, int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            try
            {
                ResultadoAcceso access = HasPermission("ExtraHoursIncidence.DownloadMonth", securityToken);
                if (!access.Acceso)
                {
                    return Ok(new { error = "Error 1001, permisos insuficientes" });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Recopilar las entradas del mes
                    DateTime firstDayOfMonth = new DateTime(year, month, 1);
                    DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                    List<ExtraHoursEntry> entries = fetchEntriesOfMonth(conn, firstDayOfMonth, centroId);

                    return generateExcelOverview(HttpContext, conn, lastDayOfMonth, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn), entries, getCompanyFromCentro(conn, null, centroId), centroId);
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        [HttpGet]
        [Route("validate-total-for-client/{centroId}/{year}/{month}/")]
        public IActionResult ValidateTotalForClient(string centroId, int year, int month)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            try
            {
                string author = ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_EXTRAS); //Puede requerir un nivel de acceso mas alto
                if (author == null)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                result = validateTotal(centroId, year, month, author, false);
            }
            catch (Exception)
            {
                result = new { error = "Error 5721, no se han podido validar el reporte" };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("validate-total/{centroId}/{year}/{month}/")]
        public async Task<IActionResult> ValidateTotal(string centroId, int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("jefeDni", out JsonElement jefeDniJson) &&
                json.TryGetProperty("jefePwd", out JsonElement jefePwdJson))
            {
                string jefeDni = jefeDniJson.GetString();
                string jefePwd = jefePwdJson.GetString();

                try
                {
                    ResultadoAcceso access = HasPermission("ExtraHoursIncidence.ValidateTotal", securityToken);
                    if (!access.Acceso)
                    {
                        return Ok(new { error = "Error 1001, permisos insuficientes" });
                    }

                    if (!access.EsJefe && !HasJefePermission(null, null, "NotAttendIncidence.Create", jefeDni, jefePwd))
                    {
                        result = new { error = "Error 1008, credenciales de jefe incorrectas" };
                    }
                    else
                    {
                        result = validateTotal(centroId, year, month, FindUsernameBySecurityToken(securityToken), true);
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5721, no se han podido validar el reporte" };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route("exists-total-for-client/{centroId}/{year}/{month}/")]
        public IActionResult ExistsTotalForClient(string centroId, int year, int month)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_EXTRAS) == null)
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    var reports = listTotals(conn, year, month, clientToken);
                    var report = reports.FirstOrDefault(r => r.centroId == centroId);

                    if (report.centroId == null)
                        result = new { error = "Error, no tienes permiso sobre el centro" };
                    else
                        result = new { error = false, report.state };
                }

            }
            catch (Exception)
            {
                result = new { error = "Error 5721, no se han podido listar los reportes totales" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("exists-total/{centroId}/{year}/{month}/")]
        public IActionResult ExistsTotal(string centroId, int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ExtraHoursIncidence.ExistsTotal", securityToken);
            if (!access.Acceso)
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    var reports = listTotals(conn, year, month, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn));
                    var report = reports.FirstOrDefault(r => r.centroId == centroId);

                    if (report.centroId == null)
                        result = new { error = "Error, no tienes permiso sobre el centro" };
                    else
                        result = new { error = false, report.state };
                }

            }
            catch (Exception)
            {
                result = new { error = "Error 5721, no se han podido listar los reportes totales" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("mark-readed/{reportId}/")]
        public IActionResult MarkReaded(string reportId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ExtraHoursIncidence.MarkReaded", securityToken);
            if (!access.Acceso)
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE incidencia_horas_extra SET rrhhUnreaded = 0 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", reportId);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }

            }
            catch (Exception)
            {
                result = new { error = "Error 5721, no se han podido marcar como leida la verificacion" };
            }

            return Ok(result);
        }

        // Subida por excel

        [HttpPost]
        [Route(template: "download-template-for-client/")]
        public async Task<IActionResult> GenerateTemplateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            try
            {
                string author = ClientHasPermission(clientToken, null, null, CL_PERMISSION_EXTRAS);
                if (author == null)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                    json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                    json.TryGetProperty("day", out JsonElement dayJson) &&
                    json.TryGetProperty("total", out JsonElement totalJson))
                {
                    string companyId = companyIdJson.GetString();
                    string centroId = centroIdJson.GetString();
                    DateTime day = GetJsonDate(dayJson) ?? DateTime.Now;
                    bool total = totalJson.GetBoolean();

                    if (!checkIncidenceCanBeCreated(day))
                    {
                        return new BadRequestObjectResult("Error 4729, no se puede crear un reporte de un mes anterior una vez pasado el día 5 del mes siguiente.");
                    }
                    if (total && (companyId == null || centroId == null))
                    {
                        return new BadRequestObjectResult("Error 4730, los reportes totales deben ser de una empresa y centro concreto.");
                    }

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        //Si es total, recopilar todas las entradas del mes del centro
                        List<ExtraHoursEntry> entries = new();
                        if (total)
                        {
                            entries = fetchEntriesOfMonth(conn, day, centroId);
                        }

                        return generateExcelTemplate(HttpContext, conn, day, clientToken, null, companyId, centroId, entries);
                    }
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        [HttpPost]
        [Route(template: "download-template/")]
        public async Task<IActionResult> GenerateTemplate()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            try
            {
                ResultadoAcceso access = HasPermission("ExtraHoursIncidence.GenerateTemplate", securityToken);
                if (!access.Acceso)
                {
                    return Ok(new { error = "Error 1002, permisos insuficientes" });
                }

                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                    json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                    json.TryGetProperty("day", out JsonElement dayJson) &&
                    json.TryGetProperty("total", out JsonElement totalJson))
                {
                    string companyId = companyIdJson.GetString();
                    string centroId = centroIdJson.GetString();
                    DateTime day = GetJsonDate(dayJson) ?? DateTime.Now;
                    bool total = totalJson.GetBoolean();

                    /*if (!checkIncidenceCanBeCreated(day))
                    {
                        return new BadRequestObjectResult("Error 4729, no se puede crear un reporte de un mes anterior una vez pasado el día 5 del mes siguiente.");
                    }*/
                    if (total && (companyId == null || centroId == null))
                    {
                        return new BadRequestObjectResult("Error 4730, los reportes totales deben ser de una empresa y centro concreto.");
                    }

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        //Si es total, recopilar todas las entradas del mes del centro
                        List<ExtraHoursEntry> entries = new();
                        if (total)
                        {
                            entries = fetchEntriesOfMonth(conn, day, centroId);
                        }

                        return generateExcelTemplate(HttpContext, conn, day, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn), companyId, centroId, entries);
                    }
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        [HttpPost]
        [Route(template: "upload-template-for-client/")]
        public async Task<IActionResult> UploadTemplateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            try
            {
                string author = ClientHasPermission(clientToken, null, null, CL_PERMISSION_EXTRAS);
                if (author == null)
                {
                    return Ok(new List<object>() { new { error = "Error 1002, permisos insuficientes" } });
                }

                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("xlsx", out JsonElement xlsxJson) &&
                    json.TryGetProperty("total", out JsonElement totalJson))
                {
                    string xlsxString = xlsxJson.GetString();
                    bool total = totalJson.GetBoolean();

                    result = uploadExcelTemplate(xlsxString, total, author);
                }
            }
            catch (Exception)
            {
                result = new List<object>() { new { error = "Error 5821, no se ha podido procesar el documento" } };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "upload-template/")]
        public async Task<IActionResult> UploadTemplate()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            try
            {
                ResultadoAcceso access = HasPermission("ExtraHoursIncidence.UploadTemplate", securityToken);
                if (!access.Acceso)
                {
                    return Ok(new List<object>() { new { error = "Error 1001, permisos insuficientes" } });
                }

                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("xlsx", out JsonElement xlsxJson) &&
                    json.TryGetProperty("total", out JsonElement totalJson) &&
                    json.TryGetProperty("jefeDni", out JsonElement jefeDniJson) &&
                    json.TryGetProperty("jefePwd", out JsonElement jefePwdJson))
                {
                    string xlsxString = xlsxJson.GetString();
                    bool total = totalJson.GetBoolean();
                    string jefeDni = jefeDniJson.GetString();
                    string jefePwd = jefePwdJson.GetString();

                    if (!access.EsJefe && !HasJefePermission(null, null, "NotAttendIncidence.Create", jefeDni, jefePwd))
                    {
                        result = new List<object>() { new { error = "Error 1008, credenciales de jefe incorrectas" } };
                    }
                    else
                    {
                        result = uploadExcelTemplate(xlsxString, total, FindUsernameBySecurityToken(securityToken));
                    }
                }
            }
            catch (Exception)
            {
                result = new List<object>() { new { error = "Error 5821, no se ha podido procesar el documento" } };
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        public struct ExtraHoursIncidence
        {
            public string id { get; set; }
            public int number { get; set; }
            public string companyId { get; set; }
            public string centroId { get; set; }
            public string centroAlias { get; set; }
            public DateTime day { get; set; }
            public string state { get; set; } // acumulativa: acumulativa | total: pendiente-validar -> validada (sin-subir)
            public DateTime creationTime { get; set; }
            public string createdBy { get; set; }
            public bool total { get; set; }
            public bool verified { get; set; }
            public List<ExtraHoursEntry> entries { get; set; }
        }

        public struct ExtraHoursEntry
        {
            public string id { get; set; }
            public string candidateId { get; set; }
            public string candidateDni { get; set; }
            public string candidateName { get; set; }
            public string concepto { get; set; }
            public double cantidad { get; set; }    //horas: a cuanto se pagan las horas | plus: a cuanto se paga el plus | vacaciones: 0
            public double cantidadTotal { get; set; }
            public double multiplicador { get; set; } //horas: cuantas horas | plus: cuantos pluses | vacaciones: 1 
            public DateTime? vacacionesInicio { get; set; }
            public DateTime? vacacionesFin { get; set; }
            public bool neto { get; set; }
            public string tipo { get; set; } // horas, plus, vacaciones
            public bool preestablecida { get; set; }
            public string createdBy { get; set; } // Used in overview
        }

        public struct ExtraHourTotalReport
        {
            public string id { get; set; }
            public string companyId { get; set; }
            public string centroId { get; set; }
            public string companyName { get; set; }
            public string companyCif { get; set; }
            public string centroCcc { get; set; }
            public string centroAlias { get; set; }
            public string centroDomicilio { get; set; }
            public DateTime monthYeardate { get; set; }
            public DateTime? uploadDate { get; set; }
            public DateTime? validationDate { get; set; }
            public string state { get; set; }
            public string authorName { get; set; }
            public bool rrhhUnreaded { get; set; }
        }

        public struct TemplateRowInfo
        {
            public string id { get; set; }
            public string name { get; set; }
            public string dni { get; set; }
            public List<ExtraHoursEntry> hours { get; set; }
            public List<ExtraHoursEntry> pluses { get; set; }
            public List<ExtraHoursEntry> vacations { get; set; }
        }

        public struct ExtraHoursEntryResume
        {
            public List<ExtraHoursEntryResumeDay> dias { get; set; }
            public List<ExtraHoursEntry> vacaciones { get; set; }
            public bool empty { get; set; }
        }
        public struct ExtraHoursEntryResumeDay
        {
            public DateTime date { get; set; }
            public List<ExtraHoursEntry> extras { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static List<string> fetchReportIdsOfMonth(DateTime day, string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Obtener el ultimo reporte total del mes del centro, si lo hay
            string lastTotalId = null;
            DateTime? lastTotalCreationTime = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT TOP 1 I.id, I.creationTime " +
                    "FROM incidencia_horas_extra I " +
                    "WHERE I.centroId = @CENTRO AND MONTH(I.day) = MONTH(@DAY) AND YEAR(I.day) = YEAR(@DAY) AND I.total = 1 " +
                    "ORDER BY I.creationTime DESC";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@DAY", day);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        lastTotalId = reader.GetString(reader.GetOrdinal("id"));
                        lastTotalCreationTime = reader.GetDateTime(reader.GetOrdinal("creationTime"));
                    }
                }
            }

            //Obtener los reportes parciales posteriores al reporte total encontrado, dentro del centro y mes. Si no se encuentra, todos los del mes.
            List<string> selectedReports = new();
            if (lastTotalId != null) selectedReports.Add(lastTotalId);
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT I.id " +
                    "FROM incidencia_horas_extra I " +
                    "WHERE I.centroId = @CENTRO AND MONTH(I.day) = MONTH(@DAY) AND YEAR(I.day) = YEAR(@DAY) AND I.total = 0 AND " +
                    "(@LAST IS NULL OR @LAST < I.creationTime)";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@DAY", day);
                command.Parameters.AddWithValue("@LAST", lastTotalCreationTime.HasValue ? lastTotalCreationTime.Value : DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        selectedReports.Add(reader.GetString(reader.GetOrdinal("id")));
                    }
                }
            }

            return selectedReports;
        }

        public static List<ExtraHoursEntry> fetchEntriesOfMonth(SqlConnection conn, DateTime day, string centroId)
        {
            List<ExtraHoursEntry> entries = new();

            //Obtener los reportes que importan para este mes
            List<string> selectedReports = fetchReportIdsOfMonth(day, centroId, conn);

            //Obtener las entradas de todos los reportes seleccionados
            foreach (string id in selectedReports)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT E.*, C.id as candidateId, I.createdBy " +
                        "FROM incidencia_horas_extra_entrada E INNER JOIN incidencia_horas_extra I ON(E.incidenceId = I.id) LEFT OUTER JOIN candidatos C ON(E.candidateDni = C.dni) " +
                        "WHERE I.id = @ID";
                    command.Parameters.AddWithValue("@ID", id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var entry = new ExtraHoursEntry()
                            {
                                candidateId = reader.IsDBNull(reader.GetOrdinal("candidateId"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("candidateId")),
                                candidateDni = reader.GetString(reader.GetOrdinal("candidateDni")),
                                candidateName = reader.GetString(reader.GetOrdinal("candidateName")),
                                concepto = reader.GetString(reader.GetOrdinal("concepto")),
                                cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                                multiplicador = reader.GetDouble(reader.GetOrdinal("multiplicador")),
                                vacacionesInicio = reader.IsDBNull(reader.GetOrdinal("vacacionesInicio")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesInicio")),
                                vacacionesFin = reader.IsDBNull(reader.GetOrdinal("vacacionesFin")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesFin")),
                                neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1,
                                tipo = reader.GetString(reader.GetOrdinal("tipo")),
                                preestablecida = reader.GetInt32(reader.GetOrdinal("preestablecida")) == 1,
                                createdBy = reader.GetString(reader.GetOrdinal("createdBy"))
                            };
                            entry.cantidadTotal = Math.Round(entry.cantidad * entry.multiplicador * 100) / 100;
                            entries.Add(entry);
                        }
                    }
                }
            }

            return entries;
        }

        public static FileContentResult generateExcelTemplate(HttpContext httpContext, SqlConnection conn, DateTime day, string clientToken, string securityToken, string companyId = null, string centroId = null, List<ExtraHoursEntry> rawEntries = null, bool isReadOnly = false)
        {
            aggregateForReport(conn, clientToken, securityToken, companyId, centroId, rawEntries ?? new List<ExtraHoursEntry>(), out List<TemplateRowInfo> rowsInfo, out List<CompanyData> companies);

            //Generar libro
            IWorkbook workbook = new XSSFWorkbook();
            IDataFormat dataFormatCustom = workbook.CreateDataFormat();

            //Fuentes
            IFont fontTitle = workbook.CreateFont();
            fontTitle.FontName = "Century Gothic";
            fontTitle.IsBold = true;
            fontTitle.FontHeightInPoints = 15;
            fontTitle.Color = IndexedColors.Black.Index;

            IFont fontHeaderBlack = workbook.CreateFont();
            fontHeaderBlack.FontName = "Century Gothic";
            fontHeaderBlack.IsBold = true;
            fontHeaderBlack.FontHeightInPoints = 13;
            fontHeaderBlack.Color = IndexedColors.Black.Index;

            IFont fontHeader = workbook.CreateFont();
            fontHeader.FontName = "Century Gothic";
            fontHeader.IsBold = true;
            fontHeader.FontHeightInPoints = 11;
            fontHeader.Color = IndexedColors.White.Index;

            IFont fontBlack = workbook.CreateFont();
            fontBlack.FontName = "Century Gothic";
            fontBlack.IsBold = true;
            fontBlack.FontHeightInPoints = 10;
            fontBlack.Color = IndexedColors.Black.Index;

            //Formatos
            ICellStyle titleStyle = workbook.CreateCellStyle();
            titleStyle.SetFont(fontTitle);

            XSSFCellStyle headerYellowStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerYellowStyle.SetFont(fontHeaderBlack);
            headerYellowStyle.FillForegroundColorColor = new XSSFColor(new byte[3] { 255, 242, 204 });
            headerYellowStyle.FillPattern = FillPattern.SolidForeground;
            headerYellowStyle.BorderTop = BorderStyle.Thin;
            headerYellowStyle.BorderBottom = BorderStyle.Thin;
            headerYellowStyle.BorderRight = BorderStyle.Thin;
            headerYellowStyle.BorderLeft = BorderStyle.Thin;

            XSSFCellStyle headerYellowDateStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerYellowDateStyle.SetFont(fontHeaderBlack);
            headerYellowDateStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 255, 242, 204 });
            headerYellowDateStyle.FillPattern = FillPattern.SolidForeground;
            headerYellowDateStyle.BorderTop = BorderStyle.Thin;
            headerYellowDateStyle.BorderBottom = BorderStyle.Thin;
            headerYellowDateStyle.BorderRight = BorderStyle.Thin;
            headerYellowDateStyle.BorderLeft = BorderStyle.Thin;
            headerYellowDateStyle.DataFormat = dataFormatCustom.GetFormat("dd/MM/yyyy");
            headerYellowDateStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerGreenStyle.SetFont(fontHeader);
            headerGreenStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 112, 173, 71 });
            headerGreenStyle.FillPattern = FillPattern.SolidForeground;
            headerGreenStyle.BorderTop = BorderStyle.Thin;
            headerGreenStyle.BorderBottom = BorderStyle.Thin;
            headerGreenStyle.BorderRight = BorderStyle.Thin;
            headerGreenStyle.BorderLeft = BorderStyle.Thin;
            headerGreenStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerRedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerRedStyle.SetFont(fontHeader);
            headerRedStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 192, 0, 0 });
            headerRedStyle.FillPattern = FillPattern.SolidForeground;
            headerRedStyle.BorderTop = BorderStyle.Thin;
            headerRedStyle.BorderBottom = BorderStyle.Thin;
            headerRedStyle.BorderRight = BorderStyle.Thin;
            headerRedStyle.BorderLeft = BorderStyle.Thin;
            headerRedStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerBlueStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerBlueStyle.SetFont(fontHeader);
            headerBlueStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 47, 117, 181 });
            headerBlueStyle.FillPattern = FillPattern.SolidForeground;
            headerBlueStyle.BorderTop = BorderStyle.Thin;
            headerBlueStyle.BorderBottom = BorderStyle.Thin;
            headerBlueStyle.BorderRight = BorderStyle.Thin;
            headerBlueStyle.BorderLeft = BorderStyle.Thin;
            headerBlueStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerOrangeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerOrangeStyle.SetFont(fontHeader);
            headerOrangeStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 237, 125, 49 });
            headerOrangeStyle.FillPattern = FillPattern.SolidForeground;
            headerOrangeStyle.BorderTop = BorderStyle.Thin;
            headerOrangeStyle.BorderBottom = BorderStyle.Thin;
            headerOrangeStyle.BorderRight = BorderStyle.Thin;
            headerOrangeStyle.BorderLeft = BorderStyle.Thin;
            headerOrangeStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerObsidianStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerObsidianStyle.SetFont(fontHeader);
            headerObsidianStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 51, 63, 79 });
            headerObsidianStyle.FillPattern = FillPattern.SolidForeground;
            headerObsidianStyle.BorderTop = BorderStyle.Thin;
            headerObsidianStyle.BorderBottom = BorderStyle.Thin;
            headerObsidianStyle.BorderRight = BorderStyle.Thin;
            headerObsidianStyle.BorderLeft = BorderStyle.Thin;
            headerObsidianStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle tableCellGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            //tableCellGreenStyle.SetFont(fontBlack);
            tableCellGreenStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 226, 239, 218 });
            tableCellGreenStyle.FillPattern = FillPattern.SolidForeground;
            tableCellGreenStyle.BorderTop = BorderStyle.Thin;
            tableCellGreenStyle.BorderBottom = BorderStyle.Thin;
            tableCellGreenStyle.BorderRight = BorderStyle.Thin;
            tableCellGreenStyle.BorderLeft = BorderStyle.Thin;

            XSSFCellStyle tableCellRedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            //tableCellRedStyle.SetFont(fontBlack);
            tableCellRedStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 252, 228, 214 });
            tableCellRedStyle.FillPattern = FillPattern.SolidForeground;
            tableCellRedStyle.BorderTop = BorderStyle.Thin;
            tableCellRedStyle.BorderBottom = BorderStyle.Thin;
            tableCellRedStyle.BorderRight = BorderStyle.Thin;
            tableCellRedStyle.BorderLeft = BorderStyle.Thin;

            XSSFCellStyle lockedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            lockedStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 117, 113, 113 });
            lockedStyle.FillPattern = FillPattern.SolidForeground;
            lockedStyle.BorderTop = BorderStyle.Thin;
            lockedStyle.BorderBottom = BorderStyle.Thin;
            lockedStyle.BorderRight = BorderStyle.Thin;
            lockedStyle.BorderLeft = BorderStyle.Thin;
            lockedStyle.IsLocked = true;

            XSSFCellStyle freeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            freeStyle.BorderTop = BorderStyle.Thin;
            freeStyle.BorderBottom = BorderStyle.Thin;
            freeStyle.BorderRight = BorderStyle.Thin;
            freeStyle.BorderLeft = BorderStyle.Thin;
            freeStyle.Alignment = HorizontalAlignment.Center;
            freeStyle.IsLocked = isReadOnly;

            ISheet sheet = workbook.CreateSheet("Resumen");

            //Tamaños de filas y columnas
            ICell cell;
            IRow row;
            sheet.SetColumnWidth(1, 25 * 256);
            sheet.SetColumnWidth(2, 35 * 256);
            sheet.SetColumnWidth(4, 35 * 256);
            sheet.SetColumnWidth(5, 35 * 256);

            //Lista de filas porque npoi no ayuda
            Dictionary<int, IRow> rows = new();

            //Avisos de la primera pagina
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 1, 5));
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(2, 2, 1, 5));
            row = getOrCreateRow(sheet, rows, 1);
            cell = row.CreateCell(1);
            cell.CellStyle = titleStyle;
            if (isReadOnly)
            {
                cell.SetCellValue("ESTE DOCUMENTO NO ES EDITABLE");
                getOrCreateRow(sheet, rows, 2).ZeroHeight = true;
            }
            else
            {
                cell.SetCellValue("Por favor, no modifique el orden de páginas de este documento ni las cabezeras de las tablas.");
                row = getOrCreateRow(sheet, rows, 2);
                cell = row.CreateCell(1);
                cell.CellStyle = titleStyle;
                cell.SetCellValue("Modifique solo lo indicado en cada hoja. Gracias.");
            }

            //Fecha
            row = getOrCreateRow(sheet, rows, 4);
            cell = row.CreateCell(1);
            cell.CellStyle = headerYellowStyle;
            cell.SetCellValue("Fecha del reporte");
            cell = row.CreateCell(2);
            cell.CellStyle = headerYellowDateStyle;
            cell.SetCellValue(day.Date);

            //Listado de trabajadores
            row = getOrCreateRow(sheet, rows, 6);
            cell = row.CreateCell(1);
            cell.CellStyle = headerGreenStyle;
            cell.SetCellValue("Trabajadores");
            cell = row.CreateCell(2);
            cell.CellStyle = headerGreenStyle;
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(6, 6, 1, 2));
            row = getOrCreateRow(sheet, rows, 7);
            cell = row.CreateCell(1);
            cell.CellStyle = headerGreenStyle;
            cell.SetCellValue("DNI");
            cell = row.CreateCell(2);
            cell.CellStyle = headerGreenStyle;
            cell.SetCellValue("Nombre");
            for (int i = 0; i < rowsInfo.Count; i++)
            {
                row = getOrCreateRow(sheet, rows, 8 + i);
                cell = row.CreateCell(1);
                cell.CellStyle = tableCellGreenStyle;
                cell.SetCellValue(rowsInfo[i].dni);
                cell = row.CreateCell(2);
                cell.CellStyle = tableCellGreenStyle;
                cell.SetCellValue(rowsInfo[i].name);
            }

            //Listado de empresas y centros
            int empRow = 7;
            row = getOrCreateRow(sheet, rows, empRow);
            cell = row.CreateCell(4);
            cell.CellStyle = headerRedStyle;
            cell.SetCellValue("Empresas");
            cell = row.CreateCell(5);
            cell.CellStyle = headerRedStyle;
            cell.SetCellValue("Centros");
            empRow++;
            foreach (CompanyData company in companies)
            {
                row = getOrCreateRow(sheet, rows, empRow);
                cell = row.CreateCell(4);
                cell.CellStyle = tableCellRedStyle;
                cell.SetCellValue(company.nombre);
                foreach (CentroData centro in company.centros)
                {
                    row = getOrCreateRow(sheet, rows, empRow);
                    if (row.GetCell(4) == null)
                    {
                        cell = row.CreateCell(4);
                        cell.CellStyle = tableCellRedStyle;
                    }
                    cell = row.CreateCell(5);
                    cell.CellStyle = tableCellRedStyle;
                    cell.SetCellValue(centro.alias);
                    empRow++;
                }

                if (company.centros.Count == 0) empRow++;
            }
            sheet.ProtectSheet("1234");

            //3 paginas de datos
            foreach (string type in new[] { "horas", "plus", "vacaciones" })
            {
                switch (type)
                {
                    case "horas":
                        sheet = workbook.CreateSheet("Horas extra");
                        break;
                    case "plus":
                        sheet = workbook.CreateSheet("Pluses");
                        break;
                    case "vacaciones":
                        sheet = workbook.CreateSheet("Vacaciones");
                        break;
                }
                rows = new();

                //Avisos
                ICell firstRegionalInstructionCell = null, secondRegionalInstructionCell = null;
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 1, 20));
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(2, 2, 1, 20));
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(3, 3, 1, 20));
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(4, 4, 1, 20));
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(5, 5, 1, 20));
                row = getOrCreateRow(sheet, rows, 1);
                cell = row.CreateCell(1);
                cell.CellStyle = titleStyle;
                if (isReadOnly)
                {
                    cell.SetCellValue("ESTE DOCUMENTO NO ES EDITABLE");
                    getOrCreateRow(sheet, rows, 2).ZeroHeight = true;
                    getOrCreateRow(sheet, rows, 3).ZeroHeight = true;
                    getOrCreateRow(sheet, rows, 4).ZeroHeight = true;
                    getOrCreateRow(sheet, rows, 5).ZeroHeight = true;
                }
                else
                {
                    cell.SetCellValue("Una celda gris indica que el puesto del trabajador no tiene ese tipo de extra.");
                    row = getOrCreateRow(sheet, rows, 2);
                    cell = row.CreateCell(1);
                    firstRegionalInstructionCell = cell;
                    cell.CellStyle = titleStyle;
                    row = getOrCreateRow(sheet, rows, 3);
                    cell = row.CreateCell(1);
                    secondRegionalInstructionCell = cell;
                    cell.CellStyle = titleStyle;
                    row = getOrCreateRow(sheet, rows, 4);
                    cell = row.CreateCell(1);
                    cell.CellStyle = titleStyle;
                    cell.SetCellValue("Para indicar un extra que no aparezca en ninguna columna, deberá acceder al portal de cliente");
                    row = getOrCreateRow(sheet, rows, 5);
                    cell = row.CreateCell(1);
                    cell.CellStyle = titleStyle;
                    cell.SetCellValue("y generar un reporte online con dichos extras.");
                }

                ICellStyle regionalHeaderSyle = headerGreenStyle;
                List<ExtraHoursEntry> infoRowPluses = new();
                switch (type)
                {
                    case "horas":
                        regionalHeaderSyle = headerBlueStyle;
                        infoRowPluses = rowsInfo.SelectMany(r => r.hours).ToList();
                        firstRegionalInstructionCell?.SetCellValue("Introduzca la CANTIDAD de horas(0, 0.5, 1, 1.5, etc...) extras por trabajador en su categoría correspondiente");
                        secondRegionalInstructionCell?.SetCellValue("en LAS CELDAS BLANCAS.");
                        break;
                    case "plus":
                        regionalHeaderSyle = headerOrangeStyle;
                        infoRowPluses = rowsInfo.SelectMany(r => r.pluses).ToList();
                        firstRegionalInstructionCell?.SetCellValue("Introduzca la CANTIDAD de pluses (0, 1, 2, etc...) por trabajador en su categoría correspondiente");
                        secondRegionalInstructionCell?.SetCellValue("en LAS CELDAS BLANCAS.");
                        break;
                    case "vacaciones":
                        regionalHeaderSyle = headerObsidianStyle;
                        infoRowPluses = rowsInfo.SelectMany(r => r.vacations).ToList();
                        firstRegionalInstructionCell?.SetCellValue("Introduzca la CANTIDAD de pluses (0, 1, 2, etc...) de vacaciones por trabajador en su categoría correspondiente");
                        secondRegionalInstructionCell?.SetCellValue("en LAS CELDAS BLANCAS.");
                        break;
                }

                //Obtener los tipos de pluses sin repeticion 
                List<ExtraHoursEntry> pluses = infoRowPluses.GroupBy(r => r.candidateDni).Select(r => r.First()).ToList();

                //Bloquear todas las casillas por defecto
                for (int r = 7; r <= rowsInfo.Count + 9; r++)
                {
                    for (int c = 1; c <= pluses.Count + 2; c++)
                    {
                        getOrCreateRow(sheet, rows, r).CreateCell(c).CellStyle = lockedStyle;
                    }
                }

                //Cabecera de trabajadores
                row = getOrCreateRow(sheet, rows, 8);
                cell = row.CreateCell(1);
                cell.CellStyle = headerGreenStyle;
                cell.SetCellValue("Trabajadores");
                cell = row.CreateCell(2);
                cell.CellStyle = headerGreenStyle;
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(8, 8, 1, 2));
                row = getOrCreateRow(sheet, rows, 9);
                cell = row.CreateCell(1);
                cell.CellStyle = headerGreenStyle;
                cell.SetCellValue("DNI");
                cell = row.CreateCell(2);
                cell.CellStyle = headerGreenStyle;
                cell.SetCellValue("Nombre");

                //Insertar las cabeceras de cada plus
                int pCol = 3;
                foreach (ExtraHoursEntry plus in pluses)
                {
                    sheet.SetColumnWidth(pCol, 25 * 256);
                    cell = getOrCreateRow(sheet, rows, 7).GetCell(pCol);
                    cell.SetCellValue(plus.candidateDni);
                    cell = getOrCreateRow(sheet, rows, 8).GetCell(pCol);
                    cell.CellStyle = regionalHeaderSyle;
                    cell.SetCellValue(plus.concepto);
                    cell = getOrCreateRow(sheet, rows, 9).GetCell(pCol);
                    cell.CellStyle = regionalHeaderSyle;
                    if (plus.cantidad != 0) cell.SetCellValue(plus.cantidad + "€ " + (plus.neto ? "Neto" : "Bruto"));
                    if (plus.vacacionesInicio.HasValue && plus.vacacionesFin.HasValue) cell.SetCellValue($"{plus.vacacionesInicio.Value.ToString("dd/MM/yyyy")} - {plus.vacacionesFin.Value.ToString("dd/MM/yyyy")}");
                    pCol++;
                }

                //Insertar trabajadores y desbloquear las casillas que corresponden
                int cRow = 10;
                foreach (TemplateRowInfo rowInfo in rowsInfo)
                {
                    row = getOrCreateRow(sheet, rows, cRow);
                    cell = row.GetCell(1);
                    cell.CellStyle = tableCellGreenStyle;
                    cell.SetCellValue(rowInfo.dni);
                    cell = row.GetCell(2);
                    cell.CellStyle = tableCellGreenStyle;
                    cell.SetCellValue(rowInfo.name);

                    //Desbloquear las que correspondan
                    IRow idRow = getOrCreateRow(sheet, rows, 7);
                    for (int i = 3; i < pCol; i++)
                    {
                        cell = row.GetCell(i);
                        string plusId = idRow.GetCell(i).StringCellValue;
                        ExtraHoursEntry plus = new ExtraHoursEntry() { candidateDni = null };
                        switch (type)
                        {
                            case "horas":
                                plus = rowInfo.hours.FirstOrDefault(p => p.candidateDni == plusId);
                                break;
                            case "plus":
                                plus = rowInfo.pluses.FirstOrDefault(p => p.candidateDni == plusId);
                                break;
                            case "vacaciones":
                                plus = rowInfo.vacations.FirstOrDefault(p => p.candidateDni == plusId);
                                break;
                        }
                        if (plus.candidateDni != null)
                        {
                            cell.CellStyle = freeStyle;
                            if (plus.multiplicador != 0)
                            {
                                if (type.Equals("vacaciones"))
                                    cell.SetCellValue("X");
                                else
                                    cell.SetCellValue(plus.multiplicador);
                            }
                        }
                    }
                    cRow++;
                }

                //Eliminar las IDS
                pCol = 3;
                foreach (ExtraHoursEntry plus in pluses)
                {
                    cell = getOrCreateRow(sheet, rows, 7).GetCell(pCol);
                    cell.SetCellValue("");
                    pCol++;
                }

                //Propiedades generales de la tabla
                sheet.SetColumnWidth(1, 20 * 256);
                sheet.SetColumnWidth(2, 35 * 256);
                getOrCreateRow(sheet, rows, 7).ZeroHeight = true;

                sheet.ProtectSheet("1234");
            }

            string tmpDir = GetTemporaryDirectory();

            //Guardado
            string fileName = "Reporte.xlsx";
            string tmpFile = Path.Combine(tmpDir, fileName);
            FileStream file = new FileStream(tmpFile, FileMode.Create);
            workbook.Write(file);
            file.Close();

            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            httpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(tmpFile), contentType)
            {
                FileDownloadName = fileName
            };

            Directory.Delete(tmpDir, true);

            return response;
        }

        public static FileContentResult generateExcelOverview(HttpContext httpContext, SqlConnection conn, DateTime day, string clientToken, string securityToken, List<ExtraHoursEntry> rawEntries, string companyId = null, string centroId = null)
        {
            //Agrupar por creador
            Dictionary<string, string> names = new() { { "System", "Sistema" } };
            //Antes names se usaba para asociar los emails a los nomrbes de los usuarios CL
            HashSet<string> rjUsers = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT username FROM users";
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rjUsers.Add(reader.GetString(reader.GetOrdinal("username")));
                    }
                }
            }
            IEnumerable<IGrouping<string, ExtraHoursEntry>> rawGroupedEntries = rawEntries.GroupBy(e => rjUsers.Contains(e.createdBy) ? "ThinkAndJob" : (names.ContainsKey(e.createdBy) ? names[e.createdBy] : e.createdBy));
            List<TemplateRowInfo> allRowsInfo = new();
            Dictionary<string, List<TemplateRowInfo>> groupedRowsInfo = new();
            List<CompanyData> companies = new();

            aggregateForReport(conn, clientToken, securityToken, companyId, centroId, new(), out allRowsInfo, out companies);

            foreach (IGrouping<string, ExtraHoursEntry> group in rawGroupedEntries)
            {
                aggregateForReport(conn, clientToken, securityToken, companyId, centroId, group.ToList(), out List<TemplateRowInfo> rowsInfo, out _);
                groupedRowsInfo[group.Key] = rowsInfo;
            }

            //Generar libro
            IWorkbook workbook = new XSSFWorkbook();
            IDataFormat dataFormatCustom = workbook.CreateDataFormat();

            //Fuentes
            IFont fontTitle = workbook.CreateFont();
            fontTitle.FontName = "Century Gothic";
            fontTitle.IsBold = true;
            fontTitle.FontHeightInPoints = 15;
            fontTitle.Color = IndexedColors.Black.Index;

            IFont fontHeaderBlack = workbook.CreateFont();
            fontHeaderBlack.FontName = "Century Gothic";
            fontHeaderBlack.IsBold = true;
            fontHeaderBlack.FontHeightInPoints = 13;
            fontHeaderBlack.Color = IndexedColors.Black.Index;

            IFont fontHeader = workbook.CreateFont();
            fontHeader.FontName = "Century Gothic";
            fontHeader.IsBold = true;
            fontHeader.FontHeightInPoints = 11;
            fontHeader.Color = IndexedColors.White.Index;

            IFont fontBlack = workbook.CreateFont();
            fontBlack.FontName = "Century Gothic";
            fontBlack.IsBold = true;
            fontBlack.FontHeightInPoints = 10;
            fontBlack.Color = IndexedColors.Black.Index;

            //Formatos
            ICellStyle titleStyle = workbook.CreateCellStyle();
            titleStyle.SetFont(fontTitle);

            XSSFCellStyle headerYellowStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerYellowStyle.SetFont(fontHeaderBlack);
            headerYellowStyle.FillForegroundColorColor = new XSSFColor(new byte[3] { 255, 242, 204 });
            headerYellowStyle.FillPattern = FillPattern.SolidForeground;
            headerYellowStyle.BorderTop = BorderStyle.Thin;
            headerYellowStyle.BorderBottom = BorderStyle.Thin;
            headerYellowStyle.BorderRight = BorderStyle.Thin;
            headerYellowStyle.BorderLeft = BorderStyle.Thin;

            XSSFCellStyle headerYellowDateStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerYellowDateStyle.SetFont(fontHeaderBlack);
            headerYellowDateStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 255, 242, 204 });
            headerYellowDateStyle.FillPattern = FillPattern.SolidForeground;
            headerYellowDateStyle.BorderTop = BorderStyle.Thin;
            headerYellowDateStyle.BorderBottom = BorderStyle.Thin;
            headerYellowDateStyle.BorderRight = BorderStyle.Thin;
            headerYellowDateStyle.BorderLeft = BorderStyle.Thin;
            headerYellowDateStyle.DataFormat = dataFormatCustom.GetFormat("dd/MM/yyyy");
            headerYellowDateStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerGreenStyle.SetFont(fontHeader);
            headerGreenStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 112, 173, 71 });
            headerGreenStyle.FillPattern = FillPattern.SolidForeground;
            headerGreenStyle.BorderTop = BorderStyle.Thin;
            headerGreenStyle.BorderBottom = BorderStyle.Thin;
            headerGreenStyle.BorderRight = BorderStyle.Thin;
            headerGreenStyle.BorderLeft = BorderStyle.Thin;
            headerGreenStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerRedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerRedStyle.SetFont(fontHeader);
            headerRedStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 192, 0, 0 });
            headerRedStyle.FillPattern = FillPattern.SolidForeground;
            headerRedStyle.BorderTop = BorderStyle.Thin;
            headerRedStyle.BorderBottom = BorderStyle.Thin;
            headerRedStyle.BorderRight = BorderStyle.Thin;
            headerRedStyle.BorderLeft = BorderStyle.Thin;
            headerRedStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerBlueStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerBlueStyle.SetFont(fontHeader);
            headerBlueStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 47, 117, 181 });
            headerBlueStyle.FillPattern = FillPattern.SolidForeground;
            headerBlueStyle.BorderTop = BorderStyle.Thin;
            headerBlueStyle.BorderBottom = BorderStyle.Thin;
            headerBlueStyle.BorderRight = BorderStyle.Thin;
            headerBlueStyle.BorderLeft = BorderStyle.Thin;
            headerBlueStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerOrangeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerOrangeStyle.SetFont(fontHeader);
            headerOrangeStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 237, 125, 49 });
            headerOrangeStyle.FillPattern = FillPattern.SolidForeground;
            headerOrangeStyle.BorderTop = BorderStyle.Thin;
            headerOrangeStyle.BorderBottom = BorderStyle.Thin;
            headerOrangeStyle.BorderRight = BorderStyle.Thin;
            headerOrangeStyle.BorderLeft = BorderStyle.Thin;
            headerOrangeStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle headerObsidianStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerObsidianStyle.SetFont(fontHeader);
            headerObsidianStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 51, 63, 79 });
            headerObsidianStyle.FillPattern = FillPattern.SolidForeground;
            headerObsidianStyle.BorderTop = BorderStyle.Thin;
            headerObsidianStyle.BorderBottom = BorderStyle.Thin;
            headerObsidianStyle.BorderRight = BorderStyle.Thin;
            headerObsidianStyle.BorderLeft = BorderStyle.Thin;
            headerObsidianStyle.Alignment = HorizontalAlignment.Center;

            XSSFCellStyle tableCellGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            //tableCellGreenStyle.SetFont(fontBlack);
            tableCellGreenStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 226, 239, 218 });
            tableCellGreenStyle.FillPattern = FillPattern.SolidForeground;
            tableCellGreenStyle.BorderTop = BorderStyle.Thin;
            tableCellGreenStyle.BorderBottom = BorderStyle.Thin;
            tableCellGreenStyle.BorderRight = BorderStyle.Thin;
            tableCellGreenStyle.BorderLeft = BorderStyle.Thin;

            XSSFCellStyle tableCellRedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            //tableCellRedStyle.SetFont(fontBlack);
            tableCellRedStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 252, 228, 214 });
            tableCellRedStyle.FillPattern = FillPattern.SolidForeground;
            tableCellRedStyle.BorderTop = BorderStyle.Thin;
            tableCellRedStyle.BorderBottom = BorderStyle.Thin;
            tableCellRedStyle.BorderRight = BorderStyle.Thin;
            tableCellRedStyle.BorderLeft = BorderStyle.Thin;

            XSSFCellStyle lockedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            lockedStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 117, 113, 113 });
            lockedStyle.FillPattern = FillPattern.SolidForeground;
            lockedStyle.BorderTop = BorderStyle.Thin;
            lockedStyle.BorderBottom = BorderStyle.Thin;
            lockedStyle.BorderRight = BorderStyle.Thin;
            lockedStyle.BorderLeft = BorderStyle.Thin;
            lockedStyle.IsLocked = true;

            XSSFCellStyle freeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            freeStyle.BorderTop = BorderStyle.Thin;
            freeStyle.BorderBottom = BorderStyle.Thin;
            freeStyle.BorderRight = BorderStyle.Thin;
            freeStyle.BorderLeft = BorderStyle.Thin;
            freeStyle.Alignment = HorizontalAlignment.Center;
            freeStyle.IsLocked = true;

            ISheet sheet = workbook.CreateSheet("Resumen");

            //Tamaños de filas y columnas
            ICell cell;
            IRow row;
            sheet.SetColumnWidth(1, 25 * 256);
            sheet.SetColumnWidth(2, 35 * 256);
            sheet.SetColumnWidth(4, 35 * 256);
            sheet.SetColumnWidth(5, 35 * 256);

            //Lista de filas porque npoi no ayuda
            Dictionary<int, IRow> rows = new();

            //Avisos de la primera pagina
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 1, 5));
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(2, 2, 1, 5));
            row = getOrCreateRow(sheet, rows, 1);
            cell = row.CreateCell(1);
            cell.CellStyle = titleStyle;
            cell.SetCellValue("ESTE DOCUMENTO NO ES EDITABLE");
            getOrCreateRow(sheet, rows, 2).ZeroHeight = true;

            //Fecha
            row = getOrCreateRow(sheet, rows, 4);
            cell = row.CreateCell(1);
            cell.CellStyle = headerYellowStyle;
            cell.SetCellValue("Fecha del reporte");
            cell = row.CreateCell(2);
            cell.CellStyle = headerYellowDateStyle;
            cell.SetCellValue(day.Date);

            //Listado de trabajadores
            row = getOrCreateRow(sheet, rows, 6);
            cell = row.CreateCell(1);
            cell.CellStyle = headerGreenStyle;
            cell.SetCellValue("Trabajadores");
            cell = row.CreateCell(2);
            cell.CellStyle = headerGreenStyle;
            sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(6, 6, 1, 2));
            row = getOrCreateRow(sheet, rows, 7);
            cell = row.CreateCell(1);
            cell.CellStyle = headerGreenStyle;
            cell.SetCellValue("DNI");
            cell = row.CreateCell(2);
            cell.CellStyle = headerGreenStyle;
            cell.SetCellValue("Nombre");
            for (int i = 0; i < allRowsInfo.Count; i++)
            {
                row = getOrCreateRow(sheet, rows, 8 + i);
                cell = row.CreateCell(1);
                cell.CellStyle = tableCellGreenStyle;
                cell.SetCellValue(allRowsInfo[i].dni);
                cell = row.CreateCell(2);
                cell.CellStyle = tableCellGreenStyle;
                cell.SetCellValue(allRowsInfo[i].name);
            }

            //Listado de empresas y centros
            int empRow = 7;
            row = getOrCreateRow(sheet, rows, empRow);
            cell = row.CreateCell(4);
            cell.CellStyle = headerRedStyle;
            cell.SetCellValue("Empresas");
            cell = row.CreateCell(5);
            cell.CellStyle = headerRedStyle;
            cell.SetCellValue("Centros");
            empRow++;
            foreach (CompanyData company in companies)
            {
                row = getOrCreateRow(sheet, rows, empRow);
                cell = row.CreateCell(4);
                cell.CellStyle = tableCellRedStyle;
                cell.SetCellValue(company.nombre);
                foreach (CentroData centro in company.centros)
                {
                    row = getOrCreateRow(sheet, rows, empRow);
                    if (row.GetCell(4) == null)
                    {
                        cell = row.CreateCell(4);
                        cell.CellStyle = tableCellRedStyle;
                    }
                    cell = row.CreateCell(5);
                    cell.CellStyle = tableCellRedStyle;
                    cell.SetCellValue(centro.alias);
                    empRow++;
                }

                if (company.centros.Count == 0) empRow++;
            }
            sheet.ProtectSheet("1234");

            //Una pagina por cada grupo
            foreach (KeyValuePair<string, List<TemplateRowInfo>> rowsInfoKP in groupedRowsInfo)
            {
                string createdBy = rowsInfoKP.Key;
                List<TemplateRowInfo> rowsInfo = rowsInfoKP.Value;

                sheet = workbook.CreateSheet(createdBy);
                rows = new();

                //Avisos
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 1, 20));
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(3, 3, 1, 20));
                row = getOrCreateRow(sheet, rows, 1);
                cell = row.CreateCell(1);
                cell.CellStyle = titleStyle;
                cell.SetCellValue("ESTE DOCUMENTO NO ES EDITABLE");
                row = getOrCreateRow(sheet, rows, 3);
                cell = row.CreateCell(1);
                cell.CellStyle = titleStyle;
                cell.SetCellValue("Extras creados por: " + createdBy);

                int pCol = 1;
                foreach (string type in new[] { "horas", "plus", "vacaciones" })
                {
                    ICellStyle regionalHeaderSyle = headerGreenStyle;
                    List<ExtraHoursEntry> infoRowPluses = new();
                    switch (type)
                    {
                        case "horas":
                            regionalHeaderSyle = headerBlueStyle;
                            infoRowPluses = rowsInfo.SelectMany(r => r.hours).ToList();
                            break;
                        case "plus":
                            regionalHeaderSyle = headerOrangeStyle;
                            infoRowPluses = rowsInfo.SelectMany(r => r.pluses).ToList();
                            break;
                        case "vacaciones":
                            regionalHeaderSyle = headerObsidianStyle;
                            infoRowPluses = rowsInfo.SelectMany(r => r.vacations).ToList();
                            break;
                    }

                    //Obtener los tipos de pluses sin repeticion 
                    List<ExtraHoursEntry> pluses = infoRowPluses.GroupBy(r => r.candidateDni).Select(r => r.First()).ToList();

                    //Cabecera de trabajadores
                    int sCol = pCol;
                    row = getOrCreateRow(sheet, rows, 8);
                    cell = row.CreateCell(pCol);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("Trabajadores");
                    cell = row.CreateCell(pCol + 1);
                    cell.CellStyle = headerGreenStyle;
                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(8, 8, sCol, sCol + 1));
                    row = getOrCreateRow(sheet, rows, 9);
                    cell = row.CreateCell(pCol);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("DNI");
                    cell = row.CreateCell(pCol + 1);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("Nombre");

                    pCol += 2;

                    //Insertar las cabeceras de cada plus
                    foreach (ExtraHoursEntry plus in pluses)
                    {
                        sheet.SetColumnWidth(pCol, 25 * 256);
                        cell = getOrCreateRow(sheet, rows, 7).GetCell(pCol) ?? getOrCreateRow(sheet, rows, 7).CreateCell(pCol);
                        cell.SetCellValue(plus.candidateDni);
                        cell = getOrCreateRow(sheet, rows, 8).GetCell(pCol) ?? getOrCreateRow(sheet, rows, 8).CreateCell(pCol);
                        cell.CellStyle = regionalHeaderSyle;
                        cell.SetCellValue(plus.concepto);
                        cell = getOrCreateRow(sheet, rows, 9).GetCell(pCol) ?? getOrCreateRow(sheet, rows, 9).CreateCell(pCol);
                        cell.CellStyle = regionalHeaderSyle;
                        if (plus.cantidad != 0) cell.SetCellValue(plus.cantidad + "€ " + (plus.neto ? "Neto" : "Bruto"));
                        if (plus.vacacionesInicio.HasValue && plus.vacacionesFin.HasValue) cell.SetCellValue($"{plus.vacacionesInicio.Value.ToString("dd/MM/yyyy")} - {plus.vacacionesFin.Value.ToString("dd/MM/yyyy")}");
                        pCol++;
                    }

                    //Insertar trabajadores y desbloquear las casillas que corresponden
                    int cRow = 10;
                    foreach (TemplateRowInfo rowInfo in rowsInfo)
                    {
                        row = getOrCreateRow(sheet, rows, cRow);
                        bool anyPlus = false;

                        //Desbloquear las que correspondan
                        IRow idRow = getOrCreateRow(sheet, rows, 7);
                        for (int i = sCol + 2; i < pCol; i++)
                        {
                            cell = row.GetCell(i) ?? row.CreateCell(i);
                            string plusId = idRow.GetCell(i).StringCellValue; //TODO: fix esto
                            //string plusId = "";
                            ExtraHoursEntry plus = new ExtraHoursEntry() { candidateDni = null };
                            switch (type)
                            {
                                case "horas":
                                    plus = rowInfo.hours.FirstOrDefault(p => p.candidateDni == plusId);
                                    break;
                                case "plus":
                                    plus = rowInfo.pluses.FirstOrDefault(p => p.candidateDni == plusId);
                                    break;
                                case "vacaciones":
                                    plus = rowInfo.vacations.FirstOrDefault(p => p.candidateDni == plusId);
                                    break;
                            }
                            if (plus.candidateDni != null)
                            {
                                cell.CellStyle = freeStyle;
                                if (plus.multiplicador != 0)
                                {
                                    if (type.Equals("vacaciones"))
                                        cell.SetCellValue("X");
                                    else
                                        cell.SetCellValue(plus.multiplicador);
                                }
                                anyPlus = true;
                            }
                            else
                            {
                                cell.CellStyle = lockedStyle;
                            }
                        }

                        if (!anyPlus)
                        {
                            for (int i = sCol + 2; i < pCol; i++)
                                (row.GetCell(i) ?? row.CreateCell(i)).CellStyle = null;
                            continue;
                        }

                        //Escribir los datos del candidato
                        cell = row.GetCell(sCol) ?? row.CreateCell(sCol);
                        cell.CellStyle = tableCellGreenStyle;
                        cell.SetCellValue(rowInfo.dni);
                        cell = row.GetCell(sCol + 1) ?? row.CreateCell(sCol + 1);
                        cell.CellStyle = tableCellGreenStyle;
                        cell.SetCellValue(rowInfo.name);

                        cRow++;
                    }

                    //Usar la fila de IDs para poner el tipo de extra
                    row = getOrCreateRow(sheet, rows, 7);
                    for (int i = sCol; i < pCol; i++)
                    {
                        cell = row.GetCell(i) ?? row.CreateCell(i);
                        cell.CellStyle = regionalHeaderSyle;
                    }
                    cell = row.GetCell(sCol);
                    switch (type)
                    {
                        case "horas": cell.SetCellValue("Horas adicionales"); break;
                        case "plus": cell.SetCellValue("Pluses"); break;
                        case "vacaciones": cell.SetCellValue("Vacaciones"); break;
                    }
                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(7, 7, sCol, pCol - 1));

                    pCol += 2;

                    //Propiedades generales de la tabla
                    sheet.SetColumnWidth(sCol, 20 * 256);
                    sheet.SetColumnWidth(sCol + 1, 35 * 256);
                    //getOrCreateRow(sheet, rows, 7).ZeroHeight = true;

                    sheet.ProtectSheet("1234");
                }
            }

            string tmpDir = GetTemporaryDirectory();

            //Guardado
            string fileName = "Reporte.xlsx";
            string tmpFile = Path.Combine(tmpDir, fileName);
            FileStream file = new FileStream(tmpFile, FileMode.Create);
            workbook.Write(file);
            file.Close();

            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            httpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(tmpFile), contentType)
            {
                FileDownloadName = fileName
            };

            Directory.Delete(tmpDir, true);

            return response;
        }

        private static void aggregateForReport(SqlConnection conn, string clientToken, string securityToken, string companyId, string centroId, List<ExtraHoursEntry> entries, out List<TemplateRowInfo> rowsInfo, out List<CompanyData> companies)
        {
            rowsInfo = new();
            companies = new();

            //Obtener el listado de candidatos
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT C.id, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, C.dni, \n" +
                                      "E.nombre as eName, E.id as eId, CE.alias as cAlias, CE.id as cId \n" +
                                      "FROM candidatos C \n" +
                                      "INNER JOIN centros CE ON(C.centroId = CE.id) \n" +
                                      "INNER JOIN empresas E ON(CE.companyId = E.id) \n" +
                                      (clientToken != null ? "INNER JOIN client_user_centros CUC ON(CE.id = CUC.centroId) \n" : "") +
                                      (clientToken != null ? "INNER JOIN client_users CU ON(CUC.clientUserId = CU.id) \n" : "") +
                                      (securityToken != null ? "INNER JOIN asociacion_usuario_empresa AUE ON(E.id = AUE.companyId) \n" : "") +
                                      (securityToken != null ? "INNER JOIN users U ON(U.id = AUE.userId) \n" : "") +
                                      "WHERE \n" +
                                      (clientToken != null ? "CU.token = @TOKEN AND \n" : "") +
                                      (securityToken != null ? "U.securityToken = @TOKEN AND \n" : "") +
                                      "(@COMPANY IS NULL OR E.id = @COMPANY) AND \n" +
                                      "(@CENTRO IS NULL OR CE.id = @CENTRO) \n" +
                                      "ORDER BY name";
                command.Parameters.AddWithValue("@TOKEN", clientToken ?? (object)securityToken ?? DBNull.Value);
                command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rowsInfo.Add(new TemplateRowInfo()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            hours = new(),
                            pluses = new(),
                            vacations = new()
                        });
                        string eName = reader.GetString(reader.GetOrdinal("eName"));
                        string eId = reader.GetString(reader.GetOrdinal("eId"));
                        string cAlias = reader.GetString(reader.GetOrdinal("cAlias"));
                        string cId = reader.GetString(reader.GetOrdinal("cId"));

                        CompanyData company;
                        if (companies.Any(c => c.id == eId))
                        {
                            company = companies.Find(c => c.id == eId);
                        }
                        else
                        {
                            company = new CompanyData()
                            {
                                id = eId,
                                nombre = eName,
                                centros = new()
                            };
                            companies.Add(company);
                        }
                        if (company.centros.All(c => c.id != cId))
                        {
                            company.centros.Add(new CentroData()
                            {
                                id = cId,
                                alias = cAlias
                            });
                        }
                    }
                }
            }

            //El campo candidateDni de las entries se usa como id para los *tipos de extras.
            entries = entries.Select(e =>
            {
                e.candidateDni = $"{e.concepto.Replace(" ", "").ToLower()}-{(e.neto ? '1' : '0')}-{e.cantidad:F2}-{(e.vacacionesInicio.HasValue ? e.vacacionesInicio.Value.ToString("ddMMyyyy") : "Z")}-{(e.vacacionesFin.HasValue ? e.vacacionesFin.Value.ToString("ddMMyyyy") : "Z")}-{e.tipo}";
                return e;
            }).ToList();

            //Obtener los pluses para cada trabajador
            foreach (TemplateRowInfo rowInfo in rowsInfo)
            {
                var candidateEntries = entries.Where(e => e.candidateId == rowInfo.id);

                var others = candidateEntries.GroupBy(e => e.candidateDni, e => e);
                foreach (var entry in others)
                {
                    ExtraHoursEntry grouped = new ExtraHoursEntry()
                    {
                        candidateDni = entry.Key,
                        concepto = entry.First().concepto,
                        neto = entry.First().neto,
                        cantidad = entry.First().cantidad,
                        vacacionesInicio = entry.First().vacacionesInicio,
                        vacacionesFin = entry.First().vacacionesFin,
                        tipo = entry.First().tipo,
                        multiplicador = entry.Select(e => e.multiplicador).Sum(),
                        preestablecida = false
                    };
                    switch (grouped.tipo)
                    {
                        case "horas":
                            rowInfo.hours.Add(grouped);
                            break;
                        case "plus":
                            rowInfo.pluses.Add(grouped);
                            break;
                        case "vacaciones":
                            rowInfo.vacations.Add(grouped);
                            break;
                    }
                }
            }
        }

        public static object uploadExcelTemplate(string xlsxString, bool total, string author)
        {
            byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
            string tmpDir = GetTemporaryDirectory();
            string tmpFile = Path.Combine(tmpDir, "template.xlsx");
            System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);

            List<ExtraHoursEntry> entries = new();

            DataFormatter formatter = new DataFormatter();
            IWorkbook workbook = new XSSFWorkbook(tmpFile);
            ISheet sheet = workbook.GetSheetAt(0);

            //Fecha
            var day = sheet.GetRow(4).GetCell(2).DateCellValue;

            //3 paginas de datos
            foreach (string type in new[] { "horas", "plus", "vacaciones" })
            {
                switch (type)
                {
                    case "horas":
                        sheet = workbook.GetSheetAt(1);
                        break;
                    case "plus":
                        sheet = workbook.GetSheetAt(2);
                        break;
                    case "vacaciones":
                        sheet = workbook.GetSheetAt(3);
                        break;
                }

                //Iterar por las filas, trabajadores
                int r = 10;
                while (true)
                {
                    var row = sheet.GetRow(r);
                    string dni = row?.GetCell(1).StringCellValue;
                    if (dni == null) break;

                    //Iteraar por las columnas, pluses
                    int c = 3;
                    while (true)
                    {
                        if (sheet.GetRow(8).GetCell(c)?.StringCellValue == null) break;

                        string multiplicidadString = formatter.FormatCellValue(row.GetCell(c));
                        double multiplicidad = 0;

                        if (double.TryParse(multiplicidadString, out double num))
                        {
                            multiplicidad = num;
                        }
                        if (multiplicidadString.ToUpper().Trim().Equals("X"))
                        {
                            multiplicidad = 1;
                        }

                        if (multiplicidad > 0)
                        {
                            double cantidad = 0;
                            bool neto = false;
                            string definicionString = sheet.GetRow(9).GetCell(c).StringCellValue.Trim();
                            DateTime? vacacionesInicio = null, vacacionesFin = null;
                            if (definicionString.Length != 0)
                            {
                                if (type == "vacaciones")
                                {
                                    string[] dateString = definicionString.Split(" - ");
                                    vacacionesInicio = DateTime.ParseExact(dateString[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                                    vacacionesFin = DateTime.ParseExact(dateString[1], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    string[] cantidadString = definicionString.Split("€");
                                    cantidad = double.Parse(cantidadString[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture);
                                    neto = cantidadString[1].Trim().ToUpper().Equals("NETO");
                                }
                            }
                            ExtraHoursEntry rowInfo = new ExtraHoursEntry()
                            {
                                candidateDni = dni,
                                tipo = type,
                                multiplicador = multiplicidad,
                                concepto = sheet.GetRow(8).GetCell(c).StringCellValue,
                                cantidad = cantidad,
                                neto = neto,
                                vacacionesInicio = vacacionesInicio,
                                vacacionesFin = vacacionesFin,
                                preestablecida = cantidad == 0

                            };
                            if (!type.Equals("horas")) rowInfo.cantidad = Math.Round(rowInfo.cantidad);
                            entries.Add(rowInfo);
                        }
                        c++;
                    }

                    r++;
                }
            }
            workbook.Close();

            Directory.Delete(tmpDir, true);

            //Clasificar por centro
            Dictionary<string, List<ExtraHoursEntry>> entriesClasified = new();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                foreach (ExtraHoursEntry entry in entries)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT centroId, id FROM candidatos WHERE dni = @DNI";
                        command.Parameters.AddWithValue("@DNI", entry.candidateDni);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string candidateId = reader.GetString(reader.GetOrdinal("id"));
                                string centroId = reader.GetString(reader.GetOrdinal("centroId"));
                                List<ExtraHoursEntry> list;
                                if (entriesClasified.ContainsKey(centroId))
                                {
                                    list = entriesClasified[centroId];
                                }
                                else
                                {
                                    list = new();
                                    entriesClasified[centroId] = list;
                                }
                                ExtraHoursEntry newEntry = entry;
                                newEntry.candidateId = candidateId;
                                list.Add(newEntry);
                            }
                        }
                    }
                }
            }

            List<object> results = new();
            foreach (KeyValuePair<string, List<ExtraHoursEntry>> pair in entriesClasified)
            {
                results.Add(createIncidence(pair.Value, day.Value.Date, pair.Key, total, author));
            }

            if (results.Count == 0)
            {
                results.Add(new { error = "Error 4398, La plantila está vacía." });
            }

            return results;
        }

        public static List<ExtraHoursEntry> parseEntriesJson(JsonElement entriesJson)
        {
            List<ExtraHoursEntry> entries = new();

            foreach (JsonElement entryJson in entriesJson.EnumerateArray())
            {
                if (entryJson.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                    entryJson.TryGetProperty("concepto", out JsonElement conceptoJson) &&
                    entryJson.TryGetProperty("cantidad", out JsonElement cantidadJson) &&
                    entryJson.TryGetProperty("multiplicador", out JsonElement multiplicadorJson) &&
                    entryJson.TryGetProperty("vacacionesInicio", out JsonElement vacacionesInicioJson) &&
                    entryJson.TryGetProperty("vacacionesFin", out JsonElement vacacionesFinJson) &&
                    entryJson.TryGetProperty("neto", out JsonElement netoJson) &&
                    entryJson.TryGetProperty("tipo", out JsonElement tipoJson) &&
                    entryJson.TryGetProperty("preestablecida", out JsonElement preestablecidaJson))
                {
                    ExtraHoursEntry entry = new ExtraHoursEntry()
                    {
                        candidateId = candidateIdJson.GetString(),
                        concepto = conceptoJson.GetString(),
                        cantidad = GetJsonDouble(cantidadJson) ?? 0,
                        multiplicador = GetJsonDouble(multiplicadorJson) ?? 1,
                        vacacionesInicio = GetJsonDate(vacacionesInicioJson),
                        vacacionesFin = GetJsonDate(vacacionesFinJson),
                        neto = GetJsonBool(netoJson) ?? false,
                        tipo = tipoJson.GetString(),
                        preestablecida = GetJsonBool(preestablecidaJson).Value
                    };
                    entry.cantidadTotal = Math.Round(entry.cantidad * entry.multiplicador * 100) / 100;
                    entries.Add(entry);
                }
            }

            return entries;
        }
        public static object createIncidence(List<ExtraHoursEntry> entries, DateTime day, string centroId, bool total, string author)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            if (total)
            {
                //It can be any day
                //DateTime firstDayOfMonth = new DateTime(day.Year, day.Month, 1);
                //day = firstDayOfMonth.AddMonths(1).AddDays(-1);
            }

            string id = ComputeStringHash(centroId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    bool failed = false;

                    try
                    {
                        //Reglas de negocio para creacion del reporte
                        if (!checkIncidenceCanBeCreated(day))
                        {
                            failed = true;
                            result = new { error = "Error 4729, no se puede crear un reporte de un mes anterior una vez pasado el día 5 del mes siguiente." };
                        }

                        //Si el reporte es total, obtener todos los extras que han sido validados por candidatos, para que no tengan que volver a validarlos
                        List<ExtraHoursEntry> acceptedEntries = new();
                        if (!failed && total)
                        {
                            List<string> selectedReports = fetchReportIdsOfMonth(day, centroId, conn, transaction);

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT E.*, I.day " +
                                                      "FROM incidencia_horas_extra_entrada E " +
                                                      "INNER JOIN incidencia_horas_extra I ON(E.incidenceId = I.id) " +
                                                      "INNER JOIN candidatos C ON (E.candidateDni = C.dni) " +
                                                      "WHERE I.id = @REPORT AND " +
                                                      "E.revisadaPorCandidato = 1";
                                command.Parameters.Add("@REPORT", System.Data.SqlDbType.VarChar);
                                foreach (string reportId in selectedReports)
                                {
                                    command.Parameters["@REPORT"].Value = reportId;
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            acceptedEntries.Add(new()
                                            {
                                                candidateDni = reader.GetString(reader.GetOrdinal("candidateDni")),
                                                concepto = reader.GetString(reader.GetOrdinal("concepto")),
                                                cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                                                multiplicador = reader.GetDouble(reader.GetOrdinal("multiplicador")),
                                                vacacionesInicio = reader.IsDBNull(reader.GetOrdinal("vacacionesInicio")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesInicio")),
                                                vacacionesFin = reader.IsDBNull(reader.GetOrdinal("vacacionesFin")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesFin")),
                                                neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1,
                                                tipo = reader.GetString(reader.GetOrdinal("tipo")),
                                                preestablecida = reader.GetInt32(reader.GetOrdinal("preestablecida")) == 1
                                            });
                                        }
                                    }
                                }
                            }
                        }

                        //Reglas de negocio para creacion de reportes totales
                        if (!failed && total && !checkIncidenceIsUniqueForCentroAndMonth(centroId, day, conn, transaction))
                        {
                            failed = true;
                            result = new { error = "Error 4729, ya existe un reporte total verificado para este centro este mes" };
                        }

                        //Obtener la empresa que tene ese centro
                        string companyId = getCompanyFromCentro(conn, transaction, centroId);
                        if (!failed && companyId == null)
                        {
                            failed = true;
                            result = new { error = "Error 4728, no se ha podido obtener la id de la empresa." };
                        }

                        //Crear el reporte
                        if (!failed)
                        {
                            string state = total ? "pendiente-validar" : "acumulativa";
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;

                                command.CommandText =
                                    "INSERT INTO incidencia_horas_extra " +
                                    "(id, centroId, day, state, createdBy, total) VALUES " +
                                    "(@ID, @CENTRO_ID, @DAY, @STATE, @CREATED_BY, @TOTAL)";
                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                                command.Parameters.AddWithValue("@DAY", day.Date);
                                command.Parameters.AddWithValue("@STATE", state);
                                command.Parameters.AddWithValue("@CREATED_BY", author);
                                command.Parameters.AddWithValue("@TOTAL", total ? 1 : 0);

                                command.ExecuteNonQuery();
                            }

                            //Insertar las entradas
                            foreach (ExtraHoursEntry entryIter in entries)
                            {
                                ExtraHoursEntry entry = entryIter;
                                string entryId = ComputeStringHash(centroId + entry.candidateId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + entry.concepto + entry.multiplicador + entry.neto + entry.cantidad);

                                //Obtener el nombre y DNI del candidato
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;

                                    command.CommandText =
                                        "SELECT TRIM(CONCAT(nombre, ' ', apellidos)) as name, dni FROM candidatos WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ID", entry.candidateId);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            entry.candidateName = reader.GetString(reader.GetOrdinal("name"));
                                            entry.candidateDni = reader.GetString(reader.GetOrdinal("dni"));
                                        }
                                    }
                                }

                                if (entry.candidateName == null || entry.candidateDni == null) continue;

                                //Comprobar si ya existe un extra igual aceptado por el candidato para que no etnga que aceptarlo de nuevo
                                bool acepted = acceptedEntries.Any(e => extrasEquals(e, entry));

                                //Insertar el extra
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;

                                    command.CommandText =
                                        "INSERT INTO incidencia_horas_extra_entrada " +
                                        "(id, incidenceId, candidateDni, candidateName, concepto, cantidad, multiplicador, neto, tipo, preestablecida, vacacionesInicio, vacacionesFin, revisadaPorCandidato) VALUES " +
                                        "(@ID, @INCIDENCE_ID, @DNI, @NAME, @CONCEPTO, @CANTIDAD, @MULTIPLICADOR, @NETO, @TIPO, @PREESTABLECIDA, @VACACIONES_START, @VACACIONES_END, @ACCEPTED)";
                                    command.Parameters.AddWithValue("@ID", entryId);
                                    command.Parameters.AddWithValue("@INCIDENCE_ID", id);
                                    command.Parameters.AddWithValue("@DNI", entry.candidateDni);
                                    command.Parameters.AddWithValue("@NAME", entry.candidateName);
                                    command.Parameters.AddWithValue("@CONCEPTO", entry.concepto);
                                    command.Parameters.AddWithValue("@CANTIDAD", entry.cantidad);
                                    command.Parameters.AddWithValue("@MULTIPLICADOR", entry.multiplicador);
                                    command.Parameters.AddWithValue("@NETO", entry.neto);
                                    command.Parameters.AddWithValue("@VACACIONES_START", (object)entry.vacacionesInicio ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@VACACIONES_END", (object)entry.vacacionesFin ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@TIPO", entry.tipo.ToLower());
                                    command.Parameters.AddWithValue("@PREESTABLECIDA", entry.preestablecida ? 1 : 0);
                                    command.Parameters.AddWithValue("@ACCEPTED", acepted ? 1 : 0);

                                    command.ExecuteNonQuery();
                                }
                            }

                            //Comprobar si el reporte puede autocerrarse, si no contiene demasiadas horas extra | ya no se hace
                            int n = 0;
                            try
                            {
                                ExtraHoursIncidence? incidence = getIncidence(conn, transaction, id);
                                if (incidence.HasValue) n = incidence.Value.number;
                            }
                            catch (Exception) { }

                            result = new { error = false, id, n };
                        }

                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new { error = "Error 5720, no se han podido crear el reporte" };
                    }

                    if (failed)
                    {
                        transaction.Rollback();
                    }
                    else
                    {
                        transaction.Commit();
                    }
                }
            }

            return result;
        }

        public static bool extrasEquals(ExtraHoursEntry a, ExtraHoursEntry b)
        {
            return a.concepto == b.concepto && a.cantidad == b.cantidad && a.multiplicador == b.multiplicador && a.neto == b.neto && a.tipo == b.tipo && a.preestablecida == b.preestablecida && a.vacacionesInicio == b.vacacionesInicio && a.vacacionesFin == b.vacacionesFin && a.candidateDni == b.candidateDni;
        }
        public static bool checkIncidenceCanBeCreated(DateTime origin)
        {
            DateTime now = DateTime.Now;
            DateTime lastMonth = DateTime.Now.AddDays(-(now.Day + 1));
            return (origin.Month == now.Month && origin.Year == now.Year) || (origin.Month == lastMonth.Month && origin.Year == lastMonth.Year && now.Day <= 5);
        }

        public static bool checkIncidenceIsUniqueForCentroAndMonth(string centroId, DateTime day, SqlConnection conn, SqlTransaction transaction)
        {
            bool exists = false;

            //Buscar si hay reportes totales para este mes y centro
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT id FROM incidencia_horas_extra WHERE centroId = @CENTRO_ID AND MONTH(day) = MONTH(@DAY) AND YEAR(day) = YEAR(@DAY) AND total = 1";
                command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                command.Parameters.AddWithValue("@DAY", day);

                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        exists = true;
            }

            //Borrarlos todos, aunque esten validados
            if (exists)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "DELETE E FROM incidencia_horas_extra I INNER JOIN incidencia_horas_extra_entrada E ON(E.incidenceId = I.id) WHERE I.centroId = @CENTRO_ID AND MONTH(I.day) = MONTH(@DAY) AND YEAR(I.day) = YEAR(@DAY) AND I.total = 1";
                    command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                    command.Parameters.AddWithValue("@DAY", day);
                    command.ExecuteNonQuery();
                }

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "DELETE FROM incidencia_horas_extra WHERE centroId = @CENTRO_ID AND MONTH(day) = MONTH(@DAY) AND YEAR(day) = YEAR(@DAY) AND total = 1";
                    command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                    command.Parameters.AddWithValue("@DAY", day);
                    command.ExecuteNonQuery();
                }

                exists = false;
            }

            return !exists;
        }

        public static List<ExtraHoursIncidence> listIncidences(int? number, string companyId, string companyKey, string centroId, string centroKey, string state, DateTime? startDate, DateTime? endDate, bool? total, string securityToken, string clientToken, int page = 0, int perpage = 10)
        {
            List<ExtraHoursIncidence> incidences = new();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT I.id, I.number, I.centroId, I.day, I.state, I.creationTime, I.createdBy, I.total, I.validationDate, CE.alias as centroAlias, " +
                        "E.id as companyId, E.nombre as clientName, E.cif, E.id as companyId " +
                        "FROM incidencia_horas_extra I INNER JOIN centros CE ON(I.centroId = CE.id) INNER JOIN empresas E ON(CE.companyId = E.id) WHERE " +
                        "(@NUMBER IS NULL OR @NUMBER = I.number) AND " +
                        "(@COMPANY_ID IS NULL OR @COMPANY_ID = E.id) AND " +
                        "(@COMPANY_KEY IS NULL OR E.nombre LIKE @COMPANY_KEY OR E.cif LIKE @COMPANY_KEY) AND " +
                        "(@CENTRO_ID IS NULL OR @CENTRO_ID = CE.id) AND " +
                        "(@CENTRO_KEY IS NULL OR CONCAT(E.nombre, ' ', CE.alias) LIKE @CENTRO_KEY) AND " +
                        "(@STATE IS NULL OR @STATE = I.state) AND " +
                        "(@TOTAL IS NULL OR @TOTAL = I.total) AND " +
                        "(@START_DATE IS NULL OR I.day >= @START_DATE ) AND " +
                        "(@END_DATE IS NULL OR I.day <= @END_DATE) AND " +
                        "(@SECURITY_TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) WHERE U.securityToken = @SECURITY_TOKEN AND AUE.companyId = E.id)) AND " +
                        "(@CLIENT_TOKEN IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE U.token = @CLIENT_TOKEN AND CUC.centroId = CE.id)) " +
                        "ORDER BY I.creationTime DESC OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY";
                    command.Parameters.AddWithValue("@NUMBER", (object)number ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY_ID", (object)companyId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY_KEY", companyKey != null ? ("%" + companyKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_KEY", centroKey != null ? ("%" + centroKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@STATE", (object)state ?? DBNull.Value);
                    command.Parameters.AddWithValue("@START_DATE", (object)startDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@END_DATE", (object)endDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@TOTAL", total != null ? (total.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@SECURITY_TOKEN", (object)securityToken ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CLIENT_TOKEN", (object)clientToken ?? DBNull.Value);
                    command.Parameters.AddWithValue("@OFFSET", page * perpage);
                    command.Parameters.AddWithValue("@LIMIT", perpage);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            incidences.Add(new ExtraHoursIncidence()
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                number = reader.GetInt32(reader.GetOrdinal("number")),
                                companyId = reader.GetString(reader.GetOrdinal("companyId")),
                                centroId = reader.GetString(reader.GetOrdinal("centroId")),
                                centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                                day = reader.GetDateTime(reader.GetOrdinal("day")),
                                state = reader.GetString(reader.GetOrdinal("state")),
                                creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                                total = reader.GetInt32(reader.GetOrdinal("total")) == 1,
                                verified = !reader.IsDBNull(reader.GetOrdinal("validationDate")),
                                createdBy = reader.GetString(reader.GetOrdinal("createdBy"))
                            });
                        }
                    }
                }
            }

            ExtraHoursIncidence lastVerifiedTotal = incidences.FirstOrDefault(i => i.total && i.verified);
            if (lastVerifiedTotal.id != null)
            {
                for (int i = 0; i < incidences.Count; i++)
                {
                    ExtraHoursIncidence incidence = incidences[i];
                    if (!incidence.total &&
                        incidence.day.Year == lastVerifiedTotal.day.Year &&
                        incidence.day.Month == lastVerifiedTotal.day.Month &&
                        incidence.creationTime < lastVerifiedTotal.creationTime)
                    {
                        incidence.verified = true;
                        incidences[i] = incidence;
                    }
                }
            }


            return incidences;
        }

        public static int countIncidences(int? number, string companyId, string companyKey, string centroId, string centroKey, string state, DateTime? startDate, DateTime? endDate, bool? total, string securityToken, string clientToken)
        {
            int n;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT COUNT(*) " +
                        "FROM incidencia_horas_extra I INNER JOIN centros CE ON(I.centroId = CE.id) INNER JOIN empresas E ON(CE.companyId = E.id) WHERE " +
                        "(@NUMBER IS NULL OR @NUMBER = I.number) AND " +
                        "(@COMPANY_ID IS NULL OR @COMPANY_ID = E.id) AND " +
                        "(@COMPANY_KEY IS NULL OR E.nombre LIKE @COMPANY_KEY OR E.cif LIKE @COMPANY_KEY) AND " +
                        "(@CENTRO_ID IS NULL OR @CENTRO_ID = CE.id) AND " +
                        "(@CENTRO_KEY IS NULL OR CONCAT(E.nombre, ' ', CE.alias) LIKE @CENTRO_KEY) AND " +
                        "(@STATE IS NULL OR @STATE = I.state) AND " +
                        "(@TOTAL IS NULL OR @TOTAL = I.total) AND " +
                        "(@START_DATE IS NULL OR I.day >= @START_DATE ) AND " +
                        "(@END_DATE IS NULL OR I.day <= @END_DATE) AND " +
                        "(@SECURITY_TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) WHERE U.securityToken = @SECURITY_TOKEN AND AUE.companyId = E.id)) AND " +
                        "(@CLIENT_TOKEN IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE U.token = @CLIENT_TOKEN AND CUC.centroId = CE.id)) ";
                    command.Parameters.AddWithValue("@NUMBER", (object)number ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY_ID", (object)companyId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY_KEY", companyKey != null ? ("%" + companyKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_KEY", centroKey != null ? ("%" + centroKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@STATE", (object)state ?? DBNull.Value);
                    command.Parameters.AddWithValue("@START_DATE", (object)startDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@END_DATE", (object)endDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@TOTAL", total != null ? (total.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@SECURITY_TOKEN", (object)securityToken ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CLIENT_TOKEN", (object)clientToken ?? DBNull.Value);

                    n = (int)command.ExecuteScalar();
                }
            }

            return n;
        }

        public static ExtraHoursIncidence? getIncidence(SqlConnection conn, SqlTransaction transaction, string incidenceId)
        {
            bool found = false;
            ExtraHoursIncidence incidence = new ExtraHoursIncidence();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT I.*, CE.id as centroId, CE.alias as centroAlias, " +
                    "E.id as companyId, E.nombre as clientName, E.cif " +
                    "FROM incidencia_horas_extra I INNER JOIN centros CE ON(I.centroId = CE.id) INNER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE I.id = @ID";
                command.Parameters.AddWithValue("@ID", incidenceId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        incidence = new ExtraHoursIncidence()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            number = reader.GetInt32(reader.GetOrdinal("number")),
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            centroId = reader.GetString(reader.GetOrdinal("centroId")),
                            centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                            day = reader.GetDateTime(reader.GetOrdinal("day")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            total = reader.GetInt32(reader.GetOrdinal("total")) == 1,
                            verified = !reader.IsDBNull(reader.GetOrdinal("validationDate")),
                            createdBy = reader.GetString(reader.GetOrdinal("createdBy")),
                            entries = new()
                        };
                        found = true;
                    }
                }
            }

            if (found)
            {
                //Introducir las entradas del reporte
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT E.*, C.id as candidateId " +
                        //", tooManyExtraHours = CASE WHEN (SELECT SUM(E2.multiplicador) FROM incidencia_horas_extra_entrada E2 INNER JOIN incidencia_horas_extra I2 ON(E2.incidenceId = I2.id) WHERE UPPER(E2.concepto) LIKE '%HORAS%EXTRA%' AND E2.preestablecida = 1 AND E2.candidateDni = E.candidateDni AND year(I2.day) = year(I.day) AND month(I2.day) = month(I.day)) > 40 THEN 1 ELSE 0 END " +
                        "FROM incidencia_horas_extra_entrada E INNER JOIN incidencia_horas_extra I ON(E.incidenceId = I.id) LEFT OUTER JOIN candidatos C ON(E.candidateDni = C.dni) " +
                        "WHERE incidenceId = @ID";
                    command.Parameters.AddWithValue("@ID", incidenceId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var entry = new ExtraHoursEntry()
                            {
                                candidateId = reader.IsDBNull(reader.GetOrdinal("candidateId"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("candidateId")),
                                candidateDni = reader.GetString(reader.GetOrdinal("candidateDni")),
                                candidateName = reader.GetString(reader.GetOrdinal("candidateName")),
                                concepto = reader.GetString(reader.GetOrdinal("concepto")),
                                cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                                multiplicador = reader.GetDouble(reader.GetOrdinal("multiplicador")),
                                vacacionesInicio = reader.IsDBNull(reader.GetOrdinal("vacacionesInicio")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesInicio")),
                                vacacionesFin = reader.IsDBNull(reader.GetOrdinal("vacacionesFin")) ? null : reader.GetDateTime(reader.GetOrdinal("vacacionesFin")),
                                neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1,
                                tipo = reader.GetString(reader.GetOrdinal("tipo")),
                                preestablecida = reader.GetInt32(reader.GetOrdinal("preestablecida")) == 1
                            };
                            entry.cantidadTotal = Math.Round(entry.cantidad * entry.multiplicador * 100) / 100;
                            incidence.entries.Add(entry);
                        }
                    }
                }
            }

            return found ? incidence : null;
        }

        public static List<ExtraHourTotalReport> listTotals(SqlConnection conn, int year, int month, string clientToken = null, string securityToken = null)
        {
            List<ExtraHourTotalReport> reports = new();
            //string dateString = $"{MESES[month-1]}-{year:D2}";

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT LI.id as lid, VI.id as vid, \n" +
                                      "E.id as companyId, \n" +
                                      "E.nombre as companyName, \n" +
                                      "E.cif as companyCif, \n" +
                                      "CE.id as centroId, \n" +
                                      "CE.ccc as centroCcc, \n" +
                                      "CE.alias as centroAlias, \n" +
                                      "CE.domicilio as centroDomicilio, \n" +
                                      "LI.creationTime, \n" +
                                      "VI.validationDate, \n" +
                                      "VI.rrhhUnreaded, \n" +
                                      "CCU.username as authorName, \n" +
                                      "state = CASE WHEN LI.id IS NULL THEN '0sin-subir' WHEN VI.id IS NULL THEN '1pendiente-validar' ELSE '2validada' END \n" +
                                      "FROM \n" +
                                      "centros CE INNER JOIN empresas E ON(CE.companyId = E.id) \n" +
                                      (clientToken != null ? "INNER JOIN client_user_centros CUC ON(CE.id = CUC.centroId) \n" : "") +
                                      (clientToken != null ? "INNER JOIN client_users CU ON(CUC.clientUserId = CU.id) \n" : "") +
                                      (securityToken != null ? "INNER JOIN asociacion_usuario_empresa AUE ON(E.id = AUE.companyId) \n" : "") +
                                      (securityToken != null ? "INNER JOIN users U ON(U.id = AUE.userId) \n" : "") +
                                      "OUTER APPLY (SELECT TOP 1 * FROM incidencia_horas_extra WHERE YEAR(day) = @YEAR AND MONTH(day) = @MONTH AND centroId = CE.id ORDER BY creationTime DESC) LI \n" +
                                      "OUTER APPLY (SELECT I.* FROM (SELECT TOP 1 * FROM incidencia_horas_extra WHERE YEAR(day) = @YEAR AND MONTH(day) = @MONTH AND centroId = CE.id ORDER BY creationTime DESC) I WHERE I.validationDate IS NOT NULL) VI \n" +
                                      "LEFT OUTER JOIN client_users CCU ON(LI.createdBy = CCU.username) \n" +
                                      ((clientToken != null || securityToken != null) ? "WHERE \n" : "") +
                                      (clientToken != null ? "CU.token = @TOKEN \n" : "") +
                                      (securityToken != null ? "U.securityToken = @TOKEN \n" : "") +
                                      "ORDER BY state, CAST(E.nombre as VARCHAR(50)), CE.alias";
                command.Parameters.AddWithValue("@YEAR", year);
                command.Parameters.AddWithValue("@MONTH", month);
                command.Parameters.AddWithValue("@TOKEN", clientToken ?? (object)securityToken ?? DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        reports.Add(new ExtraHourTotalReport()
                        {
                            id = reader.IsDBNull(reader.GetOrdinal("vid")) ? (reader.IsDBNull(reader.GetOrdinal("lid")) ? null : reader.GetString(reader.GetOrdinal("lid"))) : reader.GetString(reader.GetOrdinal("vid")),
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            centroId = reader.GetString(reader.GetOrdinal("centroId")),
                            companyName = reader.GetString(reader.GetOrdinal("companyName")),
                            companyCif = reader.GetString(reader.GetOrdinal("companyCif")),
                            centroCcc = reader.IsDBNull(reader.GetOrdinal("centroCcc")) ? null : reader.GetString(reader.GetOrdinal("centroCcc")),
                            centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                            centroDomicilio = reader.IsDBNull(reader.GetOrdinal("centroDomicilio")) ? null : reader.GetString(reader.GetOrdinal("centroDomicilio")),
                            monthYeardate = new DateTime(year, month, 1),
                            uploadDate = reader.IsDBNull(reader.GetOrdinal("creationTime")) ? null : reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            validationDate = reader.IsDBNull(reader.GetOrdinal("validationDate")) ? null : reader.GetDateTime(reader.GetOrdinal("validationDate")),
                            state = reader.GetString(reader.GetOrdinal("state")).Substring(1),
                            authorName = reader.IsDBNull(reader.GetOrdinal("authorName")) ? null : reader.GetString(reader.GetOrdinal("authorName")),
                            rrhhUnreaded = !reader.IsDBNull(reader.GetOrdinal("rrhhUnreaded")) && (reader.GetInt32(reader.GetOrdinal("rrhhUnreaded")) == 1)
                        });
                    }
                }
            }

            return reports;
        }


        public static object validateTotal(string centroId, int year, int month, string author, bool byRRHH)
        {
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                //Buscar el ultimo reporte
                string lastId = null;
                bool lastIsTotal = false, lastIsValidated = false;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT TOP 1 id, total, validationDate " +
                                          "FROM incidencia_horas_extra " +
                                          "WHERE centroId = @CENTRO AND YEAR(day) = @YEAR AND MONTH(day) = @MONTH " +
                                          "ORDER BY creationTime DESC";
                    command.Parameters.AddWithValue("@CENTRO", centroId);
                    command.Parameters.AddWithValue("@YEAR", year);
                    command.Parameters.AddWithValue("@MONTH", month);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            lastId = reader.GetString(reader.GetOrdinal("id"));
                            lastIsTotal = reader.GetInt32(reader.GetOrdinal("total")) == 1;
                            lastIsValidated = !reader.IsDBNull(reader.GetOrdinal("validationDate"));
                        }
                    }
                }

                if (lastId == null)
                {
                    return new { error = "Error 4742, este centro no tiene ningun reporte el mes seleccionado" };
                }

                if (lastIsValidated)
                {
                    return new { error = "Error 4742, este centro ya esta validado en el mes seleccionado" };
                }

                //Si el ultimo no es total, debe generarse un total nuevo
                if (!lastIsTotal)
                {
                    DateTime firstDayOfMonth = new DateTime(year, month, 1);
                    DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                    List<ExtraHoursEntry> entries = fetchEntriesOfMonth(conn, firstDayOfMonth, centroId);
                    object creationResult = createIncidence(entries, lastDayOfMonth, centroId, true, author);

                    if (!creationResult.GetType().GetProperties().Any(p => p.Name.Equals("id")))
                        return creationResult;
                    lastId = (string)creationResult.GetType().GetProperty("id")?.GetValue(creationResult);
                }

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "UPDATE incidencia_horas_extra SET validationDate = getdate(), closed = 1, state = 'validada', rrhhUnreaded = @UNREADED WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", lastId);
                    command.Parameters.AddWithValue("@UNREADED", byRRHH ? 0 : 1);
                    command.ExecuteNonQuery();
                }
            }

            return new { error = false };
        }
        public static string getCompanyFromCentro(SqlConnection conn, SqlTransaction transaction, string centroId)
        {
            string result = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT companyId FROM centros WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", centroId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        result = reader.GetString(reader.GetOrdinal("companyId"));
                    }
                }
            }

            return result;
        }

        public static IRow getOrCreateRow(ISheet sheet, Dictionary<int, IRow> rows, int n)
        {
            if (!rows.ContainsKey(n))
            {
                rows[n] = sheet.CreateRow(n);
            }
            return rows[n];
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }



}
