using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using System.Text.Json;
using ThinkAndJobSolution.Controllers.MainHome.RRHH;
using ThinkAndJobSolution.Utils;
using ThinkAndJobSolution.Controllers.Commons;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers._Helper.Ohers;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    [Route("api/v1/incidence-generic")]
    [ApiController]
    [Authorize]
    public class IncidenceGenericController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpPost]
        [Route(template: "list/{securityToken}")]
        public async Task<IActionResult> List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("GenericIncidence.List", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("title", out JsonElement titleJson) &&
                json.TryGetProperty("category", out JsonElement categoryJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) &&
                json.TryGetProperty("perpage", out JsonElement perpageJson))
            {
                int? number = GetJsonInt(numberJson);
                string candidateKey = candidateKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                string title = titleJson.GetString();
                string category = categoryJson.GetString();
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                int page = Int32.Parse(pageJson.GetString());
                int perpage = Int32.Parse(perpageJson.GetString());
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        result = new
                        {
                            error = false,
                            incidences = listIncidences(conn, candidateId, candidateKey, title, number, category, state, closed, startDate, endDate, null, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn), null, null, null, page, perpage)
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5710, no se han podido listar las incidencias" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list-count/")]
        public async Task<IActionResult> ListCount()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("GenericIncidence.ListCount", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("number", out JsonElement numberJson) &&
                json.TryGetProperty("candidateKey", out JsonElement candidateKeyJson) &&
                json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson) &&
                json.TryGetProperty("title", out JsonElement titleJson) &&
                json.TryGetProperty("category", out JsonElement categoryJson) &&
                json.TryGetProperty("startDate", out JsonElement startDateJson) &&
                json.TryGetProperty("endDate", out JsonElement endDateJson))
            {
                int? number = GetJsonInt(numberJson);
                string candidateKey = candidateKeyJson.GetString();
                string candidateId = candidateIdJson.GetString();
                string state = stateJson.GetString();
                bool? closed = GetJsonBool(closedJson);
                string title = titleJson.GetString();
                string category = categoryJson.GetString();
                DateTime? startDate = GetJsonDate(startDateJson);
                DateTime? endDate = GetJsonDate(endDateJson);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        result = countIncidences(conn, candidateId, candidateKey, title, number, category, state, closed, startDate, endDate, null, null, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn), null, null, null);
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5710, no se han podido contar las incidencias" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "count-new-user/")]
        public IActionResult CountNewUser()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("GenericIncidence.CountNewUser", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = countIncidences(conn, null, null, null, null, null, null, null, null, null, true, null, null, GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn), null, null, null);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido contar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-candidate/{candidateId}")]
        public IActionResult ListForCandidate(string candidateId)
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
                        incidences = listIncidences(conn, candidateId, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 0, 20)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido listar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "count-new-candidate/{candidateId}")]
        public IActionResult CountNewCandidate(string candidateId)
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
                    result = countIncidences(conn, candidateId, null, null, null, null, null, null, null, null, null, true, null, null, null, null, null);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido contar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-client/{clientToken}")]
        public IActionResult ListForClient(string clientToken)
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
                        incidences = listIncidences(conn, null, null, null, null, null, null, null, null, null, null, null, null, clientToken, null, null, 0, 1000)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido listar las incidencias" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "api/v1/incidence-generic/count-new-client/{clientToken}")]
        public IActionResult CountNewClient(string clientToken)
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
                    result = countIncidences(conn, null, null, null, null, null, null, null, null, null, null, true, null, null, clientToken, null, null);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido contar las incidencias" };
                }
            }
            return Ok(result);
        }

        //Obtencion
        [HttpGet]
        [Route(template: "{incidenceId}/{type}")]
        public IActionResult Get(string incidenceId, string type)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                bool failed = false;

                try
                {
                    GenericIncidence? incidence = getIncidence(incidenceId, conn);

                    if (incidence == null)
                    {
                        failed = true;
                        result = new { error = "Error 4591, incidencia no encontrada" };
                    }

                    if (!failed)
                    {
                        string paramToSet = type == "ca" ? "hasCandidateUnread" : (type == "cl" ? "hasClientUnread" : "hasUserUnread");
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = $"UPDATE incidencia_general SET {paramToSet} = 0 WHERE id = @INCIDENCE_ID";
                            command.Parameters.AddWithValue("@INCIDENCE_ID", incidenceId);
                            command.ExecuteNonQuery();
                        }
                    }

                    //Quitar el put
                    if (incidence.Value.put != null)
                    {
                        GenericIncidence copyIncidence = incidence.Value;
                        copyIncidence.put = "true";
                        incidence = copyIncidence;
                    }

                    if (!failed)
                    {
                        result = new
                        {
                            error = false,
                            incidence
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido obtener la incidencia" };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "download-put/{incidenceId}")]
        public IActionResult DownloadPut(string incidenceId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                bool failed = false;
                try
                {
                    GenericIncidence? incidence = getIncidence(incidenceId, conn);
                    if (incidence == null)
                    {
                        failed = true;
                        result = new { error = "Error 4591, incidencia no encontrada" };
                    }
                    if (incidence.Value.put == null)
                    {
                        failed = true;
                        result = new { error = "Error 4592, esta incidencia no contiene una actualización guardada" };
                    }
                    if (!failed)
                    {
                        result = new
                        {
                            error = false,
                            put = incidence.Value.put
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido obtener la incidencia" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-doc/{docRef}")]
        public IActionResult DownloadReferencedDoc(string docRef)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                bool failed = false;
                try
                {
                    string doc = getCandidateDocByRef(docRef, conn);
                    if (doc == null)
                    {
                        failed = true;
                        result = new { error = "Error 4591, documento no encontrado" };
                    }
                    if (!failed)
                    {
                        result = new
                        {
                            error = false,
                            doc
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido obtener el documento" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-id-by-number/{number}")]
        public IActionResult GetIdByNumber(int number)
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
                    string id = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id FROM incidencia_general WHERE number = @NUMBER";
                        command.Parameters.AddWithValue("@NUMBER", number);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                id = reader.GetString(reader.GetOrdinal("id"));
                    }
                    if (id == null)
                        result = new { error = "Error 4711, incidencia no encontrada" };
                    else
                        result = new { error = false, id };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5711, no se han podido obtener la incidencia" };
                }
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
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("GenericIncidence.Create", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson) && json.TryGetProperty("title", out JsonElement titleJson) &&
                json.TryGetProperty("description", out JsonElement descriptionJson) && json.TryGetProperty("category", out JsonElement categoryJson) &&
                json.TryGetProperty("put", out JsonElement putJson) && json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string candidateId = candidateIdJson.GetString();
                string title = titleJson.GetString();
                string description = descriptionJson.GetString();
                string category = categoryJson.GetString();
                string put = putJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        string id = ComputeStringHash(candidateId + title + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO incidencia_general (id, candidateId, title, description, category, state, hasUserUnread, hasCandidateUnread) VALUES (@ID, @CANDIDATE_ID, @TITLE, @DESCRIPTION, @CATEGORY, 'abierta', 0, 1)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@TITLE", title);
                            command.Parameters.AddWithValue("@DESCRIPTION", description);
                            command.Parameters.AddWithValue("@CATEGORY", category);
                            command.ExecuteNonQuery();

                            if (put != null) SaveFile(new[] { "candidate", candidateId, "incidence-generic", id, "put" }, put);
                            SaveFileList(new[] { "candidate", candidateId, "incidence-generic", id }, attachments, "attachment_");
                        }
                        await notifyCandidate(candidateId, id, conn);
                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5711, no se han podido insertar la incidencia" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "create-for-candidate")]
        public async Task<IActionResult> CreateForCandidate()
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson) && json.TryGetProperty("title", out JsonElement titleJson) &&
                json.TryGetProperty("description", out JsonElement descriptionJson) && json.TryGetProperty("category", out JsonElement categoryJson) &&
                json.TryGetProperty("put", out JsonElement putJson) && json.TryGetProperty("attachments", out JsonElement attachmentsJson) &&
                json.TryGetProperty("candidateDocRef", out JsonElement candidateDocRefJson))
            {
                string candidateId = candidateIdJson.GetString();
                string title = titleJson.GetString();
                string description = descriptionJson.GetString();
                string category = categoryJson.GetString();
                string put = putJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);
                List<string> extras;
                if (!json.TryGetStringList("extras", out extras))
                    extras = null;
                string candidateDocRef = candidateDocRefJson.GetString();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        string id = ComputeStringHash(candidateId + title + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                        int number;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "INSERT INTO incidencia_general " +
                                "(id, candidateId, title, description, category, state, hasUserUnread, hasClientUnread, extras) OUTPUT inserted.number VALUES " +
                                "(@ID, @CANDIDATE_ID, @TITLE, @DESCRIPTION, @CATEGORY, 'abierta', 1, 1, @EXTRAS)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@TITLE", title);
                            command.Parameters.AddWithValue("@DESCRIPTION", description);
                            command.Parameters.AddWithValue("@CATEGORY", category);
                            command.Parameters.AddWithValue("@EXTRAS", extras == null ? DBNull.Value : JsonSerializer.Serialize(extras));
                            number = (int)command.ExecuteScalar();
                            if (put != null) SaveFile(new[] { "candidate", candidateId, "incidence-generic", id, "put" }, put);
                            SaveFileList(new[] { "candidate", candidateId, "incidence-generic", id }, attachments, "attachment_");
                        }
                        if (candidateDocRef != null)
                            setCandidateDocIncidenceByRef(candidateDocRef, number, conn);

                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5711, no se han podido insertar la incidencia" };
                    }
                }
            }
            return Ok(result);
        }

        //Actualizacion
        [HttpPatch]
        [Route(template: "update-state/{incidenceId}/")]
        public async Task<IActionResult> UpdateState(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("GenericIncidence.UpdateState", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("closed", out JsonElement closedJson))
            {
                string state = stateJson.GetString();
                bool closed = closedJson.GetBoolean();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            GenericIncidence? incidence = getIncidence(incidenceId, conn, transaction);
                            if (incidence == null)
                                return Ok(new { error = "Error 4851, incidencia no encontrada" });
                            if (state == "aceptada")
                                closed = true;
                            GenericIncidence updatedIncidence = incidence.Value;
                            updatedIncidence.state = state;
                            updatedIncidence.closed = closed;
                            updatedIncidence.hasCandidateUnread = true;
                            updatedIncidence.hasClientUnread = true;
                            await updateIncidenceState(updatedIncidence, conn, transaction);

                            if (updatedIncidence.state == "rechazada" && updatedIncidence.candidateDocNeedsSign && updatedIncidence.candidateDocRef != null)
                            {
                                setCandidateDocCanSign(updatedIncidence.candidateDocRef, true, conn, transaction);
                                EventMailer.SendEmail(new EventMailer.Email()
                                {
                                    template = "candidateRequiredDoc",
                                    inserts = new() { { "url", AccessController.getAutoLoginUrl("ca", updatedIncidence.candidateId, null, conn, null) }, { "sign_insert", ", que requiere firma" } },
                                    toEmail = updatedIncidence.candidateEmail,
                                    toName = updatedIncidence.candidateName,
                                    subject = "[Think&Job] Documento requiere firma",
                                    priority = EventMailer.EmailPriority.MODERATE
                                });
                                await PushNotificationController.sendNotification(new() { type = "ca", id = updatedIncidence.candidateId }, new()
                                {
                                    title = "Documento pendiente de firma",
                                    type = "candidate-documento-requerido"
                                }, conn, transaction);
                            }

                            transaction.Commit();
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5712, no se han podido actualizar la incidencia" };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "close-for-client/{incidenceId}/")]
        public async Task<IActionResult> CloseForClient(string incidenceId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("accepted", out JsonElement acceptedJson))
            {
                bool accepted = GetJsonBool(acceptedJson) ?? false;
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            GenericIncidence? incidence = getIncidence(incidenceId, conn, transaction);
                            if (incidence == null)
                                return Ok(new { error = "Error 4851, incidencia no encontrada" });

                            //Comprobar que tiene permiso para esta incidencia
                            string centroId = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT centroId FROM candidatos WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", incidence.Value.candidateId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                    if (reader.Read())
                                        centroId = reader.GetString(reader.GetOrdinal("centroId"));
                            }
                            string author = ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_INCIDENCIAS, conn, transaction);
                            if (author == null)
                            {
                                return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
                            }
                            if (incidence.Value.closed)
                            {
                                return Ok(new{error = "Error 4712, la incidencia está cerrada."});
                            }
                            GenericIncidence updatedIncidence = incidence.Value;
                            updatedIncidence.state = accepted ? "aceptada" : "rechazada";
                            updatedIncidence.closed = true;
                            updatedIncidence.hasUserUnread = true;
                            updatedIncidence.hasCandidateUnread = true;
                            await updateIncidenceState(updatedIncidence, conn, transaction);

                            transaction.Commit();
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5712, no se han podido actualizar la incidencia" };
                        }
                    }
                }
            }
            return Ok(result);
        }

        //Mensajes
        [HttpPost]
        [Route(template: "message/{incidenceId}/")]
        public async Task<IActionResult> PostMessage(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("GenericIncidence.PostMessage", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("text", out JsonElement textJson) && json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string text = textJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    string username = FindUsernameBySecurityToken(securityToken, conn);

                    try
                    {
                        GenericIncidence? incidence = getIncidence(incidenceId, conn);
                        if (incidence == null)
                            return Ok(new { error = "Error 4851, incidencia no encontrada" });
                        string id = ComputeStringHash(text + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO incidencia_general_mensajes (id, incidenceId, username, text) VALUES (@ID, @INCIDENCE_ID, @USERNAME, @TEXT)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@INCIDENCE_ID", incidenceId);
                            command.Parameters.AddWithValue("@USERNAME", username);
                            command.Parameters.AddWithValue("@TEXT", text);
                            command.ExecuteNonQuery();

                            SaveFileList(new[] { "candidate", incidence.Value.candidateId, "incidence-generic", incidenceId, "message", id }, attachments, "attachment_");
                        }
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE incidencia_general SET hasCandidateUnread = 1 WHERE id = @INCIDENCE_ID";
                            command.Parameters.AddWithValue("@INCIDENCE_ID", incidenceId);
                            command.ExecuteNonQuery();
                        }
                        await notifyCandidate(incidence.Value.candidateId, incidenceId, conn);
                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5711, no se han podido insertar el mensaje" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "api/v1/incidence-generic/message/{incidenceId}")]
        public async Task<IActionResult> PostMessageForCandidate(string incidenceId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("text", out JsonElement textJson) && json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string text = textJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        GenericIncidence? incidence = getIncidence(incidenceId, conn);

                        if (incidence == null)
                            return Ok(new { error = "Error 4851, incidencia no encontrada" });

                        if (incidence.Value.closed || incidence.Value.state != "requiere-participacion")
                            return Ok(new { error = "Error 4852, incidencia no modificable" });

                        string id = ComputeStringHash(text + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO incidencia_general_mensajes (id, incidenceId, username, text) VALUES (@ID, @INCIDENCE_ID, @USERNAME, @TEXT)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@INCIDENCE_ID", incidenceId);
                            command.Parameters.AddWithValue("@USERNAME", DBNull.Value);
                            command.Parameters.AddWithValue("@TEXT", text);
                            command.ExecuteNonQuery();

                            SaveFileList(new[] { "candidate", incidence.Value.candidateId, "incidence-generic", incidenceId, "message", id }, attachments, "attachment_");
                        }
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE incidencia_general SET hasUserUnread = 1, hasClientUnread = 1, state = @STATE WHERE id = @INCIDENCE_ID";
                            command.Parameters.AddWithValue("@INCIDENCE_ID", incidenceId);
                            command.Parameters.AddWithValue("@STATE", "en-tramite");
                            command.ExecuteNonQuery();
                        }
                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5711, no se han podido insertar el mensaje" };
                    }
                }
            }
            return Ok(result);
        }


        [HttpPost]
        [Route(template: "api/v1/incidence-generic/message-for-client/{incidenceId}/")]
        public async Task<IActionResult> PostMessageForClient(string incidenceId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("text", out JsonElement textJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson))
            {
                string text = textJson.GetString();
                List<string> attachments = GetJsonStringList(attachmentsJson);

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        GenericIncidence? incidence = getIncidence(incidenceId, conn);

                        if (incidence == null)
                            return Ok(new { error = "Error 4851, incidencia no encontrada" });

                        //Comprobar que tiene permiso para esta incidencia
                        string centroId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT centroId FROM candidatos WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", incidence.Value.candidateId);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    centroId = reader.GetString(reader.GetOrdinal("centroId"));
                        }
                        string author = ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_INCIDENCIAS, conn);
                        if (author == null)
                        {
                            return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
                        }
                        if (incidence.Value.closed)
                        {
                            return Ok(new{error = "Error 4712, la incidencia está cerrada."});
                        }

                        string id = ComputeStringHash(text + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO incidencia_general_mensajes (id, incidenceId, username, text) VALUES (@ID, @INCIDENCE_ID, @USERNAME, @TEXT)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@INCIDENCE_ID", incidenceId);
                            command.Parameters.AddWithValue("@USERNAME", author);
                            command.Parameters.AddWithValue("@TEXT", text);
                            command.ExecuteNonQuery();

                            SaveFileList(new[] { "candidate", incidence.Value.candidateId, "incidence-generic", incidenceId, "message", id }, attachments, "attachment_");
                        }
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE incidencia_general SET hasCandidateUnread = 1, state = 'requiere-participacion' WHERE id = @INCIDENCE_ID";
                            command.Parameters.AddWithValue("@INCIDENCE_ID", incidenceId);
                            command.ExecuteNonQuery();
                        }

                        await notifyCandidate(incidence.Value.candidateId, incidenceId, conn);

                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5711, no se han podido insertar el mensaje" };
                    }
                }
            }
            return Ok(result);
        }



        //Eliminacion

        [HttpDelete]
        [Route(template: "{incidenceId}/")]
        public IActionResult Delete(string incidenceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("GenericIncidence.Delete", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transacition = conn.BeginTransaction())
                {
                    try
                    {
                        string candidateId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transacition;
                            command.CommandText = "SELECT candidateId FROM incidencia_general WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", incidenceId);
                            using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                        };
                        if (candidateId == null)
                        {
                            result = new { error = "Error 4712, incidencia no encontrada" };
                        }
                        else
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transacition;
                                command.CommandText = "DELETE FROM incidencia_general_mensajes WHERE incidenceId = @ID";
                                command.Parameters.AddWithValue("@ID", incidenceId);
                                command.ExecuteNonQuery();
                            };

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transacition;
                                command.CommandText = "DELETE FROM incidencia_general WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", incidenceId);
                                command.ExecuteNonQuery();

                                DeleteDir(new[] { "candidate", candidateId, "incidence-generic", incidenceId });
                            };
                            LogToDB(LogType.DELETION, "Incidencia de candidato eliminada", FindUsernameBySecurityToken(securityToken), conn, transacition);
                            transacition.Commit();
                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        transacition.Rollback();
                        result = new { error = "Error 5713, no se han podido eliminar la incidencia" };
                    }
                }
            }
            return Ok(result);
        }



        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        #region "Clases"
        public struct GenericIncidence
        {
            public string id { get; set; }
            public int number { get; set; }
            public string candidateDni { get; set; }
            public string candidateName { get; set; }
            public string candidateEmail { get; set; }
            public string candidateId { get; set; }
            public string companyCif { get; set; }
            public string companyName { get; set; }
            public string centroAlias { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public string category { get; set; } // Actualizar datos vitales | Caducidad del permiso de trabajo | Apelación de extras | Otros
            public string state { get; set; } // abierta, en-tramite, requiere-participacion, aceptada, rechazada
            public DateTime creationTime { get; set; }
            public string put { get; set; }
            public bool closed { get; set; }
            public List<string> attachments { get; set; }
            public List<GenericIncidenceMessage> messages { get; set; }
            public bool hasUserUnread { get; set; }
            public bool hasCandidateUnread { get; set; }
            public bool hasClientUnread { get; set; }
            public List<string> extras { get; set; }
            public string candidateDocRef { get; set; }
            public bool candidateDocNeedsSign { get; set; }
        }

        public struct GenericIncidenceMessage
        {
            public string id { get; set; }
            public string username { get; set; }
            public string text { get; set; }
            public DateTime date { get; set; }
            public List<string> attachments { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static List<GenericIncidence> listIncidences(SqlConnection conn, string candidateId, string candidateKey, string title, int? number, string category, string state, bool? closed, DateTime? startDate, DateTime? endDate, bool? hasUserUnread, bool? hasCandidateUnread, string securityToken, string clientToken, string centroId, string companyId, int page, int perpage)
        {
            List<GenericIncidence> incidences = new List<GenericIncidence>();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT I.id, I.number, I.candidateId, I.title, I.category, I.state, I.creationTime, I.closed, I.hasCandidateUnread, I.hasClientUnread, " +
                    "hasUserUnreadCalc = CASE WHEN I.hasUserUnread = 1 OR I.state IN ('abierta', 'en-tramite') THEN 1 ELSE 0 END, " +
                    "C.dni as candidateDni, " +
                    "NULLIF(TRIM(CONCAT(C.nombre, ' ', C.apellidos)), '') as candidateName " +
                    "FROM incidencia_general I " +
                    "INNER JOIN candidatos C ON(I.candidateId = C.id) " +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "WHERE " +
                    "(@CANDIDATE IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @CANDIDATE OR C.dni LIKE @CANDIDATE) AND " +
                    "(@CANDIDATE_ID IS NULL OR C.id = @CANDIDATE_ID) AND " +
                    "(@TITLE IS NULL OR I.title LIKE @TITLE) AND " +
                    "(@NUMBER IS NULL OR I.number = @NUMBER) AND " +
                    "(@CATEGORY IS NULL OR I.category = @CATEGORY) AND " +
                    "(@STATE IS NULL OR I.state = @STATE) AND " +
                    "(@CLOSED IS NULL OR I.closed = @CLOSED) AND " +
                    "(@START_DATE IS NULL OR I.creationTime >= @START_DATE) AND " +
                    "(@END_DATE IS NULL OR I.creationTime <= @END_DATE) AND " +
                    "(@USER_UNREAD IS NULL OR ((@USER_UNREAD = 0 AND I.hasUserUnread = 0 AND I.state NOT IN ('abierta', 'en-tramite')) OR (@USER_UNREAD = 1 AND (I.hasUserUnread = 1 OR I.state IN ('abierta', 'en-tramite'))))) AND " +
                    "(@CANDIDATE_UNREAD IS NULL OR @CANDIDATE_UNREAD = I.hasCandidateUnread) AND " +
                    "((@TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) INNER JOIN empresas E ON(AUE.companyId = E.id) INNER JOIN centros CE ON(E.id = CE.companyId) INNER JOIN trabajos T ON(CE.id = T.centroId) WHERE U.securityToken = @TOKEN AND T.signLink = C.lastSignLink))) AND " +
                    "(@CLIENT IS NULL OR (EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE CUC.centroId = C.centroId AND U.token = @CLIENT) AND (I.category = 'Apelación de extras'))) AND " +
                    "(@CENTRO IS NULL OR @CENTRO = CE.id) AND " +
                    "(@COMPANY IS NULL OR @COMPANY = CE.companyId) " +
                    "ORDER BY I.creationTime DESC " +
                    "OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY";
                command.Parameters.AddWithValue("@CANDIDATE", candidateKey != null ? ("%" + candidateKey + "%") : DBNull.Value);
                command.Parameters.AddWithValue("@CANDIDATE_ID", (object)candidateId ?? DBNull.Value);
                command.Parameters.AddWithValue("@TITLE", title != null ? ("%" + title + "%") : DBNull.Value);
                command.Parameters.AddWithValue("@NUMBER", (object)number ?? DBNull.Value);
                command.Parameters.AddWithValue("@CATEGORY", (object)category ?? DBNull.Value);
                command.Parameters.AddWithValue("@STATE", (object)state ?? DBNull.Value);
                command.Parameters.AddWithValue("@CLOSED", closed == null ? DBNull.Value : (closed.Value ? 1 : 0));
                command.Parameters.AddWithValue("@START_DATE", (object)startDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@END_DATE", (object)endDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@USER_UNREAD", hasUserUnread == null ? DBNull.Value : (hasUserUnread.Value ? 1 : 0));
                command.Parameters.AddWithValue("@CANDIDATE_UNREAD", hasCandidateUnread == null ? DBNull.Value : (hasCandidateUnread.Value ? 1 : 0));
                command.Parameters.AddWithValue("@TOKEN", (object)securityToken ?? DBNull.Value);
                command.Parameters.AddWithValue("@CLIENT", (object)clientToken ?? DBNull.Value);
                command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);
                command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);
                command.Parameters.AddWithValue("@OFFSET", page * perpage);
                command.Parameters.AddWithValue("@LIMIT", perpage);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        incidences.Add(new GenericIncidence()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            number = reader.GetInt32(reader.GetOrdinal("number")),
                            candidateName = reader.GetString(reader.GetOrdinal("candidateName")),
                            candidateDni = reader.GetString(reader.GetOrdinal("candidateDni")),
                            candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            title = reader.GetString(reader.GetOrdinal("title")),
                            category = reader.GetString(reader.GetOrdinal("category")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            hasUserUnread = reader.GetInt32(reader.GetOrdinal("hasUserUnreadCalc")) == 1,
                            hasCandidateUnread = reader.GetInt32(reader.GetOrdinal("hasCandidateUnread")) == 1,
                            hasClientUnread = reader.GetInt32(reader.GetOrdinal("hasClientUnread")) == 1
                        });
                    }
                }
            }

            return incidences;
        }
        public static int countIncidences(SqlConnection conn, string candidateId, string candidateKey, string title, int? number, string category, string state, bool? closed, DateTime? startDate, DateTime? endDate, bool? hasUserUnread, bool? hasCandidateUnread, bool? hasClientUnread, string securityToken, string clientToken, string centroId, string companyId)
        {
            int n;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) " +
                    "FROM incidencia_general I " +
                    "INNER JOIN candidatos C ON(I.candidateId = C.id) " +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "WHERE " +
                    "(@CANDIDATE IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @CANDIDATE OR C.dni LIKE @CANDIDATE) AND " +
                    "(@CANDIDATE_ID IS NULL OR C.id = @CANDIDATE_ID) AND " +
                    "(@TITLE IS NULL OR I.title LIKE @TITLE) AND " +
                    "(@NUMBER IS NULL OR I.number = @NUMBER) AND " +
                    "(@CATEGORY IS NULL OR I.category = @CATEGORY) AND " +
                    "(@STATE IS NULL OR I.state = @STATE) AND " +
                    "(@CLOSED IS NULL OR I.closed = @CLOSED) AND " +
                    "(@START_DATE IS NULL OR I.creationTime >= @START_DATE) AND " +
                    "(@END_DATE IS NULL OR I.creationTime <= @END_DATE) AND " +
                    "(@USER_UNREAD IS NULL OR ((@USER_UNREAD = 0 AND I.hasUserUnread = 0 AND I.state NOT IN ('abierta', 'en-tramite')) OR (@USER_UNREAD = 1 AND (I.hasUserUnread = 1 OR I.state IN ('abierta', 'en-tramite'))))) AND " +
                    "(@CANDIDATE_UNREAD IS NULL OR @CANDIDATE_UNREAD = I.hasCandidateUnread) AND " +
                    "(@CLIENT_UNREAD IS NULL OR @CLIENT_UNREAD = I.hasClientUnread) AND " +
                    "((@TOKEN IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) INNER JOIN empresas E ON(AUE.companyId = E.id) INNER JOIN centros CE ON(E.id = CE.companyId) INNER JOIN trabajos T ON(CE.id = T.centroId) WHERE U.securityToken = @TOKEN AND T.signLink = C.lastSignLink))) AND " +
                    "(@CLIENT IS NULL OR (EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE CUC.centroId = C.centroId AND U.token = @CLIENT) AND (I.category = 'Apelación de extras'))) AND " +
                    "(@CENTRO IS NULL OR @CENTRO = CE.id) AND " +
                    "(@COMPANY IS NULL OR @COMPANY = CE.companyId) ";
                command.Parameters.AddWithValue("@CANDIDATE", candidateKey != null ? ("%" + candidateKey + "%") : DBNull.Value);
                command.Parameters.AddWithValue("@CANDIDATE_ID", (object)candidateId ?? DBNull.Value);
                command.Parameters.AddWithValue("@TITLE", title != null ? ("%" + title + "%") : DBNull.Value);
                command.Parameters.AddWithValue("@NUMBER", (object)number ?? DBNull.Value);
                command.Parameters.AddWithValue("@CATEGORY", (object)category ?? DBNull.Value);
                command.Parameters.AddWithValue("@STATE", (object)state ?? DBNull.Value);
                command.Parameters.AddWithValue("@CLOSED", closed == null ? DBNull.Value : (closed.Value ? 1 : 0));
                command.Parameters.AddWithValue("@START_DATE", (object)startDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@END_DATE", (object)endDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@USER_UNREAD", hasUserUnread == null ? DBNull.Value : (hasUserUnread.Value ? 1 : 0));
                command.Parameters.AddWithValue("@CANDIDATE_UNREAD", hasCandidateUnread == null ? DBNull.Value : (hasCandidateUnread.Value ? 1 : 0));
                command.Parameters.AddWithValue("@CLIENT_UNREAD", hasClientUnread == null ? DBNull.Value : (hasClientUnread.Value ? 1 : 0));
                command.Parameters.AddWithValue("@TOKEN", (object)securityToken ?? DBNull.Value);
                command.Parameters.AddWithValue("@CLIENT", (object)clientToken ?? DBNull.Value);
                command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);
                command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);

                n = (int)command.ExecuteScalar();
            }

            return n;
        }
        private static GenericIncidence? getIncidence(string incidenceId, SqlConnection conn, SqlTransaction transaction = null)
        {
            GenericIncidence incidence;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT I.id, I.number, I.candidateId, I.title, I.description, I.category, I.state, I.creationTime, I.closed, I.extras, C.dni as candidateDni, CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.email as candidateEmail, " +
                    "CE.alias as centroAlias, E.cif as companyCif, E.nombre as companyName " +
                    "FROM incidencia_general I " +
                    "INNER JOIN candidatos C ON(I.candidateId = C.id) " +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE I.id = @ID";
                command.Parameters.AddWithValue("@ID", incidenceId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        incidence = new GenericIncidence()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            number = reader.GetInt32(reader.GetOrdinal("number")),
                            candidateDni = reader.GetString(reader.GetOrdinal("candidateDni")),
                            candidateName = reader.GetString(reader.GetOrdinal("candidateName")),
                            candidateEmail = reader.GetString(reader.GetOrdinal("candidateEmail")),
                            candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            companyCif = reader.GetString(reader.GetOrdinal("companyCif")),
                            companyName = reader.GetString(reader.GetOrdinal("companyName")),
                            centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                            title = reader.GetString(reader.GetOrdinal("title")),
                            description = reader.GetString(reader.GetOrdinal("description")),
                            category = reader.GetString(reader.GetOrdinal("category")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            messages = new()
                        };
                        incidence.put = ReadFile(new[] { "candidate", incidence.candidateId, "incidence-generic", incidence.id, "put" });
                        incidence.attachments = ReadFileList(new[] { "candidate", incidence.candidateId, "incidence-generic", incidence.id }, "attachment_");

                        JsonElement? extras = reader.IsDBNull(reader.GetOrdinal("extras")) ? null : JsonDocument.Parse(reader.GetString(reader.GetOrdinal("extras"))).RootElement;
                        if (extras != null && extras.Value.ValueKind == JsonValueKind.Array)
                        {
                            incidence.extras = new();
                            foreach (JsonElement extra in extras.Value.EnumerateArray())
                                incidence.extras.Add(extra.GetString());
                        }

                    }
                    else
                    {
                        return null;
                    }
                }
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT M.id, M.username, M.text, M.date " +
                                      "FROM incidencia_general_mensajes M WHERE M.incidenceId = @ID " +
                                      "ORDER BY M.date DESC";
                command.Parameters.AddWithValue("@ID", incidenceId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        GenericIncidenceMessage message = new GenericIncidenceMessage()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            username = reader.IsDBNull(reader.GetOrdinal("username"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("username")),
                            text = reader.GetString(reader.GetOrdinal("text")),
                            date = reader.GetDateTime(reader.GetOrdinal("date"))
                        };
                        message.attachments = ReadFileList(new[] { "candidate", incidence.candidateId, "incidence-generic", incidence.id, "message", message.id }, "attachment_");
                        incidence.messages.Add(message);
                    }
                }
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "(SELECT ref = CONCAT('doc-', id), needsSign " +
                    "FROM candidate_docs WHERE incidenceNumber = @NUMBER) " +
                    "UNION" +
                    "(SELECT ref = CONCAT('template-', CDTA.id), needsSign = CASE WHEN CDT.signPlacement IS NULL THEN 0 ELSE 1 END " +
                    "FROM candidate_doc_template_asignation CDTA " +
                    "INNER JOIN candidate_doc_template_batch CDTB ON(CDTA.batchId = CDTB.id) " +
                    "INNER JOIN candidate_doc_template CDT ON(CDTB.templateId = CDT.id) " +
                    "WHERE CDTA.incidenceNumber = @NUMBER)";
                command.Parameters.AddWithValue("@NUMBER", incidence.number);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        incidence.candidateDocRef = reader.GetString(reader.GetOrdinal("ref"));
                        incidence.candidateDocNeedsSign = reader.GetInt32(reader.GetOrdinal("needsSign")) == 1;
                    }
                }
            }

            return incidence;
        }
        private static async Task updateIncidenceState(GenericIncidence incidence, SqlConnection conn, SqlTransaction transaction)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "UPDATE incidencia_general SET state = @STATE, closed = @CLOSED, hasUserUnread = @UNREAD_USER, hasCandidateUnread = @UNREAD_CANDIDATE, hasClientUnread = @UNREAD_CLIENT WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", incidence.id);
                command.Parameters.AddWithValue("@STATE", incidence.state);
                command.Parameters.AddWithValue("@CLOSED", incidence.closed ? 1 : 0);
                command.Parameters.AddWithValue("@UNREAD_USER", incidence.hasClientUnread ? 1 : 0);
                command.Parameters.AddWithValue("@UNREAD_CANDIDATE", incidence.hasCandidateUnread ? 1 : 0);
                command.Parameters.AddWithValue("@UNREAD_CLIENT", incidence.hasClientUnread ? 1 : 0);
                command.ExecuteNonQuery();
            };

            //Si esta aceptada y cerrada, hay que procesarla
            if (incidence.state == "aceptada" && incidence.closed)
            {
                //Si tiene extras, hay que borrarlos
                if (incidence.extras != null)
                {
                    //Clasificar las ids entre extras y vacaciones del calendario
                    List<string> extrasIds = new();
                    List<Tuple<DateTime, DateTime>> vacacionesCalendario = new();
                    foreach (string id in incidence.extras)
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

                    //Borrar los extras incluidos
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText =
                            "DELETE FROM incidencia_horas_extra_entrada WHERE id = @ID";
                        command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);

                        foreach (string id in extrasIds)
                        {
                            command.Parameters["@ID"].Value = id;
                            command.ExecuteNonQuery();
                        }
                    }

                    //Borrar las vacaciones
                    CandidateVacacionesController.unsetVacacionesReyected(incidence.candidateId, vacacionesCalendario, conn, transaction);
                }
            }

            if (incidence.hasCandidateUnread)
            {
                await notifyCandidate(incidence.candidateId, incidence.id, conn, transaction);
            }
        }

        private static async Task notifyCandidate(string candidateId, string incidenceId, SqlConnection conn, SqlTransaction transaction = null)
        {
            await PushNotificationController.sendNotification(new() { type = "ca", id = candidateId }, new()
            {
                title = "Incidencia pendiente de revisión",
                type = "candidate-incidencia-generica",
                data = new() { { "id", incidenceId } }
            }, conn, transaction);
        }
        private static string getCandidateDocByRef(string candidateDocRef, SqlConnection conn, SqlTransaction transaction = null)
        {
            string doc = null;
            if (candidateDocRef.StartsWith("doc-"))
            {
                string docId = candidateDocRef.Substring(4);
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT candidateId FROM candidate_docs WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", docId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                            doc = ReadFile(new[] { "candidate", candidateId, "docs", docId, "doc" });
                        }
                    }
                }
            }
            else if (candidateDocRef.StartsWith("template-"))
            {
                string assignationId = candidateDocRef.Substring(9);
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "SELECT CDT.id " +
                        "FROM candidate_doc_template CDT " +
                        "INNER JOIN candidate_doc_template_batch CDTB ON(CDT.id = CDTB.templateId) " +
                        "INNER JOIN candidate_doc_template_asignation CDTA ON(CDTB.id = CDTA.batchId) " +
                        "WHERE CDTA.id = @ID";
                    command.Parameters.AddWithValue("@ID", assignationId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string templateId = reader.GetString(reader.GetOrdinal("id"));
                            doc = ReadFile(new[] { "candidate_doc_template", templateId });
                        }
                    }
                }
            }
            return doc;
        }
        private static void setCandidateDocIncidenceByRef(string candidateDocRef, int incidenceNumber, SqlConnection conn, SqlTransaction transaction = null)
        {
            if (candidateDocRef.StartsWith("doc-"))
            {
                string docId = candidateDocRef.Substring(4);
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "UPDATE candidate_docs SET incidenceNumber = @NUMBER, canSign = CAST(0 as bit) WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", docId);
                    command.Parameters.AddWithValue("@NUMBER", incidenceNumber);
                    command.ExecuteNonQuery();
                }
            }
            else if (candidateDocRef.StartsWith("template-"))
            {
                string assignationId = candidateDocRef.Substring(9);
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "UPDATE candidate_doc_template_asignation SET incidenceNumber = @NUMBER, canSign = CAST(0 as bit) WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", assignationId);
                    command.Parameters.AddWithValue("@NUMBER", incidenceNumber);
                    command.ExecuteNonQuery();
                }
            }
        }
        private static void setCandidateDocCanSign(string candidateDocRef, bool canSign, SqlConnection conn, SqlTransaction transaction = null)
        {
            if (candidateDocRef.StartsWith("doc-"))
            {
                string docId = candidateDocRef.Substring(4);
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "UPDATE candidate_docs SET canSign = @SIGN WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", docId);
                    command.Parameters.AddWithValue("@SIGN", canSign);
                    command.ExecuteNonQuery();
                }
            }
            else if (candidateDocRef.StartsWith("template-"))
            {
                string assignationId = candidateDocRef.Substring(9);
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "UPDATE candidate_doc_template_asignation SET canSign = @SIGN WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", assignationId);
                    command.Parameters.AddWithValue("@SIGN", canSign);
                    command.ExecuteNonQuery();
                }
            }
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
