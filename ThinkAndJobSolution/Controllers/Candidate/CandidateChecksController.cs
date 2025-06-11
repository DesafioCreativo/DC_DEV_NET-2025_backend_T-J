using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Model.Client;
using ThinkAndJobSolution.Controllers._Model.Horario;
using ThinkAndJobSolution.Controllers.MainHome.RRHH;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.Client.IncidenceExtraHoursController;
using static ThinkAndJobSolution.Controllers.Client.IncidenceNotAttendController;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.ClientUserController;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    //[Route("api/[controller]")]
    //[ApiController]
    [Route("api/v1/candidate-checks")]
    [ApiController]
    [Authorize]
    public class CandidateChecksController : ControllerBase
    {

        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        //Listado

        [HttpGet]
        [Route("get/{centroId}/{year}/{month}/{day}/")]
        public IActionResult Get(string centroId, int year, int month, int day)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("CandidateChecks.Get", securityToken).Acceso)
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
                    bool? requiereChecks = checkCentroRequiresChecks(centroId, conn);
                    if (requiereChecks == null)
                        result = new { error = "Error 4763, centro no encontrado." };
                    else if (!requiereChecks.Value)
                        result = new { error = "Error 4764, este centro no requiere validación." };
                    else
                        result = new { error = false, checkList = getCheckList(centroId, new DateTime(year, month, day).Date, null, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5760, no se ha podido obtener la lista de validación" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("get-for-client/{centroId}/{year}/{month}/{day}/")]
        public IActionResult GetForClient(string centroId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_CHECKEO, conn) == null)
                        return Ok(new { error = "Error 1002, permisos insuficientes" });

                    bool? requiereChecks = checkCentroRequiresChecks(centroId, conn);
                    if (requiereChecks == null)
                        result = new { error = "Error 4763, centro no encontrado." };
                    else if (!requiereChecks.Value)
                        result = new { error = "Error 4764, este centro no requiere validación." };
                    else
                        result = new { error = false, checkList = getCheckList(centroId, new DateTime(year, month, day).Date, clientToken, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5760, no se ha podido obtener la lista de validación" };
                }
            }

            return Ok(result);
        }

        //Actualización

        [HttpPut]
        [Route("update/{centroId}/{year}/{month}/{day}/")]
        public async Task<IActionResult> Update(string centroId, int year, int month, int day)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("CandidateChecks.Update", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        bool? requiereChecks = checkCentroRequiresChecks(centroId, conn, transaction);
                        if (requiereChecks == null)
                            return Ok(new { error = "Error 4763, centro no encontrado." });
                        else if (!requiereChecks.Value)
                            return Ok(new { error = "Error 4764, este centro no requiere validación." });

                        CheckList checkList = new CheckList()
                        {
                            centroId = centroId,
                            day = new DateTime(year, month, day).Date,
                            checks = new List<Check>()
                        };

                        //Comprobar cuandos dias hacen del reporte
                        int passedDays = (int)(DateTime.Now.Date - checkList.day).TotalDays;

                        if (passedDays > 3)
                        {
                            return Ok(new { error = "Error 4761, no puedes actualizar una validación de hace más de 3 días" });
                        }

                        foreach (JsonElement element in json.EnumerateArray())
                        {
                            Check? check = parseCheck(element);
                            if (check != null)
                                checkList.checks.Add(check.Value);
                        }

                        updateCheckList(checkList, FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5761, no se han podido actualizar la validación" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route("update-for-client/{centroId}/{year}/{month}/{day}/")]
        public async Task<IActionResult> UpdateForClient(string centroId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        bool? requiereChecks = checkCentroRequiresChecks(centroId, conn, transaction);
                        if (requiereChecks == null)
                            return Ok(new { error = "Error 4763, centro no encontrado." });
                        else if (!requiereChecks.Value)
                            return Ok(new { error = "Error 4764, este centro no requiere validación." });

                        CheckList checkList = new CheckList()
                        {
                            centroId = centroId,
                            day = new DateTime(year, month, day).Date,
                            checks = new List<Check>()
                        };

                        //Comprobar cuandos dias hacen del reporte
                        int passedDays = (int)(DateTime.Now.Date - checkList.day.Date).TotalDays;

                        string author = ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_CHECKEO, conn, transaction, out ClientUserAccess access);
                        if (author == null)
                        {
                            return Ok(new { error = "Error 1002, permisos insuficientes" });
                        }
                        int maxDaysPassed = access.isSuper ? 3 : 1;

                        if (passedDays > maxDaysPassed)
                        {
                            return Ok(new { error = $"Error 4761, no puedes actualizar una validación de hace más de {maxDaysPassed} día/s" });
                        }

                        foreach (JsonElement element in json.EnumerateArray())
                        {
                            Check? check = parseCheck(element);
                            if (check != null)
                                checkList.checks.Add(check.Value);
                        }

                        updateCheckList(checkList, author, conn, transaction);

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5761, no se han podido actualizar la validación" };
                    }
                }
            }

            return Ok(result);
        }

        //Auxiliar

        [HttpGet]
        [Route("get-state-for-client/{centroId}/{year}/{month}/{day}/")]
        public IActionResult GetStateForClient(string centroId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_CHECKEO, conn) == null)
                        return Ok(new { error = "Error 1002, permisos insuficientes" });

                    bool? requiereChecks = checkCentroRequiresChecks(centroId, conn);
                    if (requiereChecks == null)
                        return Ok(new { error = "Error 4763, centro no encontrado." });
                    else if (!requiereChecks.Value)
                        return Ok(new { error = false, state = 0 });

                    List<Check> checks = getCheckList(centroId, new DateTime(year, month, day).Date, clientToken, conn).checks;

                    if (checks.Count == 0)
                        return Ok(new { error = false, state = 1 });
                    else if (checks.Any(c => c.check == null))
                        return Ok(new { error = false, state = 2 });
                    else
                        return Ok(new { error = false, state = 3 });
                }
                catch (Exception)
                {
                    result = new { error = "Error 5760, no se ha podido obtener la lista de validación" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("get-notattend-attachments/{checkId}")]
        public IActionResult GetNotAttendAttachments(string checkId)
        {
            return Ok(new { error = false, attachments = ReadFileList(new[] { "check", checkId, "notattend" }) });
        }

        [HttpGet]
        [Route("get-avisos-cese/")]
        public IActionResult GetAvisosCese()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("CandidateChecks.GetAvisosCese", securityToken);
            if (!access.Acceso)
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
                    string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);

                    List<AvisoFinCesion> avisos = new();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT C.id, TRIM(CONCAT(C.nombre, '', C.apellidos)) as fullname, C.dni, CE.id as centroId,  CE.alias, E.nombre, MAX(CH.day) as day " +
                            "FROM candidate_checks CH " +
                            "INNER JOIN candidatos C ON(CH.candidateId = C.id AND CH.centroId = C.centroId) " +
                            "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                            "INNER JOIN empresas E ON(CE.companyId LIKE E.id) " +
                            "WHERE CH.contractFinished = 1 AND " +
                            "(@TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) INNER JOIN centros CE ON(AUE.companyId = CE.companyId) WHERE U.securityToken = @TOKEN AND CE.id = C.centroId)) " +
                            "GROUP BY C.id, C.nombre, C.apellidos, C.dni, CE.id, CE.alias, E.nombre " +
                            "ORDER BY day DESC, CE.alias, fullname";
                        command.Parameters.AddWithValue("@TOKEN", (object)filterSecurityToken ?? DBNull.Value);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                avisos.Add(new AvisoFinCesion()
                                {
                                    candidateId = reader.GetString(reader.GetOrdinal("id")),
                                    candidateName = reader.GetString(reader.GetOrdinal("fullname")),
                                    candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                                    centroId = reader.GetString(reader.GetOrdinal("centroId")),
                                    centroAlias = reader.GetString(reader.GetOrdinal("alias")),
                                    companyName = reader.GetString(reader.GetOrdinal("nombre")),
                                    date = reader.GetDateTime(reader.GetOrdinal("day"))
                                });
                            }
                        }
                    }

                    result = new { error = false, avisos };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5772, no se han podido obtener los avisos de cese" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("count-avisos-cese/")]
        public IActionResult CountAvisosCese()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("CandidateChecks.CountAvisosCese", securityToken);
            if (!access.Acceso)
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
                    string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);

                    int avisos = 0;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM ( " +
                            "SELECT C.id, C.centroId " +
                            "FROM candidate_checks CH " +
                            "INNER JOIN candidatos C ON(CH.candidateId = C.id AND CH.centroId = C.centroId) " +
                            "WHERE CH.contractFinished = 1 AND " +
                            "(@TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) INNER JOIN centros CE ON(AUE.companyId = CE.companyId) WHERE U.securityToken = @TOKEN AND CE.id = C.centroId)) " +
                            "GROUP BY C.id, C.centroId " +
                            ") V";
                        command.Parameters.AddWithValue("@TOKEN", (object)filterSecurityToken ?? DBNull.Value);
                        avisos = (int)command.ExecuteScalar();
                    }

                    result = new { error = false, avisos };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5773, no se han podido contar los avisos de cese" };
                }
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route("ignore-aviso-cese/{candidateId}/{centroId}/")]
        public IActionResult IgnoreAvisoCese(string candidateId, string centroId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("CandidateChecks.IgnoreAvisoCese", securityToken).Acceso)
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
                        command.CommandText =
                            "UPDATE candidate_checks SET contractFinished = 0 WHERE candidateId = @CANDIDATE AND centroId = @CENTRO AND contractFinished = 1";
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        command.Parameters.AddWithValue("@CENTRO", centroId);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5773, no se han podido obtener ignorar el aviso" };
                }
            }

            return Ok(result);
        }

        //Procesado bach y tareas programadas

        [HttpGet]
        [Route("process/")]
        public async Task<IActionResult> Process()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("CandidateChecks.Process", securityToken).Acceso)
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
                    //Se procesan las de hace 4 días. Esta tarea se ejecuta a las 6:00, eso deja posibilidad de editar los checks 3 dias
                    DateTime day = DateTime.Now.Date.AddDays(-4);
                    //day = DateTime.Now.Date; // Cambiar esto cuando terminemos de probar

                    //Marcar como procesados todos los checks no procesados de la fecha o anteriores, si no tienen incidencias que crear (aunque este marcado como bueno)
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE candidate_checks SET processed = 1 " +
                            "WHERE processed = 0 AND day <= @DAY AND (extraHoursReportData IS NULL AND notAttendIncidenceData IS NULL)";
                        command.Parameters.AddWithValue("@DAY", day);
                        command.ExecuteNonQuery();
                    }

                    //Obtener todos los checks aun no procesados de la fecha o anterior
                    List<Check> checks = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT id, day, centroId, candidateId, extraHoursReportData, notAttendIncidenceData, author " +
                            "FROM candidate_checks " +
                            "WHERE processed = 0 AND day <= @DAY";
                        command.Parameters.AddWithValue("@DAY", day);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                checks.Add(new Check()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    day = reader.GetDateTime(reader.GetOrdinal("day")),
                                    centroId = reader.GetString(reader.GetOrdinal("centroId")),
                                    candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                                    extraHoursReportData = reader.IsDBNull(reader.GetOrdinal("extraHoursReportData")) ? null : parseExtraHoursReportData(reader.GetString(reader.GetOrdinal("extraHoursReportData"))),
                                    notAttendIncidenceData = reader.IsDBNull(reader.GetOrdinal("notAttendIncidenceData")) ? null : parseNotAttendIncidenceData(reader.GetString(reader.GetOrdinal("notAttendIncidenceData"))),
                                    author = reader.IsDBNull(reader.GetOrdinal("author")) ? null : reader.GetString(reader.GetOrdinal("author"))
                                });
                            }
                        }
                    }

                    //Procesar cada check
                    Dictionary<string, string> errors = new();
                    foreach (Check check in checks)
                    {
                        if (check.extraHoursReportData == null && check.notAttendIncidenceData == null)
                        {
                            //No se han podido parsear las incidencias, se marca como procesado igualmente
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "UPDATE candidate_checks SET processed = 1 WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", check.id);
                                command.ExecuteNonQuery();
                            }

                            //Borrar la carpeta del check
                            DeleteDir(new[] { "check", check.id });
                        }
                        else
                        {
                            using (SqlTransaction transaction = conn.BeginTransaction())
                            {
                                try
                                {
                                    //Crear el reporte de horas extra
                                    string extraHoursId = null;
                                    if (check.extraHoursReportData != null)
                                    {
                                        //Generar la lista de entradas
                                        List<ExtraHoursEntry> entries = new();
                                        foreach (ExtraHoursReportData entry in check.extraHoursReportData)
                                        {
                                            entries.Add(new ExtraHoursEntry()
                                            {
                                                candidateId = check.candidateId,
                                                concepto = entry.concepto,
                                                cantidad = entry.cantidad,
                                                multiplicador = entry.multiplicador,
                                                vacacionesInicio = entry.vacacionesInicio,
                                                vacacionesFin = entry.vacacionesFin,
                                                neto = entry.neto,
                                                tipo = entry.tipo,
                                                preestablecida = entry.preestablecida
                                            });
                                        }
                                        object iResult = createIncidence(entries, check.day, check.centroId, false, check.author ?? "System");
                                        object iError = iResult.GetType().GetProperty("error").GetValue(iResult);
                                        if (iError is string)
                                            throw new Exception(iError as string);
                                        object iId = iResult.GetType().GetProperty("id").GetValue(iResult);
                                        if (iId is string)
                                            extraHoursId = iId as string;
                                    }

                                    //Crear incidencia de falta de asistencia
                                    string notAttendId = null;
                                    if (check.notAttendIncidenceData != null)
                                    {
                                        string incidenceProblems = testNotAttendIncidence(check.notAttendIncidenceData.Value, check, conn, transaction);
                                        if (incidenceProblems == null)
                                        {
                                            string companyId = getCompanyFromCentro(conn, transaction, check.centroId);

                                            //Crear la incidencia e insertar el evento
                                            string state = "borrador";
                                            notAttendId = ComputeStringHash(check.centroId + check.candidateId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                                            using (SqlCommand command = conn.CreateCommand())
                                            {
                                                command.Connection = conn;
                                                command.Transaction = transaction;
                                                command.CommandText =
                                                    "INSERT INTO incidencia_falta_asistencia " +
                                                    "(id, centroId, candidateId, details, date, lostHours, baja, altCategory, category, state, hasCandidateUnread) VALUES " +
                                                    "(@ID, @CENTRO_ID, @CANDIDATE_ID, @DETAILS, @DATE, @LOST_HOURS, @BAJA, @ALT_CATEGORY, @CATEGORY, @STATE, 1)";
                                                command.Parameters.AddWithValue("@ID", notAttendId);
                                                command.Parameters.AddWithValue("@CENTRO_ID", check.centroId);
                                                command.Parameters.AddWithValue("@CANDIDATE_ID", check.candidateId);
                                                command.Parameters.AddWithValue("@DETAILS", check.notAttendIncidenceData.Value.details);
                                                command.Parameters.AddWithValue("@DATE", check.day);
                                                command.Parameters.AddWithValue("@LOST_HOURS", (object)check.notAttendIncidenceData.Value.lostHours ?? DBNull.Value);
                                                command.Parameters.AddWithValue("@BAJA", check.notAttendIncidenceData.Value.baja ? 1 : 0);
                                                command.Parameters.AddWithValue("@ALT_CATEGORY", check.notAttendIncidenceData.Value.altCategory);
                                                command.Parameters.AddWithValue("@CATEGORY", "Sin determinar");
                                                command.Parameters.AddWithValue("@STATE", state);

                                                command.ExecuteNonQuery();
                                            }

                                            List<string> attachments = ReadFileList(new[] { "check", check.id, "notattend" });
                                            if (attachments.Count > 0)
                                                SaveFileList(new[] { "companies", companyId, "centro", check.centroId, "incidence-not-attend", notAttendId }, attachments, "client_attachment_");

                                            NotAttendIncidence? incidence = getIncidence(conn, transaction, notAttendId, true);

                                            //Aplicar auto-cat si no faltan documentos
                                            if (!altCatRequiresDocuments(check.notAttendIncidenceData.Value.altCategory) || attachments.Count > 0)
                                            {
                                                if (incidence.HasValue)
                                                    state = await applyAutoCategory(incidence.Value, "candidato", conn, transaction);
                                            }

                                            insertEvent(conn, transaction, new NotAttendIncidenceEvent()
                                            {
                                                incidenceId = notAttendId,
                                                author = check.author ?? "System",
                                                action = "Incidencia creada por verificación diaria",
                                                state = state
                                            });
                                        }
                                    }

                                    //Marcarla como procesada
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText =
                                            "UPDATE candidate_checks SET processed = 1, notAttendIncidenceId = @NOTATTEND, extraHoursReportId = @EXTRAHOURS WHERE id = @ID";
                                        command.Parameters.AddWithValue("@ID", check.id);
                                        command.Parameters.AddWithValue("@NOTATTEND", (object)notAttendId ?? DBNull.Value);
                                        command.Parameters.AddWithValue("@EXTRAHOURS", (object)extraHoursId ?? DBNull.Value);
                                        command.ExecuteNonQuery();
                                    }

                                    transaction.Commit();

                                    //Borrar la carpeta del check
                                    DeleteDir(new[] { "check", check.id });
                                }
                                catch (Exception e)
                                {
                                    transaction.Rollback();
                                    errors[check.id] = e.Message + " | " + e.StackTrace;
                                }
                            }
                        }
                    }

                    result = new { error = false, errors };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5769, no se ha podido procesar la verificacion" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("delete-old-checks/")]
        public IActionResult DeleteOldChecks()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("CandidateChecks.DeleteOldChecks", securityToken).Acceso)
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
                    //La fecha a partir de la cual se borran los checks
                    DateTime day = DateTime.Now.Date.AddYears(-1);

                    //Obtener las IDs para poder borrar las carpetas
                    List<string> ids = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id FROM candidate_checks WHERE day < @DAY";
                        command.Parameters.AddWithValue("@DAY", day);
                        using (SqlDataReader reader = command.ExecuteReader())
                            while (reader.Read())
                                ids.Add(reader.GetString(reader.GetOrdinal("id")));
                    }

                    //Borrar esos checks
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM candidate_checks WHERE day < @DAY";
                        command.Parameters.AddWithValue("@DAY", day);
                        command.ExecuteNonQuery();
                    }

                    //Borrar las carpetas
                    foreach (string id in ids)
                        DeleteDir(new[] { "check", id });

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5770, no se han podido eliminar los checks antiguos" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        public struct CheckList // <- Lo que envio cuando me solicitas los checks de un centro
        {
            public string centroId { get; set; }
            public DateTime day { get; set; }
            public List<Check> checks { get; set; }
        }
        public struct Check // <- Lo que se guarda en la BD
        {
            public string id { get; set; }
            public string candidateId { get; set; }
            public string candidateDni { get; set; }
            public string candidateName { get; set; }
            public DateTime day { get; set; }
            public string turno { get; set; }
            public Turno horario { get; set; }
            public DateTime? lastUpdate { get; set; }
            public bool? check { get; set; }
            public string extraHoursReportId { get; set; } // Null si no se ha formalizado o no hay
            public string notAttendIncidenceId { get; set; } // Null si no se ha formalizado o no hay
            public List<ExtraHoursReportData> extraHoursReportData { get; set; } // Lo guardo en Json, desaparece al formalizar el check
            public NotAttendIncidenceData? notAttendIncidenceData { get; set; }  // Lo guardo en Json, desaparece al formalizar el check
            public bool contractFinished { get; set; }
            public bool descanso { get; set; }
            public bool festivo { get; set; }
            public Turno horarioAjustado { get; set; }
            public bool editable { get; set; }
            public string centroId { get; set; }
            public string author { get; set; }
        }

        public struct ExtraHoursReportData
        {
            public string concepto { get; set; }
            public double cantidad { get; set; }
            public double multiplicador { get; set; }
            public DateTime? vacacionesInicio { get; set; }
            public DateTime? vacacionesFin { get; set; }
            public bool neto { get; set; }
            public string tipo { get; set; } // horas, plus, vacaciones
            public bool preestablecida { get; set; }
        }
        public struct NotAttendIncidenceData
        {
            public string details { get; set; }
            public double? lostHours { get; set; }
            public bool baja { get; set; }
            public string altCategory { get; set; }
            public List<string> clientAttachments { get; set; } // No guardar en la BD
        }

        public struct AvisoFinCesion
        {
            public string candidateId { get; set; }
            public string candidateName { get; set; }
            public string candidateDni { get; set; }
            public string centroId { get; set; }
            public string centroAlias { get; set; }
            public string companyName { get; set; }
            public DateTime date { get; set; }
        }
        public struct WarningMaster
        {
            public string centroId { get; set; }
            public string centroAlias { get; set; }
            public int nFaltantes { get; set; }
            public List<string> candidates { get; set; }
        }

        public struct EstadoTurno
        {
            public string name { get; set; }
            public string value { get; set; }
            public int state { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        private static CheckList getCheckList(string centroId, DateTime day, string clientToken, SqlConnection conn, SqlTransaction transaction = null)
        {
            string username = null;
            if (clientToken != null)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT U.username, AL.ordering FROM client_users U INNER JOIN client_access_levels AL ON(U.accessLevel = AL.id) WHERE U.token = @TOKEN";
                    command.Parameters.AddWithValue("@TOKEN", clientToken);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (reader.GetInt32(reader.GetOrdinal("ordering")) > 3) //Si no es super, filtrar los candidatos que le corresponden por su ID
                            {
                                username = reader.GetString(reader.GetOrdinal("username"));
                            }
                        }
                    }
                }
            }

            List<Check> checks = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CH.*, C.id as cid, C.dni, nombreCompleto = CONCAT(C.nombre, ' ', C.apellidos), TU.turno as TuTurno " +
                    "FROM candidatos C " +
                    "CROSS JOIN (VALUES ('manana'), ('tarde'), ('noche'), (NULL)) TU(turno) " +
                    "LEFT OUTER JOIN candidate_checks CH ON (C.id = CH.candidateId AND CH.day = @DAY AND ((TU.turno IS NULL AND CH.turno IS NULL) OR TU.turno = CH.turno)) " +
                    "WHERE C.centroId = @CENTRO AND " +
                    "C.active = 1 AND (C.fechaComienzoTrabajo IS NULL OR c.fechaComienzoTrabajo <= @DAY) AND (C.fechaFinTrabajo IS NULL OR c.fechaFinTrabajo >= @DAY) " + //Filtrar por cesion activa ese día
                    "ORDER BY nombreCompleto, SUBSTRING(TU.turno, 4, 5)";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@DAY", day);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Check check = new Check()
                        {
                            id = reader.IsDBNull(reader.GetOrdinal("id")) ? null : reader.GetString(reader.GetOrdinal("id")),
                            candidateId = reader.GetString(reader.GetOrdinal("cid")),
                            candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                            candidateName = reader.GetString(reader.GetOrdinal("nombreCompleto")),
                            day = day,
                            turno = reader.IsDBNull(reader.GetOrdinal("TuTurno")) ? null : reader.GetString(reader.GetOrdinal("TuTurno")),
                            horario = null,
                            lastUpdate = reader.IsDBNull(reader.GetOrdinal("lastUpdate")) ? null : reader.GetDateTime(reader.GetOrdinal("lastUpdate")),
                            check = reader.IsDBNull(reader.GetOrdinal("check")) ? null : (reader.GetInt32(reader.GetOrdinal("check")) == 1),
                            extraHoursReportId = reader.IsDBNull(reader.GetOrdinal("extraHoursReportId")) ? null : reader.GetString(reader.GetOrdinal("extraHoursReportId")),
                            notAttendIncidenceId = reader.IsDBNull(reader.GetOrdinal("notAttendIncidenceId")) ? null : reader.GetString(reader.GetOrdinal("notAttendIncidenceId")),
                            extraHoursReportData = reader.IsDBNull(reader.GetOrdinal("extraHoursReportData")) ? null : parseExtraHoursReportData(reader.GetString(reader.GetOrdinal("extraHoursReportData"))),
                            notAttendIncidenceData = reader.IsDBNull(reader.GetOrdinal("notAttendIncidenceData")) ? null : parseNotAttendIncidenceData(reader.GetString(reader.GetOrdinal("notAttendIncidenceData"))),
                            contractFinished = reader.IsDBNull(reader.GetOrdinal("contractFinished")) ? false : (reader.GetInt32(reader.GetOrdinal("contractFinished")) == 1),
                            descanso = reader.IsDBNull(reader.GetOrdinal("descanso")) ? false : (reader.GetInt32(reader.GetOrdinal("descanso")) == 1),
                            festivo = reader.IsDBNull(reader.GetOrdinal("festivo")) ? false : (reader.GetInt32(reader.GetOrdinal("festivo")) == 1),
                            horarioAjustado = reader.IsDBNull(reader.GetOrdinal("horarioAjustado")) ? null : HorariosController.parseTurno(reader.GetString(reader.GetOrdinal("horarioAjustado")), true),
                            editable = !(reader.IsDBNull(reader.GetOrdinal("processed")) ? false : (reader.GetInt32(reader.GetOrdinal("processed")) == 1))
                        };

                        if (check.id == null)
                            check.id = ComputeStringHash(day + check.candidateId + check.turno);

                        checks.Add(check);
                    }
                }
            }

            Dictionary<string, Dia> horario = new();
            Dictionary<string, bool> horarioEmpty = new();
            List<Check> filteredChecks = new List<Check>();
            foreach (Check check in checks)
            {
                Dia dia;
                bool empty;
                if (horario.ContainsKey(check.candidateId))
                {
                    dia = horario[check.candidateId];
                    empty = horarioEmpty[check.candidateId];
                }
                else
                {
                    dia = HorariosController.getCandidateDia(check.candidateId, day, out empty, conn, transaction);
                    horario[check.candidateId] = dia;
                    horarioEmpty[check.candidateId] = empty;
                }

                //No mostrar a las personas que esten de baja o de vacaciones
                if (dia.baja || dia.vacaciones) continue;

                //No mostrar a las personas de las que no eres responsable. Ignorar si eres super (username no se definio previamente)
                //Nos mostrar los checks de turnos que no tenga el candidato ese día. Usar el check sin turno si no tiene horario
                Check checkTmp = check;
                switch (check.turno)
                {
                    case null:
                        if (!empty) continue;
                        break;
                    case "manana":
                        if (dia.manana == null || !(username == null || dia.manana.responsable == username)) continue;
                        checkTmp.horario = dia.manana;
                        break;
                    case "tarde":
                        if (dia.tarde == null || !(username == null || dia.tarde.responsable == username)) continue;
                        checkTmp.horario = dia.tarde;
                        break;
                    case "noche":
                        if (dia.noche == null || !(username == null || dia.noche.responsable == username)) continue;
                        checkTmp.horario = dia.noche;
                        break;
                }
                filteredChecks.Add(checkTmp);
            }

            return new CheckList()
            {
                centroId = centroId,
                day = day,
                checks = filteredChecks
            };
        }

        private void updateCheckList(CheckList checkList, string author, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Omitir los que aun no esten definidos
            //checkList.checks = checkList.checks.FindAll(c => c.check != null);
            foreach (Check check in checkList.checks)
            {
                //Comprobar si el check existe y si ya esta procesado
                bool exists = false, processed = false;
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT processed FROM candidate_checks WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", check.id);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            exists = true;
                            processed = reader.GetInt32(reader.GetOrdinal("processed")) == 1;
                        }
                    }
                }

                //Saltarselo si ya esta procesado
                if (processed) continue;

                //Borrar el check actual, si ya existe
                if (exists)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText = "DELETE FROM candidate_checks WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", check.id);
                        command.ExecuteNonQuery();
                    }
                }

                //Insertar el check nuevo
                NotAttendIncidenceData? notAttendIncidenceData = null;
                if (check.notAttendIncidenceData != null)
                {
                    NotAttendIncidenceData newData = check.notAttendIncidenceData.Value;
                    newData.clientAttachments = null;
                    notAttendIncidenceData = newData;
                }
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "INSERT INTO candidate_checks(id, candidateId, day, turno, [check], extraHoursReportData, notAttendIncidenceData, contractFinished, descanso, festivo, horarioAjustado, centroId, author) " +
                        "VALUES (@ID, @CANDIDATE, @DAY, @TURNO, @CHECK, @EXTRA, @NOTATTEND, @FINISHED, @DESCANSO, @FESTIVO, @HORARIO_AJUSTADO, @CENTRO, @AUTHOR)";
                    command.Parameters.AddWithValue("@ID", check.id);
                    command.Parameters.AddWithValue("@CANDIDATE", check.candidateId);
                    command.Parameters.AddWithValue("@DAY", checkList.day);
                    command.Parameters.AddWithValue("@TURNO", (object)check.turno ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CHECK", check.check == null ? DBNull.Value : (check.check.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@EXTRA", check.extraHoursReportData == null ? DBNull.Value : JsonSerializer.Serialize(check.extraHoursReportData));
                    command.Parameters.AddWithValue("@NOTATTEND", notAttendIncidenceData == null ? DBNull.Value : JsonSerializer.Serialize(notAttendIncidenceData));
                    command.Parameters.AddWithValue("@FINISHED", check.contractFinished ? 1 : 0);
                    command.Parameters.AddWithValue("@DESCANSO", check.descanso ? 1 : 0);
                    command.Parameters.AddWithValue("@FESTIVO", check.festivo ? 1 : 0);
                    command.Parameters.AddWithValue("@HORARIO_AJUSTADO", check.horarioAjustado == null ? DBNull.Value : JsonSerializer.Serialize(HorariosController.serializeTurno(check.horarioAjustado)));
                    command.Parameters.AddWithValue("@CENTRO", checkList.centroId);
                    command.Parameters.AddWithValue("@AUTHOR", (object)author ?? DBNull.Value);
                    command.ExecuteNonQuery();
                }

                //Operaciones en el sistema de ficheros
                DeleteDir(new[] { "check", check.id, "notattend" });
                if (check.notAttendIncidenceData != null && check.notAttendIncidenceData.Value.clientAttachments != null)
                {
                    SaveFileList(new[] { "check", check.id, "notattend" }, check.notAttendIncidenceData.Value.clientAttachments, "client-");
                }
            }
        }

        private bool? checkCentroRequiresChecks(string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            bool? requiereChecks = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT requiereChecks = CASE WHEN EXISTS(SELECT * FROM horarios WHERE centroId = @ID) THEN 1 ELSE 0 END";
                command.Parameters.AddWithValue("@ID", centroId);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        requiereChecks = reader.GetInt32(reader.GetOrdinal("requiereChecks")) == 1;
            }

            return requiereChecks;
        }

        private static NotAttendIncidenceData? parseNotAttendIncidenceData(JsonElement json)
        {
            NotAttendIncidenceData? data = null;

            if (json.ValueKind == JsonValueKind.Object)
                if (json.TryGetProperty("details", out JsonElement details) &&
                   json.TryGetProperty("lostHours", out JsonElement lostHours) &&
                   json.TryGetProperty("baja", out JsonElement baja) &&
                   json.TryGetProperty("altCategory", out JsonElement altCategory) &&
                   json.TryGetProperty("clientAttachments", out JsonElement clientAttachments))
                {
                    return new NotAttendIncidenceData()
                    {
                        details = details.GetString(),
                        lostHours = GetJsonDouble(lostHours),
                        baja = GetJsonBool(baja) ?? false,
                        altCategory = altCategory.GetString(),
                        clientAttachments = clientAttachments.ValueKind == JsonValueKind.Null ? null : GetJsonStringListUnsecure(clientAttachments)
                    };
                }

            return data;
        }

        private static List<ExtraHoursReportData> parseExtraHoursReportData(JsonElement json)
        {
            List<ExtraHoursReportData> list = new();

            if (json.ValueKind == JsonValueKind.Array)
                foreach (JsonElement element in json.EnumerateArray())
                {
                    if (element.TryGetProperty("concepto", out JsonElement concepto) &&
                        element.TryGetProperty("cantidad", out JsonElement cantidad) &&
                        element.TryGetProperty("multiplicador", out JsonElement multiplicador) &&
                        element.TryGetProperty("vacacionesInicio", out JsonElement vacacionesInicio) &&
                        element.TryGetProperty("vacacionesFin", out JsonElement vacacionesFin) &&
                        element.TryGetProperty("neto", out JsonElement neto) &&
                        element.TryGetProperty("tipo", out JsonElement tipo) &&
                        element.TryGetProperty("preestablecida", out JsonElement preestablecida))
                    {
                        list.Add(new ExtraHoursReportData()
                        {
                            concepto = concepto.GetString(),
                            cantidad = GetJsonDouble(cantidad) ?? 0,
                            multiplicador = GetJsonDouble(multiplicador) ?? 1,
                            vacacionesInicio = GetJsonDate(vacacionesInicio),
                            vacacionesFin = GetJsonDate(vacacionesFin),
                            neto = GetJsonBool(neto) ?? false,
                            tipo = tipo.GetString(),
                            preestablecida = GetJsonBool(preestablecida) ?? false
                        });
                    }
                }
            else
                list = null;

            return list;
        }

        private static Check? parseCheck(JsonElement json)
        {
            if (json.TryGetProperty("id", out JsonElement id) &&
               json.TryGetProperty("candidateId", out JsonElement candidateId) &&
               json.TryGetProperty("day", out JsonElement day) &&
               json.TryGetProperty("turno", out JsonElement turno) &&
               json.TryGetProperty("check", out JsonElement check) &&
               json.TryGetProperty("extraHoursReportId", out JsonElement extraHoursReportId) &&
               json.TryGetProperty("notAttendIncidenceId", out JsonElement notAttendIncidenceId) &&
               json.TryGetProperty("extraHoursReportData", out JsonElement extraHoursReportData) &&
               json.TryGetProperty("notAttendIncidenceData", out JsonElement notAttendIncidenceData) &&
               json.TryGetProperty("contractFinished", out JsonElement contractFinished) &&
               json.TryGetProperty("descanso", out JsonElement descanso) &&
               json.TryGetProperty("festivo", out JsonElement festivo) &&
               json.TryGetProperty("horarioAjustado", out JsonElement horarioAjustado))
            {
                return new Check()
                {
                    id = id.GetString(),
                    candidateId = candidateId.GetString(),
                    day = day.GetDateTime(),
                    turno = turno.GetString(),
                    check = GetJsonBool(check),
                    extraHoursReportId = extraHoursReportId.GetString(),
                    notAttendIncidenceId = notAttendIncidenceId.GetString(),
                    extraHoursReportData = parseExtraHoursReportData(extraHoursReportData),
                    notAttendIncidenceData = parseNotAttendIncidenceData(notAttendIncidenceData),
                    contractFinished = GetJsonBool(contractFinished) ?? false,
                    descanso = GetJsonBool(descanso) ?? false,
                    festivo = GetJsonBool(festivo) ?? false,
                    horarioAjustado = HorariosController.parseTurno(horarioAjustado)
                };
            }
            else
            {
                return null;
            }
        }
        private static NotAttendIncidenceData? parseNotAttendIncidenceData(string json)
        {
            return parseNotAttendIncidenceData(JsonDocument.Parse(json).RootElement);
        }
        
        private static List<ExtraHoursReportData> parseExtraHoursReportData(string json)
        {
            return parseExtraHoursReportData(JsonDocument.Parse(json).RootElement);
        }

        public static string testNotAttendIncidence(NotAttendIncidenceData incidence, Check check, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Reglas de negocio para creacion de incidencias
            if (!checkIncidenceIsUniqueForCandidateAndDay(check.candidateId, check.day, conn, transaction))
                return "El trabajador ya tiene una incidencia ese día";

            //Comprobar si faltan datos
            if (incidence.altCategory == null || string.IsNullOrWhiteSpace(incidence.details))
                return "Faltan datos";

            return null;
        }

        public static void deleteCheckInTransaction(SqlConnection conn, SqlTransaction transaction, string checkId)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM candidate_checks WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", checkId);
                command.ExecuteNonQuery();
                DeleteDir(new[] { "check", checkId });
            }
        }

        public static List<WarningMaster> getCheckWarnings(string clientUserId, DateTime day, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Obtener los centros a los que tiene acceso y que tienen activado el horario
            List<CentroData> centros = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CE.id, CE.alias " +
                    "FROM client_user_centros CUC " +
                    "INNER JOIN centros CE ON(CUC.centroId = CE.id) " +
                    "WHERE CUC.clientUserId = @ID AND EXISTS(SELECT * FROM horarios H WHERE H.centroId = CE.id) " +
                    "ORDER BY CE.id, CE.alias";
                command.Parameters.AddWithValue("@ID", clientUserId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        centros.Add(new CentroData()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            alias = reader.GetString(reader.GetOrdinal("alias"))
                        });
                    }
                }
            }

            //Obtener el token del usuario cliente
            string clientToken;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT token FROM client_users WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", clientUserId);
                clientToken = (string)command.ExecuteScalar();
            }

            //Obtener los checks faltantes en cada centro
            List<WarningMaster> warnings = new();
            foreach (CentroData centro in centros)
            {
                List<Check> checks = getCheckList(centro.id, day.Date, clientToken, conn, transaction).checks;
                WarningMaster warning = new WarningMaster()
                {
                    centroId = centro.id,
                    centroAlias = centro.alias,
                    nFaltantes = checks.Count(c => c.check == null)
                };
                if (warning.nFaltantes > 0)
                    warnings.Add(warning);
            }

            return warnings;
        }


        public static HashSet<string> getCandidatesWithBajaFromIncidenceNotAttend(string centroId, DateTime day, SqlConnection conn, SqlTransaction transaction = null)
        {
            HashSet<string> candidates = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT DISTINCT candidateId " +
                    "FROM incidencia_falta_asistencia " +
                    "WHERE candidateId IS NOT NULL AND " +
                    "(@CENTRO IS NULL OR centroId = @CENTRO) AND " +
                    "baja = 1 AND " +
                    "state = 'aceptada' AND " +
                    "date <= @DAY AND " +
                    "(bajaEnd >= @DAY OR bajaRevision >= @DAY)";
                command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);
                command.Parameters.AddWithValue("@DAY", day.Date);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        candidates.Add(reader.GetString(reader.GetOrdinal("candidateId")));
            }
            return candidates;
        }


        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
