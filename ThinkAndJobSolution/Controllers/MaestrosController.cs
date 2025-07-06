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

        #region Paises

        [AllowAnonymous]
        [HttpGet]
        [Route("country/list/")]
        public IActionResult LeerPaises()
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
                        paises = ListadoDePaises(conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar los paises" };
                }
            }
            return Ok(result);
        }

        public static List<Pais> ListadoDePaises(SqlConnection conn)
        {
            List<Pais> paises = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM const_paises ORDER BY nombre";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        paises.Add(new Pais()
                        {
                            iso3 = reader.IsDBNull(reader.GetOrdinal("iso3")) ? "" : reader.GetString(reader.GetOrdinal("iso3")),
                            iso2 = reader.IsDBNull(reader.GetOrdinal("iso2")) ? "" : reader.GetString(reader.GetOrdinal("iso2")),
                            codigo = reader.IsDBNull(reader.GetOrdinal("codigo")) ? 0 : reader.GetInt32(reader.GetOrdinal("codigo")),
                            nombre = reader.IsDBNull(reader.GetOrdinal("nombre")) ? "" : reader.GetString(reader.GetOrdinal("nombre")),
                            schengen = !reader.IsDBNull(reader.GetOrdinal("schengen")) && reader.GetBoolean(reader.GetOrdinal("schengen"))
                        });
                    }
                }
            }
            return paises;
        }

        #endregion

        #region Provincias

        [AllowAnonymous]
        [HttpGet]
        [Route("province/list/")]
        public IActionResult LeerProvincias()
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
                        provincias = ListadoDeProvincias(conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar las provincias" };
                }
            }
            return Ok(result);
        }

        public static List<Provincia> ListadoDeProvincias(SqlConnection conn)
        {
            List<Provincia> provincias = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT * FROM const_provincias ORDER BY nombre";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        provincias.Add(new Provincia()
                        {
                            _ref = reader.IsDBNull(reader.GetOrdinal("ref")) ? 0 : reader.GetInt32(reader.GetOrdinal("ref")),
                            nombre = reader.IsDBNull(reader.GetOrdinal("nombre")) ? "" : reader.GetString(reader.GetOrdinal("nombre")),
                            id_integration = reader.IsDBNull(reader.GetOrdinal("id_integration")) ? "" : reader.GetString(reader.GetOrdinal("id_integration")),
                            id_api = reader.IsDBNull(reader.GetOrdinal("id_api")) ? 0 : reader.GetInt32(reader.GetOrdinal("id_api")),
                            code = reader.IsDBNull(reader.GetOrdinal("code")) ? "" : reader.GetString(reader.GetOrdinal("code")),
                            timezone = reader.IsDBNull(reader.GetOrdinal("timezone")) ? "" : reader.GetString(reader.GetOrdinal("timezone")),
                            name_dt = reader.IsDBNull(reader.GetOrdinal("name_dt")) ? "" : reader.GetString(reader.GetOrdinal("name_dt")),
                            parent = reader.IsDBNull(reader.GetOrdinal("parent")) ? 0 : reader.GetInt32(reader.GetOrdinal("parent"))
                        });
                    }
                }
            }
            return provincias;
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
                            id_integration = reader.IsDBNull(reader.GetOrdinal("id_integration")) ? "" : reader.GetString(reader.GetOrdinal("id_integration")),
                            id_api = reader.IsDBNull(reader.GetOrdinal("id_api")) ? 0 : reader.GetInt32(reader.GetOrdinal("id_api")),
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
