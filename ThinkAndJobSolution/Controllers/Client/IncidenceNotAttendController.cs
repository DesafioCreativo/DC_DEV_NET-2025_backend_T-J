using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System.Drawing;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers.Candidate;
using ThinkAndJobSolution.Controllers.Commons;
using ThinkAndJobSolution.Controllers.MainHome.Comercial;
using ThinkAndJobSolution.Controllers.MainHome.RRHH;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.Candidate.IncidenceGenericController;

namespace ThinkAndJobSolution.Controllers.Client
{
    [Route("api/v1/incidence-notattend")]
    [ApiController]
    [Authorize]
    public class IncidenceNotAttendController : ControllerBase
    {

        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpPost]
        [Route("create/{centroId}/")]
        public async Task<IActionResult> Create(string centroId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("NotAttendIncidence.Create", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("details", out JsonElement detailsJson) &&
                json.TryGetProperty("date", out JsonElement dateJson) &&
                json.TryGetProperty("lostHours", out JsonElement lostHoursJson) &&
                json.TryGetProperty("baja", out JsonElement bajaJson) &&
                json.TryGetProperty("bajaEnd", out JsonElement bajaEndJson) &&
                json.TryGetProperty("bajaRevision", out JsonElement bajaRevisionJson) &&
                json.TryGetProperty("dniSustitucion", out JsonElement dniSustitucionJson) &&
                json.TryGetProperty("altCategory", out JsonElement altCategoryJson) &&
                json.TryGetProperty("category", out JsonElement categoryJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson) &&
                json.TryGetProperty("jefeDni", out JsonElement jefeDniJson) &&
                json.TryGetProperty("jefePwd", out JsonElement jefePwdJson))
            {
                string candidateId = candidateIdJson.GetString();
                string details = detailsJson.GetString();
                DateTime? date = GetJsonDate(dateJson);
                double? lostHours = GetJsonDouble(lostHoursJson);
                bool baja = bajaJson.GetBoolean();
                DateTime? bajaEnd = GetJsonDate(bajaEndJson);
                DateTime? bajaRevision = GetJsonDate(bajaRevisionJson);
                string dniSustitucion = dniSustitucionJson.GetString();
                string altCategory = altCategoryJson.GetString();
                string category = categoryJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);
                string jefeDni = jefeDniJson.GetString();
                string jefePwd = jefePwdJson.GetString();

                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        bool failed = false;

                        //Si no es jefe, debe incluir las credenciales de un jefe
                        if (!access.EsJefe && !HasJefePermission(conn, null, "NotAttendIncidence.Create", jefeDni, jefePwd))
                        {
                            failed = true;
                            result = new { error = "Error 1008, credenciales de jefe incorrectas" };
                        }

                        //Reglas de negocio para creacion de incidencias
                        if (!failed && !checkIncidenceIsUniqueForCandidateAndDay(candidateId, date.Value, conn, null))
                        {
                            failed = true;
                            result = new { error = "Error 4729, ya existe una incidencia de este trabajador en este día" };
                        }

                        //Obtener la empresa que itene ese centro
                        string companyId = getCompanyFromCentro(conn, null, centroId);
                        if (!failed && companyId == null)
                        {
                            failed = true;
                            result = new { error = "Error 4728, no se ha podido obtener la id de la empresa" };
                        }

                        //Crear la incidencia e insertar el evento
                        if (!failed)
                        {
                            string state = "pendiente";
                            string id = ComputeStringHash(centroId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "INSERT INTO incidencia_falta_asistencia " +
                                    "(id, centroId, candidateId, details, date, lostHours, baja, bajaEnd, bajaRevision, dniSustitucion, altCategory, category, state, hasCandidateUnread, hasClientUnread) VALUES " +
                                    "(@ID, @CENTRO_ID, @CANDIDATE_ID, @DETAILS, @DATE, @LOST_HOURS, @BAJA, @BAJA_END, @BAJA_REVISION, @DNI_SUSTITUCION, @ALT_CATEGORY, @CATEGORY, @STATE, 1, 1)";
                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@DETAILS", details);
                                command.Parameters.AddWithValue("@DATE", date.Value);
                                command.Parameters.AddWithValue("@LOST_HOURS", (object)lostHours ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BAJA", baja ? 1 : 0);
                                command.Parameters.AddWithValue("@BAJA_END", (object)bajaEnd ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BAJA_REVISION", (object)bajaRevision ?? DBNull.Value);
                                command.Parameters.AddWithValue("@DNI_SUSTITUCION", (object)dniSustitucion ?? DBNull.Value);
                                command.Parameters.AddWithValue("@ALT_CATEGORY", altCategory ?? "Sin determinar");
                                command.Parameters.AddWithValue("@CATEGORY", category ?? "Sin determinar");
                                command.Parameters.AddWithValue("@STATE", state);

                                command.ExecuteNonQuery();
                            }
                            SaveFileList(new[] { "companies", companyId, "centro", centroId, "incidence-not-attend", id }, attachments, "rrhh_attachment_");
                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = id,
                                author = FindUsernameBySecurityToken(securityToken, conn),
                                action = "RRHH crea incidencia",
                                state = state
                            });
                            deleteNotAttendIncidenceDataInChecks(candidateId, date.Value, conn);

                            await sendEmailAcceptCandidate(candidateId, id, date.Value, conn, null);
                            await sendEmailAcceptClient(candidateId, date.Value, conn, null);

                            result = new { error = false, id };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5733, No se ha podido crear la incidencia" };
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
            if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("details", out JsonElement detailsJson) &&
                json.TryGetProperty("date", out JsonElement dateJson) &&
                json.TryGetProperty("lostHours", out JsonElement lostHoursJson) &&
                json.TryGetProperty("baja", out JsonElement bajaJson) &&
                json.TryGetProperty("altCategory", out JsonElement altCategoryJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string candidateId = candidateIdJson.GetString();
                string details = detailsJson.GetString();
                DateTime? date = GetJsonDate(dateJson);
                double? lostHours = GetJsonDouble(lostHoursJson);
                bool baja = bajaJson.GetBoolean();
                string altCategory = altCategoryJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        bool failed = false;
                        //Comprobar que el cliente tiene permiso para usar este centro
                        string author = ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_INCIDENCIAS);
                        if (author == null)
                        {
                            failed = true;
                            result = new { error = "Error 1002, permisos insuficientes" };
                        }

                        //Reglas de negocio para creacion de incidencias
                        if (!failed && !checkIncidenceCanBeCreated(date.Value))
                        {
                            failed = true;
                            result = new { error = "Error 4729, no se puede crear una incidencia de hace 3 días" };
                        }

                        //Reglas de negocio para creacion de incidencias
                        if (!failed && !checkIncidenceIsUniqueForCandidateAndDay(candidateId, date.Value, conn, null))
                        {
                            failed = true;
                            result = new { error = "Error 4729, ya existe una incidencia de este trabajador en este día" };
                        }

                        //Obtener la empresa que itene ese centro
                        string companyId = getCompanyFromCentro(conn, null, centroId);
                        if (!failed && companyId == null)
                        {
                            failed = true;
                            result = new { error = "Error 4728, no se ha podido obtener la id de la empresa" };
                        }

                        //Crear la incidencia e insertar el evento
                        if (!failed)
                        {
                            string state = "borrador";
                            string id = ComputeStringHash(centroId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "INSERT INTO incidencia_falta_asistencia " +
                                    "(id, centroId, candidateId, details, date, lostHours, baja, altCategory, category, state, hasCandidateUnread) VALUES " +
                                    "(@ID, @CENTRO_ID, @CANDIDATE_ID, @DETAILS, @DATE, @LOST_HOURS, @BAJA, @ALT_CATEGORY, @CATEGORY, @STATE, 1)";
                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@DETAILS", details);
                                command.Parameters.AddWithValue("@DATE", date.Value);
                                command.Parameters.AddWithValue("@LOST_HOURS", (object)lostHours ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BAJA", baja ? 1 : 0);
                                command.Parameters.AddWithValue("@ALT_CATEGORY", altCategory);
                                command.Parameters.AddWithValue("@CATEGORY", "Sin determinar");
                                command.Parameters.AddWithValue("@STATE", state);

                                command.ExecuteNonQuery();
                            }
                            SaveFileList(new[] { "companies", companyId, "centro", centroId, "incidence-not-attend", id }, attachments, "client_attachment_");

                            int n = 0;
                            NotAttendIncidence? incidence = getIncidence(conn, null, id, true);
                            if (incidence != null) n = incidence.Value.number;

                            //Aplicar auto-cat si no faltan documentos
                            if (!altCatRequiresDocuments(altCategory) || attachments.Count > 0)
                            {
                                if (incidence.HasValue)
                                    state = await applyAutoCategory(incidence.Value, "candidato", conn);
                            }

                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = id,
                                author = author,
                                action = "Empresa crea incidencia",
                                state = state
                            });
                            deleteNotAttendIncidenceDataInChecks(candidateId, date.Value, conn);

                            await sendEmailAcceptCandidate(candidateId, id, date.Value, conn, null);

                            result = new { error = false, id, n, requiresDocs = state.Equals("borrador") };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5720, no se han podido crear la incidencia" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("create-for-candidate/{candidateId}")]
        public async Task<IActionResult> CreateForCandidate(string candidateId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("details", out JsonElement detailsJson) &&
                json.TryGetProperty("date", out JsonElement dateJson) &&
                json.TryGetProperty("lostHours", out JsonElement lostHoursJson) &&
                json.TryGetProperty("baja", out JsonElement bajaJson) &&
                json.TryGetProperty("altCategory", out JsonElement altCategoryJson) &&
                json.TryGetProperty("reason", out JsonElement reasonJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string details = detailsJson.GetString();
                DateTime? date = GetJsonDate(dateJson);
                double? lostHours = GetJsonDouble(lostHoursJson);
                bool baja = bajaJson.GetBoolean();
                string altCategory = altCategoryJson.GetString();
                string reason = reasonJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        bool failed = false;

                        //Comprobar que el candidato existe
                        NameAndDniCif? nameAndDni = checkCandidateExistance(conn, null, candidateId);
                        if (nameAndDni == null)
                        {
                            failed = true;
                            result = new { error = "Error 4722, trabajador no encontrado" };
                        }

                        //Regla de negocio sobre cuando se puede crear una incidencia
                        if (!failed && !checkIncidenceCanBeCreated(date.Value))
                        {
                            failed = true;
                            result = new { error = "Error 4729, no se puede crear una incidencia de hace 3 días" };
                        }

                        //Reglas de negocio para creacion de incidencias
                        if (!failed && !checkIncidenceIsUniqueForCandidateAndDay(candidateId, date.Value, conn, null))
                        {
                            failed = true;
                            result = new { error = "Error 4729, ya tienes una incidencia en este día" };
                        }

                        //Obtener el centro del candidato
                        string centroId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT centroId FROM candidatos WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", candidateId);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    if (!reader.IsDBNull(reader.GetOrdinal("centroId")))
                                        centroId = reader.GetString(reader.GetOrdinal("centroId"));
                        }

                        if (centroId == null)
                        {
                            failed = true;
                            result = new { error = "Error 4729, no tienes asignado ningún centro de trabajo" };
                        }

                        string companyId = null;
                        if (!failed)
                        {
                            companyId = getCompanyFromCentro(conn, null, centroId);
                            if (companyId == null)
                            {
                                failed = true;
                                result = new { error = "Error 4728, no se ha podido obtener la id de la empresa" };
                            }
                        }

                        //Crear la incidencia e insertar el evento
                        if (!failed)
                        {
                            string state = "borrador";
                            string id = ComputeStringHash(candidateId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "INSERT INTO incidencia_falta_asistencia " +
                                    "(id, centroId, candidateId, details, date, lostHours, baja, altCategory, category, state, reason, hasClientUnread) VALUES " +
                                    "(@ID, @CENTRO_ID, @CANDIDATE_ID, @DETAILS, @DATE, @LOST_HOURS, @BAJA, @ALT_CATEGORY, @CATEGORY, @STATE, @REASON, 1)";
                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@DETAILS", details);
                                command.Parameters.AddWithValue("@DATE", date.Value);
                                command.Parameters.AddWithValue("@LOST_HOURS", (object)lostHours ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BAJA", baja ? 1 : 0);
                                command.Parameters.AddWithValue("@ALT_CATEGORY", altCategory);
                                command.Parameters.AddWithValue("@CATEGORY", "Sin determinar");
                                command.Parameters.AddWithValue("@STATE", state);
                                command.Parameters.AddWithValue("@REASON", reason);

                                command.ExecuteNonQuery();
                            }
                            SaveFileList(new[] { "companies", companyId, "centro", centroId, "incidence-not-attend", id }, attachments, "candidate_attachment_");

                            //Aplicar auto-cat si no faltan documentos
                            if (!altCatRequiresDocuments(altCategory) || attachments.Count > 0)
                            {
                                NotAttendIncidence? incidence = getIncidence(conn, null, id, true);
                                if (incidence.HasValue)
                                    state = await applyAutoCategory(incidence.Value, "cliente", conn);
                            }

                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = id,
                                author = "-candidate-",
                                action = "Trabajador crea incidencia",
                                state = state
                            });
                            deleteNotAttendIncidenceDataInChecks(candidateId, date.Value, conn);

                            await sendEmailAcceptClient(candidateId, date.Value, conn, null);

                            result = new { error = false, id, requiresDocs = state.Equals("borrador") };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5720, no se han podido crear la incidencia" };
                    }
                }
            }

            return Ok(result);
        }

        // Modificar manualmente una incidencia para resolver un conflicto o cerrarla y abrirla
        [HttpPut]
        [Route("update/{incidenceId}/")]
        public async Task<IActionResult> Update(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("NotAttendIncidence.Update", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("details", out JsonElement detailsJson) &&
                json.TryGetProperty("date", out JsonElement dateJson) &&
                json.TryGetProperty("lostHours", out JsonElement lostHoursJson) &&
                json.TryGetProperty("baja", out JsonElement bajaJson) &&
                json.TryGetProperty("bajaEnd", out JsonElement bajaEndJson) &&
                json.TryGetProperty("bajaRevision", out JsonElement bajaRevisionJson) &&
                json.TryGetProperty("dniSustitucion", out JsonElement dniSustitucionJson) &&
                json.TryGetProperty("altCategory", out JsonElement altCategoryJson) &&
                json.TryGetProperty("category", out JsonElement categoryJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string details = detailsJson.GetString();
                DateTime? date = GetJsonDate(dateJson);
                double? lostHours = GetJsonDouble(lostHoursJson);
                bool baja = bajaJson.GetBoolean();
                DateTime? bajaEnd = GetJsonDate(bajaEndJson);
                DateTime? bajaRevision = GetJsonDate(bajaRevisionJson);
                string dniSustitucion = dniSustitucionJson.GetString();
                string altCategory = altCategoryJson.GetString();
                string category = categoryJson.GetString();
                string state = stateJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        bool failed = false;
                        NotAttendIncidence? incidence = getIncidence(conn, null, incidenceId, false);
                        //Comprobar que exista y que no este cerrada
                        if (incidence == null)
                        {
                            failed = true;
                            result = new { error = "Error 4721, incidencia no encontrada" };
                        }
                        else
                        {
                            if (incidence.Value.closed)
                            {
                                failed = true;
                                result = new { error = "Error 4724, la incidencia esta cerrada no se puede modificar" };
                            }
                        }
                        //Modificar la incidencia e insertar el evento
                        if (!failed)
                        {
                            //Detectar que campos han sido modificados
                            List<string> changes = new();
                            if (incidence.Value.details != details) changes.Add("details");
                            if (incidence.Value.date != date) changes.Add("date");
                            if (incidence.Value.lostHours != lostHours) changes.Add("lostHours");
                            if (incidence.Value.baja != baja) changes.Add("baja");
                            if (incidence.Value.bajaEnd != bajaEnd) changes.Add("bajaEnd");
                            if (incidence.Value.bajaRevision != bajaRevision) changes.Add("bajaRevision");
                            if (incidence.Value.dniSustitucion != dniSustitucion) changes.Add("dniSustitucion");
                            if (incidence.Value.altCategory != altCategory) changes.Add("altCategory");
                            if (incidence.Value.category != category) changes.Add("category");
                            if (attachments.Count > 0) { changes.Add("attachments"); changes.Add("rrhh-attachments"); };
                            if (changes.Count == 0) changes = incidence.Value.lastChanges;

                            //Si no quiere cambiar el estado, ponerlo a pendiente de que el cliente acepte
                            if (incidence.Value.state == state)
                            {
                                if (incidence.Value.state.Equals("espera-pendiente-candidato"))
                                    state = "pendiente-candidato";
                                else if (incidence.Value.state.Equals("espera-pendiente-cliente"))
                                    state = "pendiente-cliente";
                                else if (incidence.Value.state.Equals("conflicto"))
                                    state = "pendiente";
                            }

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "UPDATE incidencia_falta_asistencia SET " +
                                    "details = @DETAILS, date = @DATE, lostHours = @LOST_HOURS, baja = @BAJA, bajaEnd = @BAJA_END, bajaRevision = @BAJA_REVISION, dniSustitucion = @DNI_SUSTITUCION, altCategory = @ALT_CATEGORY, category = @CATEGORY, state = @STATE, hasCandidateUnread = 1, hasClientUnread = 1, lastUpdatedFields = @CHANGES " +
                                    "WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", incidenceId);
                                command.Parameters.AddWithValue("@DETAILS", details);
                                command.Parameters.AddWithValue("@DATE", date.Value);
                                command.Parameters.AddWithValue("@LOST_HOURS", (object)lostHours ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BAJA", baja ? 1 : 0);
                                command.Parameters.AddWithValue("@BAJA_END", (object)bajaEnd ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BAJA_REVISION", (object)bajaRevision ?? DBNull.Value);
                                command.Parameters.AddWithValue("@DNI_SUSTITUCION", (object)dniSustitucion ?? DBNull.Value);
                                command.Parameters.AddWithValue("@ALT_CATEGORY", altCategory);
                                command.Parameters.AddWithValue("@CATEGORY", category);
                                command.Parameters.AddWithValue("@STATE", state);
                                command.Parameters.AddWithValue("@CHANGES", JsonSerializer.Serialize(changes));

                                command.ExecuteNonQuery();
                            }
                            SaveFileList(new[] { "companies", incidence.Value.companyId, "centro", incidence.Value.centroId, "incidence-not-attend", incidenceId }, attachments, "rrhh_attachment_");
                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = incidenceId,
                                author = FindUsernameBySecurityToken(securityToken, conn),
                                action = "Incidencia modificada por RRHH",
                                state = state
                            });

                            if (state.Equals("pendiente-candidato") || state.Equals("pendiente"))
                                await sendEmailAcceptCandidate(incidence.Value.candidateId, incidenceId, date.Value, conn, null);
                            if (state.Equals("pendiente-cliente") || state.Equals("pendiente"))
                                await sendEmailAcceptClient(incidenceId, date.Value, conn, null);

                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5721, no se han podido modificar la incidencia" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPatch]
        [Route("update-closed/{incidenceId}/{securityToken}")]
        public async Task<IActionResult> UpdateClosed(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("NotAttendIncidence.UpdateClosed", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("closed", out JsonElement closedJson))
            {
                bool closed = closedJson.GetBoolean();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        bool failed = false;
                        NotAttendIncidence? incidence = getIncidence(conn, null, incidenceId, false);

                        //Comprobar que exista y que no este cerrada
                        if (incidence == null)
                        {
                            failed = true;
                            result = new { error = "Error 4721, incidencia no encontrada" };
                        }
                        else
                        {
                            if (incidence.Value.closed == closed)
                            {
                                failed = true;
                                result = new { error = "Error 4725, la incidencia ya esta en el estado deseado" };
                            }
                        }

                        if (!failed)
                        {
                            if (closed) //Cerrar la incidencia
                            {
                                if (incidence.Value.state.Equals("aceptada"))
                                {
                                    closeIncidence(incidence.Value, conn);
                                }
                            }
                            else //Abrir la incidencia
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText =
                                        "UPDATE incidencia_falta_asistencia SET " +
                                        "closed = @CLOSED " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ID", incidenceId);
                                    command.Parameters.AddWithValue("@CLOSED", 0);

                                    command.ExecuteNonQuery();
                                }
                            }

                            //Insertar el evento
                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = incidenceId,
                                author = FindUsernameBySecurityToken(securityToken, conn),
                                action = $"Incidencia {(closed ? "cerrada" : "abierta")}",
                                state = incidence.Value.state
                            });

                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5721, no se han podido modificar la incidencia" };
                    }
                }
            }
            return Ok(result);
        }

        // Modificar una incidencia -> pasa de nuevo a pendiente, no puede estar cerrada
        [HttpPut]
        [Route("update-for-client/{incidenceId}/")]
        public async Task<IActionResult> UpdateForClient(string incidenceId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("details", out JsonElement detailsJson) &&
                json.TryGetProperty("date", out JsonElement dateJson) &&
                json.TryGetProperty("lostHours", out JsonElement lostHoursJson) &&
                json.TryGetProperty("baja", out JsonElement bajaJson) &&
                json.TryGetProperty("altCategory", out JsonElement altCategoryJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string details = detailsJson.GetString();
                DateTime? date = GetJsonDate(dateJson);
                double? lostHours = GetJsonDouble(lostHoursJson);
                bool baja = bajaJson.GetBoolean();
                string altCategory = altCategoryJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        bool failed = false;
                        NotAttendIncidence? incidence = getIncidence(conn, null, incidenceId, true);
                        //Comprobar que exista y que no este cerrada
                        if (incidence == null)
                        {
                            failed = true;
                            result = new { error = "Error 4721, incidencia no encontrada" };
                        }
                        else
                        {
                            if (incidence.Value.closed)
                            {
                                failed = true;
                                result = new { error = "Error 4724, la incidencia esta cerrada no se puede modificar" };
                            }
                        }
                        //Comprobar que el usuario de cliente tiene permiso sobre el centro
                        string author = "-client-";
                        if (!failed)
                        {
                            author = ClientHasPermission(clientToken, null, incidence.Value.centroId, CL_PERMISSION_INCIDENCIAS);
                            if (author == null)
                            {
                                failed = true;
                                result = new { error = "Error 1002, permisos insuficientes" };
                            }
                        }
                        //Modificar la incidencia e insertar el evento
                        if (!failed)
                        {
                            //Detectar que campos han sido modificados
                            List<string> changes = new();
                            if (incidence.Value.details != details) changes.Add("details");
                            if (incidence.Value.date != date) changes.Add("date");
                            if (incidence.Value.lostHours != lostHours) changes.Add("lostHours");
                            if (incidence.Value.baja != baja) changes.Add("baja");
                            if (incidence.Value.altCategory != altCategory) changes.Add("altCategory");
                            if (attachments.Count != incidence.Value.clientAttachments.Count) { changes.Add("attachments"); changes.Add("client-attachments"); };
                            if (changes.Count == 0) changes = incidence.Value.lastChanges;

                            string state = incidence.Value.state;
                            string category = incidence.Value.category;
                            if (!incidence.Value.altCategory.Equals(altCategory))
                                category = "Sin determinar";

                            //Es una modificacion de los datos, el candidato debe aceptarla
                            bool applyAutoCat = false;
                            if (incidence.Value.state.Equals("borrador"))
                            {
                                applyAutoCat = attachments.Count > 0 || !CATEGORIES_THAT_REQUIRE_DOCUMENTS.Contains(altCategory);
                            }
                            else
                            {
                                applyAutoCat = !(
                                    Equals(incidence.Value.details, details) &&
                                    Equals(incidence.Value.date, date) &&
                                    Equals(incidence.Value.lostHours, lostHours) &&
                                    Equals(incidence.Value.baja, baja) &&
                                    Equals(incidence.Value.altCategory, altCategory)
                                );
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "UPDATE incidencia_falta_asistencia SET " +
                                    "details = @DETAILS, date = @DATE, lostHours = @LOST_HOURS, baja = @BAJA, altCategory = @ALT_CATEGORY, category = @CATEGORY, state = @STATE, hasCandidateUnread = 1, lastUpdatedFields = @CHANGES " +
                                    "WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", incidenceId);
                                command.Parameters.AddWithValue("@DETAILS", details);
                                command.Parameters.AddWithValue("@DATE", date.Value);
                                command.Parameters.AddWithValue("@LOST_HOURS", (object)lostHours ?? DBNull.Value);
                                command.Parameters.AddWithValue("@BAJA", baja ? 1 : 0);
                                command.Parameters.AddWithValue("@ALT_CATEGORY", altCategory);
                                command.Parameters.AddWithValue("@CATEGORY", category);
                                command.Parameters.AddWithValue("@STATE", state);
                                command.Parameters.AddWithValue("@CHANGES", JsonSerializer.Serialize(changes));
                                command.ExecuteNonQuery();
                            }
                            SaveFileListNoRepetition(new[] { "companies", incidence.Value.companyId, "centro", incidence.Value.centroId, "incidence-not-attend", incidenceId }, attachments, "client_attachment_");
                            if (applyAutoCat)
                            {
                                NotAttendIncidence? updatedIncidence = getIncidence(conn, null, incidenceId, true);
                                state = await applyAutoCategory(updatedIncidence.Value, "candidato", conn);
                            }

                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = incidenceId,
                                author = author,
                                action = "Incidencia modificada por empresa",
                                state = state
                            });

                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5722, no se han podido modificar la incidencia" };
                    }
                }
            }
            return Ok(result);
        }

        // Acepta o rechaza una incidencia
        [HttpPut]
        [Route("accepts-for-client/{incidenceId}/")]
        public async Task<IActionResult> AcceptsForClient(string incidenceId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("accepts", out JsonElement acceptsJson))
            {
                bool accepts = acceptsJson.GetBoolean();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        bool failed = false;
                        NotAttendIncidence? incidence = getIncidence(conn, null, incidenceId, false);
                        //Comprobar que exista y que no este cerrada
                        if (incidence == null)
                        {
                            failed = true;
                            result = new { error = "Error 4721, incidencia no encontrada" };
                        }
                        else
                        {
                            if (incidence.Value.closed)
                            {
                                failed = true;
                                result = new { error = "Error 4724, la incidencia esta cerrada no se puede modificar" };
                            }
                        }
                        //Comprobar que el usuario de cliente tiene permiso sobre el centro
                        string author = "-client-";
                        if (!failed)
                        {
                            author = ClientHasPermission(clientToken, null, incidence.Value.centroId, CL_PERMISSION_INCIDENCIAS);
                            if (author == null)
                            {
                                failed = true;
                                result = new { error = "Error 1002, permisos insuficientes" };
                            }
                        }
                        //Modificar la incidencia e insertar el evento
                        if (!failed)
                        {
                            string state = incidence.Value.state;
                            if (incidence.Value.state.Equals("pendiente-cliente") || incidence.Value.state.Equals("pendiente"))
                            {
                                //Es para aceptar o rechazar, no se pueden modificar datos
                                if (accepts)
                                {
                                    state = incidence.Value.state.Equals("pendiente") ? "pendiente-candidato" : "aceptada";
                                }
                                else
                                {
                                    state = "conflicto";
                                }

                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText =
                                        "UPDATE incidencia_falta_asistencia SET " +
                                        "state = @STATE " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ID", incidenceId);
                                    command.Parameters.AddWithValue("@STATE", state);

                                    command.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                failed = true;
                                result = new { error = "Error 4838, no puedes aceptar/rechazar una incidencia que no este pendiente" };
                            }

                            if (!failed)
                            {
                                insertEvent(conn, null, new NotAttendIncidenceEvent()
                                {
                                    incidenceId = incidenceId,
                                    author = author,
                                    action = "Incidencia " + (accepts ? "aceptada" : "rechazada") + " por empresa",
                                    state = state
                                });

                                if (state.Equals("aceptada"))
                                {
                                    NotAttendIncidence? updatedIncidence = getIncidence(conn, null, incidenceId, true);
                                    if (updatedIncidence.HasValue) closeIncidence(updatedIncidence.Value, conn);
                                }

                                result = new { error = false };
                            }
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5722, no se han podido modificar la incidencia" };
                    }
                }
            }
            return Ok(result);
        }

        // Candidato acepta o rechaza la incidencia y agrega causa, respuessta o adjunto -> solo si esta pendiente
        [HttpPut]
        [Route("update-for-candidate/{incidenceId}")]
        public async Task<IActionResult> UpdateForCandidate(string incidenceId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("reason", out JsonElement reasonJson) &&
                json.TryGetProperty("response", out JsonElement responseJson) &&
                json.TryGetProperty("accepts", out JsonElement acceptsJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string reason = reasonJson.GetString();
                string response = responseJson.GetString();
                bool accepts = acceptsJson.GetBoolean();
                List<string> attachments = GetJsonStringList(attachmentsJson);
                if (reason != null && reason.Trim().Length == 0) reason = null;
                if (response != null && response.Trim().Length == 0) response = null;
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        bool failed = false;
                        NotAttendIncidence? incidence = getIncidence(conn, null, incidenceId, false);
                        //Comprobar que exista y que este abierto y pendiente
                        if (incidence == null)
                        {
                            failed = true;
                            result = new { error = "Error 4721, incidencia no encontrada" };
                        }
                        else
                        {
                            if (incidence.Value.closed)
                            {
                                failed = true;
                                result = new { error = "Error 4724, la incidencia esta cerrada no se puede modificar" };
                            }
                            else
                            {
                                if (!(incidence.Value.state.Equals("pendiente-candidato") || incidence.Value.state.Equals("pendiente")))
                                {
                                    failed = true;
                                    result = new { error = "Error 4724, solo puedes modificar una incidencia está pendiente de aceptación" };
                                }
                            }
                        }

                        //Modificar la incidencia e insertar el evento
                        if (!failed)
                        {
                            //Detectar que campos han sido modificados
                            List<string> changes = new();
                            if (incidence.Value.reason != reason) changes.Add("reason");
                            if (incidence.Value.response != response) changes.Add("response");
                            if (attachments.Count > 0) { changes.Add("attachments"); changes.Add("candidate-attachments"); };
                            if (changes.Count == 0) changes = incidence.Value.lastChanges;

                            string state;
                            if (accepts)
                            {
                                state = incidence.Value.state.Equals("pendiente") ? "pendiente-cliente" : "aceptada";
                            }
                            else
                            {
                                state = "conflicto";
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "UPDATE incidencia_falta_asistencia SET " +
                                    "reason = @REASON, response = @RESPONSE, state = @STATE, hasClientUnread = 1, lastUpdatedFields = @CHANGES " +
                                    "WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", incidenceId);
                                command.Parameters.AddWithValue("@REASON", (object)reason ?? DBNull.Value);
                                command.Parameters.AddWithValue("@RESPONSE", (object)response ?? DBNull.Value);
                                command.Parameters.AddWithValue("@STATE", state);
                                command.Parameters.AddWithValue("@CHANGES", JsonSerializer.Serialize(changes));

                                command.ExecuteNonQuery();
                            }
                            SaveFileList(new[] { "companies", incidence.Value.companyId, "centro", incidence.Value.centroId, "incidence-not-attend", incidenceId }, attachments, "candidate_attachment_");
                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = incidenceId,
                                author = "-candidate-",
                                action = "Incidencia " + (accepts ? "aceptada" : "rechazada") + " por trabajador",
                                state = state
                            });

                            if (state.Equals("aceptada"))
                            {
                                NotAttendIncidence? updatedIncidence = getIncidence(conn, null, incidenceId, true);
                                if (updatedIncidence.HasValue) closeIncidence(updatedIncidence.Value, conn);
                            }

                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5722, no se han podido modificar la incidencia" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route("upload-attachments-for-candidate/{incidenceId}")]
        public async Task<IActionResult> UploadAttachmentsForCandidate(string incidenceId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("publishDraft", out JsonElement publishDraftJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                bool publishDraft = publishDraftJson.GetBoolean();
                List<string> attachments = GetJsonStringList(attachmentsJson);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        bool failed = false;
                        NotAttendIncidence? incidence = getIncidence(conn, null, incidenceId, false);
                        //Comprobar que exista y que este abierta
                        if (incidence == null)
                        {
                            failed = true;
                            result = new { error = "Error 4721, incidencia no encontrada" };
                        }
                        else
                        {
                            if (incidence.Value.closed)
                            {
                                failed = true;
                                result = new { error = "Error 4724, la incidencia esta cerrada no se puede modificar" };
                            }
                            else
                            {
                                if (!incidence.Value.state.Equals("borrador"))
                                {
                                    failed = true;
                                    result = new { error = "Error 4724, solo puedes agregar adjuntos a una incidencia no enviada" };
                                }
                            }
                        }

                        //Modificar la incidencia e insertar el evento
                        if (!failed)
                        {
                            string state = incidence.Value.state;

                            SaveFileList(new[] { "companies", incidence.Value.companyId, "centro", incidence.Value.centroId, "incidence-not-attend", incidenceId }, attachments, "candidate_attachment_");

                            if (publishDraft)
                            {
                                NotAttendIncidence? updatedIncidence = getIncidence(conn, null, incidenceId, true);
                                if (updatedIncidence.HasValue) state = await applyAutoCategory(updatedIncidence.Value, "cliente", conn);
                            }

                            insertEvent(conn, null, new NotAttendIncidenceEvent()
                            {
                                incidenceId = incidenceId,
                                author = "-candidate-",
                                action = "Adjuntos aportados por trabajador",
                                state = state
                            });

                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5722, no se han podido modificar la incidencia" };
                    }
                }
            }

            return Ok(result);
        }

        // Eliminar la incidencia
        [HttpDelete]
        [Route("{incidenceId}/")]
        public IActionResult Delete(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("NotAttendIncidence.Delete", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    bool failed = false;
                    string centroId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT centroId FROM incidencia_falta_asistencia WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", incidenceId);
                        using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) centroId = reader.GetString(reader.GetOrdinal("centroId"));
                    }
                    if (centroId == null)
                    {
                        failed = true;
                        result = new { error = "Error 4721, no se ha encontrado la incidencia" };
                    }
                    string companyId = getCompanyFromCentro(conn, null, centroId);
                    if (!failed && companyId == null)
                    {
                        failed = true;
                        result = new { error = "Error 4728, no se ha podido obtener la id de la empresa" };
                    }
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidate_checks SET notAttendIncidenceId = NULL WHERE notAttendIncidenceId = @ID";
                            command.Parameters.AddWithValue("@ID", incidenceId);
                            command.ExecuteNonQuery();
                        }

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM incidencia_falta_asistencia_eventos WHERE incidenceId = @ID";
                            command.Parameters.AddWithValue("@ID", incidenceId);
                            command.ExecuteNonQuery();
                        }

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM incidencia_falta_asistencia WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", incidenceId);
                            command.ExecuteNonQuery();
                        }

                        DeleteDir(new[] { "companies", companyId, "centro", centroId, "incidence-not-attend", incidenceId });

                        LogToDB(LogType.DELETION, "Incidencia de falta de asistencia", FindUsernameBySecurityToken(securityToken), conn);
                        result = new { error = false };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido borrar la incidencia" };
                }
            }
            return Ok(result);
        }

        // Obtener datos de la incidencia
        [HttpGet]
        [Route("get/{incidenceId}")]
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
                    NotAttendIncidence? incidence = getIncidence(conn, null, incidenceId, true);

                    if (incidence == null)
                    {
                        result = new { error = "Error 4721, no se ha encontrado la incidencia" };
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
                    result = new { error = "Error 5720, no se han podido obtener la incidencia" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("get-for-client/{incidenceId}")]
        public IActionResult GetForClient(string incidenceId)
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
                    NotAttendIncidence? incidenceOrNot = getIncidence(conn, null, incidenceId, true);

                    if (incidenceOrNot == null)
                    {
                        result = new { error = "Error 4721, no se ha encontrado la incidencia" };
                    }
                    else
                    {
                        NotAttendIncidence incidence = incidenceOrNot.Value;
                        incidence.companyRRHH = null;
                        incidence.hasCandidateUnread = false;
                        incidence.centroId = null;
                        incidence.companyId = null;

                        result = new
                        {
                            error = false,
                            incidence
                        };
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE incidencia_falta_asistencia SET hasClientUnread = 0 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", incidenceId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido obtener la incidencia" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("get-for-candidate/{incidenceId}")]
        public IActionResult GetForCandidate(string incidenceId)
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
                    NotAttendIncidence? incidenceOrNot = getIncidence(conn, null, incidenceId, true);
                    if (incidenceOrNot == null)
                    {
                        result = new { error = "Error 4721, no se ha encontrado la incidencia" };
                    }
                    else
                    {
                        NotAttendIncidence incidence = incidenceOrNot.Value;
                        incidence.companyRRHH = null;
                        incidence.hasClientUnread = false;
                        incidence.centroId = null;
                        incidence.companyId = null;
                        incidence.companyCif = null;

                        result = new
                        {
                            error = false,
                            incidence
                        };
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE incidencia_falta_asistencia SET hasCandidateUnread = 0 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", incidenceId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido obtener la incidencia" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("get-assoc-client-users/{incidenceId}")]
        public IActionResult GetAssociatedClientUsers(string incidenceId)
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
                    result = new
                    {
                        error = false,
                        users = getClientUserLinkedToIncidence(incidenceId, conn, null)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido obtener los usuarios de cliente asociados a la incidencia" };
                }
            }
            return Ok(result);
        }

        // Listado de incidencias para rrhh, clientes y candidatos
        [HttpPost]
        [Route("list/")]
        public async Task<IActionResult> List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("NotAttendIncidence.List", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) &&
                json.TryGetProperty("perpage", out JsonElement perpageJson))
            {
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string candidateKey = candidateKeyJson.GetString();
                int? number = GetJsonInt(numberJson);
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                int page = Int32.Parse(pageJson.GetString());
                int perpage = Int32.Parse(perpageJson.GetString());

                try
                {
                    result = new
                    {
                        error = false,
                        incidences = listIncidences(null, centroId, candidateId, null, centroKey, candidateKey, number, state, closed, startDate, endDate, null, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe), null, page, perpage)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido listar las incidencias" };
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
            ResultadoAcceso access = HasPermission("NotAttendIncidence.ListCount", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson))
            {
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string candidateKey = candidateKeyJson.GetString();
                int? number = GetJsonInt(numberJson);
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);

                try
                {
                    result = countIncidences(null, centroId, candidateId, null, centroKey, candidateKey, number, state, closed, startDate, endDate, null, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe), null);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido listar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("list-count-require-attention/")]
        public IActionResult ListCountRequireAttention()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("NotAttendIncidence.ListCount", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) " +
                            "FROM incidencia_falta_asistencia I WHERE " +
                            "(I.state = 'espera-pendiente-candidato' OR I.state = 'espera-pendiente-cliente' OR I.state = 'conflicto') AND " +
                            "(@TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) INNER JOIN centros C ON(AUE.companyId = C.companyId) WHERE U.securityToken = @TOKEN AND C.id = I.centroId))";
                        command.Parameters.AddWithValue("@TOKEN", (object)filterSecurityToken ?? DBNull.Value);

                        result = (int)command.ExecuteScalar();
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5720, no se han podido listar las incidencias" };
            }
            return Ok(result);
        }
        
        [HttpPost]
        //[Route(template: "list-for-client/{clientToken}")]
        [Route(template: "list-for-client/")]
        //public async Task<IActionResult> ListForClient(string clientToken)
        public async Task<IActionResult> ListForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_INCIDENCIAS) == null)
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("companyKey", out JsonElement companyKeyJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("hasUnread", out JsonElement hasUnreadJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) &&
                json.TryGetProperty("perpage", out JsonElement perpageJson))
            {
                string companyId = companyIdJson.GetString();
                string companyKey = companyKeyJson.GetString();
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string candidateKey = candidateKeyJson.GetString();
                int? number = GetJsonInt(numberJson);
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? hasUnread = GetJsonBool(hasUnreadJson);
                int page = Int32.Parse(pageJson.GetString());
                int perpage = Int32.Parse(perpageJson.GetString());

                try
                {
                    List<NotAttendIncidence> incidences = listIncidences(companyId, centroId, candidateId, companyKey,
                        centroKey, candidateKey, number, state, closed, startDate, endDate, null, hasUnread, null,
                        clientToken, page, perpage);
                    for (int i = 0; i < incidences.Count; i++)
                    {
                        NotAttendIncidence incidence = incidences[i];
                        incidence.companyId = null;
                        incidence.centroId = null;
                        incidence.companyName = null;
                        incidence.companyCif = null;
                        incidence.companyRRHH = null;
                        incidence.candidateId = null;
                        incidence.category = null;
                        incidences[i] = incidence;
                    }

                    result = new
                    {
                        error = false,
                        incidences
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido listar las incidencias" };
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
            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_INCIDENCIAS) == null)
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("companyKey", out JsonElement companyKeyJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("hasUnread", out JsonElement hasUnreadJson))
            {
                string companyId = companyIdJson.GetString();
                string companyKey = companyKeyJson.GetString();
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string candidateKey = candidateKeyJson.GetString();
                int? number = GetJsonInt(numberJson);
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? hasUnread = GetJsonBool(hasUnreadJson);
                try
                {
                    result = countIncidences(companyId, centroId, candidateId, companyKey, centroKey, candidateKey, number, state, closed, startDate, endDate, null, hasUnread, null, clientToken);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido listar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("list-for-client-plain/")]
        public IActionResult ListForClientPlain()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_INCIDENCIAS) == null)
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }
            List<object> incidences = new();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT I.id, I.number, I.state, I.closed, I.date, I.creationTime, I.hasClientUnread, " +
                        "E.nombre as companyName, NULLIF(TRIM(CONCAT(C.nombre, ' ', C.apellidos)), '') as candidateName, C.dni as candidateDni, " +
                        "CE.alias as centroAlias " +
                        "FROM incidencia_falta_asistencia I INNER JOIN centros CE ON(I.centroId = CE.id) INNER JOIN empresas E ON(CE.companyId = E.id) " +
                        "LEFT OUTER JOIN candidatos C ON(I.candidateId = C.id) " +
                        "INNER JOIN client_user_centros CUC ON(CE.id = CUC.centroId) " +
                        "INNER JOIN client_users CU ON(CUC.clientUserId = CU.id) WHERE " +
                        "CU.token = @TOKEN " +
                        "ORDER BY I.creationTime DESC";
                    command.Parameters.AddWithValue("@TOKEN", clientToken);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            incidences.Add(new
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                number = reader.GetInt32(reader.GetOrdinal("number")),
                                state = attenuateStateForClient(reader.GetString(reader.GetOrdinal("state"))),
                                closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                                companyName = reader.GetString(reader.GetOrdinal("companyName")),
                                centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                                candidateName = reader.IsDBNull(reader.GetOrdinal("candidateName"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("candidateName")),
                                candidateDni = reader.IsDBNull(reader.GetOrdinal("candidateDni"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("candidateDni")),
                                date = reader.GetDateTime(reader.GetOrdinal("date")),
                                creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                                hasClientUnread = reader.GetInt32(reader.GetOrdinal("hasClientUnread")) == 1
                            });
                        }
                    }
                }
            }
            result = new { error = false, incidences };
            return Ok(result);
        }

        [HttpPost]
        [Route("list-for-candidate/{candidateId}")]
        public async Task<IActionResult> ListForCandidate(string candidateId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("hasUnread", out JsonElement hasUnreadJson))
            {
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? hasUnread = GetJsonBool(hasUnreadJson);

                try
                {
                    result = new
                    {
                        error = false,
                        incidences = listIncidences(null, null, candidateId, null, null, null, null, state, closed, startDate, endDate, hasUnread, null, null, null)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido listar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("list-count-for-candidate/{candidateId}")]
        public async Task<IActionResult> ListCountForCandidate(string candidateId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("hasUnread", out JsonElement hasUnreadJson))
            {
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? hasUnread = GetJsonBool(hasUnreadJson);

                try
                {
                    result = countIncidences(null, null, candidateId, null, null, null, null, state, closed, startDate, endDate, hasUnread, null, null, null);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido listar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("count-new-for-candidate/{candidateId}")]
        public IActionResult CountNewForCandidate(string candidateId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) " +
                            "FROM incidencia_falta_asistencia I " +
                            "WHERE " +
                            "I.candidateId = @CANDIDATE_ID AND " +
                            "I.closed = 0 AND " +
                            "(I.state = 'pendiente' OR I.state = 'pendiente-candidato' OR I.hasCandidateUnread = 1)";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                        result = (int)command.ExecuteScalar();
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5720, no se han podido listar las incidencias" };
            }
            return Ok(result);
        }

        //Generacion de excels
        [HttpPost]
        [Route("excel/")]
        public async Task<IActionResult> DownloadExcel()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            ResultadoAcceso access = HasPermission("NotAttendIncidence.DownloadExcel", securityToken);
            if (!access.Acceso) return new NoContentResult();
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("maxResults", out JsonElement maxResultsJson))
            {
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string candidateKey = candidateKeyJson.GetString();
                int? number = GetJsonInt(numberJson);
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                int maxResults = GetJsonInt(maxResultsJson) ?? 10000;

                try
                {
                    return generateExcel(listIncidences(null, centroId, candidateId, null, centroKey, candidateKey, number, state, closed, startDate, endDate, null, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe), null, 0, maxResults), true);
                }
                catch (Exception)
                {
                }
            }
            return new NoContentResult();
        }


        [HttpPost]
        [Route("excel-for-client/{clientToken}")]
        public async Task<IActionResult> DownloadExcelForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_INCIDENCIAS) == null) return new NoContentResult();
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("companyKey", out JsonElement companyKeyJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("centroKey", out JsonElement centroKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("hasUnread", out JsonElement hasUnreadJson) &&
                json.TryGetProperty("maxResults", out JsonElement maxResultsJson))
            {
                string companyId = companyIdJson.GetString();
                string companyKey = companyKeyJson.GetString();
                string centroId = centroIdJson.GetString();
                string centroKey = centroKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string candidateKey = candidateKeyJson.GetString();
                int? number = GetJsonInt(numberJson);
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                bool? hasUnread = GetJsonBool(hasUnreadJson);
                int maxResults = GetJsonInt(maxResultsJson) ?? 10000;

                try
                {
                    return generateExcel(listIncidences(companyId, centroId, candidateId, companyKey, centroKey, candidateKey, number, state, closed, startDate, endDate, null, hasUnread, null, clientToken, 0, maxResults));
                }
                catch (Exception)
                {
                }
            }
            return new NoContentResult();
        }




        [HttpPost]
        [Route(template: "list-generalized-for-client/")]
        public async Task<IActionResult> ListGeneralizedForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_INCIDENCIAS) == null)
            {
                return Ok(new { error = "Error 1002, permisos insuficientes" });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson))
            {
                string companyId = companyIdJson.GetString();
                string centroId = centroIdJson.GetString();

                try
                {
                    List<CLGeneralizedIncidence> incidences = new();

                    List<NotAttendIncidence> incidencesNotAttend = listIncidences(companyId, centroId, null, null, null, null, null, null, null, null, null, null, null, null, clientToken, 0, 10000);

                    incidences.AddRange(incidencesNotAttend.Select(i => new CLGeneralizedIncidence()
                    {
                        id = i.id,
                        number = i.number,
                        type = "notattend",
                        candidateDni = i.candidateDni,
                        candidateName = i.candidateName,
                        category = i.category,
                        state = i.state,
                        closed = i.closed,
                        creationTime = i.creationTime
                    }));

                    List<GenericIncidence> incidencesGeneric;
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        incidencesGeneric = IncidenceGenericController.listIncidences(conn, null, null, null, null, null, null, null, null, null, null, null, null, clientToken, centroId, companyId, 0, 10000);
                    }

                    incidences.AddRange(incidencesGeneric.Select(i => new CLGeneralizedIncidence()
                    {
                        id = i.id,
                        number = i.number,
                        type = "generic",
                        candidateDni = i.candidateDni,
                        candidateName = i.candidateName,
                        category = i.category,
                        state = i.state,
                        closed = i.closed,
                        creationTime = i.creationTime
                    }));

                    incidences = incidences.OrderByDescending(i => i.creationTime).ToList();

                    result = new
                    {
                        error = false,
                        incidences
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5720, no se han podido listar las incidencias" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        public struct NotAttendIncidence
        {
            public string id { get; set; }
            public int number { get; set; }
            public string companyId { get; set; }
            public string centroId { get; set; }
            public string centroAlias { get; set; }
            public string companyName { get; set; }
            public string companyCif { get; set; }
            public string companyRRHH { get; set; }
            public string candidateId { get; set; }
            public string candidateName { get; set; }
            public string candidateDni { get; set; }
            public string details { get; set; }
            public DateTime date { get; set; }
            public double? lostHours { get; set; }
            public bool baja { get; set; }
            public DateTime? bajaEnd { get; set; }
            public DateTime? bajaRevision { get; set; }
            public string dniSustitucion { get; set; }
            public string nombreSustitucion { get; set; }
            public string idSustitucion { get; set; }
            public string altCategory { get; set; }
            public string category { get; set; }
            public string reason { get; set; }
            public string state { get; set; }
            public string response { get; set; }
            public bool closed { get; set; }
            public DateTime creationTime { get; set; }
            public List<string> clientAttachments { get; set; }
            public List<string> candidateAttachments { get; set; }
            public List<string> rrhhAttachments { get; set; }
            public List<NotAttendIncidenceEvent> events { get; set; }
            public bool hasCandidateUnread { get; set; }
            public bool hasClientUnread { get; set; }
            public string type { get; set; }
            public string createdBy { get; set; }
            public List<string> lastChanges { get; set; } // details | date | lostHours | baja | bajaEnd | bajaRevision | dniSustitucion | altCategory | category | reason | response | attachments | rrhh-attachments | client-attachments | candidate-attachments
        }

        public struct NotAttendIncidenceEvent
        {
            public int id { get; set; }
            public string incidenceId { get; set; }
            public string author { get; set; } // -candidate- | username | clientUser
            public string authorRole { get; set; }
            public string action { get; set; }
            public string state { get; set; } // borrador | espera-pendiente-candidato | espera-pendiente-cliente | pendiente | pendiente-candidato | pendiente-cliente | aceptada | rechazada | conflicto
            public DateTime time { get; set; }
        }

        public struct NameAndDniCif
        {
            public string name { get; set; }
            public string dniCif { get; set; }
        }
        public struct CLGeneralizedIncidence
        {
            public string id { get; set; }
            public int number { get; set; }
            public string type { get; set; }
            public string candidateName { get; set; }
            public string candidateDni { get; set; }
            public string category { get; set; }
            public string state { get; set; }
            public bool closed { get; set; }
            public DateTime creationTime { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static bool checkIncidenceCanBeCreated(DateTime origin)
        {
            //DateTime now = DateTime.Now;
            //DateTime lastMonth = DateTime.Now.AddDays(-(now.Day + 1));
            //return (origin.Month == now.Month && origin.Year == now.Year) || (origin.Month == lastMonth.Month && origin.Year == lastMonth.Year && now.Day <= 5);
            DateTime today = DateTime.Now.Date;
            DateTime falta = origin.Date;
            return falta <= today && (today - falta).Days <= 3;
        }
        public static bool checkIncidenceIsUniqueForCandidateAndDay(string candidateId, DateTime origin, SqlConnection conn, SqlTransaction transaction)
        {
            bool exists = false;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT id FROM incidencia_falta_asistencia WHERE candidateId = @CANDIDATE AND date = @DATE";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                command.Parameters.AddWithValue("@DATE", origin);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        exists = true;
            }

            return !exists;
        }
        public static List<NotAttendIncidence> listIncidences(string companyId, string centroId, string candidateId, string companyKey, string centroKey, string candidateKey, int? number, string state, bool? closed, DateTime? startDate, DateTime? endDate, bool? hasCandidateUnread, bool? hasClientUnread, string securityToken, string clientToken, int page = 0, int perpage = 10)
        {
            List<NotAttendIncidence> incidences = new List<NotAttendIncidence>();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT I.id, I.number, I.centroId, I.candidateId, I.date, I.altCategory, I.category, I.state, I.closed, I.creationTime, I.baja, I.hasCandidateUnread, I.hasClientUnread, " +
                        "E.nombre as companyName, E.cif as companyCif, E.id as companyId, CE.alias as centroAlias, NULLIF(TRIM(CONCAT(C.nombre, ' ', C.apellidos)), '') as candidateName, C.dni, " +
                        "companyRRHH = (SELECT TOP 1 CONCAT(U.name, ' ', U.surname) FROM users U INNER JOIN asociacion_usuario_empresa AUE ON(U.id = AUE.userId) WHERE AUE.companyId = E.id) " +
                        "FROM incidencia_falta_asistencia I INNER JOIN centros CE ON(I.centroId = CE.id) INNER JOIN empresas E ON(CE.companyId = E.id) " +
                        "LEFT OUTER JOIN candidatos C ON(I.candidateId = C.id) WHERE " +
                        "(@COMPANY_ID IS NULL OR @COMPANY_ID = E.id) AND " +
                        "(@CENTRO_ID IS NULL OR @CENTRO_ID = CE.id) AND " +
                        "(@CANDIDATE_ID IS NULL OR @CANDIDATE_ID = C.id) AND " +
                        "(@COMPANY_KEY IS NULL OR E.nombre LIKE @COMPANY_KEY OR E.cif LIKE @COMPANY_KEY) AND " +
                        "(@CENTRO_KEY IS NULL OR CONCAT(E.nombre, ' ', CE.alias) LIKE @CENTRO_KEY) AND " +
                        "(@CANDIDATE_KEY IS NULL OR C.dni LIKE @CANDIDATE_KEY OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @CANDIDATE_KEY OR C.telefono LIKE @CANDIDATE_KEY) AND " +
                        "(@NUMBER IS NULL OR @NUMBER = I.number) AND " +
                        "(@STATE IS NULL OR @STATE = I.state) AND " +
                        "(@CLOSED IS NULL OR @CLOSED = I.closed) AND " +
                        "(@START_DATE IS NULL OR I.creationTime >= @START_DATE) AND " +
                        "(@END_DATE IS NULL OR I.creationTime <= @END_DATE) AND " +
                        "(@CANDIDATE_UNREAD IS NULL OR hasCandidateUnread = @CANDIDATE_UNREAD) AND " +
                        "(@CLIENT_UNREAD IS NULL OR hasClientUnread = @CLIENT_UNREAD) AND " +
                        "(@SECURITY_TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) WHERE U.securityToken = @SECURITY_TOKEN AND AUE.companyId = E.id)) AND " +
                        "(@CLIENT_TOKEN IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE U.token = @CLIENT_TOKEN AND CUC.centroId = CE.id)) " +
                        "ORDER BY I.creationTime DESC OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY";
                    command.Parameters.AddWithValue("@COMPANY_ID", (object)companyId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CANDIDATE_ID", (object)candidateId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY_KEY", companyKey != null ? ("%" + companyKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_KEY", centroKey != null ? ("%" + centroKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@CANDIDATE_KEY", candidateKey != null ? ("%" + candidateKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@NUMBER", (object)number ?? DBNull.Value);
                    command.Parameters.AddWithValue("@STATE", (object)state ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CLOSED", closed != null ? (closed.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@START_DATE", (object)startDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@END_DATE", (object)endDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CANDIDATE_UNREAD", hasCandidateUnread != null ? (hasCandidateUnread.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@CLIENT_UNREAD", hasClientUnread != null ? (hasClientUnread.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@SECURITY_TOKEN", (object)securityToken ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CLIENT_TOKEN", (object)clientToken ?? DBNull.Value);
                    command.Parameters.AddWithValue("@OFFSET", page * perpage);
                    command.Parameters.AddWithValue("@LIMIT", perpage);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            incidences.Add(new NotAttendIncidence()
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                number = reader.GetInt32(reader.GetOrdinal("number")),
                                companyId = reader.GetString(reader.GetOrdinal("companyId")),
                                centroId = reader.GetString(reader.GetOrdinal("centroId")),
                                centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                                companyName = reader.GetString(reader.GetOrdinal("companyName")),
                                companyCif = reader.GetString(reader.GetOrdinal("companyCif")),
                                companyRRHH = reader.IsDBNull(reader.GetOrdinal("companyRRHH")) ? null : reader.GetString(reader.GetOrdinal("companyRRHH")),
                                candidateId = reader.IsDBNull(reader.GetOrdinal("candidateId")) ? null : reader.GetString(reader.GetOrdinal("candidateId")),
                                candidateName = reader.IsDBNull(reader.GetOrdinal("candidateName")) ? null : reader.GetString(reader.GetOrdinal("candidateName")),
                                candidateDni = reader.IsDBNull(reader.GetOrdinal("dni")) ? null : reader.GetString(reader.GetOrdinal("dni")),
                                date = reader.GetDateTime(reader.GetOrdinal("date")),
                                altCategory = reader.GetString(reader.GetOrdinal("altCategory")),
                                category = reader.GetString(reader.GetOrdinal("category")),
                                state = reader.GetString(reader.GetOrdinal("state")),
                                closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                                creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                                baja = reader.GetInt32(reader.GetOrdinal("baja")) == 1,
                                hasCandidateUnread = reader.GetInt32(reader.GetOrdinal("hasCandidateUnread")) == 1,
                                hasClientUnread = reader.GetInt32(reader.GetOrdinal("hasClientUnread")) == 1,
                                type = "notattend"
                            });
                        }
                    }
                }
            }

            return incidences;
        }
        public static int countIncidences(string companyId, string centroId, string candidateId, string companyKey, string centroKey, string candidateKey, int? number, string state, bool? closed, DateTime? startDate, DateTime? endDate, bool? hasCandidateUnread, bool? hasClientUnread, string securityToken, string clientToken)
        {
            int n = 0;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT COUNT(*) " +
                        "FROM incidencia_falta_asistencia I INNER JOIN centros CE ON(I.centroId = CE.id) INNER JOIN empresas E ON(CE.companyId = E.id) " +
                        "LEFT OUTER JOIN candidatos C ON(I.candidateId = C.id) WHERE " +
                        "(@CENTRO_ID IS NULL OR @CENTRO_ID = CE.id) AND " +
                        "(@CANDIDATE_ID IS NULL OR @CANDIDATE_ID = C.id) AND " +
                        "(@COMPANY_KEY IS NULL OR E.nombre LIKE @COMPANY_KEY OR E.cif LIKE @COMPANY_KEY) AND " +
                        "(@CENTRO_KEY IS NULL OR CONCAT(E.nombre, ' ', CE.alias) LIKE @CENTRO_KEY) AND " +
                        "(@CANDIDATE_KEY IS NULL OR C.dni LIKE @CANDIDATE_KEY OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @CANDIDATE_KEY OR C.telefono LIKE @CANDIDATE_KEY) AND " +
                        "(@NUMBER IS NULL OR @NUMBER = I.number) AND " +
                        "(@STATE IS NULL OR @STATE = I.state) AND " +
                        "(@CLOSED IS NULL OR @CLOSED = I.closed) AND " +
                        "(@START_DATE IS NULL OR I.creationTime >= @START_DATE) AND " +
                        "(@END_DATE IS NULL OR I.creationTime <= @END_DATE) AND " +
                        "(@CANDIDATE_UNREAD IS NULL OR hasCandidateUnread = @CANDIDATE_UNREAD) AND " +
                        "(@CLIENT_UNREAD IS NULL OR hasClientUnread = @CLIENT_UNREAD) AND" +
                        "(@SECURITY_TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) WHERE U.securityToken = @SECURITY_TOKEN AND AUE.companyId = E.id)) AND " +
                        "(@CLIENT_TOKEN IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE U.token = @CLIENT_TOKEN AND CUC.centroId = CE.id)) ";
                    command.Parameters.AddWithValue("@COMPANY_ID", (object)companyId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CANDIDATE_ID", (object)candidateId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY_KEY", companyKey != null ? ("%" + companyKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO_KEY", centroKey != null ? ("%" + centroKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@CANDIDATE_KEY", candidateKey != null ? ("%" + candidateKey + "%") : DBNull.Value);
                    command.Parameters.AddWithValue("@NUMBER", (object)number ?? DBNull.Value);
                    command.Parameters.AddWithValue("@STATE", (object)state ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CLOSED", closed != null ? (closed.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@START_DATE", (object)startDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@END_DATE", (object)endDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CANDIDATE_UNREAD", hasCandidateUnread != null ? (hasCandidateUnread.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@CLIENT_UNREAD", hasClientUnread != null ? (hasClientUnread.Value ? 1 : 0) : DBNull.Value);
                    command.Parameters.AddWithValue("@SECURITY_TOKEN", (object)securityToken ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CLIENT_TOKEN", (object)clientToken ?? DBNull.Value);

                    n = (int)command.ExecuteScalar();
                }
            }

            return n;
        }
        public static NotAttendIncidence? getIncidence(SqlConnection conn, SqlTransaction transaction, string incidenceId, bool complete)
        {
            bool found = false;
            NotAttendIncidence incidence = new NotAttendIncidence();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT I.id, I.number, I.centroId, I.candidateId, I.details, I.date, I.lostHours, I.baja, I.bajaEnd, I.bajaRevision, I.dniSustitucion, I.altCategory, I.category, I.reason, I.state, I.response, I.closed, I.creationTime, I.lastUpdatedFields, I.hasCandidateUnread, I.hasClientUnread, E.id as companyId, CE.alias as centroAlias," +
                    "E.nombre as companyName, E.cif as companyCif, NULLIF(TRIM(CONCAT(C.nombre, ' ', C.apellidos)), '') as candidateName, C.dni, " +
                    "TRIM(CONCAT(CS.nombre, ' ', CS.apellidos)) as sustitutoName, CS.id as idSustitucion " +
                    "FROM incidencia_falta_asistencia I INNER JOIN centros CE ON(I.centroId = CE.id) INNER JOIN empresas E ON(CE.companyId = E.id) " +
                    "LEFT OUTER JOIN candidatos C ON(I.candidateId = C.id) LEFT OUTER JOIN candidatos CS ON(I.dniSustitucion = CS.dni) " +
                    "WHERE I.id = @ID";
                command.Parameters.AddWithValue("@ID", incidenceId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        incidence = new NotAttendIncidence()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            number = reader.GetInt32(reader.GetOrdinal("number")),
                            centroId = reader.GetString(reader.GetOrdinal("centroId")),
                            centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            companyName = reader.GetString(reader.GetOrdinal("companyName")),
                            companyCif = reader.GetString(reader.GetOrdinal("companyCif")),
                            candidateId = reader.IsDBNull(reader.GetOrdinal("candidateId")) ? null : reader.GetString(reader.GetOrdinal("candidateId")),
                            candidateName = reader.IsDBNull(reader.GetOrdinal("candidateName")) ? null : reader.GetString(reader.GetOrdinal("candidateName")),
                            candidateDni = reader.IsDBNull(reader.GetOrdinal("dni")) ? null : reader.GetString(reader.GetOrdinal("dni")),
                            details = reader.GetString(reader.GetOrdinal("details")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            lostHours = reader.IsDBNull(reader.GetOrdinal("lostHours")) ? null : reader.GetDouble(reader.GetOrdinal("lostHours")),
                            baja = reader.GetInt32(reader.GetOrdinal("baja")) == 1,
                            bajaEnd = reader.IsDBNull(reader.GetOrdinal("bajaEnd")) ? null : reader.GetDateTime(reader.GetOrdinal("bajaEnd")),
                            bajaRevision = reader.IsDBNull(reader.GetOrdinal("bajaRevision")) ? null : reader.GetDateTime(reader.GetOrdinal("bajaRevision")),
                            dniSustitucion = reader.IsDBNull(reader.GetOrdinal("dniSustitucion")) ? null : reader.GetString(reader.GetOrdinal("dniSustitucion")),
                            nombreSustitucion = reader.IsDBNull(reader.GetOrdinal("sustitutoName")) ? null : reader.GetString(reader.GetOrdinal("sustitutoName")),
                            idSustitucion = reader.IsDBNull(reader.GetOrdinal("idSustitucion")) ? null : reader.GetString(reader.GetOrdinal("idSustitucion")),
                            altCategory = reader.GetString(reader.GetOrdinal("altCategory")),
                            category = reader.GetString(reader.GetOrdinal("category")),
                            reason = reader.IsDBNull(reader.GetOrdinal("reason")) ? null : reader.GetString(reader.GetOrdinal("reason")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            response = reader.IsDBNull(reader.GetOrdinal("response")) ? null : reader.GetString(reader.GetOrdinal("response")),
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            lastChanges = reader.IsDBNull(reader.GetOrdinal("lastUpdatedFields")) ? new() : GetJsonStringList(reader.GetString(reader.GetOrdinal("lastUpdatedFields"))),
                            events = new List<NotAttendIncidenceEvent>(),
                            hasCandidateUnread = reader.GetInt32(reader.GetOrdinal("hasCandidateUnread")) == 1,
                            hasClientUnread = reader.GetInt32(reader.GetOrdinal("hasClientUnread")) == 1
                        };
                        found = true;
                    }
                }
            }

            if (found && complete)
            {
                incidence.clientAttachments = ReadFileList(new[] { "companies", incidence.companyId, "centro", incidence.centroId, "incidence-not-attend", incidence.id }, "client_attachment_");
                incidence.candidateAttachments = ReadFileList(new[] { "companies", incidence.companyId, "centro", incidence.centroId, "incidence-not-attend", incidence.id }, "candidate_attachment_");
                incidence.rrhhAttachments = ReadFileList(new[] { "companies", incidence.companyId, "centro", incidence.centroId, "incidence-not-attend", incidence.id }, "rrhh_attachment_");

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "SELECT V.id, V.author, V.action, V.state, V.time FROM incidencia_falta_asistencia_eventos V WHERE V.incidenceId = @ID ORDER BY V.time DESC";
                    command.Parameters.AddWithValue("@ID", incidence.id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            NotAttendIncidenceEvent evento = new NotAttendIncidenceEvent() //event es una palabra reservada
                            {
                                id = reader.GetInt32(reader.GetOrdinal("id")),
                                author = reader.GetString(reader.GetOrdinal("author")),
                                action = reader.GetString(reader.GetOrdinal("action")),
                                authorRole = null,
                                state = reader.GetString(reader.GetOrdinal("state")),
                                time = reader.GetDateTime(reader.GetOrdinal("time"))
                            };
                            if (evento.author.Equals("-candidate-"))
                            {
                                evento.author = incidence.candidateName;
                                evento.authorRole = "candidate";
                            }
                            if (evento.authorRole == null)
                            {
                                evento.authorRole = evento.author.Contains("@") ? "client" : "thinkandjob";
                            }
                            incidence.events.Add(evento);
                        }
                    }
                }

                //Si hay eventos, el primer autor del primer evento es el creador de la incidencia
                if (incidence.events.Count > 0)
                {
                    incidence.createdBy = incidence.events[0].author;
                }
            }

            return found ? incidence : null;
        }

        public static NameAndDniCif? checkCandidateExistance(SqlConnection conn, SqlTransaction transaction, string candidateid)
        {
            NameAndDniCif? result = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT CONCAT(nombre, ' ', apellidos) as name, dni FROM candidatos WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", candidateid);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        result = new NameAndDniCif()
                        {
                            name = reader.GetString(reader.GetOrdinal("name")),
                            dniCif = reader.GetString(reader.GetOrdinal("dni"))
                        };
                    }
                }
            }

            return result;
        }
        private string getCompanyFromCentro(SqlConnection conn, SqlTransaction transaction, string centroId)
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
        public static void insertEvent(SqlConnection conn, SqlTransaction transaction, NotAttendIncidenceEvent ev)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "INSERT INTO incidencia_falta_asistencia_eventos " +
                    "(incidenceId, author, action, state) VALUES " +
                    "(@INCIDENCE_ID, @AUTHOR, @ACTION, @STATE)";
                command.Parameters.AddWithValue("@INCIDENCE_ID", ev.incidenceId);
                command.Parameters.AddWithValue("@AUTHOR", ev.author);
                command.Parameters.AddWithValue("@ACTION", ev.action);
                command.Parameters.AddWithValue("@STATE", ev.state);

                command.ExecuteNonQuery();
            }
        }

        public static async Task<string> applyAutoCategory(NotAttendIncidence incidence, string whoHasToAccept, SqlConnection conn, SqlTransaction transaction = null)
        {
            string state;
            string category = getAutoCategoryEquivalence(incidence);
            bool espera = incidence.category.Equals("Sin determinar") && !hasAutoCategoryEquivalence(incidence);
            if (espera)
            {
                state = "espera-pendiente-" + whoHasToAccept;
            }
            else
            {
                state = "pendiente-" + whoHasToAccept;
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "UPDATE incidencia_falta_asistencia SET state = @STATE, hasCandidateUnread = @UNREAD_CANDIDATE, hasClientUnread = @UNREAD_CLIENT, category = @CATEGORY WHERE id = @ID";
                command.Parameters.AddWithValue("@STATE", state);
                command.Parameters.AddWithValue("@UNREAD_CANDIDATE", whoHasToAccept.Equals("candidato") ? 1 : 0);
                command.Parameters.AddWithValue("@UNREAD_CLIENT", whoHasToAccept.Equals("cliente") ? 1 : 0);
                command.Parameters.AddWithValue("@CATEGORY", category);
                command.Parameters.AddWithValue("@ID", incidence.id);
                command.ExecuteNonQuery();
            }

            if (!espera)
            {
                if (whoHasToAccept.Equals("cliente"))
                    await sendEmailAcceptClient(incidence.candidateId, incidence.date, conn, transaction);
                else if (whoHasToAccept.Equals("candidato"))
                    await sendEmailAcceptCandidate(incidence.candidateId, incidence.id, incidence.date, conn, transaction);
            }

            return state;
        }


        public static Dictionary<string, string> ATTENUATED_STATE_FOR_CLIENT = new()
        {
            { "espera-pendiente-candidato", "Esperando a que se cumplimente" },
            { "espera-pendiente-cliente", "Esperando a que se cumplimente" },
            { "pendiente", "Pendiente" },
            { "pendiente-candidato", "Pendiente" },
            { "pendiente-cliente", "Pendiente" },
            { "aceptada", "Aceptada" },
            { "rechazada", "Rechazada" },
            { "conflicto", "Conflicto" },
            { "borrador", "Borrador" }
        };
        public static string attenuateStateForClient(string state)
        {
            if (ATTENUATED_STATE_FOR_CLIENT.ContainsKey(state))
                return ATTENUATED_STATE_FOR_CLIENT[state];
            else
                return state;
        }

        public static Dictionary<string, string> ATTENUATED_STATE_FOR_RRHH = new()
        {
            { "espera-pendiente-candidato", "Esperando a RRHH (cliente)" },
            { "espera-pendiente-cliente", "Esperando a RRHH (candidato)" },
            { "pendiente", "Pendiente de candidato y cliente" },
            { "pendiente-candidato", "Pendiente de candidato" },
            { "pendiente-cliente", "Pendiente de cliente" },
            { "aceptada", "Aceptada" },
            { "rechazada", "Rechazada" },
            { "conflicto", "Conflicto" },
            { "borrador", "Borrador" }
        };
        public static string attenuateStateForRRHH(string state)
        {
            if (ATTENUATED_STATE_FOR_RRHH.ContainsKey(state))
                return ATTENUATED_STATE_FOR_RRHH[state];
            else
                return state;
        }


        public static readonly Dictionary<string, string> AUTO_CATEGORY_EQUIVALENCE = new()
        {
            { "Enfermedad/Cuidado familiar", "Licencia no retribuida" },
            { "Llegar tarde", "Licencia no retribuida" },
            { "Estar indispuesto", "Licencia no retribuida" }
        };
        public static bool hasAutoCategoryEquivalence(NotAttendIncidence incidence)
        {
            return incidence.candidateAttachments.Count == 0 && incidence.clientAttachments.Count == 0 && !incidence.baja && AUTO_CATEGORY_EQUIVALENCE.ContainsKey(incidence.altCategory);
        }
        public static string getAutoCategoryEquivalence(NotAttendIncidence incidence)
        {
            return hasAutoCategoryEquivalence(incidence)
                ? AUTO_CATEGORY_EQUIVALENCE[incidence.altCategory]
                : "Sin determinar";

        }

        public static readonly List<string> CATEGORIES_THAT_REQUIRE_DOCUMENTS = new() { "Baja médica", "Reposo", "Accidente", "Fallecimiento familiar", "Ingreso familiar", "Enfermedad/Cuidado familiar", "Gestiones legales", "Permiso matrimonio", "Maternidad/Paternidad", "Otros" };
        public static bool altCatRequiresDocuments(string altCategory)
        {
            return CATEGORIES_THAT_REQUIRE_DOCUMENTS.Contains(altCategory);
        }

        
        public static void closeIncidence(NotAttendIncidence incidence, SqlConnection conn)
        {
            //Calcular el numero de horas a restar
            double ajuste = 0;
            if (incidence.lostHours != null)
            {
                ajuste = -incidence.lostHours.Value;
            }
            else
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT weekHours FROM contratos WHERE candidateId = @CANDIDATE_ID";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", incidence.candidateId);

                    using (SqlDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            ajuste = reader.GetInt32(reader.GetOrdinal("weekHours")) / 5;
                }
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "UPDATE incidencia_falta_asistencia SET " +
                    "closed = @CLOSED " +
                    "WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", incidence.id);
                command.Parameters.AddWithValue("@CLOSED", 1);

                command.ExecuteNonQuery();
            }
        }

        public static async Task sendEmailAcceptCandidate(string candidateId, string incidenceId, DateTime date, SqlConnection conn, SqlTransaction transaction)
        {
            try
            {
                string email = null, nombre = null;

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "SELECT email, nombre FROM candidatos WHERE id = @CANDIDATE";
                    command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            email = reader.GetString(reader.GetOrdinal("email"));
                            nombre = reader.GetString(reader.GetOrdinal("nombre"));
                        }
                    }
                }

                if (email != null)
                {
                    EventMailer.SendEmail(new EventMailer.Email()
                    {
                        template = "incidenceNotAttendNotice",
                        inserts = new() { { "date", date.ToShortDateString() }, { "url", AccessController.getAutoLoginUrl("ca", candidateId, null, conn, transaction) } },
                        toEmail = email,
                        toName = nombre,
                        subject = "[Think&Job] Incidencia pendiente de revisión",
                        priority = EventMailer.EmailPriority.MODERATE
                    });
                }

                await PushNotificationController.sendNotification(new() { type = "ca", id = candidateId }, new()
                {
                    title = "Incidencia pendiente de revisión",
                    type = "candidate-incidencia-falta-asistencia",
                    data = new() { { "id", incidenceId } }
                }, conn, transaction);
            }
            catch (Exception)
            {
            }
        }
        public static async Task sendEmailAcceptClient(string incidenceId, DateTime date, SqlConnection conn, SqlTransaction transaction)
        {
            try
            {
                List<ClientUserController.ClientUser> users = getClientUserLinkedToIncidence(incidenceId, conn, transaction);

                //Enviar los correos si hay emails a los que enviar
                if (users.Count > 0)
                {
                    foreach (ClientUserController.ClientUser user in users)
                    {
                        Dictionary<string, string> inserts = new() { { "date", date.ToShortDateString() }, { "url", AccessController.getAutoLoginUrl("cl", user.id, null, conn, transaction) } };
                        EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "incidenceNotAttendNotice",
                            inserts = inserts,
                            toEmail = user.email,
                            subject = "[Think&Job] Incidencia pendiente de revisión",
                            priority = EventMailer.EmailPriority.MODERATE
                        });
                    }

                    await PushNotificationController.sendNotifications(users.Select(user => new PushNotificationController.UID() { type = "cl", id = user.id }), new()
                    {
                        title = "Incidencia pendiente de revisión",
                        type = "client-incidencia-falta-asistencia",
                        data = new() { { "id", incidenceId } }
                    }, conn, transaction);
                }
            }
            catch (Exception)
            {
            }
        }
        public static List<ClientUserController.ClientUser> getClientUserLinkedToIncidence(string incidenceId, SqlConnection conn, SqlTransaction transaction)
        {
            List<ClientUserController.ClientUser> users = new();

            //Buscar eventos de la incidencia cuyo autor sea un usuario CL y tengan email, seran a los que se notifique
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT U.email, U.id " +
                    "FROM incidencia_falta_asistencia_eventos E " +
                    "INNER JOIN client_users U ON(E.author = U.username) " +
                    "WHERE E.incidenceId = @INCIDENCE AND " +
                    "NOT EXISTS(SELECT * FROM client_user_dashboard_restrictions CUDR WHERE CUDR.userId = U.id AND CUDR.type = 'IncidenciaFaltaAsistencia')";
                command.Parameters.AddWithValue("@INCIDENCE", incidenceId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new ClientUserController.ClientUser()
                        {
                            email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                            id = reader.GetString(reader.GetOrdinal("id"))
                        });
                    }
                }
            }

            //Si no hay usuarios implicados, seleccionar todos los que tengan permiso sobre el centro + no tengan prohibido el permiso de incidencias + no tengan desactivados los avisos de incidenciasNotAttend
            if (users.Count == 0)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT U.email, U.id \n" +
                        "FROM client_user_centros CUC \n" +
                        "INNER JOIN client_users U ON(CUC.clientUserId = U.id) \n" +
                        "INNER JOIN incidencia_falta_asistencia I ON(CUC.centroId = I.centroId) \n" +
                        "WHERE I.id = @INCIDENCE AND \n" +
                        "NOT EXISTS(SELECT * FROM client_user_permission_restrictions CUPR WHERE CUPR.userId = U.id AND CUPR.permission = 'Incidencias') AND \n" +
                        "NOT EXISTS(SELECT * FROM client_user_dashboard_restrictions CUDR WHERE CUDR.userId = U.id AND CUDR.type = 'IncidenciaFaltaAsistencia')";
                    command.Parameters.AddWithValue("@INCIDENCE", incidenceId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new ClientUserController.ClientUser()
                            {
                                email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                id = reader.GetString(reader.GetOrdinal("id"))
                            });
                        }
                    }
                }
            }

            return users;
        }

        private static void deleteNotAttendIncidenceDataInChecks(string candidateId, DateTime day, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Obtener la ID del check, si es que hay alguno con datos de incidencia notAttend para ese candidato para ese dia
            string checkId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT id FROM candidate_checks WHERE candidateId = @CANDIDATE AND day = @DAY AND notAttendIncidenceData IS NOT NULL";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                command.Parameters.AddWithValue("@DAY", day.Date);

                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        checkId = reader.GetString(reader.GetOrdinal("id"));
            }
            if (checkId == null) return;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "UPDATE candidate_checks SET notAttendIncidenceData = NULL WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", checkId);

                command.ExecuteNonQuery();
            }

            DeleteDir(new[] { "check", checkId, "notattend" });
        }

        private IActionResult generateExcel(List<NotAttendIncidence> incidences, bool useRealWords = false)
        {
            //Generar el excel
            IWorkbook workbook = new XSSFWorkbook();
            string tmpDir = GetTemporaryDirectory();
            var thicc = BorderStyle.Medium;
            var thin = BorderStyle.Thin;
            var none = BorderStyle.None;
            var bgGreen = new XSSFColor(new byte[] { 226, 239, 218 });
            var bgRed = new XSSFColor(new byte[] { 254, 211, 204 });
            var bgBlue = new XSSFColor(new byte[] { 206, 225, 242 });

            //Fuentes
            IFont fontTitle = workbook.CreateFont();
            fontTitle.FontName = "Century Gothic";
            fontTitle.FontHeightInPoints = 14;
            fontTitle.Color = IndexedColors.Black.Index;

            IFont fontNormal = workbook.CreateFont();
            fontNormal.FontName = "Century Gothic";
            fontNormal.FontHeightInPoints = 10;
            fontNormal.Color = IndexedColors.Black.Index;

            //Formatos
            XSSFCellStyle headerStartStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerStartStyle.SetFont(fontTitle);
            headerStartStyle.FillForegroundColorColor = bgBlue;
            headerStartStyle.FillPattern = FillPattern.SolidForeground;
            headerStartStyle.BorderTop = thicc;
            headerStartStyle.BorderBottom = thicc;
            headerStartStyle.BorderRight = none;
            headerStartStyle.BorderLeft = thicc;
            headerStartStyle.Alignment = HorizontalAlignment.Left;
            headerStartStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle headerEndStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerEndStyle.SetFont(fontTitle);
            headerEndStyle.FillForegroundColorColor = bgBlue;
            headerEndStyle.FillPattern = FillPattern.SolidForeground;
            headerEndStyle.BorderTop = thicc;
            headerEndStyle.BorderBottom = thicc;
            headerEndStyle.BorderRight = thicc;
            headerEndStyle.BorderLeft = none;
            headerEndStyle.Alignment = HorizontalAlignment.Left;
            headerEndStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle headerStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            headerStyle.SetFont(fontTitle);
            headerStyle.FillForegroundColorColor = bgBlue;
            headerStyle.FillPattern = FillPattern.SolidForeground;
            headerStyle.BorderTop = thicc;
            headerStyle.BorderBottom = thicc;
            headerStyle.BorderRight = none;
            headerStyle.BorderLeft = none;
            headerStyle.Alignment = HorizontalAlignment.Left;
            headerStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStartStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStartStyle.SetFont(fontNormal);
            bodyStartStyle.BorderTop = none;
            bodyStartStyle.BorderBottom = thin;
            bodyStartStyle.BorderRight = none;
            bodyStartStyle.BorderLeft = thin;
            bodyStartStyle.Alignment = HorizontalAlignment.Left;
            bodyStartStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyEndStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyEndStyle.SetFont(fontNormal);
            bodyEndStyle.BorderTop = none;
            bodyEndStyle.BorderBottom = thin;
            bodyEndStyle.BorderRight = thin;
            bodyEndStyle.BorderLeft = none;
            bodyEndStyle.Alignment = HorizontalAlignment.Left;
            bodyEndStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStyle.SetFont(fontNormal);
            bodyStyle.BorderTop = none;
            bodyStyle.BorderBottom = thin;
            bodyStyle.BorderRight = none;
            bodyStyle.BorderLeft = none;
            bodyStyle.Alignment = HorizontalAlignment.Left;
            bodyStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStatusGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStatusGreenStyle.SetFont(fontNormal);
            bodyStatusGreenStyle.FillForegroundColorColor = bgGreen;
            bodyStatusGreenStyle.FillPattern = FillPattern.SolidForeground;
            bodyStatusGreenStyle.BorderTop = none;
            bodyStatusGreenStyle.BorderBottom = thin;
            bodyStatusGreenStyle.BorderRight = none;
            bodyStatusGreenStyle.BorderLeft = none;
            bodyStatusGreenStyle.Alignment = HorizontalAlignment.Center;
            bodyStatusGreenStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStatusRedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStatusRedStyle.SetFont(fontNormal);
            bodyStatusRedStyle.FillForegroundColorColor = bgRed;
            bodyStatusRedStyle.FillPattern = FillPattern.SolidForeground;
            bodyStatusRedStyle.BorderTop = none;
            bodyStatusRedStyle.BorderBottom = thin;
            bodyStatusRedStyle.BorderRight = none;
            bodyStatusRedStyle.BorderLeft = none;
            bodyStatusRedStyle.Alignment = HorizontalAlignment.Center;
            bodyStatusRedStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStatusGreenEndStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStatusGreenEndStyle.SetFont(fontNormal);
            bodyStatusGreenEndStyle.FillForegroundColorColor = bgGreen;
            bodyStatusGreenEndStyle.FillPattern = FillPattern.SolidForeground;
            bodyStatusGreenEndStyle.BorderTop = none;
            bodyStatusGreenEndStyle.BorderBottom = thin;
            bodyStatusGreenEndStyle.BorderRight = thin;
            bodyStatusGreenEndStyle.BorderLeft = none;
            bodyStatusGreenEndStyle.Alignment = HorizontalAlignment.Center;
            bodyStatusGreenEndStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStatusRedEndStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStatusRedEndStyle.SetFont(fontNormal);
            bodyStatusRedEndStyle.FillForegroundColorColor = bgRed;
            bodyStatusRedEndStyle.FillPattern = FillPattern.SolidForeground;
            bodyStatusRedEndStyle.BorderTop = none;
            bodyStatusRedEndStyle.BorderBottom = thin;
            bodyStatusRedEndStyle.BorderRight = thin;
            bodyStatusRedEndStyle.BorderLeft = none;
            bodyStatusRedEndStyle.Alignment = HorizontalAlignment.Center;
            bodyStatusRedEndStyle.VerticalAlignment = VerticalAlignment.Center;

            //Hojas
            ISheet sheet = workbook.CreateSheet("Incidencias");

            //Tamaños de filas y columnas
            ICell cell;
            IRow row;
            sheet.SetColumnWidth(1, 11 * 256);
            sheet.SetColumnWidth(2, 11 * 256);
            sheet.SetColumnWidth(3, 40 * 256);
            sheet.SetColumnWidth(4, 20 * 256);
            sheet.SetColumnWidth(5, 45 * 256);
            sheet.SetColumnWidth(6, 25 * 256);
            sheet.SetColumnWidth(7, 25 * 256);
            sheet.SetColumnWidth(8, 40 * 256);
            sheet.SetColumnWidth(9, 10 * 256);

            //Escribir cabecera
            row = sheet.CreateRow(1);
            row.HeightInPoints = 22;
            cell = row.CreateCell(1);
            cell.CellStyle = headerStartStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("NÚMERO");
            cell = row.CreateCell(2);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("DNI");
            cell = row.CreateCell(3);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("NOMBRE");
            cell = row.CreateCell(4);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("ESTADO GLOBAL");
            cell = row.CreateCell(5);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("ESTADO DE TRAMITE");
            cell = row.CreateCell(6);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("FECHA DE CREACIÓN");
            cell = row.CreateCell(7);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("FECHA DE FALTA");
            cell = row.CreateCell(8);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("CAUSA DE LA INCIDENCIA");
            cell = row.CreateCell(9);
            cell.CellStyle = headerEndStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("BAJA");

            //Escribir las incidencias
            int r = 2;
            foreach (NotAttendIncidence incidence in incidences)
            {
                row = sheet.CreateRow(r);

                //Poner los datos de la incidencia
                cell = row.CreateCell(1);
                cell.CellStyle = bodyStartStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue($"#{incidence.number}");
                cell = row.CreateCell(2);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(incidence.candidateDni.ToUpper());
                cell = row.CreateCell(3);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(incidence.candidateName.ToUpper());
                cell = row.CreateCell(4);
                cell.CellStyle = incidence.closed ? bodyStatusGreenStyle : bodyStatusRedStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(incidence.closed ? "Cerrada" : "Abierta");
                cell = row.CreateCell(5);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(useRealWords ? attenuateStateForRRHH(incidence.state) : attenuateStateForClient(incidence.state));
                cell = row.CreateCell(6);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(incidence.creationTime.ToString("dd/MM/yyyy hh:mm"));
                cell = row.CreateCell(7);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(incidence.date.ToString("dd/MM/yyyy"));
                cell = row.CreateCell(8);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(useRealWords ? incidence.category : incidence.altCategory);
                cell = row.CreateCell(9);
                cell.CellStyle = incidence.baja ? bodyStatusRedEndStyle : bodyStatusGreenEndStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(incidence.baja ? "Sí" : "No");

                r++;
            }

            //Filtros
            sheet.SetAutoFilter(new CellRangeAddress(1, r - 1, 1, 9));

            //Guardado
            //sheet.ProtectSheet("1234"); //No protejer porque entonces nos e podria usar el filtro y orden
            string fileName = "Incidencias.xlsx";
            string tmpFile = Path.Combine(tmpDir, fileName);
            FileStream file = new FileStream(tmpFile, FileMode.Create);
            workbook.Write(file);
            file.Close();

            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            HttpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(tmpFile), contentType)
            {
                FileDownloadName = fileName
            };

            Directory.Delete(tmpDir, true);

            return response;
        }



        //------------------------------------------FUNCIONES FIN---------------------------------------------

    }
}
