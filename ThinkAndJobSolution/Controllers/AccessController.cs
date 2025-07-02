using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers.Authorization;
using ThinkAndJobSolution.Request;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers
{
    [Route("api/v1/users")]
    [ApiController]
    //[Authorize]
    public class AccessController : ControllerBase
    {
        private readonly EmailSettings _emailSettings;

        public AccessController(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest request)
        {
            LoginData result = new LoginData
            {
                error = "Error 2932, no se ha podido procesar la petición.",
                authorized = false
            };

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Intentar iniciar sesión como user
                    result = tryLoginRJ(request.Username, request.Password, conn);

                    //Intentar iniciar sesión como candidato
                    if (!result.found)
                    {
                        result = tryLoginCA(request.Username, request.Password, conn);
                    }

                    //Intentar iniciar sesión como usuario cliente
                    if (!result.found)
                    {
                        result = tryLoginCL(request.Username, request.Password, conn);
                    }
                }

                if (result.authorized)
                {
                    switch (result.type)
                    {
                        case "client":
                            result.autoLoginToken = getAutoLoginString("cl", result.id, null, null, null);
                            break;
                        case "candidate":
                            result.autoLoginToken = getAutoLoginString("ca", result.candidateId, null, null, null);
                            break;
                        case "user":
                            result.autoLoginToken = getAutoLoginString("rj", result.id, null, null, null);
                            break;
                    }

                    return Ok(result);
                }

                return Unauthorized(result);
            }
            catch (Exception e)
            {
                return StatusCode(500, new LoginData
                {
                    error = "Error 5551, no se ha podido iniciar sesión: " + e.Message
                });
            }
        }


        [HttpGet]
        [Route(template: "auto-login/{code}")]
        public IActionResult AutoLogin(string code)
        {
            dynamic result = new ExpandoObject();
            result.error = "Error 2932, no se ha podido procesar la petición.";
            result.authorized = false;

            try
            {
                string decodedCode = Encoding.ASCII.GetString(Convert.FromBase64String(code));
                string[] codeParts = decodedCode.Split("-");
                string type = codeParts[0];
                string loginToken = codeParts[1];

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    switch (type)
                    {
                        case "rj":
                            result = tryLoginRJ("", "", conn);
                            break;
                        case "ca":
                            result = tryLoginCA("", "", conn);
                            break;
                        case "cl":
                            result = tryLoginCL("", "", conn);
                            break;
                    }
                }

            }
            catch (Exception e)
            {
                result = new
                {
                    error = "Error 5551, no se ha podido iniciar sesion: " + e.Message
                };
            }

            return Ok(result);
        }

        // TyC

        [HttpPatch]
        [Route(template: "accept-terms/{type}/{id}")]
        public IActionResult AcceptTermsAndConditions(string type, string id)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                //Poner a true el terminosAceptados
                using (SqlCommand command = conn.CreateCommand())
                {
                    switch (type)
                    {
                        case "ca":
                            command.CommandText = "UPDATE candidatos SET terminosAceptados = 1 WHERE id = @ID";
                            break;
                        case "cl":
                            command.CommandText = "UPDATE client_users SET terminosAceptados = 1 WHERE id = @ID";
                            break;
                        case "rj":
                            command.CommandText = "UPDATE users SET terminosAceptados = 1 WHERE id = @ID";
                            break;
                        default:
                            return Ok(new { error = "Error 2932, tipo de usuario no valido." });
                    }

                    command.Parameters.AddWithValue("@ID", id);

                    try
                    {
                        command.ExecuteNonQuery();
                        result = new
                        {
                            error = false
                        };
                    }
                    catch (Exception)
                    {
                        result = new
                        {
                            error = "Error 5517, no se ha podido modificar la aceptacion de terminos y condiciones."
                        };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "accept-terms/{type}/")]
        public IActionResult CheckTermsAndConditionsAccepted(string type)
        {
            string id = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                //Poner a true el terminosAceptados
                using (SqlCommand command = conn.CreateCommand())
                {
                    switch (type)
                    {
                        case "ca":
                            command.CommandText = "SELECT terminosAceptados FROM candidatos WHERE id = @ID";
                            break;
                        case "cl":
                            command.CommandText = "SELECT terminosAceptados FROM client_users WHERE id = @ID";
                            break;
                        case "rj":
                            command.CommandText = "SELECT terminosAceptados FROM users WHERE id = @ID";
                            break;
                        default:
                            return Ok(new { error = "Error 2932, tipo de usuario no valido." });
                    }

                    command.Parameters.AddWithValue("@ID", id);

                    try
                    {
                        bool acceptedTerms = true;
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                acceptedTerms = reader.GetInt32(reader.GetOrdinal("terminosAceptados")) == 1;
                        result = new
                        {
                            error = false,
                            terms = acceptedTerms ? null : GetTermsAndConditions()
                        };
                    }
                    catch (Exception)
                    {
                        result = new
                        {
                            error = "Error 5517, no se ha podido modificar la aceptacion de terminos y condiciones."
                        };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "accept-terms/{type}/{id}")]
        public IActionResult CheckTermsAndConditionsAccepted(string type, string id)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                //Poner a true el terminosAceptados
                using (SqlCommand command = conn.CreateCommand())
                {
                    switch (type)
                    {
                        case "ca":
                            command.CommandText = "SELECT terminosAceptados FROM candidatos WHERE id = @ID";
                            break;
                        case "cl":
                            command.CommandText = "SELECT terminosAceptados FROM client_users WHERE id = @ID";
                            break;
                        case "rj":
                            command.CommandText = "SELECT terminosAceptados FROM users WHERE id = @ID";
                            break;
                        default:
                            return Ok(new { error = "Error 2932, tipo de usuario no valido." });
                    }

                    command.Parameters.AddWithValue("@ID", id);

                    try
                    {
                        bool acceptedTerms = true;
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                acceptedTerms = reader.GetInt32(reader.GetOrdinal("terminosAceptados")) == 1;
                        result = new
                        {
                            error = false,
                            terms = acceptedTerms ? null : GetTermsAndConditions()
                        };
                    }
                    catch (Exception)
                    {
                        result = new
                        {
                            error = "Error 5517, no se ha podido modificar la aceptacion de terminos y condiciones."
                        };
                    }
                }
            }

            return Ok(result);
        }

        // Recuperacion de pwd

        [HttpPost]
        [Route("recover")]
        public async Task<IActionResult> SendRecoveryMail([FromBody] string email)
        {
            object result;
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    await conn.OpenAsync();

                    string? candidateId = null;
                    string? userId = null;
                    string? clientUserId = null;
                    bool emailVerified = true;

                    // 1. Verificar si es un candidato
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT id, email_verified FROM candidatos WHERE email = @EMAIL";
                        cmd.Parameters.AddWithValue("@EMAIL", email);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                candidateId = reader.GetString(reader.GetOrdinal("id"));
                                emailVerified = reader.GetInt32(reader.GetOrdinal("email_verified")) == 1;
                            }
                        }
                    }

                    // 2. Verificar si es un usuario RJ
                    if (candidateId == null)
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id FROM users WHERE email = @EMAIL";
                            cmd.Parameters.AddWithValue("@EMAIL", email);

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    userId = reader.GetString(reader.GetOrdinal("id"));
                                }
                            }
                        }
                    }

                    // 3. Verificar si es un client_user
                    if (candidateId == null && userId == null)
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT id FROM client_users WHERE email = @EMAIL";
                            cmd.Parameters.AddWithValue("@EMAIL", email);

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    clientUserId = reader.GetString(reader.GetOrdinal("id"));
                                }
                            }
                        }
                    }

                    // 4. Validar que el email exista en alguna tabla
                    if (candidateId == null && userId == null && clientUserId == null)
                    {
                        return Ok(new { error = "Error 4573, el email no pertenece a ningún usuario." });
                    }

                    // 5. Si es candidato, validar que tenga email confirmado
                    if (candidateId != null && !emailVerified)
                    {
                        return Ok(new { error = "Error 4573, el email no está validado, no puede usarse." });
                    }

                    // 6. Buscar si ya tiene un código de recuperación
                    string? existingCode = null;
                    DateTime existingExpiration = DateTime.Now;

                    string whereClause = "";
                    string idValue = "";

                    if (candidateId != null)
                    {
                        whereClause = "candidateId = @ID";
                        idValue = candidateId;
                    }
                    else if (userId != null)
                    {
                        whereClause = "userId = @ID";
                        idValue = userId;
                    }
                    else if (clientUserId != null)
                    {
                        whereClause = "clientUserId = @ID";
                        idValue = clientUserId;
                    }

                    if (!string.IsNullOrEmpty(whereClause))
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = $"SELECT code, expiration FROM recovery_codes WHERE {whereClause}";
                            cmd.Parameters.AddWithValue("@ID", idValue);

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    existingCode = reader.GetString(reader.GetOrdinal("code"));
                                    existingExpiration = reader.GetDateTime(reader.GetOrdinal("expiration"));
                                }
                            }
                        }
                    }

                    // 7. Si ya tiene un código, eliminarlo (omitimos tiempo de espera para permitir reenviar)
                    if (existingCode != null)
                    {
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "DELETE FROM recovery_codes WHERE code = @CODE";
                            cmd.Parameters.AddWithValue("@CODE", existingCode);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 8. Generar un nuevo código
                    string code = ComputeStringHash((candidateId ?? userId ?? clientUserId) + email + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                                  .Substring(0, 8)
                                  .ToUpper();

                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO recovery_codes (code, candidateId, userId, clientUserId)
                            VALUES (@CODE, @CANDIDATE, @USER, @CLIENTUSER)";

                        cmd.Parameters.AddWithValue("@CODE", code);
                        cmd.Parameters.AddWithValue("@CANDIDATE", (object)candidateId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@USER", (object)userId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CLIENTUSER", (object)clientUserId ?? DBNull.Value);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 9. Enviar el email
                    var inserts = new Dictionary<string, string>
                    {
                        ["url"] = InstallationConstants.PUBLIC_URL + "/access?recovery=" + code
                    };

                    string error = EventMailer.SendEmail(new EventMailer.Email
                    {
                        template = "recovery",
                        inserts = inserts,
                        toEmail = email,
                        subject = "[Think&Job] Recuperación de contraseña",
                        priority = EventMailer.EmailPriority.IMMEDIATE
                    }, _emailSettings);

                    if (error != null)
                    {
                        result = new { error };
                    }
                    else
                    {
                        result = new { error = false };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5571, no se ha podido generar el enlace de recuperación" };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "recover-password")]
        public async Task<IActionResult> ChangePass()
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("pass", out JsonElement passJson) && json.TryGetProperty("code", out JsonElement codeJson))
            {
                try
                {
                    string pass = passJson.GetString()?.Trim();
                    string code = codeJson.GetString()?.Trim();

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        //Obtener los datos del codigo de recuperacion
                        string candidateId = null, userId = null, clientUserId = null;
                        DateTime expiration = DateTime.Now;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT candidateId, userId, clientUserId, expiration FROM recovery_codes " +
                                "WHERE code LIKE @CODE";

                            command.Parameters.AddWithValue("@CODE", code);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    candidateId = reader.IsDBNull(reader.GetOrdinal("candidateId")) ? null : reader.GetString(reader.GetOrdinal("candidateId"));
                                    userId = reader.IsDBNull(reader.GetOrdinal("userId")) ? null : reader.GetString(reader.GetOrdinal("userId"));
                                    clientUserId = reader.IsDBNull(reader.GetOrdinal("clientUserId")) ? null : reader.GetString(reader.GetOrdinal("clientUserId"));
                                    expiration = reader.GetDateTime(reader.GetOrdinal("expiration"));
                                }
                            }
                        }

                        //De alguien tiene que ser
                        if (candidateId == null && userId == null && clientUserId == null)
                        {
                            return Ok(new { error = "Error 4413, codigo de recuperacion no reconocido." });
                        }

                        //Comprobar si esta caducado
                        int elapse = (int)(expiration - DateTime.Now).TotalSeconds;
                        if (elapse < 0)
                        {
                            return Ok(new { error = "Error 4414, el codigo de recuperación caduco hace " + HowManyDays(-elapse) + "." });
                        }

                        //Aplicar el cambio y recolectar datos para el email
                        string email = null, name = null;
                        if (candidateId != null)
                        {
                            string dni = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT CONCAT(nombre, ' ', apellidos) as name, email, dni FROM candidatos WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                        name = reader.GetString(reader.GetOrdinal("name"));
                                        dni = reader.GetString(reader.GetOrdinal("dni"));
                                    }
                                }
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE candidatos SET pwd = @PWD WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.Parameters.AddWithValue("@PWD", ComputeStringHash(pass));
                                command.ExecuteNonQuery();
                            }
                            if (dni != null) LogToDB(LogType.CANDIDATE_PASS_RECOVER, "Contraseña de candidato cambiada " + dni, null, conn);
                        }
                        if (userId != null)
                        {
                            string username = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT CONCAT(name, ' ', surname) as name, email, username FROM users WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", userId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                        name = reader.GetString(reader.GetOrdinal("name"));
                                        username = reader.GetString(reader.GetOrdinal("username"));
                                    }
                                }
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE users SET pwd = @PWD WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", userId);
                                command.Parameters.AddWithValue("@PWD", EncryptString(pass));
                                command.ExecuteNonQuery();
                            }
                            if (username != null) LogToDB(LogType.USER_PASS_RECOVER, "Contraseña de candidato cambiada " + username, null, conn);
                        }
                        if (clientUserId != null)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT nombre as name, email FROM client_users WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", clientUserId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                        name = reader.GetString(reader.GetOrdinal("name"));
                                    }
                                }
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE client_users SET pwd = @PWD WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", clientUserId);
                                command.Parameters.AddWithValue("@PWD", EncryptString(pass));
                                command.ExecuteNonQuery();
                            }
                            LogToDB(LogType.CLIENTUSER_PASS_RECOVER, "Contraseña de candidato cambiada " + email, null, conn);
                        }

                        //Enivar email
                        if (email != null && name != null)
                        {
                            EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "passwordChanged",
                                toEmail = email,
                                toName = name,
                                subject = "[Think&Job] Contraseña cambiada",
                                priority = EventMailer.EmailPriority.MODERATE
                            });

                            result = new { error = false };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5411, no se ha podido cambiar la contraseña." };
                }
            }

            return Ok(result);
        }

        // Version
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [Route(template: "version")]
        public IResult GetVersion()
        {
            return Results.Json(new { version = VERSION });
        }


        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------
        //public struct LoginData
        //{
        //    public bool found { get; set; }
        //    public object error { get; set; }
        //    public string token { get; set; }
        //    public string id { get; set; }
        //    public string username { get; set; }
        //    public string pwd { get; set; }
        //    public string docID { get; set; }
        //    public string docType { get; set; }
        //    public string name { get; set; }
        //    public string surname { get; set; }
        //    public string email { get; set; }
        //    public bool authorized { get; set; }
        //    public string securityToken { get; set; }
        //    public bool isExternal { get; set; }
        //    public bool hideSocieties { get; set; }
        //    public bool hasToShift { get; set; }
        //    public bool requiresKeepAlive { get; set; }
        //    public string photo { get; set; }
        //    public string type { get; set; }
        //    public string candidateId { get; set; }
        //    public string candidateDni { get; set; }
        //    public Object lastSignLink { get; set; }
        //    public DateTime lastAccess { get; set; }
        //    public string autoLoginToken { get; set; }
        //    public int? cstatus { get; set; }
        //    public int? periodoGracia { get; set; }
        //}

        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------

        #region Funciones Login

        private static LoginData tryLoginRJ(string username, string pwd, SqlConnection conn)
        {
            LoginData result = new LoginData { found = false };
            bool disabled = false;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM users WHERE pwd = @PWD AND email = @UNAME ";
                command.Parameters.AddWithValue("@UNAME", username);
                command.Parameters.AddWithValue("@PWD", EncryptString(pwd));

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        disabled = reader.GetInt32(reader.GetOrdinal("disabled")) == 1;
                        if (disabled)
                        {
                            result.error = "Error 4033, acceso denegado.";
                        }
                        else
                        {
                            string userId = reader.GetString(reader.GetOrdinal("id"));
                            result = new LoginData
                            {
                                error = false,
                                id = userId,
                                username = reader.GetString(reader.GetOrdinal("username")),
                                docID = reader.GetString(reader.GetOrdinal("DocID")),
                                docType = reader.GetString(reader.GetOrdinal("DocType")),
                                name = reader.GetString(reader.GetOrdinal("name")),
                                surname = reader.GetString(reader.GetOrdinal("surname")),
                                email = reader.GetString(reader.GetOrdinal("email")),
                                securityToken = reader.GetString(reader.GetOrdinal("securityToken")),
                                isExternal = reader.GetInt32(reader.GetOrdinal("isExternal")) == 1,
                                hideSocieties = reader.GetInt32(reader.GetOrdinal("hideSocieties")) == 1,
                                hasToShift = reader.GetInt32(reader.GetOrdinal("hasToShift")) == 1,
                                requiresKeepAlive = reader.GetInt32(reader.GetOrdinal("requiresKeepAlive")) == 1,
                                authorized = true,
                                type = "user",
                            };
                        }

                        result.found = true;
                    }
                    else
                    {
                        result = new LoginData
                        {
                            error = "Error 2930, credenciales incorrectas.",
                            authorized = false
                        };
                    }
                }
            }

            //Comprobar que tenga almenos un permiso
            if (result.found && !disabled && !CheckUserIsSuper(conn, null, null, null, result.username))
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM permisos_modulo WHERE userId = @ID";
                    command.Parameters.AddWithValue("@ID", result.id);
                    if ((int)command.ExecuteScalar() == 0)
                    {
                        result = new LoginData
                        {
                            error = "Error 2931, no tienes acceso a ningún módulo.",
                            found = true,
                            authorized = false
                        };
                    }
                }
            }

            return result;
        }

        private static LoginData tryLoginCA(string username, string pwd, SqlConnection conn)
        {
            LoginData result = new LoginData { found = false };

            bool failed = false;
            string id = null;
            DateTime? periodoGracia = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT id, dni, lastSignLink, email_verified, banned, terminosAceptados, ultimoAcceso, periodoGracia, " +
                    "workExists = CASE WHEN EXISTS(SELECT * FROM trabajos WHERE trabajos.signLink = candidatos.lastSignLink) THEN 1 ELSE 0 END " +
                    "FROM candidatos " +
                    "WHERE pwd = @PWD AND email = @EMAIL ";

                command.Parameters.AddWithValue("@EMAIL", username);
                command.Parameters.AddWithValue("@PWD", ComputeStringHash(pwd));

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        id = reader.GetString(reader.GetOrdinal("id"));
                        string dni = reader.GetString(reader.GetOrdinal("dni"));
                        string lastSignLink = reader.IsDBNull(reader.GetOrdinal("lastSignLink")) ? null : reader.GetString(reader.GetOrdinal("lastSignLink"));
                        bool emailVerified = reader.GetInt32(reader.GetOrdinal("email_verified")) == 1;
                        bool banned = reader.GetInt32(reader.GetOrdinal("banned")) == 1;
                        bool workExists = reader.GetInt32(reader.GetOrdinal("workExists")) == 1;
                        DateTime lastAccess = reader.GetDateTime(reader.GetOrdinal("ultimoAcceso"));
                        periodoGracia = reader.IsDBNull(reader.GetOrdinal("periodoGracia")) ? null : reader.GetDateTime(reader.GetOrdinal("periodoGracia"));

                        if (banned)
                        {
                            failed = true;
                            result.error = "Error 4033, acceso denegado.";
                        }
                        else
                        {
                            if (workExists)
                            {
                                if (emailVerified)
                                {
                                    result.id = id;
                                    result.error = false;
                                    result.candidateId = id;
                                    result.candidateDni = dni;
                                    result.lastSignLink = lastSignLink;
                                    result.lastAccess = lastAccess;
                                    result.type = "candidate";
                                    result.authorized = true;
                                }
                                else
                                {
                                    failed = true;
                                    result.error = "Error 4032, email no validado. Por favor, valide su email.";
                                }
                            }
                            else
                            {
                                failed = true;
                                result.error = "Error 4034, cuenta desactivada.";
                            }
                        }

                        result.found = true;
                    }
                    else
                    {
                        failed = true;
                        result.error = "Error 1031, credenciales incorrectas.";
                    }
                }
            }

            if (result.found && !failed)
            {
                UpdateLastAccess(conn, null, id);

                if (!periodoGracia.HasValue)
                {
                    periodoGracia = DateTime.Now.Date.AddDays(10);
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE candidatos SET periodoGracia = @DATE WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", id);
                        command.Parameters.AddWithValue("@DATE", periodoGracia.Value);
                        command.ExecuteNonQuery();
                    }
                }
                result.periodoGracia = (periodoGracia.Value - DateTime.Now.Date).Days;

                //Comprobar si tiene una baja vigente
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM incidencia_falta_asistencia WHERE candidateId = @CANDIDATE_ID AND baja = 1 AND bajaEnd IS NOT NULL AND bajaEnd > getdate() AND state = 'aceptada'";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", id);

                    try
                    {
                        if (command.ExecuteScalar() as Int32? != 0)
                        {
                            result.cstatus = 1;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return result;
        }

        private static LoginData tryLoginCL(string username, string pwd, SqlConnection conn)
        {
            LoginData result = new LoginData { found = false };

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM client_users WHERE pwd = @PWD AND email = @EMAIL ";
                command.Parameters.AddWithValue("@EMAIL", username);
                command.Parameters.AddWithValue("@PWD", EncryptString(pwd));
                var aaa = EncryptString(pwd);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string id = reader.GetString(reader.GetOrdinal("id"));
                        string token = reader.GetString(reader.GetOrdinal("token"));
                        string name = reader.GetString(reader.GetOrdinal("username"));
                        bool activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1;
                        if (!activo)
                        {
                            result = new LoginData { error = "Error 4033, acceso denegado" };
                        }
                        else
                        {
                            result = new LoginData
                            {
                                error = false,
                                id = id,
                                name = name,
                                token = token,
                                authorized = true,
                                type = "client"
                            };

                            result.found = true;
                        }
                    }
                    else
                    {
                        result = new LoginData
                        {
                            error = "Error 2930, credenciales incorrectas."
                        };
                    }
                }
            }

            //Comprobar que tenga acceso a almenos un centro
            if (result.found)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM client_user_centros WHERE clientUserId = @ID";
                    command.Parameters.AddWithValue("@ID", result.id);
                    if ((int)command.ExecuteScalar() == 0)
                    {
                        result = new LoginData
                        {
                            error = "Error 2931, no tienes acceso a ningún centro.",
                            found = true,
                            authorized = false
                        };
                    }
                }
            }

            //Actualizar la ultima fecha de acceso
            if (result.authorized)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "UPDATE client_users SET lastLogin = getdate() WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", result.id);
                    command.ExecuteNonQuery();
                }
            }

            return result;
        }

        #endregion

        public static string getAutoLoginUrl(string userType, string id, string token, SqlConnection lastConn, SqlTransaction transaction)
        {
            return InstallationConstants.PUBLIC_URL;
            /*
            string code = getAutoLoginString(userType, id, token, lastConn, transaction);
            if (code == null) return InstallationConstants.PUBLIC_URL;
            return InstallationConstants.PUBLIC_URL + "/access?auto=" + code;
            */
        }
        public static string getAutoLoginString(string userType, string id, string token, SqlConnection lastConn, SqlTransaction transaction)
        {
            string loginString = null;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    switch (userType)
                    {
                        case "ca":
                            command.CommandText = "SELECT id FROM candidatos WHERE id = @ID";
                            break;
                        case "rj":
                            if (id != null)
                                command.CommandText = "SELECT securityToken FROM users WHERE id = @ID";
                            else
                                command.CommandText = "SELECT securityToken FROM users WHERE securityToken = @TOKEN";
                            break;
                        case "cl":
                            if (id != null)
                                command.CommandText = "SELECT token FROM client_users WHERE id = @ID";
                            else
                                command.CommandText = "SELECT token FROM client_users WHERE token = @TOKEN";
                            break;
                        default:
                            command.CommandText = "SELECT NULL";
                            break;
                    }

                    command.Parameters.AddWithValue("@ID", (object)id ?? DBNull.Value);
                    command.Parameters.AddWithValue("@TOKEN", (object)token ?? DBNull.Value);

                    loginString = (string)command.ExecuteScalar();
                    if (loginString != null) loginString = Convert.ToBase64String(Encoding.ASCII.GetBytes(userType + "-" + loginString));
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception) { }
            return loginString;
        }

        public static LoginData getLoginData(string username, string password)
        {
            LoginData result = new LoginData
            {
                error = "Error 2932, no se ha podido procesar la petición.",
                authorized = false
            };
            try
            {                
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Intentar iniciar sesión como user
                    result = tryLoginRJ(username, password, conn);

                    //Intentar iniciar sesión como candidato
                    if (!result.found)
                    {
                        result = tryLoginCA(username, password, conn);
                    }

                    //Intentar iniciar sesión como usuario cliente
                    if (!result.found)
                    {
                        result = tryLoginCL(username, password, conn);
                    }
                }
                
                if (result.authorized)
                {
                    switch (result.type)
                    {
                        case "client":
                            result.autoLoginToken = getAutoLoginString("cl", result.id, null, null, null);
                            break;
                        case "candidate":
                            result.autoLoginToken = getAutoLoginString("ca", result.candidateId, null, null, null);
                            break;
                        case "user":
                            result.autoLoginToken = getAutoLoginString("rj", result.id, null, null, null);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                result = new LoginData
                {
                    error = "Error 5551, no se ha podido iniciar sesion: " + e.Message
                };
            }

            return result;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
