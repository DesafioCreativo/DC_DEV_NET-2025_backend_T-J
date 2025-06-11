using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using System.IO.Compression;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers.Commons;
using iTextSharp.text.pdf;
using iTextSharp.text;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    [Route("api/v1/candidate-doc")]
    [ApiController]
    [Authorize]
    public class CandidateDocsController : ControllerBase
    {

        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "list/")]
        public IActionResult ListDocs()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    result = new { error = false, docs = listDocs(candidateId, conn) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5420, No se han podido listar los documentos" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-templates/")]
        public IActionResult ListTemplates()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HelperMethods.HasPermission("CandidateDocs.ListTemplates", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    result = new { error = false, templates = listTemplates(conn) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5430, No se han podido listar las plantillas" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-batchs/")]
        public IActionResult ListBatchs()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.ListBatchs", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    List<CandidateDocBatch> batchs = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT CDTB.id as batchId, CDTB.templateId, CDTB.date, CDT.name," +
                            "total = (SELECT COUNT(*) FROM candidate_doc_template_asignation WHERE batchId = CDTB.id), " +
                            "seen = (SELECT COUNT(*) FROM candidate_doc_template_asignation WHERE batchId = CDTB.id AND seenDate IS NOT NULL), " +
                            "signed = (SELECT COUNT(*) FROM candidate_doc_template_asignation WHERE batchId = CDTB.id AND signDate IS NOT NULL)," +
                            "needsSign = CASE WHEN CDT.signPlacement IS NULL THEN 0 ELSE 1 END " +
                            "FROM candidate_doc_template_batch CDTB " +
                            "INNER JOIN candidate_doc_template CDT ON(CDT.id = CDTB.templateId) " +
                            "ORDER BY CDTB.date DESC";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                batchs.Add(new CandidateDocBatch()
                                {
                                    id = reader.GetString(reader.GetOrdinal("batchId")),
                                    templateId = reader.GetString(reader.GetOrdinal("templateId")),
                                    name = reader.GetString(reader.GetOrdinal("name")),
                                    date = reader.GetDateTime(reader.GetOrdinal("date")),
                                    nCandidates = reader.GetInt32(reader.GetOrdinal("total")),
                                    nSeen = reader.GetInt32(reader.GetOrdinal("seen")),
                                    nSigned = reader.GetInt32(reader.GetOrdinal("signed")),
                                    needsSign = reader.GetInt32(reader.GetOrdinal("needsSign")) == 1
                                });
                            }
                        }
                    }
                    result = new { error = false, batchs };
                }
                catch (Exception)
                {
                    return Ok(new { error = "Error 5435, No se han podido listar los envios" });
                }
            }
            return Ok(result);
        }

        //Obtencion
        [HttpGet]
        [Route(template: "get/{docId}")]
        public IActionResult GetDoc(string docId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    CandidateDoc? doc = getDoc(docId, conn);

                    if (doc.HasValue)
                    {
                        result = new { error = false, doc = doc.Value };
                    }
                    else
                    {
                        result = new { error = "Error 5421, No se ha encontrado el documento" };
                    }

                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5421, No se han podido obtener el documento" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-template/{templateId}")]
        public IActionResult GetTemplate(string templateId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    CandidateDocTemplate? template = template = getTemplate(templateId, conn, null, true);

                    if (template.HasValue)
                    {
                        result = new { error = false, template = template.Value };
                    }
                    else
                    {
                        result = new { error = "Error 4431, No se ha encontrado la plantilla" };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5431, No se han podido obtener la plantilla" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-batch/{batchId}")]
        public IActionResult GetBatch(string batchId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    CandidateDocBatch? batch = getBatch(batchId, conn);
                    if (batch.HasValue)
                    {
                        result = new { error = false, batch = batch.Value };
                    }
                    else
                    {
                        result = new { error = "Error 4432, No se ha encontrado el envio" };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5436, No se han podido obtener el envio" };
            }
            return Ok(result);
        }

        //Creacion
        [HttpPost]
        [Route(template: "create/{candidateId}/")]
        public async Task<IActionResult> Create(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.Create", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("name", out JsonElement nameJson) &&
                json.TryGetProperty("description", out JsonElement descriptionJson) &&
                json.TryGetProperty("filename", out JsonElement filenameJson) &&
                json.TryGetProperty("needsSign", out JsonElement needsSignJson) &&
                json.TryGetProperty("base64", out JsonElement base64Json))
            {
                CandidateDoc doc = new CandidateDoc()
                {
                    candidateId = candidateId,
                    name = nameJson.GetString(),
                    description = descriptionJson.GetString(),
                    filename = filenameJson.GetString(),
                    needsSign = HelperMethods.GetJsonBool(needsSignJson) ?? false
                };
                string base64 = base64Json.GetString();

                try
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        //Buscar los datos del candidato
                        string email, name;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT TRIM(CONCAT(nombre, ' ', apellidos)) as name, email FROM candidatos WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", doc.candidateId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    email = reader.GetString(reader.GetOrdinal("email"));
                                    name = reader.GetString(reader.GetOrdinal("name"));
                                }
                                else
                                    return Ok(new { error = "Error 4422, candidato no encontrado" });
                            }
                        }
                        string id = createDoc(doc, base64, conn);
                        EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "candidateRequiredDoc",
                            inserts = new() { { "url", AccessController.getAutoLoginUrl("ca", candidateId, null, conn, null) }, { "sign_insert", doc.needsSign ? ", que requiere firma" : "" } },
                            toEmail = email,
                            toName = name,
                            subject = doc.needsSign ? "[Think&Job] Documento requiere firma" : "[Think&Job] Documento recibido",
                            priority = EventMailer.EmailPriority.MODERATE
                        });
                        await PushNotificationController.sendNotification(new() { type = "ca", id = candidateId }, new()
                        {
                            title = doc.needsSign ? "Documento pendiente de firma" : "Documento recibido",
                            body = doc.name,
                            type = "candidate-documento-requerido",
                            data = new() { { "id", id } }
                        }, conn);

                        result = new { error = false, id };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5422, No se ha podido crear el documento" };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "create-template/")]
        public async Task<IActionResult> CreateTemplate()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.CreateTemplate", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetString("name", out string name) &&
                json.TryGetString("description", out string description) &&
                json.TryGetString("filename", out string filename) &&
                json.TryGetProperty("signPlacement", out JsonElement signPlacementJson) &&
                json.TryGetString("base64", out string base64))
            {
                CandidateDocTemplate template = new()
                {
                    name = name,
                    description = description,
                    filename = filename,
                    signPlacement = parsePageLayout(signPlacementJson)
                };
                if (template.signPlacement == null)
                {
                    return Ok(new { error = "Error 4438, No se permite crear una plantilla que no sea firmable" });
                }
                try
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        createTemplate(template, base64, conn);
                        result = new { error = false };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5432, No se ha podido crear la plantilla" };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "create-batch/")]
        public async Task<IActionResult> CreateBatch()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.CreateBatch", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetString("templateId", out string templateId) &&
                json.TryGetString("pdf", out string pdf) &&
                json.TryGetString("name", out string name) &&
                json.TryGetString("description", out string description) &&
                json.TryGetString("filename", out string filename) &&
                json.TryGetStringList("candidates", out List<string> candidates))
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string id = await createBatch(candidates, new()
                            {
                                id = templateId,
                                pdf = pdf,
                                name = name,
                                description = description,
                                filename = filename
                            }, conn, transaction);

                            transaction.Commit();
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            return Ok(new { error = "Error 5434, No se han podido enviar los documentos" });
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "create-batch-multi/")]
        public async Task<IActionResult> CreateBatchMulti()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.CreateBatch", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("templates", out JsonElement templatesJson) &&
                json.TryGetStringList("candidates", out List<string> candidates))
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (JsonElement templateJson in templatesJson.EnumerateArray())
                            {
                                if (templateJson.TryGetString("id", out string id) &&
                                   templateJson.TryGetString("pdf", out string pdf) &&
                                   templateJson.TryGetString("name", out string name) &&
                                   templateJson.TryGetString("description", out string description) &&
                                   templateJson.TryGetString("filename", out string filename))
                                {
                                    await createBatch(candidates, new()
                                    {
                                        id = id,
                                        pdf = pdf,
                                        name = name,
                                        description = description,
                                        filename = filename
                                    }, conn, transaction, false);
                                }
                            }
                            //Notificar a los candidatos
                            Dictionary<string, string> emails = new();
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT email FROM candidatos WHERE id = @ID";
                                command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                                foreach (string candidateId in candidates)
                                {
                                    command.Parameters["@ID"].Value = candidateId;
                                    using (SqlDataReader reader = command.ExecuteReader())
                                        if (reader.Read())
                                            emails[candidateId] = reader.GetString(reader.GetOrdinal("email"));
                                }
                            }
                            candidates = candidates.Where(id => emails.ContainsKey(id)).ToList();

                            foreach (string candidateId in candidates)
                            {
                                EventMailer.SendEmail(new EventMailer.Email()
                                {
                                    template = "candidateRequiredDoc",
                                    inserts = new() { { "url", AccessController.getAutoLoginUrl("ca", candidateId, null, conn, null) }, { "sign_insert", "" } },
                                    toEmail = emails[candidateId],
                                    subject = "[Think&Job] Documento recibido",
                                    priority = EventMailer.EmailPriority.MODERATE
                                });
                            }

                            await PushNotificationController.sendNotifications(candidates.Select(id => new PushNotificationController.UID() { type = "ca", id = id }), new()
                            {
                                title = "Documento recibido",
                                type = "candidate-documento-requerido"
                            }, conn, transaction);


                            transaction.Commit();
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            return Ok(new { error = "Error 5434, No se han podido enviar los documentos" });
                        }
                    }
                }
            }
            return Ok(result);
        }

        //Actualizacion
        [HttpPut]
        [Route(template: "/edit-template/{templateId}/")]
        public async Task<IActionResult> EditTemplate(string templateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.EditTemplate", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("name", out string name) &&
                json.TryGetString("description", out string description) &&
                json.TryGetProperty("signPlacement", out JsonElement signPlacementJson))
            {
                CandidateDocTemplate template = new()
                {
                    id = templateId,
                    name = name,
                    description = description,
                    signPlacement = parsePageLayout(signPlacementJson)
                };
                try
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        if (getTemplate(templateId, conn) == null)
                            return Ok(new { error = "Error 4430, La plantilla no existe" });
                        updateTemplate(template, conn);
                        result = new { error = false };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5435, No se ha podido modificar la plantilla" };
                }
            }
            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "edit-batch/{batchId}/")]
        public async Task<IActionResult> EditBatch(string batchId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.EditBatch", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetStringList("remove", out List<string> remove) &&
                json.TryGetStringList("add", out List<string> add))
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    //Si la ID es de una asignacion, obtener la ID de su batch
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT batchId FROM candidate_doc_template_asignation WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", batchId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                batchId = reader.GetString(reader.GetOrdinal("batchId"));
                    }
                    bool requiresSign;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT CASE WHEN CDT.signPlacement IS NULL THEN 0 ELSE 1 END " +
                            "FROM candidate_doc_template CDT " +
                            "INNER JOIN candidate_doc_template_batch CDTB ON(CDT.id = CDTB.templateId) " +
                            "WHERE CDTB.id = @ID";
                        command.Parameters.AddWithValue("@ID", batchId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                requiresSign = reader.GetInt32(0) == 1;
                            else
                                return Ok(new { error = "Error 4431, El envio no existe" });
                    }
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            //Agregar a los nuevos
                            await addCandidatesToBatch(add, batchId, requiresSign, conn, transaction);
                            //Eliminar los antiguos
                            removeCandidatesToBatch(remove, batchId, conn, transaction);
                            //Comprobar cuantos candidatos tiene
                            bool deleted;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM candidate_doc_template_asignation WHERE batchId = @ID";
                                command.Parameters.AddWithValue("@ID", batchId);
                                deleted = (int)command.ExecuteScalar() == 0;
                            }
                            if (deleted)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM candidate_doc_template_batch WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ID", batchId);
                                    command.ExecuteNonQuery();
                                }
                            }
                            transaction.Commit();
                            result = new { error = false, deleted };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5435, No se ha podido modificar la plantilla" };
                        }
                    }
                }
            }
            return Ok(result);
        }

        //Eliminacion
        [HttpDelete]
        [Route(template: "delete/{docId}/")]
        public IActionResult Delete(string docId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.Delete", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    CandidateDoc? doc = getDoc(docId, conn);
                    if (doc == null)
                        return Ok(new { error = "Error 4420, El documento no existe" });
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM candidate_docs WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", docId);
                        command.ExecuteNonQuery();
                    }
                    HelperMethods.DeleteDir(new[] { "candidate", doc.Value.candidateId, "docs", docId });
                    result = new { error = false };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5425, No se ha podido eliminar el documento" };
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "delete-template/{templateId}/")]
        public IActionResult DeleteTemplate(string templateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.DeleteTemplate", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                CandidateDocTemplate? template = getTemplate(templateId, conn);
                if (template == null)
                    return Ok(new { error = "Error 4430, La plantilla no existe" });
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        //Obtener los batchs que tiene
                        List<string> batchs = new();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT id FROM candidate_doc_template_batch WHERE templateId = @ID";
                            command.Parameters.AddWithValue("@ID", templateId);
                            using (SqlDataReader reader = command.ExecuteReader())
                                while (reader.Read())
                                    batchs.Add(reader.GetString(reader.GetOrdinal("id")));
                        }
                        //Borrar sus batchs
                        foreach (string batchId in batchs)
                            deleteBatch(batchId, conn, transaction);
                        //Borrar la plantilla
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM candidate_doc_template WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", templateId);
                            command.ExecuteNonQuery();
                        }

                        HelperMethods.DeleteFile(new[] { "candidate_doc_template", templateId });

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5435, No se ha podido eliminar la plantilla" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "delete-batch/{batchId}/")]
        public IActionResult DeleteBatch(string batchId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("CandidateDocs.DeleteBatch", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                CandidateDocBatch? batch = getBatch(batchId, conn);
                if (batch == null)
                    return Ok(new { error = "Error 4431, El envio no existe" });
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        deleteBatch(batchId, conn, transaction);
                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        return Ok(new { error = "Error 5437, No se ha podido borrar el envio" });
                    }
                }
            }
            return Ok(result);
        }

        //Acciones
        [HttpPost]
        [Route(template: "sign/{docId}")]
        public async Task<IActionResult> Sign(string docId)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("pdf", out JsonElement pdfJson) &&
                json.TryGetProperty("photos", out JsonElement photosJson))
            {
                try
                {
                    string pdf = pdfJson.GetString();
                    List<string> photos = HelperMethods.GetJsonStringList(photosJson);
                    if ((pdf == null && photos.Count == 0) || (pdf != null && photos.Count != 0))
                        return Ok(new { error = "Error 4424, Parametros incorrectos" });
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        CandidateDoc? doc = getDoc(docId, conn);
                        if (doc == null)
                            return Ok(new { error = "Error 4420, El documento no existe" });
                        if (!doc.Value.needsSign)
                            return Ok(new { error = "Error 4421, El documento no requiere firmado" });
                        if (doc.Value.signedPdf || doc.Value.signedPhoto)
                            return Ok(new { error = "Error 4423, El documento ya está firmado" });
                        if (!doc.Value.canSign)
                            return Ok(new { error = "Error 4422, El documento no permite ser firmado" });
                        if (pdf != null)
                        {
                            HelperMethods.SaveFile(new[] { "candidate", doc.Value.candidateId, "docs", doc.Value.id, "signed" }, pdf);
                        }
                        else if (photos.Count != 0)
                        {
                            for (int i = 0; i < photos.Count; i++)
                            {
                                string photo = HelperMethods.LimitImageSize(photos[i], true, 2000);
                                HelperMethods.SaveFile(new[] { "candidate", doc.Value.candidateId, "docs", doc.Value.id, "signedPhotos", $"photo{i + 1}.jpg" }, photo);
                            }
                        }
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidate_docs SET signedPdf = @PDF, signedPhoto = @PHOTO, canSign = CAST(0 as bit) WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", doc.Value.id);
                            command.Parameters.AddWithValue("@PDF", pdf != null);
                            command.Parameters.AddWithValue("@PHOTO", photos.Count != 0);
                            command.ExecuteNonQuery();
                        }
                        if (doc.Value.incidenceNumber != null)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE incidencia_general SET state = @STATE, closed = @CLOSED WHERE number = @NUMBER";
                                command.Parameters.AddWithValue("@NUMBER", doc.Value.incidenceNumber.Value);
                                command.Parameters.AddWithValue("@STATE", "rechazada");
                                command.Parameters.AddWithValue("@CLOSED", 1);
                                command.ExecuteNonQuery();
                            }
                        }
                        result = new { error = false };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5424, No se ha podido firmar el documento" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-doc/{docId}/{type?}")]
        public IActionResult DownloadDoc(string docId, string type)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    CandidateDoc? doc = getDoc(docId, conn);
                    if (doc == null)
                        return Ok(new { error = "Error 4420, El documento no existe" });
                    if (type == "ca")
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidate_docs SET seenDate = getdate() WHERE id = @ID AND seenDate IS NULL";
                            command.Parameters.AddWithValue("@ID", docId);
                            command.ExecuteNonQuery();
                        }
                    }
                    result = new { error = false, doc = HelperMethods.ReadFile(new[] { "candidate", doc.Value.candidateId, "docs", docId, "doc" }) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5423, No se ha podido obtener el documento" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-signed-doc/{docId}")]
        public IActionResult DownloadSignedDoc(string docId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    CandidateDoc? doc = getDoc(docId, conn);
                    if (doc == null)
                        return Ok(new { error = "Error 4420, El documento no existe" });

                    result = new { error = false, signed = HelperMethods.ReadFile(new[] { "candidate", doc.Value.candidateId, "docs", doc.Value.id, "signed" }) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5424, No se ha podido obtener el documento" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-signed-photos/{docId}")]
        public IActionResult DownloadSignedPhotos(string docId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    CandidateDoc? doc = getDoc(docId, conn);
                    if (doc == null)
                        return Ok(new { error = "Error 4420, El documento no existe" });
                    result = new { error = false, photos = HelperMethods.ReadFileList(new[] { "candidate", doc.Value.candidateId, "docs", doc.Value.id, "signedPhotos" }) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5423, No se ha podido obtener el documento" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-signed-photos-zip/{docId}")]
        public IActionResult DownloadSignedPhotosZip(string docId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    CandidateDoc? doc = getDoc(docId, conn);
                    if (doc == null)
                        return Ok(new { error = "Error 4420, El documento no existe" });
                    result = new { error = false, zip = zipImgFiles(HelperMethods.ListFiles(new[] { "candidate", doc.Value.candidateId, "docs", doc.Value.id, "signedPhotos" })) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5423, No se ha podido obtener el documento" };
            }
            return Ok(result);
        }

        //Acciones de plantilla
        [HttpPost]
        [Route(template: "sign-template/{assignationId}")]
        public async Task<IActionResult> SignTemplate(string assignationId)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string sign = await readerBody.ReadToEndAsync();
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    //Obtener la ID de la plantilla
                    int? incidenceNumber = null;
                    bool canSign = false;
                    string templateId = null, candidateId = null;
                    List<PageLayout?> layout = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT CDT.id, CDTA.candidateId, CDT.signPlacement, CDTA.canSign, CDTA.incidenceNumber " +
                            "FROM candidate_doc_template_asignation CDTA " +
                            "INNER JOIN candidate_doc_template_batch CDTB ON(CDTA.batchId = CDTB.id) " +
                            "INNER JOIN candidate_doc_template CDT ON(CDTB.templateId = CDT.id) " +
                            "WHERE CDTA.id = @ID";
                        command.Parameters.AddWithValue("@ID", assignationId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                templateId = reader.GetString(reader.GetOrdinal("id"));
                                candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                                layout = parseDBPageLayout(reader.GetString(reader.GetOrdinal("signPlacement")));
                                canSign = reader.GetBoolean(reader.GetOrdinal("canSign"));
                                incidenceNumber = reader.IsDBNull(reader.GetOrdinal("incidenceNumber")) ? null : reader.GetInt32(reader.GetOrdinal("incidenceNumber"));
                            }
                        }
                    }
                    if (templateId == null)
                        return Ok(new { error = "Error 4438, documento no encontrado" });
                    if (layout == null)
                        return Ok(new { error = "Error 4439, este documento no requiere firma" });
                    if (!canSign)
                        return Ok(new { error = "Error 4440, este documento no se puede firmar" });
                    string pdf = HelperMethods.ReadFile(new[] { "candidate_doc_template", templateId });
                    PdfReader pdfReader = new PdfReader(Convert.FromBase64String(pdf.Split(",")[1]));
                    using (MemoryStream ms = new MemoryStream())
                    {
                        PdfStamper stamper = new PdfStamper(pdfReader, ms, '\0', true);
                        if (pdfReader.NumberOfPages != layout.Count)
                            return Ok(new { error = "Error 5439, no se puede firmar el documento" });
                        byte[] signBytes = Convert.FromBase64String(sign.Split(",")[1]);
                        for (int i = 0; i < layout.Count; i++)
                        {
                            if (layout[i].HasValue)
                            {
                                PageLayout pageLayout = layout[i].Value;
                                Rectangle pageZize = pdfReader.GetPageSize(i + 1);

                                //El eje Y comienza desde abajo
                                pageLayout.y = 1 - pageLayout.y;
                                pageLayout.y -= pageLayout.h;
                                //Escalar las coordenadas
                                pageLayout.x *= pageZize.Width;
                                pageLayout.w *= pageZize.Width;
                                pageLayout.y *= pageZize.Height;
                                pageLayout.h *= pageZize.Height;

                                Image img = Image.GetInstance(signBytes);
                                //img.ScaleToFit(pageLayout.w, pageLayout.h);
                                img.ScaleAbsolute(pageLayout.w, pageLayout.h);
                                img.SetAbsolutePosition(pageLayout.x, pageLayout.y);
                                stamper.GetOverContent(i + 1).AddImage(img);
                            }
                        }
                        stamper.Close();
                        pdfReader.Close();
                        string signedPdf = "data:application/pdf;base64," + Convert.ToBase64String(ms.ToArray());
                        HelperMethods.SaveFile(new[] { "candidate", candidateId, "docs-template", assignationId }, signedPdf);
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidate_doc_template_asignation SET signDate = getdate(), canSign = CAST(0 as bit) WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            command.ExecuteNonQuery();
                        }
                        if (incidenceNumber != null)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE incidencia_general SET state = @STATE, closed = @CLOSED WHERE number = @NUMBER";
                                command.Parameters.AddWithValue("@NUMBER", incidenceNumber.Value);
                                command.Parameters.AddWithValue("@STATE", "rechazada");
                                command.Parameters.AddWithValue("@CLOSED", 1);
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                    result = new { error = false };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5438, No se ha podido firmar el documento" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-template/{templateId}/{assignationId?}")]
        public IActionResult DownloadTemplate(string templateId, string assignationId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                string pdf = HelperMethods.ReadFile(new[] { "candidate_doc_template", templateId });
                if (pdf == null)
                {
                    result = new { error = "Error 4433, plantilla no encontrada" };
                }
                else
                {
                    result = new { error = false, pdf };
                }
                if (assignationId != null)
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidate_doc_template_asignation SET seenDate = getdate() WHERE id = @ID AND seenDate IS NULL";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5433, No se ha podido descargar el documento" };
            }
            return Ok(result);
        }


        [HttpGet]
        [Route(template: "download-assignation/{assignationId}")]
        public IActionResult DownloadAssignation(string assignationId)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                string pdf = HelperMethods.ReadFile(new[] { "candidate", candidateId, "docs-template", assignationId });

                if (pdf == null)
                {
                    result = new { error = "Error 4434, documento no encontrado" };
                }
                else
                {
                    result = new { error = false, pdf };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5433, No se ha podido descargar el documento" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-batch/{batchId}/")]
        public IActionResult DownloadBatch(string batchId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HelperMethods.HasPermission("CandidateDocs.DownloadBatch", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    CandidateDocBatch? batch = getBatch(batchId, conn);
                    if (batch == null)
                        return NotFound();
                    List<Tuple<string, string>> files = batch.Value.candidates.Select(c => new Tuple<string, string>(HelperMethods.ComposePath(new[] { "candidate", c.candidateId, "docs-template", c.id }, false), $"{batch.Value.name}_{c.dni}.pdf")).ToList();
                    string contentType = "application/zip";
                    HttpContext.Response.ContentType = contentType;
                    byte[] bytes = zipPdfFiles(files);
                    if (bytes == null) return new NoContentResult();
                    var response = new FileContentResult(bytes, contentType)
                    {
                        FileDownloadName = "Modelo145.pdf"
                    };
                    return response;
                }
            }
            catch (Exception)
            {
            }
            return StatusCode(500);
        }


        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        public struct CandidateDoc
        {
            public string id { get; set; }
            public string candidateId { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string filename { get; set; }
            public bool needsSign { get; set; }
            public bool signedPdf { get; set; }
            public bool signedPhoto { get; set; }
            public int? incidenceNumber { get; set; }
            public bool canSign { get; set; }
            public DateTime date { get; set; }
            public DateTime? seenDate { get; set; }
            public string templateId { get; set; } //Exclusivo de los que van con plantilla
            public string assignationId { get; set; } //Exclusivo de los que van con plantilla
        }
        public struct CandidateDocTemplate
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string filename { get; set; }
            public List<PageLayout?> signPlacement { get; set; }
            public string pdf { get; set; }
            public bool hide { get; set; }
            public DateTime date { get; set; }
        }
        //Page layout in px, dots or relative (usually relative)
        public struct PageLayout
        {
            public float x { get; set; }
            public float y { get; set; }
            public float w { get; set; }
            public float h { get; set; }
        }
        public struct CandidateDocBatch
        {
            public string id { get; set; }
            public string templateId { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string filename { get; set; }
            public int nCandidates { get; set; }
            public int nSeen { get; set; }
            public int nSigned { get; set; }
            public List<CandidateDocAssignation> candidates { get; set; }
            public bool needsSign { get; set; }
            public DateTime date { get; set; }
        }
        public struct CandidateDocAssignation
        {
            public string id { get; set; }
            public string candidateId { get; set; }
            public string name { get; set; }
            public string dni { get; set; }
            public int? incidenceNumber { get; set; }
            public bool canSign { get; set; }
            public DateTime? seenDate { get; set; }
            public DateTime? signDate { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static List<CandidateDoc> listDocs(string candidateId, SqlConnection conn)
        {
            List<CandidateDoc> docs = new List<CandidateDoc>();

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "( \n" +
                    "SELECT CD.id, CD.candidateId, CD.name, CD.description, CD.filename, CD.needsSign, CD.signedPdf, CD.signedPhoto, CD.date, CD.seenDate, CD.incidenceNumber, CD.canSign, templateId = NULL, assignationId = NULL \n" +
                    "FROM candidate_docs CD \n" +
                    "WHERE candidateId = @ID \n" +
                    ") UNION ALL ( \n" +
                    "SELECT CDTA.id, CDTA.candidateId, CDT.name, CDT.description, CDT.filename, \n" +
                    "needsSign = CAST(CASE WHEN CDT.signPlacement IS NULL THEN 0 ELSE 1 END as bit), \n" +
                    "signedPdf = CAST(CASE WHEN CDTA.signDate IS NULL THEN 0 ELSE 1 END as bit), \n" +
                    "CAST(0 as bit), CDTB.date, CDTA.seenDate, CDTA.incidenceNumber, CDTA.canSign, CDT.id, assignationId = CDTA.id \n" +
                    "FROM candidate_doc_template CDT \n" +
                    "INNER JOIN candidate_doc_template_batch CDTB ON(CDT.id = CDTB.templateId) \n" +
                    "INNER JOIN candidate_doc_template_asignation CDTA ON(CDTB.id = CDTA.batchId) \n" +
                    "WHERE candidateId = @ID \n" +
                    ") ORDER BY date DESC";
                command.Parameters.AddWithValue("@ID", candidateId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        docs.Add(new CandidateDoc()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            description = reader.GetString(reader.GetOrdinal("description")),
                            filename = reader.GetString(reader.GetOrdinal("filename")),
                            needsSign = reader.GetBoolean(reader.GetOrdinal("needsSign")),
                            signedPdf = reader.GetBoolean(reader.GetOrdinal("signedPdf")),
                            signedPhoto = reader.GetBoolean(reader.GetOrdinal("signedPhoto")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            seenDate = reader.IsDBNull(reader.GetOrdinal("seenDate")) ? null : reader.GetDateTime(reader.GetOrdinal("seenDate")),
                            incidenceNumber = reader.IsDBNull(reader.GetOrdinal("incidenceNumber")) ? null : reader.GetInt32(reader.GetOrdinal("incidenceNumber")),
                            canSign = reader.GetBoolean(reader.GetOrdinal("canSign")),
                            templateId = reader.IsDBNull(reader.GetOrdinal("templateId")) ? null : reader.GetString(reader.GetOrdinal("templateId")),
                            assignationId = reader.IsDBNull(reader.GetOrdinal("assignationId")) ? null : reader.GetString(reader.GetOrdinal("assignationId"))
                        });
                    }
                }
            }

            return docs;
        }
        public static CandidateDoc? getDoc(string id, SqlConnection conn)
        {
            CandidateDoc? doc = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM candidate_docs WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", id);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        doc = new CandidateDoc()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            description = reader.GetString(reader.GetOrdinal("description")),
                            filename = reader.GetString(reader.GetOrdinal("filename")),
                            needsSign = reader.GetBoolean(reader.GetOrdinal("needsSign")),
                            signedPdf = reader.GetBoolean(reader.GetOrdinal("signedPdf")),
                            signedPhoto = reader.GetBoolean(reader.GetOrdinal("signedPhoto")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            incidenceNumber = reader.IsDBNull(reader.GetOrdinal("incidenceNumber")) ? null : reader.GetInt32(reader.GetOrdinal("incidenceNumber")),
                            seenDate = reader.IsDBNull(reader.GetOrdinal("seenDate")) ? null : reader.GetDateTime(reader.GetOrdinal("seenDate")),
                            canSign = reader.GetBoolean(reader.GetOrdinal("canSign"))
                        };
                    }
                }
            }

            return doc;
        }
        public static string createDoc(CandidateDoc doc, string base64, SqlConnection conn)
        {
            doc.id = HelperMethods.ComputeStringHash(doc.candidateId + doc.name + doc.description + doc.filename + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "INSERT INTO candidate_docs (id, candidateId, name, description, filename, needsSign, canSign) VALUES (@ID, @CANDIDATE, @NAME, @DESCRIPTION, @FILENAME, @SIGN, @SIGN)";
                command.Parameters.AddWithValue("@ID", doc.id);
                command.Parameters.AddWithValue("@CANDIDATE", doc.candidateId);
                command.Parameters.AddWithValue("@NAME", doc.name);
                command.Parameters.AddWithValue("@DESCRIPTION", doc.description);
                command.Parameters.AddWithValue("@FILENAME", doc.filename);
                command.Parameters.AddWithValue("@SIGN", doc.needsSign);
                command.ExecuteNonQuery();
            }

            HelperMethods.SaveFile(new[] { "candidate", doc.candidateId, "docs", doc.id, "doc" }, base64);

            return doc.id;
        }
        public static string zipImgFiles(string[] files)
        {
            string tmpDir = HelperMethods.GetTemporaryDirectory();
            string tmpZipDir = HelperMethods.GetTemporaryDirectory();

            foreach (string file in files)
            {
                string base64 = System.IO.File.ReadAllText(file);
                string filename;
                if (base64.Contains(","))
                {
                    filename = Path.GetFileNameWithoutExtension(file) + ".jpg";
                    base64 = base64.Split(",")[1];
                }
                else
                {
                    filename = Path.GetFileName(file);
                }
                System.IO.File.WriteAllBytes(Path.Combine(tmpDir, filename), Convert.FromBase64String(base64));
            }
            string tmpOutZip = Path.Combine(tmpZipDir, "file.zip");
            ZipFile.CreateFromDirectory(tmpDir, tmpOutZip);

            string zipBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(tmpOutZip));

            Directory.Delete(tmpDir, true);
            Directory.Delete(tmpZipDir, true);

            return "data:@file/zip;base64," + zipBase64;
        }
        public static string createTemplate(CandidateDocTemplate template, string base64, SqlConnection conn, SqlTransaction transaction = null)
        {
            template.id = HelperMethods.ComputeStringHash(template.name + template.description + template.filename + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "INSERT INTO candidate_doc_template (id, name, description, filename, signPlacement, hide) VALUES (@ID, @NAME, @DESCRIPTION, @FILENAME, @SIGN_PLACEMENT, @HIDE)";
                command.Parameters.AddWithValue("@ID", template.id);
                command.Parameters.AddWithValue("@NAME", template.name);
                command.Parameters.AddWithValue("@DESCRIPTION", template.description);
                command.Parameters.AddWithValue("@FILENAME", template.filename);
                command.Parameters.AddWithValue("@SIGN_PLACEMENT", template.signPlacement == null ? DBNull.Value : JsonSerializer.Serialize(template.signPlacement));
                command.Parameters.AddWithValue("@HIDE", template.hide);
                command.ExecuteNonQuery();
            }

            HelperMethods.SaveFile(new[] { "candidate_doc_template", template.id }, base64);

            return template.id;
        }
        public static void updateTemplate(CandidateDocTemplate template, SqlConnection conn)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "UPDATE candidate_doc_template SET name = @NAME, description = @DESCRIPTION, signPlacement = @SIGN_PLACEMENT WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", template.id);
                command.Parameters.AddWithValue("@NAME", template.name);
                command.Parameters.AddWithValue("@DESCRIPTION", template.description);
                command.Parameters.AddWithValue("@SIGN_PLACEMENT", template.signPlacement == null ? DBNull.Value : JsonSerializer.Serialize(template.signPlacement));
                command.ExecuteNonQuery();
            }
        }
        public static List<CandidateDocTemplate> listTemplates(SqlConnection conn)
        {
            List<CandidateDocTemplate> templates = new List<CandidateDocTemplate>();

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM candidate_doc_template WHERE hide = 0 ORDER BY date DESC";
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        templates.Add(new()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            description = reader.GetString(reader.GetOrdinal("description")),
                            filename = reader.GetString(reader.GetOrdinal("filename")),
                            signPlacement = reader.IsDBNull(reader.GetOrdinal("signPlacement")) ? null : new(),
                            date = reader.GetDateTime(reader.GetOrdinal("date"))
                        });
                    }
                }
            }

            return templates;
        }
        public static CandidateDocTemplate? getTemplate(string templateId, SqlConnection conn, SqlTransaction transaction = null, bool withPdf = false)
        {
            CandidateDocTemplate? template = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT * FROM candidate_doc_template WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", templateId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        template = new()
                        {
                            id = templateId,
                            name = reader.GetString(reader.GetOrdinal("name")),
                            description = reader.GetString(reader.GetOrdinal("description")),
                            filename = reader.GetString(reader.GetOrdinal("filename")),
                            signPlacement = reader.IsDBNull(reader.GetOrdinal("signPlacement")) ? null : parseDBPageLayout(reader.GetString(reader.GetOrdinal("signPlacement"))),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            pdf = withPdf ? HelperMethods.ReadFile(new[] { "candidate_doc_template", templateId }) : null
                        };
                    }
                }
            }

            return template;
        }
        public static async Task<string> createBatch(List<string> candidates, CandidateDocTemplate template, SqlConnection conn, SqlTransaction transaction = null, bool notificateCandidate = true)
        {
            if (template.id == null)
            {
                //Crear una plantilla nueva
                template.id = createTemplate(new()
                {
                    name = template.name,
                    description = template.description,
                    filename = template.filename,
                    signPlacement = null,
                    hide = true
                }, template.pdf, conn, transaction);
            }
            else
            {
                CandidateDocTemplate? templateTmp = getTemplate(template.id, conn, transaction);
                if (templateTmp != null)
                    template = templateTmp.Value;
            }

            string batchId = HelperMethods.ComputeStringHash(template.id + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            //Crear el batch
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO candidate_doc_template_batch (id, templateId) VALUES (@ID, @TEMPLATE)";
                command.Parameters.AddWithValue("@ID", batchId);
                command.Parameters.AddWithValue("@TEMPLATE", template.id);
                command.ExecuteNonQuery();
            }

            await addCandidatesToBatch(candidates, batchId, template.signPlacement != null, conn, transaction, notificateCandidate);

            return template.id;
        }


        public static CandidateDocBatch? getBatch(string batchId, SqlConnection conn)
        {
            CandidateDocBatch batch;

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT CDTB.id as batchId, CDTB.templateId, CDTB.date, CDT.name, CDT.description, CDT.filename," +
                    "needsSign = CASE WHEN CDT.signPlacement IS NULL THEN 0 ELSE 1 END " +
                    "FROM candidate_doc_template_batch CDTB " +
                    "INNER JOIN candidate_doc_template CDT ON(CDT.id = CDTB.templateId) " +
                    "WHERE CDTB.id = @ID";
                command.Parameters.AddWithValue("@ID", batchId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        batch = new()
                        {
                            id = reader.GetString(reader.GetOrdinal("batchId")),
                            templateId = reader.GetString(reader.GetOrdinal("templateId")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            description = reader.GetString(reader.GetOrdinal("description")),
                            filename = reader.GetString(reader.GetOrdinal("filename")),
                            needsSign = reader.GetInt32(reader.GetOrdinal("needsSign")) == 1,
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            candidates = new()
                        };
                    }
                    else return null;
                }
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT CDTA.*, C.dni, name = TRIM(CONCAT(C.nombre, ' ', C.apellidos)) " +
                    "FROM candidate_doc_template_asignation CDTA " +
                    "INNER JOIN candidatos C ON(CDTA.candidateId = C.id) " +
                    "WHERE CDTA.batchId = @ID";
                command.Parameters.AddWithValue("@ID", batchId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {

                        batch.candidates.Add(new()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            seenDate = reader.IsDBNull(reader.GetOrdinal("seenDate")) ? null : reader.GetDateTime(reader.GetOrdinal("seenDate")),
                            signDate = reader.IsDBNull(reader.GetOrdinal("signDate")) ? null : reader.GetDateTime(reader.GetOrdinal("signDate")),
                            incidenceNumber = reader.IsDBNull(reader.GetOrdinal("incidenceNumber")) ? null : reader.GetInt32(reader.GetOrdinal("incidenceNumber")),
                            canSign = reader.GetBoolean(reader.GetOrdinal("canSign"))
                        });
                    }
                }
            }

            batch.nCandidates = batch.candidates.Count;
            batch.nSeen = batch.candidates.Count(c => c.seenDate != null);
            batch.nSigned = batch.candidates.Count(c => c.signDate != null);

            return batch;
        }
        public static void deleteBatch(string batchId, SqlConnection conn, SqlTransaction transaction)
        {
            //Obtener las asignaciones
            List<Tuple<string, string>> assignations = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "SELECT id, candidateId FROM candidate_doc_template_asignation WHERE batchId = @ID";
                command.Parameters.AddWithValue("@ID", batchId);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        assignations.Add(new(reader.GetString(reader.GetOrdinal("candidateId")), reader.GetString(reader.GetOrdinal("id"))));
            }

            //Eliminar los asignados
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM candidate_doc_template_asignation WHERE batchId = @ID";
                command.Parameters.AddWithValue("@ID", batchId);
                command.ExecuteNonQuery();
            }

            //Eliminar el batch
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM candidate_doc_template_batch WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", batchId);
                command.ExecuteNonQuery();
            }

            //Eliminar las firmas de estos documentos
            foreach (Tuple<string, string> assignation in assignations)
            {
                HelperMethods.DeleteFile(new[] { "candidate", assignation.Item1, "docs-template", assignation.Item2 });
            }
        }
        public static async Task addCandidatesToBatch(List<string> candidates, string batchId, bool requiereFirma, SqlConnection conn, SqlTransaction transaction, bool notificateCandidate = true)
        {
            //Agregar a los candidatos
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO candidate_doc_template_asignation (id, batchId, candidateId, canSign) VALUES (@ID, @BATCH, @CANDIDATE, @SIGN)";
                command.Parameters.AddWithValue("@BATCH", batchId);
                command.Parameters.AddWithValue("@SIGN", requiereFirma);
                command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                foreach (string candidateId in candidates)
                {
                    command.Parameters["@ID"].Value = HelperMethods.ComputeStringHash(batchId + candidateId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    command.Parameters["@CANDIDATE"].Value = candidateId;
                    command.ExecuteNonQuery();
                }
            }

            if (notificateCandidate)
            {
                //Obtener los emails de los candidatos
                Dictionary<string, string> emails = new();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "SELECT email FROM candidatos WHERE id = @ID";
                    command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                    foreach (string candidateId in candidates)
                    {
                        command.Parameters["@ID"].Value = candidateId;
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                emails[candidateId] = reader.GetString(reader.GetOrdinal("email"));
                    }
                }
                candidates = candidates.Where(id => emails.ContainsKey(id)).ToList();

                //Enviar los emails
                foreach (string candidateId in candidates)
                {
                    EventMailer.SendEmail(new EventMailer.Email()
                    {
                        template = "candidateRequiredDoc",
                        inserts = new() { { "url", AccessController.getAutoLoginUrl("ca", candidateId, null, conn, null) }, { "sign_insert", requiereFirma ? ", que requiere firma" : "" } },
                        toEmail = emails[candidateId],
                        subject = requiereFirma ? "[Think&Job] Documento requiere firma" : "[Think&Job] Documento recibido",
                        priority = EventMailer.EmailPriority.MODERATE
                    });
                }

                //Enivar las notificaciones
                await PushNotificationController.sendNotifications(candidates.Select(id => new PushNotificationController.UID() { type = "ca", id = id }), new()
                {
                    title = requiereFirma ? "Documento pendiente de firma" : "Documento recibido",
                    type = "candidate-documento-requerido"
                }, conn, transaction);
            }
        }
        public static void removeCandidatesToBatch(List<string> candidates, string batchId, SqlConnection conn, SqlTransaction transaction)
        {
            //Eliminarlos de la asignacion
            List<Tuple<string, string>> assignations = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM candidate_doc_template_asignation OUTPUT deleted.id WHERE batchId = @BATCH AND candidateId = @CANDIDATE";
                command.Parameters.AddWithValue("@BATCH", batchId);
                command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                foreach (string candidateId in candidates)
                {
                    command.Parameters["@CANDIDATE"].Value = candidateId;
                    string assignationId = (string)command.ExecuteScalar();
                    if (assignationId != null) assignations.Add(new(candidateId, assignationId));
                }
            }

            //Borrar sus entregas
            foreach (Tuple<string, string> assignation in assignations)
            {
                HelperMethods.DeleteFile(new[] { "candidate", assignation.Item1, "docs-template", assignation.Item2 });
            }
        }
        public static List<PageLayout?> parsePageLayout(JsonElement pagesJson)
        {
            if (pagesJson.ValueKind != JsonValueKind.Array) return null;
            List<PageLayout?> layout = new();
            foreach (JsonElement pageJson in pagesJson.EnumerateArray())
            {
                if (pageJson.TryGetBoolean("signed", out bool signed))
                {
                    if (signed &&
                        pageJson.TryGetInt32("rect_x", out int rect_x) &&
                        pageJson.TryGetInt32("rect_y", out int rect_y) &&
                        pageJson.TryGetInt32("rect_ancho", out int rect_ancho) &&
                        pageJson.TryGetInt32("rect_alto", out int rect_alto) &&
                        pageJson.TryGetInt32("img_ancho", out int img_ancho) &&
                        pageJson.TryGetInt32("img_alto", out int img_alto))
                    {
                        layout.Add(new PageLayout()
                        {
                            x = (float)rect_x / img_ancho,
                            y = (float)rect_y / img_alto,
                            w = (float)rect_ancho / img_ancho,
                            h = (float)rect_alto / img_alto
                        });
                    }
                    else
                    {
                        layout.Add(null);
                    }
                }
            }
            if (!layout.Any(p => p != null)) layout = null;
            return layout;
        }
        public static List<PageLayout?> parseDBPageLayout(string pagesText)
        {
            JsonElement pagesJson = JsonDocument.Parse(pagesText).RootElement;
            if (pagesJson.ValueKind != JsonValueKind.Array) return null;
            List<PageLayout?> layout = new();
            foreach (JsonElement pageJson in pagesJson.EnumerateArray())
            {
                if (pageJson.ValueKind != JsonValueKind.Null &&
                    pageJson.TryGetSingle("x", out float x) &&
                    pageJson.TryGetSingle("y", out float y) &&
                    pageJson.TryGetSingle("w", out float w) &&
                    pageJson.TryGetSingle("h", out float h))
                {
                    layout.Add(new() { x = x, y = y, w = w, h = h });
                }
                else
                {
                    layout.Add(null);
                }
            }
            return layout;
        }
        public static byte[] zipPdfFiles(List<Tuple<string, string>> files)
        {
            string tmpDir = HelperMethods.GetTemporaryDirectory();
            string tmpZipDir = HelperMethods.GetTemporaryDirectory();

            foreach (Tuple<string, string> file in files)
            {
                if (!System.IO.File.Exists(file.Item1)) continue;
                string base64 = System.IO.File.ReadAllText(file.Item1);
                if (base64.Contains(","))
                    base64 = base64.Split(",")[1];
                System.IO.File.WriteAllBytes(Path.Combine(tmpDir, file.Item2), Convert.FromBase64String(base64));
            }
            string tmpOutZip = Path.Combine(tmpZipDir, "file.zip");
            ZipFile.CreateFromDirectory(tmpDir, tmpOutZip);

            byte[] zip = System.IO.File.ReadAllBytes(tmpOutZip);

            Directory.Delete(tmpDir, true);
            Directory.Delete(tmpZipDir, true);

            return zip;
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
