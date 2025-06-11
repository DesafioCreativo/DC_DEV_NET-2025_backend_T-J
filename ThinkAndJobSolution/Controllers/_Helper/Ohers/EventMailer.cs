using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers._Helper.Ohers
{
    public class EventMailer
    {
        public struct Attatchment
        {
            public string filename { get; set; }
            public string base64 { get; set; }
        }

        public struct EmailLog
        {
            public int id { get; set; }
            public string email { get; set; }
            public string username { get; set; }
            public DateTime date { get; set; }
            public string subject { get; set; }
            public string html { get; set; }
            public Attatchment[] attachments { get; set; }
        }

        public enum EmailPriority
        {
            MAXIMUN = 0,
            IMMEDIATE = 2,
            MODERATE = 4,
            SLOWLANE = 6
        }

        public struct Email
        {
            //Processed
            public string template { get; set; }
            public bool skipMasterTemplate { get; set; }
            public Dictionary<string, string> inserts { get; set; }
            public string username { get; set; }
            public List<Attatchment> attachments { get; set; }

            //Direct
            public int idOriginal { get; set; }
            public string toEmail { get; set; }
            public string toName { get; set; }
            public string fromName { get; set; }
            public string replyEmail { get; set; }
            public string replyName { get; set; }
            public string subject { get; set; }
            public string body { get; set; }
            public string attachmentsDir { get; set; }
            public EmailPriority priority { get; set; }
        }

        public static string SendEmail(Email mail)
        {
            string status = "Error no esperado al enviar email";

            //Crear el cuerpo del correo
            mail.body = null;
            try
            {
                mail.body = ApplyTemplate(mail.template, mail.inserts ?? new());
                if (!mail.skipMasterTemplate)
                {
                    mail.body = ApplyTemplate("masterTemplate", new Dictionary<string, string>() { { "html", mail.body } });
                }
            }
            catch (Exception e)
            {
                status = "No se ha podido aplicar la plantilla: " + e.Message;
            }

            if (mail.body != null)
            {
                int? logId = null;
                try
                {
                    //Establecer el nombre de quien envia el correo en base al usuario que lo envia
                    if (mail.username != null && mail.fromName == null)
                        mail.fromName = mail.username;

                    //Establecer el nombre de respuesta si tiene usuario e email de respuesta
                    if (mail.username != null && mail.replyEmail != null && mail.replyName == null)
                        mail.replyName = mail.username;

                    if (mail.subject == "Complaint" || mail.subject == "Suggestion")
                    {
                        logId = LogEmail(mail, mail.subject);
                    }
                    else
                    {
                        logId = LogEmail(mail);
                    }

                    if (logId == null)
                    {
                        throw new Exception("Error al logear");
                    }

                    mail.idOriginal = logId.Value;
                    if (mail.attachments != null && mail.attachments.Count > 0) mail.attachmentsDir = HelperMethods.ComposePath(new[] { "email", logId.Value.ToString() }, false);

                    if (HelperMethods.CARTERO_CONNECTION_STRING != null)
                    {
                        using (SqlConnection conn = new SqlConnection(HelperMethods.CARTERO_CONNECTION_STRING))
                        {
                            conn.Open();
                            using (SqlCommand command = new SqlCommand("dbo.sp_send_email", conn))
                            {
                                command.CommandText =
                                    "INSERT INTO mails " +
                                    "(id_original, to_email, to_name, from_name, reply_mail, reply_name, subject, body, attachments, priority) VALUES " +
                                    "(@OID, @TOMAIL, @TONAME, @FROMNAME, @REPLYMAIL, @REPLYNAME, @SUBJECT, @BODY, @ATTACHMENTS, @PRIORITY)";
                                command.Parameters.AddWithValue("@OID", mail.idOriginal);
                                command.Parameters.AddWithValue("@TOMAIL", mail.toEmail);
                                command.Parameters.AddWithValue("@TONAME", (object)mail.toName ?? DBNull.Value);
                                command.Parameters.AddWithValue("@FROMNAME", (object)mail.fromName ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REPLYMAIL", (object)mail.replyEmail ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REPLYNAME", (object)mail.replyName ?? DBNull.Value);
                                command.Parameters.AddWithValue("@SUBJECT", mail.subject);
                                command.Parameters.AddWithValue("@BODY", mail.body);
                                command.Parameters.AddWithValue("@ATTACHMENTS", (object)mail.attachmentsDir ?? DBNull.Value);
                                command.Parameters.AddWithValue("@PRIORITY", (int)mail.priority);
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                    status = null;
                }
                catch (Exception e)
                {
                    status = "No se ha podido enviar el email: " + e.Message;
                }
            }

            return status;
        }

        public static string ApplyTemplate(string template, Dictionary<string, string> inserts)
        {
            inserts["public_url"] = InstallationConstants.PUBLIC_URL;
            inserts["footer_current_year"] = DateTime.Now.Year.ToString();

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "EmailTemplates", template + ".html");
            string html = File.ReadAllText(filePath);

            foreach (KeyValuePair<string, string> insert in inserts)
            {
                html = html.Replace("{{" + insert.Key + "}}", insert.Value);
            }

            return html;
        }

        private static int? LogEmail(Email mail, string suggestionOrComplaintMailboxType = null)
        {
            int? logId = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;


                            if (!string.IsNullOrEmpty(suggestionOrComplaintMailboxType))
                            {
                                command.CommandText = "INSERT INTO suggestion_complaint_log (email, type, username, subject, html, priority) VALUES (@EMAIL, @TYPE, @USERNAME, @SUBJECT, @HTML, @PRIORITY)";

                                command.Parameters.AddWithValue("@EMAIL", mail.toEmail);
                                command.Parameters.AddWithValue("@TYPE", suggestionOrComplaintMailboxType);
                                command.Parameters.AddWithValue("@USERNAME", (object)mail.username ?? DBNull.Value);
                                command.Parameters.AddWithValue("@SUBJECT", mail.subject);
                                command.Parameters.AddWithValue("@HTML", mail.body);
                                command.Parameters.AddWithValue("@PRIORITY", (int)mail.priority);
                            }
                            else
                            {
                                command.CommandText = "INSERT INTO email_logs (email, username, subject, html) VALUES (@EMAIL, @USERNAME, @SUBJECT, @HTML)";

                                command.Parameters.AddWithValue("@EMAIL", mail.toEmail);
                                command.Parameters.AddWithValue("@USERNAME", (object)mail.username ?? DBNull.Value);
                                command.Parameters.AddWithValue("@SUBJECT", mail.subject);
                                command.Parameters.AddWithValue("@HTML", mail.body);
                            }

                            command.ExecuteNonQuery();
                        }

                        if (string.IsNullOrEmpty(suggestionOrComplaintMailboxType))
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT MAX(id) FROM email_logs";
                                logId = (int?)command.ExecuteScalar();
                            }
                            transaction.Commit();
                        }
                        else
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT MAX(id) FROM suggestion_complaint_log";
                                logId = (int?)command.ExecuteScalar();
                            }
                            transaction.Commit();


                        }
                    }
                }
                if (logId != null && mail.attachments != null)
                {
                    foreach (var attachment in mail.attachments)
                    {
                        HelperMethods.SaveFile(new[] { "email", logId.Value.ToString(), attachment.filename }, attachment.base64);
                    }
                }
            }
            catch (Exception) { }
            return logId;
        }
    }
}
