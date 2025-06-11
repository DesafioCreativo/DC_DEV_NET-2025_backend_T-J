using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers.MainHome.RRHH;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.ClientUserController;

namespace ThinkAndJobSolution.Controllers.Client
{
    [Route("api/v1/comm-client-client")]
    [ApiController]
    [Authorize]
    public class CommunicationsClientClientController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        // Creacion

        [HttpPost]
        [Route(template: "create-for-client/")]
        public async Task<IActionResult> CreateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, null, null, out ClientUserAccess access) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader jsonReader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await jsonReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("title", out string title) &&
                json.TryGetString("to", out string to) &&
                json.TryGetString("text", out string text))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            Topic topic = new Topic()
                            {
                                title = title,
                                userIdFrom = access.id,
                                userIdTo = to
                            };
                            TopicMessage msg = new()
                            {
                                text = text,
                                authorId = access.id
                            };

                            string toAccessLevel = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT accessLevel FROM client_users WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", to);
                                using (SqlDataReader reader = command.ExecuteReader())
                                    if (reader.Read())
                                        toAccessLevel = reader.GetString(reader.GetOrdinal("accessLevel"));
                            }
                            if (toAccessLevel == null)
                            {
                                return Ok(new { error = "Error 4921, usuario destino no encontrado" });
                            }
                            else if (toAccessLevel == "Administrador")
                            {
                                return Ok(new { error = "Error 4922, no puedes iniciar una comunicación con un administrador" });
                            }

                            if (json.TryGetStringList("candidates", out List<string> candidates))
                                msg.attachedCandidateIds = candidates;

                            if (json.TryGetStringList("files", out List<string> files))
                                msg.attachedFiles = files;

                            string id = createTopic(topic, msg, conn, transaction);

                            transaction.Commit();
                            result = new { error = false, id };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5920, no se ha podido enviar el mensaje" };
                        }
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "post-message-for-client/{topicId}/")]
        public async Task<IActionResult> PostMessageForClient(string topicId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, null, null, out ClientUserAccess access) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader jsonReader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await jsonReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("text", out string text))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        Topic? topic = getTopic(topicId, conn, null, false);
                        if (topic == null)
                        {
                            return Ok(new { error = "Error 4920, Mensaje no encontrado." });
                        }
                        if (topic.Value.closed)
                        {
                            return Ok(new { error = "Error 4923, Conversación cerrada, no se puede enviar el mensaje." });
                        }

                        TopicMessage msg = new()
                        {
                            topicId = topicId,
                            text = text,
                            authorId = access.id
                        };

                        if (json.TryGetStringList("candidates", out List<string> candidates))
                            msg.attachedCandidateIds = candidates;

                        if (json.TryGetStringList("files", out List<string> files))
                            msg.attachedFiles = files;

                        int id = addMessage(msg, conn);

                        result = new { error = false, id };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5921, no se ha podido enviar el mensaje" };
                    }
                }
            }

            return Ok(result);
        }

        // Actualización

        [HttpPatch]
        [Route(template: "update-closed-for-client/{topicId}/")]
        public async Task<IActionResult> UpdateClosedForClient(string topicId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, null, null, out ClientUserAccess access) == null && !access.isSuper)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader jsonReader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await jsonReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetBoolean("closed", out bool closed))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        updateTopic(topicId, closed, conn);

                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5927, no se ha podido actualizar el mensaje" };
                    }
                }
            }

            return Ok(result);
        }

        // Listado

        [HttpGet]
        [Route(template: "list-for-client/")]
        public IActionResult ListForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, null, null, out ClientUserAccess access) == null)
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
                    List<Topic> topics;
                    if (access.accessLevel == "Administrador")
                    {
                        topics = listTopicCLAll(access.id, conn);
                    }
                    else
                    {
                        topics = listTopicCLMine(access.id, conn);
                    }

                    result = new { error = false, topics };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5924, no se han podido obtener los mensajes" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list/")]
        public IActionResult List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            ResultadoAcceso access = HasPermission("CommunicationsClientClient.List", securityToken);
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
                    string userId = GuardiasController.getUserIdFilter(FindUserIdBySecurityToken(securityToken), access.EsJefe, conn);
                    List<Topic> topics = listTopicRJ(userId, conn);

                    result = new { error = false, topics };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5925, no se han podido obtener los mensajes" };
                }
            }

            return Ok(result);
        }

        // Obtencion

        [HttpGet]
        [Route(template: "get-for-client/{topicId}/")]
        public IActionResult GetForClient(string topicId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, null, null, out ClientUserAccess access) == null)
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
                    Topic? topic = getTopic(topicId, conn);
                    if (topic == null)
                    {
                        return Ok(new { error = "Error 4920, Mensaje no encontrado." });
                    }
                    setSeen(topic.Value, access.id, conn);

                    result = new { error = false, topic = topic.Value };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5922, no se ha podido obtener el mensaje" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get/{topicId}/")]
        public IActionResult Get(string topicId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CommunicationsClientClient.Get", securityToken).Acceso)
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
                    Topic? topic = getTopic(topicId, conn);
                    if (topic == null)
                    {
                        return Ok(new { error = "Error 4920, Mensaje no encontrado." });
                    }

                    result = new { error = false, topic = topic.Value };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5926, no se ha podido obtener el mensaje" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-attachment/{topicId}/{messageId}/{attachmentIndex}")]
        public IActionResult DownloadAttachment(string topicId, int messageId, int attachmentIndex)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            try
            {
                result = new { error = false, attachment = getAttachment(topicId, messageId, attachmentIndex) };
            }
            catch (Exception)
            {
                result = new { error = "Error 5923, no se ha podido obtener el fichero adjunto" };
            }

            return Ok(result);
        }

        // Extras

        [HttpGet]
        [Route(template: "list-destinaries/")]
        public IActionResult ListDestinaries()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, null, null, out ClientUserAccess access) == null)
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
                    result = new { error = false, destinaries = listDestinaries(access.id, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5928, no se ha podido listar los posibles destinatarios" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------

        // Ayuda

        public struct Topic
        {
            public string id { get; set; }
            public string title { get; set; }
            public string userIdFrom { get; set; }
            public string userIdTo { get; set; }
            public string usernameFrom { get; set; }
            public string usernameTo { get; set; }
            public bool closed { get; set; }
            public List<TopicMessage> messages { get; set; }
            public DateTime date { get; set; }
        }

        public struct TopicMessage
        {
            public int id { get; set; }
            public string topicId { get; set; }
            public string text { get; set; }
            public List<CLSimpleWorkerInfo> attachedCandidates { get; set; }
            public List<string> attachedCandidateIds { get; set; }
            public List<string> attachedFiles { get; set; }
            public int nAttachedFiles { get; set; }
            public string author { get; set; }
            public string authorId { get; set; }
            public DateTime? seenDate { get; set; }
            public DateTime date { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static string createTopic(Topic topic, TopicMessage firstMsg, SqlConnection conn, SqlTransaction transaction = null)
        {
            string id = ComputeStringHash(topic.title + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "INSERT INTO comunicaciones_cliente_cliente (id, title, userIdFrom, userIdTo) " +
                    "VALUES (@ID, @TITLE, @FROM, @TO)";
                command.Parameters.AddWithValue("@ID", id);
                command.Parameters.AddWithValue("@TITLE", topic.title);
                command.Parameters.AddWithValue("@FROM", topic.userIdFrom);
                command.Parameters.AddWithValue("@TO", topic.userIdTo);
                command.ExecuteNonQuery();
            }

            firstMsg.topicId = id;
            addMessage(firstMsg, conn, transaction);

            return id;
        }

        public static int addMessage(TopicMessage msg, SqlConnection conn, SqlTransaction transaction = null)
        {
            int id;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "INSERT INTO comunicaciones_cliente_cliente_mensajes (topicId, [text], candidates, author) " +
                    "output INSERTED.ID VALUES (@TOPIC, @TEXT, @CANDIDATES, @AUTHOR)";
                command.Parameters.AddWithValue("@TOPIC", msg.topicId);
                command.Parameters.AddWithValue("@TEXT", msg.text);
                command.Parameters.AddWithValue("@CANDIDATES", msg.attachedCandidateIds == null ? DBNull.Value : serializeCandidateIds(msg.attachedCandidateIds));
                command.Parameters.AddWithValue("@AUTHOR", msg.authorId);
                id = (int)command.ExecuteScalar();
            }

            if (msg.attachedFiles != null && msg.attachedFiles.Count > 0)
            {
                SaveFileListNumbered(new[] { "comm_client_client", msg.topicId, id.ToString() }, msg.attachedFiles);
            }

            return id;
        }

        public static void updateTopic(string topicId, bool closed, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "UPDATE comunicaciones_cliente_cliente SET closed = @CLOSED WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", topicId);
                command.Parameters.AddWithValue("@CLOSED", closed ? 1 : 0);
                command.ExecuteNonQuery();
            }
        }

        public static void setSeen(Topic topic, string userId, SqlConnection conn, SqlTransaction transaction = null)
        {
            bool isSender = userId == topic.userIdFrom;
            bool isReceiver = userId == topic.userIdTo;

            if (isSender && topic.messages.Any(m => m.seenDate == null && m.authorId == topic.userIdTo))
            {
                markSeen(topic, topic.userIdTo, conn, transaction);
            }
            else if (isReceiver && topic.messages.Any(m => m.seenDate == null && m.authorId == topic.userIdFrom))
            {
                markSeen(topic, topic.userIdFrom, conn, transaction);
            }
        }

        private static void markSeen(Topic topic, string authorId, SqlConnection conn, SqlTransaction transaction = null)
        {
            DateTime now = DateTime.Now;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "UPDATE comunicaciones_cliente_cliente_mensajes SET seenDate = @DATE WHERE author = @AUTHOR AND topicId = @TOPIC";
                command.Parameters.AddWithValue("@TOPIC", topic.id);
                command.Parameters.AddWithValue("@DATE", now);
                command.Parameters.AddWithValue("@AUTHOR", authorId);
                command.ExecuteNonQuery();
            }

            for (int i = 0; i < topic.messages.Count; i++)
            {
                TopicMessage msg = topic.messages[i];
                if (msg.authorId == authorId && msg.seenDate == null)
                {
                    msg.seenDate = now;
                    topic.messages[i] = msg;
                }
            }
        }

        public static void deleteTopic(string topicId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "DELETE FROM comunicaciones_cliente_cliente_mensajes WHERE topicId = @TOPIC";
                command.Parameters.AddWithValue("@TOPIC", topicId);
                command.ExecuteNonQuery();
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "DELETE FROM comunicaciones_cliente_cliente WHERE id = @TOPIC";
                command.Parameters.AddWithValue("@TOPIC", topicId);
                command.ExecuteNonQuery();
            }

            DeleteDir(new[] { "comm_client_client", topicId });
        }

        public static Topic? getTopic(string topicId, SqlConnection conn, SqlTransaction transaction = null, bool withMessages = true)
        {
            Topic topic;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CCC.*, CUFROM.username as usernameFrom, CUTO.username as usernameTo " +
                    "FROM comunicaciones_cliente_cliente CCC " +
                    "INNER JOIN client_users CUFROM ON(CUFROM.id = CCC.userIdFrom) " +
                    "INNER JOIN client_users CUTO ON(CUTO.id = CCC.userIdTo) " +
                    "WHERE CCC.id = @TOPIC";
                command.Parameters.AddWithValue("@TOPIC", topicId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        topic = new()
                        {
                            id = topicId,
                            title = reader.GetString(reader.GetOrdinal("title")),
                            userIdFrom = reader.GetString(reader.GetOrdinal("userIdFrom")),
                            userIdTo = reader.GetString(reader.GetOrdinal("userIdTo")),
                            usernameFrom = reader.GetString(reader.GetOrdinal("usernameFrom")),
                            usernameTo = reader.GetString(reader.GetOrdinal("usernameTo")),
                            messages = new List<TopicMessage>(),
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            date = reader.GetDateTime(reader.GetOrdinal("date"))
                        };
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            if (!withMessages)
                return topic;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CCCM.*, CU.username as authorUsername " +
                    "FROM comunicaciones_cliente_cliente_mensajes CCCM " +
                    "INNER JOIN client_users CU ON(CU.id = CCCM.author) " +
                    "WHERE CCCM.topicId = @TOPIC " +
                    "ORDER BY date ASC";
                command.Parameters.AddWithValue("@TOPIC", topicId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        TopicMessage message = new()
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            topicId = topicId,
                            text = reader.GetString(reader.GetOrdinal("text")),
                            author = reader.GetString(reader.GetOrdinal("authorUsername")),
                            authorId = reader.GetString(reader.GetOrdinal("author")),
                            seenDate = reader.IsDBNull(reader.GetOrdinal("seenDate")) ? null : reader.GetDateTime(reader.GetOrdinal("seenDate")),
                            attachedCandidateIds = reader.IsDBNull(reader.GetOrdinal("candidates")) ? null : parseCandidateIds(reader.GetString(reader.GetOrdinal("candidates"))),
                            date = reader.GetDateTime(reader.GetOrdinal("date"))

                        };
                        message.nAttachedFiles = ListFiles(new[] { "comm_client_client", topicId, message.id.ToString() }).Length;
                        topic.messages.Add(message);
                    }
                }
            }

            for (int i = 0; i < topic.messages.Count; i++)
            {
                TopicMessage message = topic.messages[i];
                if (message.attachedCandidateIds != null && message.attachedCandidateIds.Count > 0)
                {
                    message.attachedCandidates = getCandidateDataList(message.attachedCandidateIds, conn, transaction);
                    topic.messages[i] = message;
                }
            }

            return topic;
        }

        public static string getAttachment(string topicId, int messageId, int attachmentIndex)
        {
            return ReadFile(new[] { "comm_client_client", topicId, messageId.ToString(), attachmentIndex.ToString() });
        }

        //Si se pasa userId, se limita a las que hayan creado o recibido usuarios CL con los que comparta empresas
        public static List<Topic> listTopicRJ(string userId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<Topic> topics;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CCC.*, CUFROM.username as usernameFrom, CUTO.username as usernameTo " +
                    "FROM comunicaciones_cliente_cliente CCC " +
                    "INNER JOIN client_users CUFROM ON(CUFROM.id = CCC.userIdFrom) " +
                    "INNER JOIN client_users CUTO ON(CUTO.id = CCC.userIdTo) " +
                    "WHERE @USER IS NULL OR EXISTS( " +
                    "SELECT * FROM asociacion_usuario_empresa AUE " +
                    "INNER JOIN centros CE ON(CE.companyId = AUE.companyId) " +
                    "INNER JOIN client_user_centros CUC ON(CE.id = CUC.centroId) " +
                    "WHERE AUE.userId = @USER AND CUC.clientUserId IN(CCC.userIdFrom, CCC.userIdTo) " +
                    ")";
                command.Parameters.AddWithValue("@USER", (object)userId ?? DBNull.Value);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    topics = readTopics(reader);
                }
            }

            return topics;
        }

        //El usuario cliente debe ser el emisor o el receptor del mensaje
        public static List<Topic> listTopicCLMine(string userId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<Topic> topics;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CCC.*, CUFROM.username as usernameFrom, CUTO.username as usernameTo " +
                    "FROM comunicaciones_cliente_cliente CCC " +
                    "INNER JOIN client_users CUFROM ON(CUFROM.id = CCC.userIdFrom) " +
                    "INNER JOIN client_users CUTO ON(CUTO.id = CCC.userIdTo) " +
                    "WHERE @USER IN(CCC.userIdFrom, CCC.userIdTo)";
                command.Parameters.AddWithValue("@USER", userId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    topics = readTopics(reader);
                }
            }

            return topics;
        }

        //El usuario cliente debe ser tener solapamiento de centros con el emisor o el receptor del mensaje
        public static List<Topic> listTopicCLAll(string userId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<Topic> topics;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CCC.*, CUFROM.username as usernameFrom, CUTO.username as usernameTo " +
                    "FROM comunicaciones_cliente_cliente CCC " +
                    "INNER JOIN client_users CUFROM ON(CUFROM.id = CCC.userIdFrom) " +
                    "INNER JOIN client_users CUTO ON(CUTO.id = CCC.userIdTo) " +
                    "WHERE EXISTS( " +
                    "SELECT * FROM client_user_centros CUC1 " +
                    "INNER JOIN client_user_centros CUC2 ON(CUC1.centroId = CUC2.centroId) " +
                    "WHERE CUC1.clientUserId = @USER AND CUC2.clientUserId IN(CCC.userIdFrom, CCC.userIdTo) " +
                    ")";
                command.Parameters.AddWithValue("@USER", userId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    topics = readTopics(reader);
                }
            }

            return topics;
        }

        private static List<Topic> readTopics(SqlDataReader reader)
        {
            List<Topic> topics = new();

            while (reader.Read())
            {
                topics.Add(new()
                {
                    id = reader.GetString(reader.GetOrdinal("id")),
                    title = reader.GetString(reader.GetOrdinal("title")),
                    userIdFrom = reader.GetString(reader.GetOrdinal("userIdFrom")),
                    userIdTo = reader.GetString(reader.GetOrdinal("userIdTo")),
                    usernameFrom = reader.GetString(reader.GetOrdinal("usernameFrom")),
                    usernameTo = reader.GetString(reader.GetOrdinal("usernameTo")),
                    closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                    date = reader.GetDateTime(reader.GetOrdinal("date"))
                });
            }

            return topics;
        }
        public static int countUnreadedTopics(string userId, SqlConnection conn, SqlTransaction transaction = null)
        {
            int n;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT COUNT(DISTINCT CCC.id) " +
                    "FROM comunicaciones_cliente_cliente CCC " +
                    "INNER JOIN comunicaciones_cliente_cliente_mensajes CCCM ON(CCC.id = CCCM.topicId) " +
                    "WHERE CCC.userIdTo = @USER AND CCCM.author <> @USER AND CCCM.seenDate IS NULL";
                command.Parameters.AddWithValue("@USER", userId);
                n = (int)command.ExecuteScalar();
            }

            return n;
        }

        private static string serializeCandidateIds(List<string> ids)
        {
            return System.Text.Json.JsonSerializer.Serialize(ids);
        }

        private static List<string> parseCandidateIds(string jsonText)
        {
            JsonElement json = JsonDocument.Parse(jsonText).RootElement;
            List<string> ids = new();
            foreach (JsonElement idJson in json.EnumerateArray())
                ids.Add(idJson.GetString());
            return ids;
        }

        private static List<CLSimpleWorkerInfo> getCandidateDataList(List<string> ids, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<CLSimpleWorkerInfo> candidates = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.dni, C.fechaComienzoTrabajo, CA.name as work, CG.name as groupName " +
                    "FROM candidatos C " +
                    "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                    "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                    "LEFT OUTER JOIN candidate_group_members CGM ON(CGM.candidateId = C.id) " +
                    "LEFT OUTER JOIN candidate_groups CG ON(CGM.groupId = CG.id) " +
                    "WHERE C.id = @ID";
                command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                foreach (string id in ids)
                {
                    command.Parameters["@ID"].Value = id;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            candidates.Add(new CLSimpleWorkerInfo
                            {
                                id = id,
                                nombre = reader.GetString(reader.GetOrdinal("candidateName")),
                                dni = reader.GetString(reader.GetOrdinal("dni")),
                                fechaComienzoTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo")).Date,
                                work = reader.GetString(reader.GetOrdinal("work")),
                                groupName = reader.IsDBNull(reader.GetOrdinal("groupName")) ? null : reader.GetString(reader.GetOrdinal("groupName"))
                            });
                        }
                    }
                }
            }

            return candidates;
        }

        private static List<object> listDestinaries(string userId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<object> destinaries = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CU.username, CU.id " +
                    "FROM client_users CU " +
                    "WHERE CU.accessLevel <> 'Administrador' AND EXISTS( " +
                    "SELECT * FROM client_user_centros CUC1 " +
                    "INNER JOIN client_user_centros CUC2 ON(CUC1.centroId = CUC2.centroId) " +
                    "WHERE CUC1.clientUserId = @USER AND CUC2.clientUserId = CU.id " +
                    ")";
                command.Parameters.AddWithValue("@USER", userId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        destinaries.Add(new
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            username = reader.GetString(reader.GetOrdinal("username"))
                        });
                    }
                }
            }

            return destinaries;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------


    }
}