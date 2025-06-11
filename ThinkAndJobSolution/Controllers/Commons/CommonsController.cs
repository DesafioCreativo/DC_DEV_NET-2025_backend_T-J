using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Text.Json;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.Commons
{
    [Route("api/v1/commons")]
    [ApiController]
    [Authorize]
    public class CommonsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public CommonsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpPost]
        [Route("register-log/")]
        public async Task<IActionResult> Register()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkRegister.Register", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string action = await readerBody.ReadToEndAsync();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string userId;
                    using (SqlCommand command = conn.CreateCommand())
                    {

                        command.CommandText = "SELECT id FROM users WHERE hasToShift = 1 AND securityToken = @TOKEN";
                        command.Parameters.AddWithValue("@TOKEN", securityToken);
                        userId = (string)command.ExecuteScalar();
                    }

                    if (userId != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {

                            command.CommandText = "INSERT INTO telework_register (userId, action) VALUES (@USER, @ACTION)";
                            command.Parameters.AddWithValue("@USER", userId);
                            command.Parameters.AddWithValue("@ACTION", action);
                            command.ExecuteNonQuery();
                        }
                    }

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5431, no se ha podido registrar el evento" };
                }
            }

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [Route("profile-pic-nocache/{code}")]
        public IActionResult GetProfilePicNoCache(string code)
        {
            return getProfilePic(code);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("profile-pic/{code}")]
        public IActionResult GetProfilePicCache(string code)
        {
            return getProfilePic(code);
        }

        [HttpPost]
        [Route("verify-captcha")]
        public async Task<IActionResult> VerifyCaptcha()
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using StreamReader bodyReader = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("response", out JsonElement responseJson))
            {
                string responseToken = responseJson.GetString();

                var client = _httpClientFactory.CreateClient();
                var secretKey = "6LflRkEpAAAAAArP1rU9lkAIrGQO8fgSgGA2frTp"; // Secret Key de reCAPTCHA
                try
                {
                    // Para que el endpoint de google funcione, es necesario:
                    // 1. Que sea una petición POST.
                    // 2. Que el content-type sea application/x-www-form-urlencoded por defecto.
                    // 3. Que los parámetros se envíen en el body en formato urlencoded,
                    //    es decir, que se envíen en el body como si fueran parámetros de una URL.
                    var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify",
                        new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "secret", secretKey },
                        { "response", responseToken }
                    }));

                    if (response.IsSuccessStatusCode)
                    {
                        var resultJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                        bool success = resultJson.GetProperty("success").GetBoolean();
                        result = new { success, error = false };
                    }
                    else
                    {
                        result = new { error = "Error 5591, no se ha podido verificar el captcha." };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5592, no se ha podido verificar el captcha." };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------



        // Ayuda

        private FileContentResult getProfilePic(string code)
        {
            string picture = null;
            string format = "image/png";
            string defaultImage = "default_profile.png";
            try
            {
                string[] parts = code.Split('-');
                string prefix = parts[0];
                string id = parts[1];

                switch (prefix)
                {
                    case "ca":
                        picture = ReadFile(new[] { "candidate", id, "photo" });
                        break;
                    case "cl":
                        picture = ReadFile(new[] { "clientuser", id, "photo" });
                        break;
                    case "rj":
                        picture = ReadFile(new[] { "users", id, "photo" });
                        break;
                    case "candidate_group":
                        picture = ReadFile(new[] { "candidate_group", id, "photo" });
                        defaultImage = "default_candidate_group.png";
                        break;
                }
            }
            catch (Exception)
            {

            }

            byte[] bytes;
            if (picture == null)
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Images", defaultImage);
                bytes = System.IO.File.ReadAllBytes(filePath);
            }
            else
            {
                if (picture.Contains(","))
                {
                    string[] parts = picture.Split(",");
                    //Sacar el formato de la imagen
                    if (parts[0].StartsWith("data:"))
                        parts[0] = parts[0].Substring(5);
                    if (parts[0].Contains(";"))
                        parts[0] = parts[0].Split(";")[0];
                    picture = parts[1];
                }
                bytes = Convert.FromBase64String(picture);
            }

            return File(bytes, format);
        }
    }
}
