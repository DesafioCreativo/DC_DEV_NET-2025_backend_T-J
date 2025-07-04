using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;
using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers.Commons
{
    [Route("api/v1/communications")]
    [ApiController]
    [Authorize]
    public class EmailController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        // Usuario -> Candidato

        [HttpPost]
        [Route(template: "preview-email/candidate/")]
        public async Task<IActionResult> TestEmailCandidate()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.TestEmailCandidate", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("message_html", out JsonElement htmlJson))
                {
                    string html = htmlJson.GetString();

                    try
                    {
                        Dictionary<string, string> inserts = new Dictionary<string, string>();
                        inserts["html"] = html;
                        result = new
                        {
                            error = false,
                            html = EventMailer.ApplyTemplate("masterTemplate", new Dictionary<string, string>() { { "html", EventMailer.ApplyTemplate("candidateMessage", inserts) } })
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5590, no se ha podido aplicar la plantilla" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "send-email/candidate/{candidateId}/")]
        public async Task<IActionResult> SendEmailCandidate(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.SendEmailCandidate", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("title", out JsonElement titleJson) && json.TryGetProperty("message_html", out JsonElement htmlJson) &&
                    json.TryGetProperty("attachments", out JsonElement attachmentsJson))
                {
                    try
                    {
                        string title = titleJson.GetString();
                        string html = htmlJson.GetString();
                        List<EventMailer.Attatchment> attachments = new List<EventMailer.Attatchment>();

                        foreach (var attachmentJson in attachmentsJson.EnumerateArray())
                        {
                            attachments.Add(new EventMailer.Attatchment
                            {
                                filename = attachmentJson.GetProperty("filename").GetString(),
                                base64 = attachmentJson.GetProperty("base64").GetString()
                            });
                        }

                        string email = null, name = "";
                        FooterData footer;
                        using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                        {
                            conn.Open();

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT email, nombre FROM candidatos WHERE id = @CANDIDATE_ID";

                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                        name = reader.GetString(reader.GetOrdinal("nombre"));
                                    }
                                    else
                                    {
                                        result = new { error = "Error 4591, no se ha encontrado al candidato" };
                                    }
                                    if (email != null && email.Length == 0)
                                    {
                                        result = new { error = "Error 4591, el candidato no tiene ningún email verificado" };
                                        email = null;
                                    }
                                }
                            }

                            footer = getFooterBySecurityToken(securityToken, conn);
                        }

                        if (email != null)
                        {
                            Dictionary<string, string> inserts = new();
                            inserts["html"] = html;
                            string status = EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "candidateMessage",
                                inserts = inserts,
                                toEmail = email,
                                toName = name,
                                subject = title,
                                username = footer.username,
                                replyEmail = footer.email,
                                attachments = attachments,
                                priority = EventMailer.EmailPriority.IMMEDIATE
                            });
                            result = new
                            {
                                error = (object)status ?? false
                            };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 4590, parámetros incorrectos" };
                    }
                }
            }

            return Ok(result);
        }

        // Usuario -> Usuario de cliente

        [HttpPost]
        [Route(template: "preview-email/client-user/")]
        public async Task<IActionResult> TestEmailClientUser()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.TestEmailClientUser", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("message_html", out JsonElement htmlJson))
                {
                    string html = htmlJson.GetString();

                    try
                    {
                        Dictionary<string, string> inserts = new Dictionary<string, string>();
                        inserts["html"] = html;
                        result = new
                        {
                            error = false,
                            html = EventMailer.ApplyTemplate("masterTemplate", new Dictionary<string, string>() { { "html", EventMailer.ApplyTemplate("clientUserMessage", inserts) } })
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5590, no se ha podido aplicar la plantilla" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "send-email/client-user/{userId}/")]
        public async Task<IActionResult> SendEmailClientUSer(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.SendEmailClientUser", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("title", out JsonElement titleJson) && json.TryGetProperty("message_html", out JsonElement htmlJson) &&
                    json.TryGetProperty("attachments", out JsonElement attachmentsJson))
                {
                    try
                    {
                        string title = titleJson.GetString();
                        string html = htmlJson.GetString();
                        List<EventMailer.Attatchment> attachments = new List<EventMailer.Attatchment>();

                        foreach (var attachmentJson in attachmentsJson.EnumerateArray())
                        {
                            attachments.Add(new EventMailer.Attatchment
                            {
                                filename = attachmentJson.GetProperty("filename").GetString(),
                                base64 = attachmentJson.GetProperty("base64").GetString()
                            });
                        }

                        string email = null, username = "";
                        FooterData footer;
                        using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                        {
                            conn.Open();

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT email, username FROM client_users WHERE id = @USER_ID";

                                command.Parameters.AddWithValue("@USER_ID", userId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                        username = reader.GetString(reader.GetOrdinal("username"));
                                    }
                                    else
                                    {
                                        result = new { error = "Error 4591, no se ha encontrado al usuario" };
                                    }
                                }
                            }

                            footer = getFooterBySecurityToken(securityToken, conn);
                        }

                        if (email != null)
                        {
                            Dictionary<string, string> inserts = new();
                            inserts["html"] = html;
                            string status = EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "clientUserMessage",
                                inserts = inserts,
                                toEmail = email,
                                toName = username,
                                subject = title,
                                username = footer.username,
                                replyEmail = footer.email,
                                attachments = attachments,
                                priority = EventMailer.EmailPriority.IMMEDIATE
                            });
                            result = new
                            {
                                error = (object)status ?? false
                            };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 4590, parámetros incorrectos" };
                    }
                }
            }

            return Ok(result);
        }

        // Usuario -> Empresa

        [HttpPost]
        [Route(template: "preview-email/company/")]
        public async Task<IActionResult> TestEmailCompany()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.TestEmailCompany", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("message_html", out JsonElement htmlJson))
                {
                    string html = htmlJson.GetString();

                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                    }

                    try
                    {
                        Dictionary<string, string> inserts = new Dictionary<string, string>();
                        inserts["html"] = html;
                        result = new
                        {
                            error = false,
                            html = EventMailer.ApplyTemplate("masterTemplate", new Dictionary<string, string>() { { "html", EventMailer.ApplyTemplate("companyMessage", inserts) } })
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5590, no se ha podido aplicar la plantilla" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "send-email/company/{companyId}/")]
        public async Task<IActionResult> SendEmailCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.SendEmailCompany", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("title", out JsonElement titleJson) && json.TryGetProperty("message_html", out JsonElement htmlJson) &&
                    json.TryGetProperty("attachments", out JsonElement attachmentsJson))
                {
                    try
                    {
                        string title = titleJson.GetString();
                        string html = htmlJson.GetString();
                        List<EventMailer.Attatchment> attachments = new List<EventMailer.Attatchment>();

                        foreach (var attachmentJson in attachmentsJson.EnumerateArray())
                        {
                            attachments.Add(new EventMailer.Attatchment
                            {
                                filename = attachmentJson.GetProperty("filename").GetString(),
                                base64 = attachmentJson.GetProperty("base64").GetString()
                            });
                        }

                        bool failed = false;
                        string email = null, name = "";
                        FooterData footer;
                        using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                        {
                            conn.Open();

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT emailRRHH, nombreRRHH FROM empresas WHERE id = @COMPANY_ID";

                                command.Parameters.AddWithValue("@COMPANY_ID", companyId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        email = reader.IsDBNull(reader.GetOrdinal("emailRRHH")) ? null : reader.GetString(reader.GetOrdinal("emailRRHH"));
                                        name = reader.IsDBNull(reader.GetOrdinal("nombreRRHH")) ? null : reader.GetString(reader.GetOrdinal("nombreRRHH"));
                                    }
                                    else
                                    {
                                        failed = true;
                                        result = new { error = "Error 4591, no se ha encontrado la empresa" };
                                    }
                                }
                            }

                            footer = getFooterBySecurityToken(securityToken, conn);
                        }

                        if (failed)
                        {
                            return Ok(result);
                        }

                        if (email == null || name == null)
                        {
                            result = new
                            {
                                error = "Error 4592, la empresa no tiene datos de contacto"
                            };
                        }
                        else
                        {
                            Dictionary<string, string> inserts = new();
                            inserts["html"] = html;
                            string status = EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "companyMessage",
                                inserts = inserts,
                                toEmail = email,
                                toName = name,
                                subject = title,
                                username = footer.username,
                                replyEmail = footer.email,
                                attachments = attachments,
                                priority = EventMailer.EmailPriority.IMMEDIATE
                            });
                            result = new
                            {
                                error = (object)status ?? false
                            };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 4590, parámetros incorrectos" };
                    }
                }
            }

            return Ok(result);
        }

        // Usuario -> Usuario (Asistencia)

        [HttpPost]
        [Route(template: "preview-email/support/")]
        public async Task<IActionResult> TestEmailSupport()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.TestEmailSupport", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("message_html", out JsonElement htmlJson))
                {
                    string html = htmlJson.GetString();

                    try
                    {
                        Dictionary<string, string> inserts = new Dictionary<string, string>();
                        inserts["html"] = html;
                        result = new
                        {
                            error = false,
                            html = EventMailer.ApplyTemplate("masterTemplate", new Dictionary<string, string>() { { "html", EventMailer.ApplyTemplate("supportMessage", inserts) } })
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5590, no se ha podido aplicar la plantilla" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "send-email/support/{category}/")]
        public async Task<IActionResult> SendEmailSupport(string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Message.SendEmailSupport", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("title", out JsonElement titleJson) && json.TryGetProperty("message_html", out JsonElement htmlJson) &&
                    json.TryGetProperty("attachments", out JsonElement attachmentsJson))
                {
                    try
                    {
                        string title = titleJson.GetString();
                        string html = htmlJson.GetString();
                        List<EventMailer.Attatchment> attachments = new List<EventMailer.Attatchment>();

                        foreach (var attachmentJson in attachmentsJson.EnumerateArray())
                        {
                            attachments.Add(new EventMailer.Attatchment
                            {
                                filename = attachmentJson.GetProperty("filename").GetString(),
                                base64 = attachmentJson.GetProperty("base64").GetString()
                            });
                        }

                        string email = null, name = "";
                        FooterData footer;
                        using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                        {
                            conn.Open();

                            switch (category)
                            {
                                case "rrhh":
                                    email = "c.gutierrez@thinkandjob.com";
                                    name = "Cynthia Gutierrez";
                                    break;
                                default:
                                    email = "info@thinkandjob.com";
                                    name = "Info";
                                    break;
                            }

                            footer = getFooterBySecurityToken(securityToken, conn);
                        }

                        if (email != null)
                        {
                            Dictionary<string, string> inserts = new();
                            inserts["html"] = html;
                            string status = EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "supportMessage",
                                inserts = inserts,
                                toEmail = email,
                                toName = name,
                                subject = title,
                                username = footer.username,
                                replyEmail = footer.email,
                                attachments = attachments,
                                priority = EventMailer.EmailPriority.IMMEDIATE
                            });
                            result = new
                            {
                                error = (object)status ?? false
                            };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 4590, parámetros incorrectos" };
                    }
                }
            }

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost]
        [Route(template: "suggestions-and-complaints")]
        public async Task<IActionResult> SendSuggestionAndComplints(string? type, string? description)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("type", out JsonElement typeJson) && json.TryGetProperty("description", out JsonElement descriptionJson))
            {
                try
                {
                    Dictionary<string, string> inserts = new()
                    {
                        ["html"] = "<br>texto del mensaje <b>(" + (typeJson.GetString() == "complaint" ? "QUEJA" : "SUGERENCIA") + ")</b>:<br><br>" + descriptionJson.GetString().Replace("\n", "<br>") + "<br><br>"
                    };
                    result = new
                    {
                        error = EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "supportMessage",
                            inserts = inserts,
                            toEmail = InstallationConstants.SUGGESTIONS_AND_COMPLAINTS_EMAIL,
                            toName = "Quejas y Sugerencias",
                            subject = "REGISTRO DE " + (typeJson.GetString() == "complaint" ? "QUEJA" : "SUGERENCIA"),
                            priority = typeJson.GetString() == "complaint" ? EventMailer.EmailPriority.MAXIMUN : EventMailer.EmailPriority.SLOWLANE,
                        })
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 4590, parámetros incorrectos" };
                }
            }

            return Ok(result);
        }

        // Firma de RJ Users

        [HttpGet]
        [Route(template: "rj-email-sign/")]
        public IActionResult GetSignRjUser()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;

            if (!HelperMethods.HasPermission("Message.GetSignRjUser", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                FooterData footer;
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    footer = getFooterBySecurityToken(securityToken, conn);
                }

                try
                {
                    Dictionary<string, string> inserts = new Dictionary<string, string>();
                    inserts["firma_nombre_completo"] = footer.nombre;
                    inserts["firma_puesto"] = footer.puesto;
                    inserts["firma_email"] = footer.email;
                    result = new
                    {
                        error = false,
                        html = EventMailer.ApplyTemplate("rjUserSign", inserts)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5590, no se ha podido aplicar la plantilla" };
                }
            }

            return Ok(result);
        }

        // Logs

        [HttpPost]
        [Route(template: "email-log/list/")]
        public async Task<IActionResult> ListEmailLogs()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            HelperMethods.ResultadoAcceso acceso = HelperMethods.HasPermission("Message.ListEmailLog", securityToken);
            if (!acceso.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }
            else
            {

                using System.IO.StreamReader readerBody = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("title", out JsonElement titleJson) && json.TryGetProperty("remitente", out JsonElement remitenteJson) &&
                    json.TryGetProperty("receptor", out JsonElement receptorJson) &&
                    json.TryGetProperty("page", out JsonElement pageJson) && json.TryGetProperty("perpage", out JsonElement perpageJson))
                {

                    string title = titleJson.GetString();
                    string remitente = remitenteJson.GetString();
                    string receptor = receptorJson.GetString();
                    string pageString = pageJson.GetString();
                    string perpageString = perpageJson.GetString();
                    int page = Int32.Parse(pageString);
                    int perpage = Int32.Parse(perpageString);

                    if (!acceso.EsJefe)
                    {
                        remitente = HelperMethods.FindUsernameBySecurityToken(securityToken);
                    }

                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();

                        try
                        {
                            List<EventMailer.EmailLog> logs = new List<EventMailer.EmailLog>();
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT " +
                                    "EL.id, " +
                                    "EL.email, " +
                                    "EL.username, " +
                                    "EL.date, " +
                                    "EL.subject " +
                                    "FROM email_logs EL " +
                                    "LEFT OUTER JOIN candidatos C ON EL.email = C.email " +
                                    "WHERE (@TITLE IS NULL OR subject LIKE @TITLE) AND " +
                                    "(@REMITENTE IS NULL OR (@REMITENTE = '%SISTEMA%' AND EL.username IS NULL) OR EL.username LIKE @REMITENTE) AND " +
                                    "(@RECEPTOR IS NULL OR EL.email LIKE @RECEPTOR OR C.dni LIKE @RECEPTOR OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @RECEPTOR) " +
                                    "ORDER BY [date] DESC " +
                                    "OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY";

                                command.Parameters.AddWithValue("@TITLE", ((object)(title == null ? null : ("%" + title + "%"))) ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REMITENTE", ((object)(remitente == null ? null : ("%" + remitente + "%"))) ?? DBNull.Value);
                                command.Parameters.AddWithValue("@RECEPTOR", ((object)(receptor == null ? null : ("%" + receptor + "%"))) ?? DBNull.Value);
                                command.Parameters.AddWithValue("@OFFSET", page * perpage);
                                command.Parameters.AddWithValue("@LIMIT", perpage);


                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        logs.Add(readEmailLog(reader));
                                    }
                                }
                            }
                            result = new { error = false, logs };
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5701, no han podido listar los logs" };
                        }

                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "email-log/list-count/")]
        public async Task<IActionResult> ListEmailLogsCount()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            HelperMethods.ResultadoAcceso acceso = HelperMethods.HasPermission("Message.ListEmailLogCount", securityToken);
            if (!acceso.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }
            else
            {

                using System.IO.StreamReader readerBody = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("title", out JsonElement titleJson) && json.TryGetProperty("remitente", out JsonElement remitenteJson) &&
                    json.TryGetProperty("receptor", out JsonElement receptorJson))
                {

                    string title = titleJson.GetString();
                    string remitente = remitenteJson.GetString();
                    string receptor = receptorJson.GetString();

                    if (!acceso.EsJefe)
                    {
                        remitente = HelperMethods.FindUsernameBySecurityToken(securityToken);
                    }

                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();

                        try
                        {
                            int logs = 0;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT " +
                                    "EL.id, " +
                                    "EL.email, " +
                                    "EL.username, " +
                                    "EL.date, " +
                                    "EL.subject " +
                                    "FROM email_logs EL " +
                                    "LEFT OUTER JOIN candidatos C ON EL.email = C.email " +
                                    "WHERE (@TITLE IS NULL OR subject LIKE @TITLE) AND " +
                                    "(@REMITENTE IS NULL OR (@REMITENTE = 'SISTEMA' AND EL.username IS NULL) OR EL.username LIKE @REMITENTE) AND " +
                                    "(@RECEPTOR IS NULL OR EL.email LIKE @RECEPTOR OR C.dni LIKE @RECEPTOR OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @RECEPTOR) ";

                                command.Parameters.AddWithValue("@TITLE", ((object)(title == null ? null : ("%" + title + "%"))) ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REMITENTE", ((object)(remitente == null ? null : ("%" + remitente + "%"))) ?? DBNull.Value);
                                command.Parameters.AddWithValue("@RECEPTOR", ((object)(receptor == null ? null : ("%" + receptor + "%"))) ?? DBNull.Value);


                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        logs++;
                                    }
                                }
                            }
                            result = new { error = false, logs };
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5701, no han podido contar los logs" };
                        }

                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "email-log/{emailId}/")]
        public IActionResult GetEmailLog(string emailId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            SendedEmailLog result = new SendedEmailLog
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            HelperMethods.ResultadoAcceso acceso = HelperMethods.HasPermission("Message.GetEmailLog", securityToken);
            if (!acceso.Acceso)
            {
                result = new SendedEmailLog
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                bool failed = true;
                string senderUsername = null;
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    EventMailer.EmailLog? log = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT EL.id, EL.email, EL.username, EL.date, EL.subject, EL.html FROM email_logs EL WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", emailId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                log = readEmailLog(reader, true);
                                failed = false;
                                senderUsername = log.Value.username;
                            }
                            else
                            {
                                result = new SendedEmailLog { error = "Error 4993, registro de email no encontrado" };
                            }
                        }
                    }
                    if (!failed)
                    {
                        object candidate = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT C.dni, C.telefono, CONCAT(C.nombre, ' ', C.apellidos) as nombreCompleto FROM candidatos C WHERE C.email = @EMAIL";
                            command.Parameters.AddWithValue("@EMAIL", log.Value.email);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    candidate = new
                                    {
                                        dni = reader.GetString(reader.GetOrdinal("dni")),
                                        telefono = reader.GetString(reader.GetOrdinal("telefono")),
                                        nombre = reader.GetString(reader.GetOrdinal("nombreCompleto"))
                                    };
                                }
                            }
                        }
                        result = new SendedEmailLog
                        {
                            error = false,
                            log = log.Value,
                            candidate = candidate,
                            isSent = null
                        };
                    }
                }
                if (!failed && HelperMethods.CARTERO_CONNECTION_STRING != null)
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CARTERO_CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT date_send, error, priority FROM logs WHERE id_original = @ID";
                            command.Parameters.AddWithValue("@ID", emailId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    result.sendDate = reader.GetDateTime(reader.GetOrdinal("date_send"));
                                    result.sendError = reader.IsDBNull(reader.GetOrdinal("error")) ? null : reader.GetString(reader.GetOrdinal("error"));
                                    result.sendPriority = reader.GetInt32(reader.GetOrdinal("priority"));
                                    result.isSent = true;
                                }
                                else result.isSent = false;
                            }
                        }
                    }
                }
                if (!failed)
                {
                    if (!acceso.EsJefe)
                    {
                        string username = HelperMethods.FindUsernameBySecurityToken(securityToken);
                        if (!username.Equals(senderUsername))
                        {
                            return Ok(new { error = "Error 4933, no puedes ver un email que no has enviado" });
                        }
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "email-log/list-by-target/")]
        public async Task<IActionResult> ListEmailLogsByTarget()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HelperMethods.HasPermission("Message.ListEmailLogsByTarget", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }
            using StreamReader readerBody = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("target", out JsonElement targetJson) && json.TryGetProperty("system", out JsonElement systemJson))
            {

                string target = targetJson.GetString() ?? "";
                bool? system = HelperMethods.GetJsonBool(systemJson);

                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        List<EventMailer.EmailLog> logs = new List<EventMailer.EmailLog>();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT " +
                                "EL.id, " +
                                "EL.email, " +
                                "EL.username, " +
                                "EL.date, " +
                                "EL.subject " +
                                "FROM email_logs EL " +
                                "WHERE (EL.email = @TARGET) AND " +
                                "(@SYSTEM IS NULL OR (@SYSTEM = 1 AND EL.username IS NULL) OR (@SYSTEM = 0 AND EL.username IS NOT NULL)) " +
                                "ORDER BY [date] DESC ";

                            command.Parameters.AddWithValue("@TARGET", target);
                            command.Parameters.AddWithValue("@SYSTEM", system == null ? DBNull.Value : (system.Value ? 1 : 0));


                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    logs.Add(readEmailLog(reader));
                                }
                            }
                        }
                        result = new { error = false, logs };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5701, no han podido listar los logs" };
                    }

                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------

        //Ayuda

        public struct SendedEmailLog
        {
            public object error { get; set; }
            public EventMailer.EmailLog log { get; set; }
            public object candidate { get; set; }
            public bool? isSent { get; set; }
            public string sendError { get; set; }
            public DateTime sendDate { get; set; }
            public int sendPriority { get; set; }
        }

        public struct FooterData
        {
            public string username { get; set; }
            public string email { get; set; }
            public string nombre { get; set; }
            public string puesto { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        private EventMailer.EmailLog readEmailLog(SqlDataReader reader, bool full = false)
        {
            int emailId = reader.GetInt32(reader.GetOrdinal("id"));
            string[] files;
            if (full)
            {
                files = HelperMethods.ListFiles(new[] { "email", emailId.ToString() });
            }
            else
            {
                files = new string[0];
            }
            EventMailer.EmailLog log = new EventMailer.EmailLog()
            {
                id = emailId,
                email = reader.GetString(reader.GetOrdinal("email")),
                username = reader.IsDBNull(reader.GetOrdinal("username")) ? null : reader.GetString(reader.GetOrdinal("username")),
                date = reader.GetDateTime(reader.GetOrdinal("date")),
                subject = reader.GetString(reader.GetOrdinal("subject")),
                html = full ? reader.GetString(reader.GetOrdinal("html")) : null,
                attachments = new EventMailer.Attatchment[files.Length]
            };
            if (full)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    log.attachments[i] = new EventMailer.Attatchment()
                    {
                        filename = Path.GetFileName(files[i]),
                        base64 = HelperMethods.ReadFile(new[] { "email", emailId.ToString(), files[i] })
                    };
                }
            }
            return log;
        }

        private FooterData getFooterBySecurityToken(string securityToken, SqlConnection conn, SqlTransaction transaction = null)
        {
            FooterData data = new FooterData();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT email, CONCAT(name, ' ', surname) as fullName, department, username FROM users WHERE securityToken = @SECURITY_TOKEN";
                command.Parameters.AddWithValue("@SECURITY_TOKEN", securityToken);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        data.username = reader.GetString(reader.GetOrdinal("username"));
                        data.email = reader.GetString(reader.GetOrdinal("email"));
                        data.nombre = reader.GetString(reader.GetOrdinal("fullName"));
                        data.puesto = reader.GetString(reader.GetOrdinal("department")).ToUpper();
                    }
                }
            }

            return data;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
