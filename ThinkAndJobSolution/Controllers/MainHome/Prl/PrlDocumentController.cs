using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Helper;
using System.Data;
using ThinkAndJobSolution.Utils;
using System.Text.Json;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/prl-document")]
    [ApiController]
    [Authorize]
    public class PRLDocumentController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "list-for-candidate/{trabajoId}/{type?}")]
        public IActionResult ListNT(string trabajoId, string type = null)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT TR.*,\n" +
                                            "CAST(CASE WHEN EXISTS(SELECT * FROM category_documents_downloads TD WHERE TD.documentId = TR.id AND TD.candidateId = @CANDIDATE_ID)\n" +
                                            "THEN 1 ELSE 0 END AS BIT) as downloaded\n" +
                                            "FROM (\n" +
                                            "SELECT CD.* FROM category_documents CD WHERE CD.trabajoId = @TRABAJO_ID\n" +
                                            "UNION ALL\n" +
                                            "SELECT CD.* FROM category_documents CD INNER JOIN categories CA ON CD.categoryId = CA.id INNER JOIN trabajos T ON CA.id = T.categoryId WHERE T.id = @TRABAJO_ID\n" +
                                            ") TR\n" +
                                            "WHERE (@TYPE IS NULL OR TR.type = @TYPE)\n" +
                                            "ORDER BY TR.date DESC";

                    command.Parameters.AddWithValue("@TRABAJO_ID", trabajoId);
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@TYPE", HelperMethods.TestTypeFilter(type));

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        List<DocumentData> documents = new List<DocumentData>();

                        while (reader.Read())
                        {
                            DocumentData doc = new DocumentData();
                            doc.id = reader.GetString(reader.GetOrdinal("id"));
                            doc.name = reader.GetString(reader.GetOrdinal("name"));
                            doc.date = reader.GetDateTime("date").ToString();
                            doc.downloaded = reader.GetBoolean(reader.GetOrdinal("downloaded"));
                            doc.type = reader.GetString(reader.GetOrdinal("type"));
                            documents.Add(doc);
                        }

                        result = documents;
                    }
                }
            }
            return Ok(result);

        }

        [HttpGet]
        [Route(template: "list/{trabajoId}/{type?}")]
        public IActionResult List(string trabajoId, string type = null)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HelperMethods.HasPermission("PRLDocument.List", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT TR.*\n" +
                                              "FROM (\n" +
                                              "SELECT CD.*, byCategory = 0 FROM category_documents CD WHERE CD.trabajoId = @TRABAJO_ID\n" +
                                              "UNION ALL\n" +
                                              "SELECT CD.*, byCategory = 1 FROM category_documents CD INNER JOIN categories CA ON CD.categoryId = CA.id INNER JOIN trabajos T ON CA.id = T.categoryId WHERE T.id = @TRABAJO_ID\n" +
                                              ") TR\n" +
                                              "WHERE (@TYPE IS NULL OR TR.type = @TYPE)\n" +
                                              "ORDER BY TR.date DESC";
                        command.Parameters.AddWithValue("@TRABAJO_ID", trabajoId);
                        command.Parameters.AddWithValue("@TYPE", HelperMethods.TestTypeFilter(type));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            List<DocumentData> documents = new List<DocumentData>();
                            while (reader.Read())
                            {
                                DocumentData doc = new DocumentData();
                                doc.id = reader.GetString(reader.GetOrdinal("id"));
                                doc.name = reader.GetString(reader.GetOrdinal("name"));
                                doc.date = reader.GetDateTime("date").ToString();
                                doc.type = reader.GetString(reader.GetOrdinal("type"));
                                doc.byCategory = reader.GetInt32(reader.GetOrdinal("byCategory")) == 1;
                                documents.Add(doc);
                            }
                            result = documents;
                        }
                    }
                }
            }
            return Ok(result);

        }

        [HttpGet]
        [Route(template: "list-by-category/{categoryId}/{type?}")]
        public IActionResult ListByCategory(string categoryId, string type = null)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HelperMethods.HasPermission("PRLDocument.List", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT CD.* FROM category_documents CD\n" +
                                              "WHERE CD.categoryId = @CATEGORY AND\n" +
                                              "(@TYPE IS NULL OR CD.type = @TYPE)\n" +
                                              "ORDER BY CD.date DESC";
                        command.Parameters.AddWithValue("@CATEGORY", categoryId);
                        command.Parameters.AddWithValue("@TYPE", HelperMethods.TestTypeFilter(type));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            List<DocumentData> documents = new List<DocumentData>();
                            while (reader.Read())
                            {
                                DocumentData doc = new DocumentData();
                                doc.id = reader.GetString(reader.GetOrdinal("id"));
                                doc.name = reader.GetString(reader.GetOrdinal("name"));
                                doc.date = reader.GetDateTime("date").ToString();
                                doc.type = reader.GetString(reader.GetOrdinal("type"));
                                documents.Add(doc);
                            }
                            result = documents;
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRLDocument.Create", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using System.IO.StreamReader reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await reader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("name", out JsonElement nameJson) &&
                    json.TryGetProperty("type", out JsonElement typeJson) &&
                    json.TryGetProperty("trabajoId", out JsonElement trabajoIdJson) &&
                    json.TryGetProperty("categoryId", out JsonElement categoryIdJson))
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        try
                        {
                            String type = Enum.GetName(typeof(HelperMethods.TestType), (HelperMethods.TestType)System.Enum.Parse(typeof(HelperMethods.TestType), typeJson.GetString().ToUpper()));
                            using (SqlCommand commandFind = conn.CreateCommand())
                            {
                                try
                                {
                                    commandFind.CommandText =
                                        "SELECT id FROM category_documents WHERE\n" +
                                        "((@TRABAJO_ID IS NULL AND trabajoid IS NULL) OR trabajoid = @TRABAJO_ID) AND\n" +
                                        "((@CATEGORY_ID IS NULL AND categoryId IS NULL) OR categoryId = @CATEGORY_ID) AND\n" +
                                        "name = @NAME";
                                    commandFind.Parameters.AddWithValue("@TRABAJO_ID", (object)trabajoIdJson.GetString() ?? DBNull.Value);
                                    commandFind.Parameters.AddWithValue("@CATEGORY_ID", (object)categoryIdJson.GetString() ?? DBNull.Value);
                                    commandFind.Parameters.AddWithValue("@NAME", nameJson.GetString());
                                    object prevId = commandFind.ExecuteScalar();
                                    if (prevId == null)
                                    {
                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            try
                                            {
                                                string id = HelperMethods.ComputeStringHash(nameJson.GetString() + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                                                command.CommandText =
                                                    "INSERT INTO category_documents (id, trabajoId, categoryId, name, type)" +
                                                    "VALUES (@ID, @TRABAJO_ID, @CATEGORY_ID, @NAME, @TYPE)";
                                                command.Parameters.AddWithValue("@ID", id);
                                                command.Parameters.AddWithValue("@TRABAJO_ID", (object)trabajoIdJson.GetString() ?? DBNull.Value);
                                                command.Parameters.AddWithValue("@CATEGORY_ID", (object)categoryIdJson.GetString() ?? DBNull.Value);
                                                command.Parameters.AddWithValue("@NAME", nameJson.GetString());
                                                command.Parameters.AddWithValue("@TYPE", HelperMethods.TestTypeFilter(type));
                                                command.ExecuteNonQuery();
                                                result = new
                                                {
                                                    id
                                                };
                                            }
                                            catch (Exception)
                                            {
                                                result = new
                                                {
                                                    error = "Error 5991, no se ha podido crear el documento formativo."
                                                };
                                            }
                                        }
                                    }
                                    else
                                    {
                                        result = new
                                        {
                                            id = prevId
                                        };
                                    }
                                }
                                catch (Exception)
                                {
                                    result = new
                                    {
                                        error = "Error 5991, no se ha podido determinar si el documento existia o no."
                                    };
                                }
                            }
                        }
                        catch (ArgumentException)
                        {
                            result = new
                            {
                                error = "Error 4991, tipo de documento formativo no valido."
                            };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "upload/{documentId}")]
        public async Task<IActionResult> UploadDocument(string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRLDocument.UploadDocument", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body);
                string data = await bodyReader.ReadToEndAsync();
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    DocumentIdentifier identifier = getDocumentWorkId(documentId, conn);
                    HelperMethods.SaveFile(new[] { identifier.type, identifier.id, "prl_document", documentId, "pdf" }, data);
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-for-candidate/{documentId}")]
        public IActionResult DownloadDocumentForCandidate(string documentId)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            //Comprobar si el registro de la descarga existe ya o no
            bool downloadRegistered = false;
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.CommandText =
                            "SELECT COUNT(*)\n" +
                            "FROM category_documents_downloads\n" +
                            "WHERE candidateId = @CANDIDATE_ID AND documentId = @DOCUMENT_ID";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        command.Parameters.AddWithValue("@DOCUMENT_ID", documentId);
                        object count = command.ExecuteScalar();
                        downloadRegistered = count is int && ((int)count) > 0;
                    }
                    catch (Exception)
                    {
                        result = new
                        {
                            error = "Error 3991, no se ha podido verificar si el documento fue descargado o no."
                        };
                    }
                }
                //Si no existe, crearlo
                if (!downloadRegistered)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        try
                        {
                            command.CommandText =
                                "INSERT INTO category_documents_downloads (candidateId, documentId) VALUES (@CANDIDATE_ID, @DOCUMENT_ID)";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@DOCUMENT_ID", documentId);
                            command.ExecuteNonQuery();
                            downloadRegistered = true;
                        }
                        catch (Exception)
                        {
                            result = new
                            {
                                error = "Error 3991, no se ha podido registrar la descarga del documento."
                            };
                        }
                    }
                }
                //Si se ha registrado o ya estaba, obtener el documento y guardarlo en la respuesta
                if (downloadRegistered)
                {
                    DocumentIdentifier identifier = getDocumentWorkId(documentId, conn);
                    result = new { data = HelperMethods.ReadFile(new[] { identifier.type, identifier.id, "prl_document", documentId, "pdf" }) };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download/{documentId}")]
        public IActionResult DownloadDocument(string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRLDocument.DownloadDocument", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    DocumentIdentifier identifier = getDocumentWorkId(documentId, conn);
                    result = new { data = HelperMethods.ReadFile(new[] { identifier.type, identifier.id, "prl_document", documentId, "pdf" }) };
                }

            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "delete/{documentId}")]
        public IActionResult DeleteDocument(string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("PRLDocument.DeleteDocument", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        deleteDocumentInTransaction(conn, transaction, documentId);
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5894, no se ha podido eliminar el documento" };
                    }
                }
            }
            return Ok(result);
        }


        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        public struct DocumentData
        {
            public string id { get; set; }
            public string name { get; set; }
            public string date { get; set; }
            public bool downloaded { get; set; }
            public string type { get; set; }
            public bool byCategory { get; set; }
        };
        public struct DocumentIdentifier
        {
            public string type { get; set; }
            public string id { get; set; }
        }
        #endregion

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static void deleteDocumentInTransaction(SqlConnection conn, SqlTransaction transaction, string documentId)
        {
            //Tener buscado el documento para borrarlo en el fs
            DocumentIdentifier identifier = getDocumentWorkId(documentId, conn, transaction);

            //Cuando se borra un documento borrar de la tabla de descargas las correspondientes a ese doumento.
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText =
                    "DELETE FROM category_documents_downloads WHERE documentId = @ID";

                command.Parameters.AddWithValue("@ID", documentId);

                command.ExecuteNonQuery();
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText =
                        "DELETE FROM category_documents WHERE id = @ID";

                command.Parameters.AddWithValue("@ID", documentId);

                command.ExecuteNonQuery();
            }

            HelperMethods.DeleteDir(new[] { identifier.type, identifier.id, "prl_document", documentId });
        }
        public static DocumentIdentifier getDocumentWorkId(string documentId, SqlConnection conn, SqlTransaction transaction = null)
        {
            string trabajoId = null, categoryId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT trabajoId, categoryId FROM category_documents WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", documentId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        trabajoId = reader.IsDBNull(reader.GetOrdinal("trabajoId")) ? null : reader.GetString(reader.GetOrdinal("trabajoId"));
                        categoryId = reader.IsDBNull(reader.GetOrdinal("categoryId")) ? null : reader.GetString(reader.GetOrdinal("categoryId"));
                    }
                }
            }

            if (trabajoId != null) return new DocumentIdentifier() { type = "work", id = trabajoId };
            if (categoryId != null) return new DocumentIdentifier() { type = "category", id = categoryId };
            return new DocumentIdentifier();
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
