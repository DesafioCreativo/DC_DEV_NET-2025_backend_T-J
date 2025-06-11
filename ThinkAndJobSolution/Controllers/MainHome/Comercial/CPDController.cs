using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using System.IO.Compression;
using ThinkAndJobSolution.Controllers.MainHome.RRHH;
using iTextSharp.text.exceptions;
using iTextSharp.text.pdf;
using iTextSharp.text;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    //[Route("api/[controller]")]
    //[ApiController]
    [Route("api/v1/cpd")]
    [ApiController]
    [Authorize]
    public class CPDController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "list/{companyId}/{ano}/{mes}/{signed}/{candidateDNI}/")]
        public IActionResult List(string companyId, int? ano, int? mes, bool? signed, string candidateDNI)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CPD.List", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            if (companyId == "null")
                companyId = null;
            if (candidateDNI == "null")
                candidateDNI = null;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                result = new
                {
                    error = false,
                    cpds = listCPDs(conn, companyId, signed, mes, ano, candidateDNI)
                };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "api/v1/cpd/list-for-client/{companyId}/{ano}/{mes}/{signed}/")]
        public IActionResult ListForClient(string companyId, int? ano, int? mes, bool? signed)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_CPDS) == null)
            {
                return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                result = new
                {
                    error = false,
                    cpds = listCPDs(conn, companyId, signed, mes, ano)
                };
            }
            return Ok(result);
        }

        //Obtencion
        [HttpGet]
        [Route(template: "api/v1/cpd/{cpdId}")]
        public IActionResult Get(string cpdId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            CPD? cpd = getCPD(cpdId);
            if (cpd == null)
            {
                result = new
                {
                    error = "Error 4102, cpd no encontrado"
                };
            }
            else
            {
                result = new
                {
                    error = false,
                    cpd = cpd.Value
                };
            }
            return Ok(result);
        }

        //Creacion
        [HttpPost]
        [Route(template: "create/{companyId}/")]
        public async Task<IActionResult> Create(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CPD.Create", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("cpds", out JsonElement cpdsJson) &&
                json.TryGetProperty("mes", out JsonElement mesJson) && json.TryGetProperty("ano", out JsonElement anoJson))
            {
                List<CPD> cpds = new();
                if (cpdsJson.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement cpdJson in cpdsJson.EnumerateArray())
                    {
                        if (cpdJson.TryGetProperty("nombre", out JsonElement nombreJson) &&
                            cpdJson.TryGetProperty("dni", out JsonElement dniJson) &&
                            cpdJson.TryGetProperty("pdf", out JsonElement pdfJson))
                        {
                            cpds.Add(new CPD()
                            {
                                nombre = nombreJson.GetString(),
                                dni = dniJson.GetString(),
                                pdf = pdfJson.GetString()
                            });
                        }
                    }
                }
                int mes = mesJson.GetInt32();
                int ano = anoJson.GetInt32();
                if (cpds.Count == 0)
                    return Ok(new { error = "4101, no se han subido CPDs." });
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        string[] ids = new string[cpds.Count];
                        for (int i = 0; i < cpds.Count; i++)
                        {
                            //Insertar el nuevo cpd
                            string id = ComputeStringHash(companyId + mes + ano + cpds[i].dni + i + "cpd" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "INSERT INTO cpd " +
                                                      "(id, companyId, date, mes, ano, dni, nombre) VALUES " +
                                                      "(@ID, @COMPANY, @DATE, @MES, @ANO, @DNI, @NOMBRE)";
                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@COMPANY", companyId);
                                command.Parameters.AddWithValue("@DATE", DateTime.Now);
                                command.Parameters.AddWithValue("@MES", mes);
                                command.Parameters.AddWithValue("@ANO", ano);
                                command.Parameters.AddWithValue("@DNI", cpds[i].dni);
                                command.Parameters.AddWithValue("@NOMBRE", cpds[i].nombre.Substring(0, Math.Min(cpds[i].nombre.Length, 200)));
                                command.ExecuteNonQuery();
                            }
                            SaveFile(new[] { "companies", companyId, "cpd", id, "cpd" }, cpds[i].pdf);
                            ids[i] = id;
                        }
                        result = new
                        {
                            error = false,
                            ids
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "5101, no se ha podido crear el CPD." };
                }
            }
            return Ok(result);
        }

        //Eliminacion
        [HttpPost]
        [Route(template: "delete/")]
        public async Task<IActionResult> Delete()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CPD.Delete", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            List<string> ids = GetJsonStringList(json);
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                foreach (string cpdId in ids)
                {
                    //Obtener la empresa
                    string companyId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT companyId FROM cpd WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", cpdId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                companyId = reader.GetString(reader.GetOrdinal("companyId"));
                    }
                    if (companyId != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.CommandText = "DELETE FROM cpd WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", cpdId);
                                command.ExecuteNonQuery();
                                DeleteDir(new[] { "companies", companyId, "cpd", cpdId });
                                result = new { error = false };
                            }
                            catch (Exception)
                            {
                                result = new
                                {
                                    error = "Error 5130, no se ha podido eliminar el cpd"
                                };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        //Descargas
        [HttpGet]
        [Route(template: "download/{cpdId}")]
        public IActionResult Download(string cpdId)
        {
            CPD? cpd = getCPD(cpdId);
            object result;
            if (cpd == null)
            {
                result = new { error = "Error 4102, cpd no encotnrado" };
            }
            else
            {
                result = ReadFile(new[] { "companies", cpd.Value.companyId, "cpd", cpd.Value.id, "cpd" });
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-signed/{cpdId}")]
        public IActionResult DownloadSigned(string cpdId)
        {
            CPD? cpd = getCPD(cpdId);
            object result;

            if (cpd == null)
            {
                result = new { error = "Error 4102, cpd no encotnrado" };
            }
            else
            {
                result = ReadFile(new[] { "companies", cpd.Value.companyId, "cpd", cpd.Value.id, "firmado" });
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "bulk-download/")]
        public async Task<IActionResult> BulkDownload()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("CPD.BulkDownload", securityToken).Acceso)
            {
                return new ForbidResult();
            }

            try
            {
                using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("ids", out JsonElement idsJson) && json.TryGetProperty("prefix", out JsonElement prefixJson))
                {
                    List<string> ids = GetJsonStringList(idsJson);
                    string prefix = prefixJson.GetString();

                    return zipCPDs(ids, prefix);
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e);
            }
            return new NoContentResult();
        }

        [HttpPost]
        [Route(template: "bulk-download-for-client/{companyId}/")]
        public async Task<IActionResult> BulkDownloadForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_CPDS) == null)
            {
                return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
            }
            try
            {
                using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("ids", out JsonElement idsJson))
                {
                    List<string> ids = GetJsonStringList(idsJson);

                    return zipCPDs(ids, FindNameByCompanyId(companyId));
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(e);
            }
            return new NoContentResult();
        }

        [HttpGet]
        [Route(template: "bulk-download-multi-company/{ano}/{mes}/{signed}/")]
        public IActionResult BulkDownloadMultiCompany(int? ano, int? mes, bool? signed)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            ResultadoAcceso access = HasPermission("CPD.BulkDownloadMultiCompany", securityToken);
            if (!access.Acceso)
                return new ForbidResult();
            try
            {
                Dictionary<string, string> companyCacheName = new();
                Dictionary<string, Dictionary<int, Dictionary<int, List<CPD>>>> cpds = new();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT E.id as companyId, E.nombre as companyName, CPD.id, CPD.mes, CPD.ano, CPD.dni, CPD.signDate " +
                            "FROM cpd CPD " +
                            "INNER JOIN empresas E ON(CPD.companyId = E.id) " +
                            "WHERE (@SIGNED IS NULL OR (@SIGNED = 0 AND CPD.signDate IS NULL) OR (@SIGNED = 1 AND CPD.signDate IS NOT NULL)) AND " +
                            "(@MES IS NULL OR @MES = CPD.mes) AND " +
                            "(@ANO IS NULL OR @ANO = CPD.ano) " +
                            (filterSecurityToken == null ? "" : "AND EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) WHERE U.securityToken = @TOKEN AND AUE.companyId = E.id) ");
                        command.Parameters.AddWithValue("@TOKEN", (object)filterSecurityToken ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SIGNED", signed == null ? DBNull.Value : (signed.Value ? 1 : 0));
                        command.Parameters.AddWithValue("@MES", mes == null ? DBNull.Value : mes.Value);
                        command.Parameters.AddWithValue("@ANO", ano == null ? DBNull.Value : ano.Value);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string companyId = reader.GetString(reader.GetOrdinal("companyId"));
                                string companyName = reader.GetString(reader.GetOrdinal("companyName"));
                                CPD cpd = new CPD()
                                {
                                    companyId = companyId,
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    signed = !reader.IsDBNull(reader.GetOrdinal("signDate")),
                                    mes = reader.GetInt32(reader.GetOrdinal("mes")),
                                    ano = reader.GetInt32(reader.GetOrdinal("ano")),
                                    dni = reader.GetString(reader.GetOrdinal("dni"))
                                };
                                if (!companyCacheName.ContainsKey(companyId))
                                    companyCacheName[companyId] = companyName;
                                if (!cpds.ContainsKey(companyId))
                                    cpds[companyId] = new();
                                if (!cpds[companyId].ContainsKey(cpd.ano))
                                    cpds[companyId][cpd.ano] = new();
                                if (!cpds[companyId][cpd.ano].ContainsKey(cpd.mes))
                                    cpds[companyId][cpd.ano][cpd.mes] = new();
                                cpds[companyId][cpd.ano][cpd.mes].Add(cpd);
                            }
                        }
                    }
                }
                string tmpPdfDir = GetTemporaryDirectory();
                foreach (var dCompanies in cpds)
                {
                    string companyId = dCompanies.Key;
                    string companyName = companyCacheName[companyId];
                    string pdfCompanyDir = Path.Combine(tmpPdfDir, companyName);
                    Directory.CreateDirectory(pdfCompanyDir);
                    foreach (var dYears in dCompanies.Value)
                    {
                        int year = dYears.Key;
                        string pdfYearDir = Path.Combine(pdfCompanyDir, year.ToString());
                        Directory.CreateDirectory(pdfYearDir);
                        foreach (var dMonths in dYears.Value)
                        {
                            string month = MESES[dMonths.Key - 1];
                            string pdfMonthDir = Path.Combine(pdfYearDir, month);
                            Directory.CreateDirectory(pdfMonthDir);
                            saveCPDinFile(dMonths.Value, pdfMonthDir);
                        }
                    }
                }
                string tmpZipDir = GetTemporaryDirectory();
                string zipName = "cpds.zip";
                string tmpZipFile = Path.Combine(tmpZipDir, zipName);
                ZipFile.CreateFromDirectory(tmpPdfDir, tmpZipFile);
                string contentType = "application/zip";
                HttpContext.Response.ContentType = contentType;
                var response = new FileContentResult(System.IO.File.ReadAllBytes(tmpZipFile), contentType)
                {
                    FileDownloadName = zipName
                };
                Directory.Delete(tmpPdfDir, true);
                Directory.Delete(tmpZipDir, true);

                return response;
            }
            catch (Exception)
            {
                //Console.WriteLine(e);
            }
            return new NoContentResult();
        }

        [HttpPost]
        [Route(template: "sign/{cpdId}")]
        public async Task<IActionResult> Sign(string cpdId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerJson.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("sign", out JsonElement signJson))
            {
                string signBase64 = signJson.GetString();
                CPD? cpd = getCPD(cpdId);
                if (cpd == null)
                {
                    result = new { error = "Error 4002, cpd no encontrado" };
                }
                else
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                        {
                            conn.Open();
                            signCPD(cpd.Value, signBase64, conn);
                            result = new { error = false };
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is BadPasswordException)
                            result = new { error = "Error 5104, el fichero no puede ser firmado porque tiene contraseña." };
                        else
                            result = new { error = "Error 5103, no se ha podido firmar el cpd" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "bulk-sign/")]
        public async Task<IActionResult> BulkSign()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CPD.BulkSign", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerJson.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("sign", out JsonElement signJson) &&
                json.TryGetProperty("ids", out JsonElement idsJson))
            {
                string signBase64 = signJson.GetString();
                List<string> ids = GetJsonStringList(idsJson);
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        foreach (string id in ids)
                        {
                            CPD? cpd = getCPD(id, conn);
                            if (cpd == null) continue;
                            signCPD(cpd.Value, signBase64, conn);
                        }
                        result = new { error = false };
                    }
                }
                catch
                {
                    result = new { error = "Error 5103, no se ha podido firmar los cpds" };
                }
            }
            return Ok(result);
        }



        [HttpPost]
        [Route(template: "bulk-sign-for-client/{companyId}/")]
        public async Task<IActionResult> BulkSignForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_CPDS) == null)
            {
                return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
            }
            StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerJson.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("sign", out JsonElement signJson) && json.TryGetProperty("ids", out JsonElement idsJson))
            {
                string signBase64 = signJson.GetString();
                List<string> ids = GetJsonStringList(idsJson);
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        foreach (string id in ids)
                        {
                            CPD? cpd = getCPD(id, conn);
                            if (cpd == null || cpd.Value.companyId != companyId) continue;

                            signCPD(cpd.Value, signBase64, conn);
                        }
                        result = new { error = false };
                    }
                }
                catch
                {
                    result = new { error = "Error 5103, no se ha podido firmar los cpds" };
                }
            }
            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        public struct CPD
        {
            public string id { get; set; }
            public string companyId { get; set; }
            public bool signed { get; set; }
            public DateTime? signDate { get; set; }
            public DateTime date { get; set; }
            public int mes { get; set; }
            public int ano { get; set; }
            public string dni { get; set; }
            public string nombre { get; set; }
            public string pdf { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------        
        public static CPD? getCPD(string cpdId, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            CPD? cpd = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CPD.* " +
                                      "FROM cpd CPD " +
                                      "WHERE CPD.id = @ID";
                command.Parameters.AddWithValue("@ID", cpdId);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    if (reader.Read())
                    {
                        cpd = new CPD()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            signed = !reader.IsDBNull(reader.GetOrdinal("signDate")),
                            signDate = reader.IsDBNull(reader.GetOrdinal("signDate")) ? null : reader.GetDateTime(reader.GetOrdinal("signDate")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            mes = reader.GetInt32(reader.GetOrdinal("mes")),
                            ano = reader.GetInt32(reader.GetOrdinal("ano")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            nombre = reader.GetString(reader.GetOrdinal("nombre"))
                        };
                    }
                }
            }

            if (lastConn == null) conn.Close();
            return cpd;
        }
        public static List<CPD> listCPDs(SqlConnection conn, string companyId, bool? signed = null, int? mes = null, int? ano = null, string candidateDNI = null)
        {
            List<CPD> cpds = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT CPD.* " +
                                      "FROM cpd CPD " +
                                      "WHERE " +
                                      "(@COMPANY IS NULL OR @COMPANY = CPD.companyId) AND " +
                                      "(@SIGNED IS NULL OR (@SIGNED = 0 AND CPD.signDate IS NULL) OR (@SIGNED = 1 AND CPD.signDate IS NOT NULL)) AND " +
                                      "(@MES IS NULL OR @MES = CPD.mes) AND " +
                                      "(@ANO IS NULL OR @ANO = CPD.ano) AND" +
                                      "(@DNI IS NULL OR @DNI = CPD.dni) " +
                                      "ORDER BY date DESC";
                command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);
                command.Parameters.AddWithValue("@SIGNED", signed == null ? DBNull.Value : (signed.Value ? 1 : 0));
                command.Parameters.AddWithValue("@MES", mes == null ? DBNull.Value : mes.Value);
                command.Parameters.AddWithValue("@ANO", ano == null ? DBNull.Value : ano.Value);
                command.Parameters.AddWithValue("@DNI", (object)candidateDNI ?? DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        cpds.Add(new CPD()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            signed = !reader.IsDBNull(reader.GetOrdinal("signDate")),
                            signDate = reader.IsDBNull(reader.GetOrdinal("signDate")) ? null : reader.GetDateTime(reader.GetOrdinal("signDate")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            mes = reader.GetInt32(reader.GetOrdinal("mes")),
                            ano = reader.GetInt32(reader.GetOrdinal("ano")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            nombre = reader.GetString(reader.GetOrdinal("nombre"))
                        });
                    }
                }
            }

            return cpds;
        }
        private  IActionResult zipCPDs(List<string> ids, string prefix)
        {
            string tmpPdfDir = GetTemporaryDirectory();

            List<CPD> cpds = new();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                foreach (string cpdId in ids)
                {
                    CPD? cpd = getCPD(cpdId, conn);
                    if (cpd != null)
                        cpds.Add(cpd.Value);
                }
            }

            saveCPDinFile(cpds, tmpPdfDir);

            string tmpZipDir = GetTemporaryDirectory();
            string zipName = "cpds.zip";
            string tmpZipFile = Path.Combine(tmpZipDir, zipName);
            ZipFile.CreateFromDirectory(tmpPdfDir, tmpZipFile);

            string contentType = "application/zip";
            HttpContext.Response.ContentType = contentType;
            var response = new FileContentResult(System.IO.File.ReadAllBytes(tmpZipFile), contentType)
            {
                FileDownloadName = zipName
            };


            Directory.Delete(tmpPdfDir, true);
            Directory.Delete(tmpZipDir, true);

            return response;
        }
        private void saveCPDinFile(List<CPD> cpds, string dir)
        {
            Dictionary<string, int> filenames = new();
            foreach (CPD cpd in cpds)
            {
                try
                {
                    string base64 = ReadFile(new[] { "companies", cpd.companyId, "cpd", cpd.id, cpd.signed ? "firmado" : "cpd" });
                    if (base64.Contains(","))
                        base64 = base64.Split(",")[1];
                    byte[] bytes = Convert.FromBase64String(base64);
                    string filename = "CPD_" + MESES[cpd.mes - 1] + "_" + cpd.ano + "_" + cpd.dni + (cpd.signed ? "_firmado" : "");
                    if (filenames.ContainsKey(filename))
                    {
                        string number = "_" + filenames[filename];
                        filenames[filename]++;
                        filename += number;
                    }
                    else filenames[filename] = 1;
                    System.IO.File.WriteAllBytes(Path.Combine(dir, filename + ".pdf"), bytes);
                }
                catch (Exception) { }
            }
        }
        public static void signCPD(CPD cpd, string firmaBase64, SqlConnection conn)
        {
            string pdfBase64 = ReadFile(new[] { "companies", cpd.companyId, "cpd", cpd.id, "cpd" });
            PdfReader reader = new PdfReader(Convert.FromBase64String(pdfBase64.Split(",")[1]));
            using (MemoryStream ms = new MemoryStream())
            {
                PdfStamper stamper = new PdfStamper(reader, ms, '\0', true);

                float x = 350;
                float y = 10;
                float scale = 15;
                Image img = Image.GetInstance(Convert.FromBase64String(firmaBase64.Split(",")[1]));
                img.ScaleToFit(16 * scale, 9 * scale);
                img.SetAbsolutePosition(x, y);
                for (int i = 1; i <= reader.NumberOfPages; i++)
                    stamper.GetOverContent(i).AddImage(img);

                stamper.Close();

                byte[] signedPdf = ms.ToArray();
                string signedPdfBase64 = "data:@file/pdf;base64," + Convert.ToBase64String(signedPdf);
                SaveFile(new[] { "companies", cpd.companyId, "cpd", cpd.id, "firmado" }, signedPdfBase64);

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "UPDATE cpd SET signDate = getdate() WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", cpd.id);
                    command.ExecuteNonQuery();
                }
            }
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
