using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/grupo-empresarial")]
    [ApiController]
    [Authorize]
    public class GrupoEmpresarialController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        //Listado

        [HttpGet]
        [Route(template: "list/")]
        public IActionResult List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("GrupoEmpresarial.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<GrupoEmpresarial> grupos = new List<GrupoEmpresarial>();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT G.* FROM grupos_empresariales G";

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string id = reader.GetString(reader.GetOrdinal("id"));
                                GrupoEmpresarial grupo = new GrupoEmpresarial
                                {
                                    id = id,
                                    nombre = reader.GetString(reader.GetOrdinal("nombre")),
                                    descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion")) ? null : reader.GetString(reader.GetOrdinal("descripcion")),
                                    icon = ReadFile(new[] { "grupoEmpresarial", id, "icon" }),
                                    clientes = new()
                                };
                                grupos.Add(grupo);
                            }
                        }
                    }

                    foreach (GrupoEmpresarial grupo in grupos)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT E.id, E.nombre FROM empresas E WHERE grupoId = @ID";
                            command.Parameters.AddWithValue("@ID", grupo.id);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    grupo.clientes.Add(new Empresa
                                    {
                                        id = reader.GetString(reader.GetOrdinal("id")),
                                        nombre = reader.GetString(reader.GetOrdinal("nombre"))
                                    });
                                }
                            }
                        }
                    }
                    result = new
                    {
                        error = false,
                        grupos
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5781, no han podido listar grupos empresariales" };
                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-fast/")]
        public IActionResult FastList()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("GrupoEmpresarial.FastList", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<GrupoEmpresarial> grupos = new List<GrupoEmpresarial>();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT G.* FROM grupos_empresariales G";

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string id = reader.GetString(reader.GetOrdinal("id"));
                                GrupoEmpresarial grupo = new GrupoEmpresarial
                                {
                                    id = id,
                                    nombre = reader.GetString(reader.GetOrdinal("nombre"))
                                };
                                grupos.Add(grupo);
                            }
                        }
                    }
                    result = new
                    {
                        error = false,
                        grupos
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5781, no han podido listar grupos empresariales" };
                }

            }

            return Ok(result);
        }

        //Obtencion

        [HttpGet]
        [Route(template: "{grupoId}/")]
        public IActionResult Get(string grupoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("GrupoEmpresarial.Get", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    GrupoEmpresarial? grupo = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM grupos_empresariales WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", grupoId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                grupo = new GrupoEmpresarial
                                {
                                    id = grupoId,
                                    nombre = reader.GetString(reader.GetOrdinal("nombre")),
                                    descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion")) ? null : reader.GetString(reader.GetOrdinal("descripcion")),
                                    icon = ReadFile(new[] { "grupoEmpresarial", grupoId, "icon" }),
                                    clientes = new()
                                };
                            }
                            else
                            {
                                result = new { error = "Error 4860, grupo empresarial no encontrado" };
                            }
                        }
                    }

                    if (grupo != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT E.id, E.nombre FROM empresas E WHERE grupoId = @ID";
                            command.Parameters.AddWithValue("@ID", grupoId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    grupo.Value.clientes.Add(new Empresa
                                    {
                                        id = reader.GetString(reader.GetOrdinal("id")),
                                        nombre = reader.GetString(reader.GetOrdinal("nombre"))
                                    });
                                }
                            }
                        }
                    }

                    if (grupo != null)
                    {
                        result = new { error = false, grupo };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener el grupo" };
                }
            }

            return Ok(result);
        }

        //Creacion

        [HttpPost]
        [Route(template: "create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("GrupoEmpresarial.Create", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("nombre", out JsonElement nombreJson) &&
                json.TryGetProperty("descripcion", out JsonElement descripcionJson) &&
                json.TryGetProperty("icon", out JsonElement iconJson))
            {

                string nombre = nombreJson.GetString();
                string descripcion = descripcionJson.GetString();
                string icon = iconJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        string grupoId = ComputeStringHash(nombre + DateTime.Now + "gpemp");
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "INSERT INTO grupos_empresariales (id, nombre, descripcion) VALUES (@ID, @NOMBRE, @DESCRIPCION)";

                            command.Parameters.AddWithValue("@ID", grupoId);
                            command.Parameters.AddWithValue("@NOMBRE", nombre);
                            command.Parameters.AddWithValue("@DESCRIPCION", (object)descripcion ?? DBNull.Value);

                            command.ExecuteNonQuery();

                            if (icon != null)
                            {
                                DeleteFile(new[] { "grupoEmpresarial", grupoId, "icon" });
                                icon = LimitSquareImage(icon);
                                SaveFile(new[] { "grupoEmpresarial", grupoId, "icon" }, icon);
                            }
                        }

                        result = new
                        {
                            error = false
                        };

                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5872, no se ha podido crear el grupo" };
                    }

                }
            }

            return Ok(result);
        }

        //Actualizacion

        [HttpPut]
        [Route(template: "update/{grupoId}/")]
        public async Task<IActionResult> Update(string grupoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("GrupoEmpresarial.Update", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("nombre", out JsonElement nombreJson) &&
                json.TryGetProperty("descripcion", out JsonElement descripcionJson) &&
                json.TryGetProperty("icon", out JsonElement iconJson))
            {

                string nombre = nombreJson.GetString();
                string descripcion = descripcionJson.GetString();
                string icon = iconJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "UPDATE grupos_empresariales SET nombre = @NOMBRE, descripcion = @DESCRIPCION WHERE id = @ID";

                            command.Parameters.AddWithValue("@ID", grupoId);
                            command.Parameters.AddWithValue("@NOMBRE", nombre);
                            command.Parameters.AddWithValue("@DESCRIPCION", (object)descripcion ?? DBNull.Value);

                            if (command.ExecuteNonQuery() > 0)
                            {
                                DeleteFile(new[] { "grupoEmpresarial", grupoId, "icon" });
                                if (icon != null)
                                {
                                    icon = LimitSquareImage(icon);
                                    if (icon.Length != 7) SaveFile(new[] { "grupoEmpresarial", grupoId, "icon" }, icon);
                                }

                                result = new { error = false };
                            }
                            else
                            {
                                result = new { error = "4860, grupo empresarial no encontrado" };
                            }
                        }



                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5872, no se ha podido crear el grupo" };
                    }

                }
            }

            return Ok(result);
        }

        //Eliminacion

        [HttpDelete]
        [Route(template: "{grupoId}/")]
        public IActionResult Delete(string grupoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("GrupoEmpresarial.Delete", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE empresas SET grupoId = NULL WHERE grupoId = @ID";
                        command.Parameters.AddWithValue("@ID", grupoId);
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM grupos_empresariales WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", grupoId);
                        if (command.ExecuteNonQuery() > 0)
                        {
                            DeleteDir(new[] { "grupoEmpresarial", grupoId });
                            result = new { error = false };
                        }
                        else
                        {
                            result = new { error = "4860, grupo empresarial no encontrado" };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido eliminar el grupo" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------

        //Ayuda
        public struct GrupoEmpresarial
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public string descripcion { get; set; }
            public string icon { get; set; }
            public List<Empresa> clientes { get; set; }
        }

        public struct Empresa
        {
            public string id { get; set; }
            public string nombre { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
