using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers.Commons;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.Client
{
    [Route("api/v1/comm-client-candidate")]
    [ApiController]
    [Authorize]
    public class CommunicationsClientCandidateController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        // CREAR

        [HttpPost]
        [Route(template: "create-for-client/")]
        public async Task<IActionResult> CreateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("subject", out JsonElement subjectJson) &&
                json.TryGetProperty("body", out JsonElement bodyJson) &&
                json.TryGetProperty("attachments", out JsonElement attachmentsJson) &&
                json.TryGetProperty("candidateList", out JsonElement candidateListJson) &&
                json.TryGetProperty("centroList", out JsonElement centroListJson) &&
                json.TryGetProperty("companyList", out JsonElement companyListJson) &&
                json.TryGetProperty("all", out JsonElement allJson))
            {
                string subject = subjectJson.GetString();
                string body = bodyJson.GetString();
                List<EventMailer.Attatchment> attachments = new();
                foreach (var attachmentJson in attachmentsJson.EnumerateArray())
                {
                    attachments.Add(new EventMailer.Attatchment
                    {
                        filename = attachmentJson.GetProperty("filename").GetString(),
                        base64 = attachmentJson.GetProperty("base64").GetString()
                    });
                }
                List<string> candidateList = GetJsonStringList(candidateListJson);
                List<string> centroList = GetJsonStringList(centroListJson);
                List<string> companyList = GetJsonStringList(companyListJson);
                bool? all = GetJsonBool(allJson);

                string author = ClientHasPermission(clientToken, null, null, CL_PERMISSION_COMUNICACIONES);
                if (author == null)
                {
                    return Ok(new
                    {
                        error = "Error 1002, No se disponen de los privilegios suficientes."
                    });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        //Poblar la lista de candidatos con todas las opciones dadas
                        foreach (string centroId in centroList)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT C.id " +
                                    "FROM candidatos C " +
                                    "WHERE C.centroId = @CENTRO AND " +
                                    "EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE CUC.centroId = C.centroId AND U.token = @TOKEN) AND " +
                                    "(C.fechaFinTrabajo IS NULL OR fechaFinTrabajo >= @TODAY) ";
                                command.Parameters.AddWithValue("@TOKEN", clientToken);
                                command.Parameters.AddWithValue("@CENTRO", centroId);
                                command.Parameters.AddWithValue("@TODAY", DateTime.Now.Date);
                                using (SqlDataReader reader = command.ExecuteReader())
                                    while (reader.Read())
                                        candidateList.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }
                        foreach (string companyId in companyList)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT C.id " +
                                    "FROM candidatos C " +
                                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                                    "WHERE CE.companyId = @COMPANY AND " +
                                    "EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN client_users U ON(CUC.clientUserId = U.id) WHERE CUC.centroId = CE.id AND U.token = @TOKEN) AND " +
                                    "(C.fechaFinTrabajo IS NULL OR fechaFinTrabajo >= @TODAY) ";
                                command.Parameters.AddWithValue("@TOKEN", clientToken);
                                command.Parameters.AddWithValue("@COMPANY", companyId);
                                command.Parameters.AddWithValue("@TODAY", DateTime.Now.Date);

                                using (SqlDataReader reader = command.ExecuteReader())
                                    while (reader.Read())
                                        candidateList.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }
                        if (all != null && all.Value)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT C.id " +
                                    "FROM candidatos C " +
                                    "INNER JOIN client_user_centros CUC ON(C.centroId = CUC.centroId) " +
                                    "INNER JOIN client_users U ON(CUC.clientUserId = U.id) " +
                                    "WHERE U.token = @TOKEN AND " +
                                    "(C.fechaFinTrabajo IS NULL OR fechaFinTrabajo >= @TODAY) ";
                                command.Parameters.AddWithValue("@TOKEN", clientToken);
                                command.Parameters.AddWithValue("@TODAY", DateTime.Now.Date);
                                using (SqlDataReader reader = command.ExecuteReader())
                                    while (reader.Read())
                                        candidateList.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }

                        if (candidateList.Count == 0)
                        {
                            return Ok(new { error = "Error 4713, no se ha especificado ningun destinatario." });
                        }

                        candidateList = candidateList.Distinct().ToList();

                        //Crear el mensaje
                        string id = ComputeStringHash(DateTime.Now + subject);
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO comunicaciones_cliente_candidato (id, sender, subject, body) VALUES (@ID, @SENDER, @SUBJECT, @BODY)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@SENDER", author);
                            command.Parameters.AddWithValue("@SUBJECT", subject);
                            command.Parameters.AddWithValue("@BODY", body);
                            command.ExecuteNonQuery();
                        }

                        //Insertar los destinatarios
                        foreach (string candidateId in candidateList)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "INSERT INTO comunicaciones_cliente_candidato_destinatarios (messageId, candidateId) VALUES (@ID, @CANDIDATE)";
                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                                command.ExecuteNonQuery();
                            }
                        }

                        //Guardar los adjuntos
                        foreach (EventMailer.Attatchment attachment in attachments)
                        {
                            SaveFile(new[] { "comm_client_candidate", id, "attachments", attachment.filename }, attachment.base64);
                        }

                        //Enviar los correos
                        foreach (string candidateId in candidateList)
                        {
                            string email = null, name = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT C.id, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, C.email " +
                                                      "FROM candidatos C WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        name = reader.GetString(reader.GetOrdinal("name"));
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                    }
                                }
                            }
                            if (email != null && name != null)
                            {
                                EventMailer.SendEmail(new EventMailer.Email()
                                {
                                    template = "communicationClientCandidateNotification",
                                    inserts = new() { { "url", AccessController.getAutoLoginUrl("ca", candidateId, null, conn, null) } },
                                    toEmail = email,
                                    toName = name,
                                    replyEmail = author,
                                    subject = "[Think&Job] Nuevo mensaje",
                                    priority = candidateList.Count > 10 ? EventMailer.EmailPriority.SLOWLANE : (candidateList.Count > 2 ? EventMailer.EmailPriority.MODERATE : EventMailer.EmailPriority.IMMEDIATE)
                                });
                            }
                        }
                        await PushNotificationController.sendNotifications(candidateList.Select(candidateId => new PushNotificationController.UID() { type = "ca", id = candidateId }), new()
                        {
                            title = "Nuevo mensaje",
                            body = subject,
                            type = "candidate-comunicacion",
                            data = new() { { "id", id } }
                        }, conn);

                        result = new { error = false, id };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5710, no se ha podido enviar el mensaje" };
                    }
                }
            }

            return Ok(result);
        }

        // LISTAR

        [HttpGet]
        [Route(template: "list-for-client/")]
        public IActionResult ListForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            string author = ClientHasPermission(clientToken, null, null, CL_PERMISSION_COMUNICACIONES);
            if (author == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<Communication> communications = new();

                    //Obtener las comunicaciones que ha enviado
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM comunicaciones_cliente_candidato WHERE sender = @AUTHOR ORDER BY date DESC";
                        command.Parameters.AddWithValue("@AUTHOR", author);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                communications.Add(new Communication()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    sender = reader.GetString(reader.GetOrdinal("sender")),
                                    subject = reader.GetString(reader.GetOrdinal("subject")),
                                    date = reader.GetDateTime(reader.GetOrdinal("date"))
                                });
                            }
                        }
                    }

                    result = new
                    {
                        error = false,
                        communications
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se ha podido obtener las comunicaciones" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-candidate/")]
        public IActionResult ListForCandidate()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<Communication> communications = new();

                    //Obtener las comunicaciones que ha recibido
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT CCC.*, CCCD.new " +
                                              "FROM comunicaciones_cliente_candidato CCC " +
                                              "INNER JOIN comunicaciones_cliente_candidato_destinatarios CCCD ON(CCC.id = CCCD.messageId) " +
                                              "WHERE CCCD.candidateId = @CANDIDATE AND " +
                                              "CCC.date > dateadd(m, -6, getdate() - datepart(d, getdate()) + 1) " +
                                              "ORDER BY CCC.date DESC";
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                communications.Add(new Communication()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    sender = reader.GetString(reader.GetOrdinal("sender")),
                                    subject = reader.GetString(reader.GetOrdinal("subject")),
                                    date = reader.GetDateTime(reader.GetOrdinal("date")),
                                    readed = reader.GetInt32(reader.GetOrdinal("new")) == 0
                                });
                            }
                        }
                    }

                    result = new
                    {
                        error = false,
                        communications
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se ha podido obtener las comunicaciones" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "count-not-readed-for-candidate/")]
        public IActionResult CountNotReadedForCandidate()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) " +
                                              "FROM comunicaciones_cliente_candidato CCC " +
                                              "INNER JOIN comunicaciones_cliente_candidato_destinatarios CCCD ON(CCC.id = CCCD.messageId) " +
                                              "WHERE CCCD.candidateId = @CANDIDATE AND " +
                                              "CCC.date > dateadd(m, -6, getdate() - datepart(d, getdate()) + 1) AND " +
                                              "CCCD.new = 1";
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        result = (int)command.ExecuteScalar();
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se ha podido contar las comunicaciones" };
                }
            }

            return Ok(result);
        }

        // OBTENER

        [HttpGet]
        [Route(template: "get-for-client/{messageId}/")]
        public IActionResult GetForClient(string messageId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            string author = ClientHasPermission(clientToken, null, null, CL_PERMISSION_COMUNICACIONES);
            if (author == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    //Obtener la comunicacion
                    Communication? communication = getCommunication(messageId, true, conn);

                    if (communication == null)
                    {
                        result = new { error = "Error 4712, comunicacion no encontrada" };
                    }
                    else
                    {
                        if (communication.Value.sender == author)
                        {
                            result = new
                            {
                                error = false,
                                communication
                            };
                        }
                        else
                        {
                            result = new { error = "Error 4713, esta comunicacion no es del cliente" };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se ha podido obtener la incidencia" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-for-candidate/{messageId}/")]
        public IActionResult GetForCandidate(string messageId)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    //Obtener la comunicacion
                    Communication? communication = getCommunication(messageId, true, conn);

                    if (communication == null)
                    {
                        result = new { error = "Error 4712, comunicacion no encontrada" };
                    }
                    else
                    {
                        if (communication.Value.addressers.Any(a => a.id == candidateId))
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "UPDATE comunicaciones_cliente_candidato_destinatarios " +
                                    "SET new = 0, readed = (getdate()) " +
                                    "WHERE messageId = @COMMUNICATION AND candidateId = @CANDIDATE";
                                command.Parameters.AddWithValue("@COMMUNICATION", messageId);
                                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                                command.ExecuteNonQuery();
                            }

                            communication.Value.addressers.Clear();

                            result = new
                            {
                                error = false,
                                communication
                            };
                        }
                        else
                        {
                            result = new { error = "Error 4713, esta comunicacion no es para el trabajador" };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se ha podido obtener la incidencia" };
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "downloat-attachment/{messageId}")]
        public async Task<IActionResult> DownloadAttachment(string messageId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("filename", out JsonElement filenameJson))
            {
                string filename = filenameJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        result = new
                        {
                            error = false,
                            base64 = ReadFile(new[] { "comm_client_candidate", messageId, "attachments", filename })
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5710, no se ha podido obtener el adjunto" };
                    }
                }
            }

            return Ok(result);
        }

        // ELIMINAR

        [HttpDelete]
        [Route(template: "delete-for-client/{messageId}/")]
        public IActionResult DeleteForClient(string messageId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            string author = ClientHasPermission(clientToken, null, null, CL_PERMISSION_COMUNICACIONES);
            if (author == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    //Obtener la comunicacion
                    Communication? communication = getCommunication(messageId, true, conn);

                    if (communication == null)
                    {
                        result = new { error = "Error 4712, comunicacion no encontrada" };
                    }
                    else
                    {
                        if (communication.Value.sender == author)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "DELETE FROM comunicaciones_cliente_candidato_destinatarios WHERE messageId = @COMMUNICATION";
                                command.Parameters.AddWithValue("@COMMUNICATION", messageId);
                                command.ExecuteNonQuery();
                            }

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "DELETE FROM comunicaciones_cliente_candidato WHERE id = @COMMUNICATION";
                                command.Parameters.AddWithValue("@COMMUNICATION", messageId);
                                command.ExecuteNonQuery();
                            }

                            DeleteDir(new[] { "comm_client_candidate", messageId });

                            result = new { error = false };
                        }
                        else
                        {
                            result = new { error = "Error 4713, esta comunicacion no es del cliente" };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se ha podido eliminar el mensaje" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------

        // AYUDA

        public struct Communication
        {
            public string id { get; set; }
            public string sender { get; set; }
            public string senderName { get; set; }
            public string subject { get; set; }
            public string body { get; set; }
            public DateTime date { get; set; }
            public List<CommunicationAddressee> addressers { get; set; }
            public List<string> attachments { get; set; }
            public bool readed { get; set; }
        }

        public struct CommunicationAddressee
        {
            public string id { get; set; }
            public string name { get; set; }
            public string dni { get; set; }
            public bool readed { get; set; }
            public DateTime? readDate { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------

        private Communication? getCommunication(string id, bool readAddressers, SqlConnection conn)
        {
            Communication? communication = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT CCC.*, CU.username as senderName FROM comunicaciones_cliente_candidato CCC LEFT OUTER JOIN client_users CU ON(CCC.sender = CU.username) WHERE CCC.id = @ID";
                command.Parameters.AddWithValue("@ID", id);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        communication = new Communication()
                        {
                            id = id,
                            sender = reader.GetString(reader.GetOrdinal("sender")),
                            senderName = reader.IsDBNull(reader.GetOrdinal("senderName")) ? reader.GetString(reader.GetOrdinal("sender")) : reader.GetString(reader.GetOrdinal("senderName")),
                            subject = reader.GetString(reader.GetOrdinal("subject")),
                            body = reader.GetString(reader.GetOrdinal("body")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            addressers = new(),
                            attachments = ListFiles(new[] { "comm_client_candidate", id, "attachments" }).ToList().Select(Path.GetFileName).ToList()
                        };
                    }
                }
            }

            if (communication != null && readAddressers)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT C.id, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, C.dni, CCCD.new, CCCD.readed " +
                                          "FROM comunicaciones_cliente_candidato_destinatarios CCCD " +
                                          "INNER JOIN candidatos C ON(CCCD.candidateId = C.id) " +
                                          "WHERE CCCD.messageId = @ID";
                    command.Parameters.AddWithValue("@ID", id);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            communication.Value.addressers.Add(new CommunicationAddressee()
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("name")),
                                dni = reader.GetString(reader.GetOrdinal("dni")),
                                readed = reader.GetInt32(reader.GetOrdinal("new")) == 0,
                                readDate = reader.IsDBNull(reader.GetOrdinal("readed")) ? null : reader.GetDateTime(reader.GetOrdinal("readed"))
                            });
                        }
                    }
                }
            }

            return communication;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }


   

    

    

    
}
