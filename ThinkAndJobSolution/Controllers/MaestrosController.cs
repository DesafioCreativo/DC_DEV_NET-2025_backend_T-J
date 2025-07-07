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

        #region Localidades

        [AllowAnonymous]
        [HttpGet]
        [Route("locality/listByProvince/")]
        public IActionResult LeerLocalidadesPorProvince(int idProvincia)
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
                        localidades = ListadoDeLocalidadesPorProvincia(conn, idProvincia)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar las localidades" };
                }
            }
            return Ok(result);
        }

        public static List<Localidad> ListadoDeLocalidadesPorProvincia(SqlConnection conn, int idProvincia)
        {
            List<Localidad> localidades = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM const_localidades WHERE provinciaRef = " + idProvincia  +" ORDER BY nombre";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        localidades.Add(new Localidad()
                        {
                            _ref = reader.IsDBNull(reader.GetOrdinal("ref")) ? 0 : reader.GetInt32(reader.GetOrdinal("ref")),
                            provinciaRef = reader.IsDBNull(reader.GetOrdinal("provinciaRef")) ? 0 : reader.GetInt32(reader.GetOrdinal("provinciaRef")),
                            nombre = reader.IsDBNull(reader.GetOrdinal("nombre")) ? "" : reader.GetString(reader.GetOrdinal("nombre")),
                            integration_id = reader.IsDBNull(reader.GetOrdinal("integration_id")) ? "" : reader.GetString(reader.GetOrdinal("integration_id")),
                            api_id = reader.IsDBNull(reader.GetOrdinal("api_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("api_id")),
                            code = reader.IsDBNull(reader.GetOrdinal("code")) ? "" : reader.GetString(reader.GetOrdinal("code")),
                            timezone = reader.IsDBNull(reader.GetOrdinal("timezone")) ? "" : reader.GetString(reader.GetOrdinal("timezone")),
                            name_dt = reader.IsDBNull(reader.GetOrdinal("name_dt")) ? "" : reader.GetString(reader.GetOrdinal("name_dt")),
                            parent = reader.IsDBNull(reader.GetOrdinal("parent")) ? 0 : reader.GetInt32(reader.GetOrdinal("parent"))
                        });
                    }
                }
            }
            return localidades;
        }

        #endregion
    }
}
