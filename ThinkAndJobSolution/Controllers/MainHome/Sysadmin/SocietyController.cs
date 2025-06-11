using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Utils;

namespace ThinkAndJobSolution.Controllers.MainHome.Sysadmin
{
    [Route("api/v1/society")]
    [ApiController]
    [Authorize]
    public class SocietyController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "{submitId}/")]
        public IActionResult GetSocietyById(string submitId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.GetSocietyById", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM societies WHERE id = @SUBMIT_ID";
                    command.Parameters.AddWithValue("@SUBMIT_ID", submitId);
                    try
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var sociedad = new Society();
                                sociedad.id = reader.GetInt32(reader.GetOrdinal("id"));
                                sociedad.name = reader.GetString(reader.GetOrdinal("name"));
                                sociedad.logo = reader.GetString(reader.GetOrdinal("logo"));
                                sociedad.contracts = 0;
                                sociedad.maxContracts = reader.GetInt32(reader.GetOrdinal("maxContracts"));
                                sociedad.contracts = getContracts(sociedad.id);

                                result = sociedad;
                            }
                            else
                            {
                                result = new { error = "Error 3194, no existe esa sociedad." };
                            }
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 3193, error en la solicitud." };
                    }

                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list/")]
        public IActionResult GetSocietiesList()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.GetSocietiesList", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM societies ORDER BY id ASC";
                    try
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            List<Society> sociedades = new List<Society>();
                            while (reader.Read())
                            {
                                Society sociedad = new Society();
                                sociedad.id = reader.GetInt32(reader.GetOrdinal("id"));
                                sociedad.name = reader.GetString(reader.GetOrdinal("name"));
                                sociedad.logo = reader.GetString(reader.GetOrdinal("logo"));
                                sociedad.contracts = 0;
                                sociedad.maxContracts = reader.GetInt32(reader.GetOrdinal("maxContracts"));
                                sociedad.contracts = getContracts(sociedad.id);
                                sociedades.Add(sociedad);
                            }
                            result = sociedades;
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 3193, error en la solicitud." };
                    }

                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "")]
        public async Task<IActionResult> CreateSociety()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.CreateSociety", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using System.IO.StreamReader readerBody = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("name", out JsonElement nameJson) && json.TryGetProperty("logo", out JsonElement logoJson) &&
                json.TryGetProperty("maxContracts", out JsonElement maxContractsJson))
            {
                string name = nameJson.GetString().Trim();
                string logo = logoJson.GetString().Trim();
                int maxContracts = maxContractsJson.GetInt32();
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO societies (name, logo, maxContracts) VALUES (@NAME, @LOGO, @MAX_CONTRACTS)";
                        command.Parameters.AddWithValue("@NAME", name);
                        command.Parameters.AddWithValue("@LOGO", logo);
                        command.Parameters.AddWithValue("@MAX_CONTRACTS", maxContracts);
                        try
                        {
                            command.ExecuteNonQuery();
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5193, error en la solicitud." };
                        }

                    }
                }
            }
            return Ok(result);
        }


        [HttpPut]
        [Route(template: "{societyId}/")]
        public async Task<IActionResult> UpdateSociety(string societyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.UpdateSociety", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using System.IO.StreamReader readerBody = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("name", out JsonElement nameJson) && json.TryGetProperty("logo", out JsonElement logoJson) &&
                json.TryGetProperty("maxContracts", out JsonElement maxContractsJson))
            {
                string name = nameJson.GetString().Trim();
                string logo = logoJson.GetString().Trim();
                int maxContracts = maxContractsJson.GetInt32();
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE societies SET name = @NAME, logo = @LOGO, maxContracts = @MAX_CONTRACTS WHERE id = @ID";
                        command.Parameters.AddWithValue("@NAME", name);
                        command.Parameters.AddWithValue("@LOGO", logo);
                        command.Parameters.AddWithValue("@MAX_CONTRACTS", maxContracts);
                        command.Parameters.AddWithValue("@ID", societyId);
                        try
                        {
                            command.ExecuteNonQuery();
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5193, error en la solicitud." };
                        }

                    }
                }
            }
            return Ok(result);
        }


        [HttpDelete]
        [Route(template: "{societyId}/")]
        public IActionResult DeleteSociety(string societyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.DeleteSociety", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }

            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "DELETE FROM societies WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", societyId);
                    try
                    {
                        command.ExecuteNonQuery();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5193, error en la solicitud." };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "logo/")]
        public async Task<IActionResult> UploadLogo()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.UploadLogo", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("name", out JsonElement nameJson) && json.TryGetProperty("image", out JsonElement imageJson))
            {
                string name = nameJson.GetString().Trim();
                string image = imageJson.GetString();
                try
                {
                    if (name.Contains("..")) throw new Exception("Ni pensarlo");
                    if (image.Contains(","))
                    {
                        image = image.Split(",")[1];
                    }
                    string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logos", name);
                    Byte[] bytes = Convert.FromBase64String(image);
                    System.IO.File.WriteAllBytes(path, bytes);
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5011, no se ha podido guardar el fichero" };
                }
            }
            return Ok(result);
        }


        [HttpPost]
        [Route(template: "logo/delete/")]
        public async Task<IActionResult> DeleteLogo()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.DeleteLogo", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using System.IO.StreamReader readerBody = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("name", out JsonElement nameJson))
            {
                string name = nameJson.GetString().Trim();
                try
                {
                    if (name.Contains("..")) throw new Exception("Ni pensarlo");
                    string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logos", name);
                    System.IO.File.Delete(path);
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5012, no se ha podido eliminar el fichero" };
                }
            }
            return Ok(result);
        }


        [HttpGet]
        [Route(template: "logo/")]
        public IActionResult ListLogos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            if (!HelperMethods.HasPermission("Society.ListLogos", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }

            try
            {
                string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logos");
                string[] logos = System.IO.Directory.GetFiles(path);
                for (int i = 0; i < logos.Length; i++)
                {
                    logos[i] = Path.GetFileName(logos[i]);
                }
                result = new { error = false, logos };
            }
            catch (Exception)
            {
                result = new { error = "Error 5010, no se han podido listar los ficheros" };
            }
            return Ok(result);
        }



        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        private int getContracts(int societyId)
        {
            DateTime now = DateTime.Now.Date;
            if (societyId != 2) return 0;
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE cesionActiva = 1 AND test = 0";
                    command.Parameters.AddWithValue("@NOW", now);
                    try
                    {
                        return (Int32)command.ExecuteScalar();
                    }
                    catch (Exception)
                    {

                    }

                }
            }
            return 0;
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
