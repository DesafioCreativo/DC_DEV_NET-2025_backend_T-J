using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Sysadmin
{
    [Route("api/v1/insurance")]
    [ApiController]
    [Authorize]
    public class InsuranceController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "list/")]
        public IActionResult List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("Insurance.List", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<Insurance> insurances = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM servicios_prevencion";

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                insurances.Add(new Insurance()
                                {
                                    id = reader.GetInt32(reader.GetOrdinal("id")),
                                    nombre = reader.GetString(reader.GetOrdinal("nombre"))
                                });
                            }
                        }
                    }
                    result = new
                    {
                        error = false,
                        insurances
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5791, no han podido listar los servicios de prevencion" };
                }

            }
            return Ok(result);
        }


        [HttpGet]
        [Route(template: "api/v1/insurance/{insuranceId}")]
        public IActionResult Get(int insuranceId)
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
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM servicios_prevencion WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", insuranceId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new
                                {
                                    error = false,
                                    insurance = new Insurance()
                                    {
                                        id = reader.GetInt32(reader.GetOrdinal("id")),
                                        nombre = reader.GetString(reader.GetOrdinal("nombre"))
                                    }
                                };
                            }
                            else
                            {
                                result = new { error = "Error 4721, servicio de prevencion no encontrado" };
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5721, no ha podido obtener el servicio de prevencion" };
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
            if (!HasPermission("Insurance.Create", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("nombre", out JsonElement nombreJson))
            {
                string nombre = nombreJson.GetString();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO servicios_prevencion (nombre) VALUES (@NOMBRE)";
                            command.Parameters.AddWithValue("@NOMBRE", nombre);
                            command.ExecuteNonQuery();
                        }

                        result = new
                        {
                            error = false
                        };

                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5892, no se ha podido crear el servicio de prevención" };
                    }

                }
            }
            return Ok(result);
        }


        [HttpPut]
        [Route(template: "update/{insuranceId}")]
        public async Task<IActionResult> Update(int insuranceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Insurance.Update", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });                
            }
            StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await reader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("nombre", out JsonElement nombreJson))
            {
                string nombre = nombreJson.GetString();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        bool failed = false;
                        //Comprobar si existe
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT COUNT(*) FROM servicios_prevencion WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", insuranceId);
                            if ((int)command.ExecuteScalar() == 0)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 4002, servicio de prevencion no encontrado"
                                };
                            }
                        }
                        if (!failed)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE servicios_prevencion SET nombre = @NOMBRE";
                                command.Parameters.AddWithValue("@ID", insuranceId);
                                command.Parameters.AddWithValue("@NOMBRE", nombre);
                                command.ExecuteNonQuery();
                            }
                            result = new { error = false };
                        }
                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            transaction.Commit();
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{insuranceId}/")]
        public IActionResult Delete(int insuranceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Insurance.Delete", securityToken).Acceso)
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
                        command.CommandText = "UPDATE centros SET servicioPrevencion = NULL WHERE servicioPrevencion = @ID";
                        command.Parameters.AddWithValue("@ID", insuranceId);
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM servicios_prevencion WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", insuranceId);
                        if (command.ExecuteNonQuery() == 0)
                        {
                            result = new { error = "Error 4890, servicio de prevención no encontrado" };
                        }
                        else
                        {
                            result = new { error = false };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5894, no se ha podido eliminar el servicio de prevencion" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        public struct Insurance
        {
            public int id { get; set; }
            public string nombre { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
