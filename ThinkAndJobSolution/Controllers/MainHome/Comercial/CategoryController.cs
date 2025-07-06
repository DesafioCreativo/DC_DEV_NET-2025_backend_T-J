using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using ThinkAndJobSolution.Controllers.MainHome.Prl;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.CompanyContratoController;
using ThinkAndJobSolution.Controllers._Model;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/category")]
    [ApiController]
    [Authorize]
    public class CategoryController : ControllerBase
    {
        [HttpGet]
        [Route(template: "{categoryId}")]
        public IActionResult Get(string categoryId)
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
                    Categoria? category = getCategory(conn, categoryId);
                    if (category == null)
                    {
                        result = new { error = "Error 5812, categoria no encontrada" };
                    }
                    else
                    {
                        result = new { error = false, category };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener la categoria" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "calculate-affects/{categoryId}/")]
        public IActionResult CalculateRenameAffects(string categoryId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Category.CalculateRenameAffects", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            int n = 0;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) FROM centros CE WHERE EXISTS(SELECT * FROM trabajos T WHERE T.centroId = CE.id AND T.categoryId = @CATEGORY)";

                        command.Parameters.AddWithValue("@CATEGORY", categoryId);

                        n = (int)command.ExecuteScalar();
                    }
                    result = new
                    {
                        error = false,
                        n
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5894, no se ha podido calcular el efecto del renombre" };
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
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Category.Create", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });                
            }
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("name", out JsonElement nameJson) &&
                json.TryGetProperty("details", out JsonElement detailsJson))
            {
                string name = nameJson.GetString();
                string details = detailsJson.GetString();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        string categoryId = ComputeStringHash(name + details + DateTime.Now);
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "INSERT INTO categories (id, name, details) VALUES (@ID, @NAME, @DETAILS)";
                            command.Parameters.AddWithValue("@ID", categoryId);
                            command.Parameters.AddWithValue("@NAME", name);
                            command.Parameters.AddWithValue("@DETAILS", details);
                            command.ExecuteNonQuery();
                        }
                        result = new
                        {
                            error = false
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5892, no se ha podido crear la categoria" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{categoryId}/")]
        public IActionResult Delete(string categoryId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Category.Delete", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });                
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        //Obtener el nombre, porque mas tarde ya no se podra
                        string name = FindCategoryNameByCategoryId(categoryId, conn, transaction);
                        //Eliminar los documentos
                        List<string> documents = new();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT CD.id FROM category_documents CD WHERE\n" +
                                                  "CD.categoryId = @CATEGORY OR\n" +
                                                  "CD.trabajoId IN (SELECT T.id FROM trabajos T WHERE T.categoryId = @CATEGORY)";
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read()) documents.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }
                        foreach (string document in documents)
                        {
                            PRLDocumentController.deleteDocumentInTransaction(conn, transaction, document);
                        }
                        //Desasociar los tests
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM vinculos_categorias_formularios WHERE categoryId = @CATEGORY";
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.ExecuteNonQuery();
                        }
                        //Eliminar los trabajos
                        List<string> trabajos = new();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT T.id FROM trabajos T WHERE T.categoryId = @CATEGORY";
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read()) trabajos.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }
                        foreach (string trabajo in trabajos)
                        {
                            if (WorkController.deleteWorkInTransaction(conn, transaction, trabajo))
                                throw new Exception("Error al eliminar trabajo");
                        }
                        //Eliminar la categoria
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM categories WHERE id = @CATEGORY";
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.ExecuteNonQuery();
                        }
                        LogToDB(LogType.CATEGORY_DELETED, $"Categoría '{name}' borrada", FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);
                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5834, no se ha podido eliminar la categoria" };
                    }
                }
            }
            return Ok(result);
        }




        [HttpGet]
        [Route(template: "list-for-company/{companyId}/")]
        public IActionResult ListForCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Category.ListForCompany", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    //Obtener el ultimo contrato cerrado de la empresa
                    List<Puesto> puestos = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        //TODO: Volver a poner este closed = 1 cuando haya que cerrar los contratos para que las categorias apliquen.
                        command.CommandText = "SELECT TOP 1 puestos FROM company_contratos WHERE companyId = @COMPANY_ID ORDER BY date DESC"; // AND closed = 1
                        command.Parameters.AddWithValue("@COMPANY_ID", companyId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                puestos = parsePuestos(reader.GetString(reader.GetOrdinal("puestos")));
                    }
                    //Buscar las categorias en la BD
                    List<Categoria> categories = new();
                    foreach (Puesto puesto in puestos)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT * FROM categories WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", puesto.categoryId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    categories.Add(new Categoria()
                                    {
                                        id = reader.GetString(reader.GetOrdinal("id")),
                                        name = reader.GetString(reader.GetOrdinal("name")),
                                        details = reader.GetString(reader.GetOrdinal("details")),
                                        isNew = reader.GetInt32(reader.GetOrdinal("isNew")) == 1
                                    });
                                }
                            }
                        }
                    }
                    result = new
                    {
                        error = false,
                        categories
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar las categorias" };
                }
            }
            return Ok(result);
        }


        [HttpPatch]
        [Route(template: "is-not-new/{categoryId}/")]
        public IActionResult MarkNotNew(string categoryId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Category.MarkNotNew", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });                
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE categories SET isNew = 0 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", categoryId);
                        command.ExecuteNonQuery();
                    }
                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5892, no se ha podido modificar la categoria" };
                }
            }
            return Ok(result);
        }


        [HttpPut]
        [Route(template: "update/")]
        public async Task<IActionResult> Update()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Category.Update", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });                
            }
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("categoryId", out JsonElement categoryIdJson) &&
                json.TryGetProperty("name", out JsonElement nameJson) &&
                json.TryGetProperty("details", out JsonElement detailsJson))
            {
                string categoryId = categoryIdJson.GetString();
                string name = nameJson.GetString();
                string details = detailsJson.GetString();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "UPDATE categories SET name = @NAME, details = @DETAILS WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", categoryId);
                            command.Parameters.AddWithValue("@NAME", name);
                            command.Parameters.AddWithValue("@DETAILS", details);
                            command.ExecuteNonQuery();
                        }
                        result = new
                        {
                            error = false
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5893, no se ha podido actualizar la categoria" };
                    }

                }
            }
            return Ok(result);
        }

        //------------------------------WorkController Inicio------------------------------
        [HttpGet]
        [Route(template: "forms/list/{categoryId}/")]
        public IActionResult ListCategoryLinkedForms(string categoryId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Category.ListCategoryLinkedForms", securityToken).Acceso)
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
                        command.CommandText =
                                "SELECT F.id as formId, F.nombre as formName, F.tipo as testType, CASE WHEN EXISTS(" +
                                "SELECT id FROM vinculos_categorias_formularios VCF " +
                                "WHERE VCF.formularioId = f.id AND VCF.categoryId = @CATEGORY)" +
                                "THEN 'TRUE' ELSE 'FALSE' END AS checked " +
                                "FROM formularios as F ORDER BY checked DESC";
                        command.Parameters.AddWithValue("@CATEGORY", categoryId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {

                            List<object> linkedForms = new List<object>();
                            while (reader.Read())
                            {
                                linkedForms.Add(new
                                {
                                    formId = reader.GetString(reader.GetOrdinal("formId")),
                                    formName = reader.GetString(reader.GetOrdinal("formName")),
                                    type = reader.GetString(reader.GetOrdinal("testType")),
                                    check = reader.GetString(reader.GetOrdinal("checked")).Equals("TRUE")
                                });
                            }
                            result = linkedForms;
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "forms/create/{categoryId}/{formId}/")]
        public IActionResult CreateCategoryLinkedForm(string categoryId, string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Category.CreateCategoryLinkedForm", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO vinculos_categorias_formularios(id, categoryId, formularioId) VALUES (@ID, @CATEGORY, @FORM)";
                            command.Parameters.AddWithValue("@ID", ComputeStringHash(categoryId + formId));
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.Parameters.AddWithValue("@FORM", formId);
                            command.ExecuteNonQuery();
                            result = new { error = false };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new
                    {
                        error = "Error 5421, no se ha podido asignar el formulario. Inténtelo más tarde."
                    };
                }
            }
            return Ok(result);
        }


        [HttpDelete]
        [Route(template: "forms/delete/{categoryId}/{formId}/")]
        public IActionResult DeleteCategoryLinkedForm(string categoryId, string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Category.DeleteCategoryLinkedForm", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM vinculos_categorias_formularios WHERE categoryId = @CATEGORY AND formularioId = @FORM";
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.Parameters.AddWithValue("@FORM", formId);
                            command.ExecuteNonQuery();
                            result = new { error = false };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new
                    {
                        error = "Error 5421, no se ha podido desasignar el formulario. Inténtelo más tarde."
                    };
                }
            }
            return Ok(result);
        }

        


        public static Categoria? getCategory(SqlConnection conn, string categoryId)
        {
            Categoria? category = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM categories WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", categoryId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        category = new Categoria()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            details = reader.GetString(reader.GetOrdinal("details")),
                            isNew = reader.GetInt32(reader.GetOrdinal("isNew")) == 1
                        };
                    }
                }
            }
            return category;
        }
    }
}
