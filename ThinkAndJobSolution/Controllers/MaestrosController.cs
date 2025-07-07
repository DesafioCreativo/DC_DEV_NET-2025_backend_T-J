using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Model;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers
{
    [Route("api/v1/")]
    [ApiController]
    [Authorize]
    public class MaestrosController : Controller
    {
        #region Categorias

        [AllowAnonymous]
        [HttpGet]
        [Route("category/list/")]
        public IActionResult LeerCategorias()
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
                        categories = ListadoDeCategorias(conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar las categorias" };
                }
            }
            return Ok(result);
        }

        public static List<Categoria> ListadoDeCategorias(SqlConnection conn)
        {
            List<Categoria> categories = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM categories ORDER BY name";

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
            return categories;
        }

        #endregion

    }
}
