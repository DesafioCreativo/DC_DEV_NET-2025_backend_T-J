using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Model.Client;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/work")]
    [ApiController]
    [Authorize]
    public class WorkController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
		[Route(template: "get-eval-doc/{workId}/{numDoc}")]
		public IActionResult GetEvalDoc(string workId, int numDoc)
		{
            object result = new
			{
				error = "Error 2932, no se ha podido procesar la petición."
			};
			using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
			{
				conn.Open();

				using (SqlCommand command = conn.CreateCommand())
				{
					command.CommandText = "SELECT C.companyId, C.id FROM trabajos T INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @ID";
					command.Parameters.AddWithValue("@ID", workId);
					using (SqlDataReader reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							string companyId = reader.GetString(reader.GetOrdinal("companyId"));
							string centroId = reader.GetString(reader.GetOrdinal("id"));
							result = new
							{
								error = false,
								doc = ReadFile(new[] { "companies", companyId, "centro", centroId, "work", workId, "evaldoc" + numDoc })

							};
						}
					}
				}
			}
			return Ok(result);
		}

        [HttpPost]
        [Route(template: "set-eval-doc/{workId}/")]
        public async Task<IActionResult> SetEvalDoc(string workId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Work.SetEvalDoc", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader bodyReader = new StreamReader(Request.Body, Encoding.UTF8);
                string doc = await bodyReader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(doc).RootElement;
                if (json.TryGetProperty("newDocs", out JsonElement newDocsJson))
                {
                    try
                    {
                        List<string> docs = HelperMethods.GetJsonStringList(newDocsJson);
                        if (docs.Count == 0)
                        {
                            return Ok(new { error = "No hay documentos" });
                        }
                        using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                        {
                            conn.Open();
                            using (SqlCommand command = conn.CreateCommand()){
                                command.CommandText = "SELECT C.companyId, C.id FROM trabajos T INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @ID";
                                command.Parameters.AddWithValue("@ID", workId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        string companyId = reader.GetString(reader.GetOrdinal("companyId"));
                                        string centroId = reader.GetString(reader.GetOrdinal("id"));
                                        int numDocs = Directory.GetFiles(ComposePath(new[] { "companies", companyId, "centro", centroId, "work", workId })).Length;
                                        for (int i = 0; i < docs.Count; i++)
                                        {
                                            SaveFile(new[] { "companies", companyId, "centro", centroId, "work", workId, "evaldoc" + (i + numDocs) }, docs[i]);
                                        }
                                        result = new { error = false };
                                    }
                                    else
                                    {
                                        result = new { error = "Error 4396, trabajo no encontrado" };
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5424, No se ha podido recuperar el documento" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "remove-eval-doc/{workId}/{numDoc}/")]
        public IActionResult RemoveEvalDoc(string workId, int numDoc)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Work.RemoveEvalDoc", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT C.companyId, C.id FROM trabajos T INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string companyId = reader.GetString(reader.GetOrdinal("companyId"));
                                string centroId = reader.GetString(reader.GetOrdinal("id"));
                                DeleteFile(new[] { "companies", companyId, "centro", centroId, "work", workId, "evaldoc" + numDoc });
                                string[] documentos = ListFiles(new[] { "companies", companyId, "centro", centroId, "work", workId });
                                for (int i = 0; i < documentos.Length; i++)
                                {
                                    RenameFile(documentos[i], new[] { "companies", companyId, "centro", centroId, "work", workId, "evaldoc" + i });
                                }
                                result = new { error = false };
                            }
                            else
                            {
                                result = new { error = "Error 4396, trabajo no encontrado" };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "remove-all-eval-doc/{workId}/")]
        public IActionResult RemoveAllEvalDoc(string workId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Work.RemoveAllEvalDoc", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT C.companyId, C.id FROM trabajos T INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string companyId = reader.GetString(reader.GetOrdinal("companyId"));
                                string centroId = reader.GetString(reader.GetOrdinal("id"));
                                string[] evalList = ListFiles(new[] { "companies", companyId, "centro", centroId, "work", workId });
                                for (int i = 0; i < evalList.Length; i++)
                                {
                                    DeleteFile(new[] { "companies", companyId, "centro", centroId, "work", workId, "evaldoc" + i });
                                }
                                result = new { error = false };
                            }
                            else
                            {
                                result = new { error = "Error 4396, trabajo no encontrado" };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "set-eval-doc-for-client/{workId}/")]
        public async Task<IActionResult> SetEvalDocForClient(string workId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using StreamReader bodyReader = new StreamReader(Request.Body, Encoding.UTF8);
            string doc = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(doc).RootElement;
            if (json.TryGetProperty("newDocs", out JsonElement newDocsJson))
            {
                try
                {
                    List<string> docs = HelperMethods.GetJsonStringList(newDocsJson);
                    if (docs.Count == 0)
                    {
                        return Ok(new { error = "No hay documentos" });
                    }

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        string companyId = null, centroId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT C.companyId, C.id FROM trabajos T INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @ID";
                            command.Parameters.AddWithValue("@ID", workId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    companyId = reader.GetString(reader.GetOrdinal("companyId"));
                                    centroId = reader.GetString(reader.GetOrdinal("id"));
                                }
                                else
                                {
                                    return Ok(new { error = "Error 4396, trabajo no encontrado" });
                                }
                            }
                        }
                        if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_CATEGORIAS, conn) == null)
                            return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });

                        for (int i = 0; i < docs.Count; i++)
                        {
                            SaveFile(new[] { "companies", companyId, "centro", centroId, "work", workId, "evaldoc" + i }, docs[i]);
                        }
                        result = new { error = false };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error " };
                }
            }
            return Ok(result);
        }




        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "CLASES"
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static bool deleteWorkInTransaction(SqlConnection conn, SqlTransaction transaction, string workId)
        {
            bool failed = false;

            //Obtener la ID de la empresa a la que pertenece el trabajo
            string companyId = null, centroId = null, signLink = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "SELECT C.companyId, C.id, T.signLink FROM trabajos T INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @ID";
                    command.Parameters.AddWithValue("@ID", workId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId"));
                            signLink = reader.GetString(reader.GetOrdinal("signLink"));
                            centroId = reader.GetString(reader.GetOrdinal("id"));
                        }
                        else
                        {
                            failed = true;
                        }
                    }
                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            //Eliminar las entregas de cuestionarios para este trabajo, los arhivos
            List<string> submissions = new List<string>();
            using (SqlCommand command = conn.CreateCommand())
            {
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "SELECT id FROM emision_cuestionarios WHERE trabajoId LIKE @ID";
                    command.Parameters.AddWithValue("@ID", workId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            submissions.Add(reader.GetString(reader.GetOrdinal("id")));
                        }
                    }
                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            //Eliminar las entregas de cuestionarios para este trabajo
            using (SqlCommand command = conn.CreateCommand())
            {
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM emision_cuestionarios WHERE trabajoId LIKE @ID";
                    command.Parameters.AddWithValue("@ID", workId);
                    command.ExecuteNonQuery();

                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            //Eliminar las asociaciones de formularios del trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM vinculos_trabajos_formularios WHERE trabajoId LIKE @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Antes de borar los documentos, borrar las descargas de estos codumentos
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE TD FROM category_documents_downloads TD INNER JOIN category_documents T ON TD.documentId = T.id WHERE T.trabajoid = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Al borrar un trabajo se eliminenen todos los doumentos asociados
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM category_documents WHERE trabajoid = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Quitar los lastSignLink de los candidatos
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET lastSignLink = NULL WHERE lastSignLink = @SIGNLINK";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.Parameters.AddWithValue("@SIGNLINK", signLink);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Eliminar los plueses preestablecidos del trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM trabajos_pluses WHERE trabajoid = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Borrar el registro de fichaje de este trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM workshifts WHERE signLink = @SIGNLINK";
                        command.Parameters.AddWithValue("@SIGNLINK", signLink);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Eliminar el trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM trabajos WHERE id LIKE @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                        DeleteDir(new[] { "work", workId });
                        foreach (string submitId in submissions)
                        {
                            DeleteDir(new[] { "questionnaire_submissions", submitId });
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Eliminar la carpeta del trabajo
            if (!failed)
            {
                DeleteDir(new[] { "companies", companyId, "centro", centroId, "work", workId });
            }

            return failed;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
