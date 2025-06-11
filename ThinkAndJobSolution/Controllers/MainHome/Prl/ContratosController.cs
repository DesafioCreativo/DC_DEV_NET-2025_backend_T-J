using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;
using System.Text.Json;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/contratos")]
    [ApiController]
    [Authorize]
    public class ContratosController : ControllerBase
    {

        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "get/{candidateId}/")]
        public IActionResult Get(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Contratos.Get", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id, weekHours FROM contratos WHERE candidateId = @CANDIDATE_ID";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new
                                {
                                    error = false,
                                    contrato = new Contrato()
                                    {
                                        id = reader.GetString(reader.GetOrdinal("id")),
                                        weekHours = reader.IsDBNull(reader.GetOrdinal("weekHours")) ? null : reader.GetInt32(reader.GetOrdinal("weekHours"))
                                    }
                                };
                            }
                            else
                            {
                                result = new
                                {
                                    error = false
                                };
                            }
                        }
                    }
                }

            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-no-hours/")]
        public IActionResult ListNoHours()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Contratos.ListNoHours", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT CA.dni, CA.id, CONCAT(CA.nombre, ' ', CA.apellidos) as name FROM contratos CO INNER JOIN candidatos CA ON(CO.candidateId = CA.id) WHERE CO.weekHours IS NULL";
                        List<object> candidates = new List<object>();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                candidates.Add(new
                                {
                                    dni = reader.GetString(reader.GetOrdinal("dni")),
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    name = reader.GetString(reader.GetOrdinal("name"))
                                });
                            }
                        }
                        result = new { error = false, candidates };
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-for-candidate/{signLink}")]
        public IActionResult GetForCandidate(string signLink)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT CO.id, CO.downloaded FROM contratos CO INNER JOIN candidatos CA ON(CA.id = CO.candidateId) WHERE CA.id = @CANDIDATE_ID AND CA.lastSignLink = @SIGN_LINK";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@SIGN_LINK", signLink);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = new
                            {
                                error = false,
                                hasContrato = true,
                                contrato = new Contrato()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    downloaded = reader.GetInt32(reader.GetOrdinal("downloaded")) == 1
                                }
                            };
                        }
                        else
                        {
                            result = new
                            {
                                error = false,
                                hasContrato = false
                            };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "count-new-for-candidate/{signLink}")]
        public IActionResult CountNewForCandidate(string signLink)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM contratos CO INNER JOIN candidatos CA ON(CA.id = CO.candidateId) WHERE CA.id = @CANDIDATE_ID AND CA.lastSignLink = @SIGN_LINK AND CO.downloaded = 0";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                    result = command.ExecuteScalar();
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download/{contratoId}/{candidate?}")]
        public IActionResult Download(string contratoId, bool candidate = false)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            string candidateId = null;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT candidateId FROM contratos WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", contratoId);
                    using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                }

                if (candidate)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE contratos SET downloaded = 1 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", contratoId);
                        command.ExecuteNonQuery();
                    }
                }
            }
            if (candidateId == null)
            {
                result = new
                {
                    error = "Error 4801, contrato no encontrado"
                };
            }
            else
            {
                result = new
                {
                    error = false,
                    pdf = ReadFile(new[] { "candidate", candidateId, "contrato", contratoId, "pdf" })
                };
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{contratoId}/")]
        public IActionResult Delete(string contratoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Contratos.Delete", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string candidateId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT candidateId FROM contratos WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", contratoId);
                        using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                    }
                    if (candidateId == null)
                    {
                        result = new
                        {
                            error = "Error 4724, contrato no encontrado"
                        };
                    }
                    else
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM contratos WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", contratoId);
                            command.ExecuteNonQuery();

                            DeleteDir(new[] { "candidate", candidateId, "contrato", contratoId });
                            result = new
                            {
                                error = false
                            };
                        }
                    }
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
            if (!HasPermission("Contratos.Create", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson) && json.TryGetProperty("weekHours", out JsonElement weekHoursJson) &&
                    json.TryGetProperty("pdf", out JsonElement pdfJson))
                {
                    string candidateId = candidateIdJson.GetString();
                    int weekHours = weekHoursJson.GetInt32();
                    string pdf = pdfJson.GetString();
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        try
                        {
                            bool failed = false;
                            //Comprobar que el candidato no tenga ya un contrato
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT COUNT(*) FROM contratos WHERE candidateId = @CANDIDATE_ID";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                if ((int)command.ExecuteScalar() > 0)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 4725, este candidato ya tiene un contrato"
                                    };
                                }
                            }
                            if (!failed)
                            {
                                string id = ComputeStringHash(candidateId + weekHours + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "INSERT INTO contratos (id, candidateId, weekHours) VALUES (@ID, @CANDIDATE_ID, @WEEK_HOURS)";
                                    command.Parameters.AddWithValue("@ID", id);
                                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                    command.Parameters.AddWithValue("@WEEK_HOURS", weekHours);
                                    command.ExecuteNonQuery();
                                    SaveFile(new[] { "candidate", candidateId, "contrato", id, "pdf" }, pdf);
                                    result = new
                                    {
                                        error = false,
                                        id
                                    };
                                }
                            }
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5701, no han podido realizar la operacion" };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "update-hours/{contratoId}/")]
        public async Task<IActionResult> UpdateHours(string contratoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Contratos.UpdateHours", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("weekHours", out JsonElement weekHoursJson))
                {
                    int weekHours = weekHoursJson.GetInt32();
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        try
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE contratos SET weekHours = @WEEK_HOURS WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", contratoId);
                                command.Parameters.AddWithValue("@WEEK_HOURS", weekHours);
                                if (command.ExecuteNonQuery() == 0)
                                {
                                    result = new { error = "Error 4702, contrato no encontrado" };
                                }
                                else
                                {
                                    result = new { error = false };
                                }
                            }
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5701, no han podido realizar la operacion" };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "bulk-upload/")]
        public async Task<IActionResult> BulkUpload()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Contratos.BulkUpload", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("zip", out JsonElement zipJson) && json.TryGetProperty("xlsx", out JsonElement xlsxJson))
                {
                    string zip = zipJson.GetString();
                    Dictionary<string, int> hours = new Dictionary<string, int>();
                    try
                    {
                        string xlsxString = xlsxJson.GetString();
                        byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
                        string tmpDir = GetTemporaryDirectory();
                        string tmpFile = Path.Combine(tmpDir, "template.xlsx");
                        System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);
                        XSSFWorkbook workbook = new XSSFWorkbook(tmpFile);
                        ISheet sheet = workbook.GetSheetAt(0);
                        DataFormatter formatter = new DataFormatter();
                        if (!CheckExcelTemplateId(workbook, "bulk-contratos"))
                            return Ok(new { error = "Error 4010, plantilla incorrecta." });

                        //Obtener los centros
                        int r = 1;
                        while (true)
                        {
                            IRow row = sheet.GetRow(r++);
                            if (row == null || row.GetCell(0) == null || row.GetCell(1) == null) break;
                            hours[formatter.FormatCellValue(row.GetCell(0))?.Trim()] = Int32.Parse(formatter.FormatCellValue(row.GetCell(1))?.Trim());
                        }
                        workbook.Close();
                        Directory.Delete(tmpDir, true);
                    }
                    catch (Exception)
                    {
                        return Ok(new { error = "Error 5447, error al procesar documento." });
                    }
                    try
                    {
                        List<string> fails = new List<string>();
                        List<string> oks = new List<string>();
                        string tmpZipDir = GetTemporaryDirectory();
                        string zipFilePath = Path.Combine(tmpZipDir, "zip.zip");
                        string pdfDirectory = Path.Combine(tmpZipDir, "pdfs");
                        Directory.CreateDirectory(pdfDirectory);
                        System.IO.File.WriteAllBytes(zipFilePath, Convert.FromBase64String(zip.Split(",")[1]));
                        ZipFile.ExtractToDirectory(zipFilePath, pdfDirectory);
                        using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                        {
                            conn.Open();
                            using (SqlTransaction transaction = conn.BeginTransaction())
                            {
                                string[] files = Directory.GetFiles(pdfDirectory);
                                foreach (string file in files)
                                {
                                    string name = Path.GetFileName(file);
                                    try
                                    {
                                        //Parsear el nombre del fichero CL_20211018-00738-77383401Q.pdf
                                        string dni = name.Split(".")[0].Split("_")[1].Split("-")[2];
                                        string pdf = "data:application/pdf;base64," + Convert.ToBase64String(System.IO.File.ReadAllBytes(file));
                                        string candidateId = null;
                                        string contratoId = null;
                                        //Comprobar si el candidato existe
                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            command.Connection = conn;
                                            command.Transaction = transaction;
                                            command.CommandText = "SELECT id FROM candidatos WHERE dni = @DNI";
                                            command.Parameters.AddWithValue("@DNI", dni);
                                            using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) candidateId = reader.GetString(reader.GetOrdinal("id"));
                                        }
                                        //Comprobar si el candidato tiene ya un contrato
                                        if (candidateId != null)
                                        {
                                            using (SqlCommand command = conn.CreateCommand())
                                            {
                                                command.Connection = conn;
                                                command.Transaction = transaction;
                                                command.CommandText = "SELECT id FROM contratos WHERE candidateId = @ID";
                                                command.Parameters.AddWithValue("@ID", candidateId);
                                                using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) contratoId = reader.GetString(reader.GetOrdinal("id"));
                                            }
                                        }
                                        else throw new Exception("Candidato no encontrado");
                                        //Eliminar el contrato previo
                                        if (contratoId != null)
                                        {
                                            using (SqlCommand command = conn.CreateCommand())
                                            {
                                                command.Connection = conn;
                                                command.Transaction = transaction;
                                                command.CommandText = "DELETE FROM contratos WHERE id = @ID";
                                                command.Parameters.AddWithValue("@ID", contratoId);
                                                command.ExecuteNonQuery();
                                            }
                                            DeleteDir(new[] { "candidate", candidateId, "contrato", contratoId });
                                        }
                                        //Insertar el nuevo contrato
                                        string newContratoId = ComputeStringHash(candidateId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            command.Connection = conn;
                                            command.Transaction = transaction;
                                            command.CommandText = "INSERT INTO contratos (id, candidateId, weekHours) VALUES (@ID, @CANDIDATE_ID, @WEEK_HOURS)";
                                            command.Parameters.AddWithValue("@ID", newContratoId);
                                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                            command.Parameters.AddWithValue("@WEEK_HOURS", hours.ContainsKey(dni) ? hours[dni] : DBNull.Value);

                                            command.ExecuteNonQuery();

                                            SaveFile(new[] { "candidate", candidateId, "contrato", newContratoId, "pdf" }, pdf);
                                        }
                                        oks.Add(newContratoId);
                                    }
                                    catch (Exception)
                                    {
                                        fails.Add(name);
                                    }
                                }
                                if (fails.Count > 0)
                                {
                                    //transaction.Rollback();
                                    transaction.Commit(); // Nos quedamos con los que han funcionado

                                    result = new
                                    {
                                        error = "Error 4828, Error al procesar los ficheros: " + String.Join(", ", fails.ToArray())
                                    };
                                }
                                else
                                {
                                    transaction.Commit();
                                    result = new
                                    {
                                        error = false
                                    };
                                }
                            }

                        }

                        Directory.Delete(tmpZipDir, true);
                    }
                    catch (Exception)
                    {
                        result = new
                        {
                            error = "Error 5444, error al procesar fichero comprimido."
                        };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "bulk-download/")]
        public async Task<IActionResult> BulkDownload()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Contratos.BulkDownload", securityToken).Acceso)
            {
                return new ForbidResult();
            }
            try
            {
                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("candidate", out JsonElement candidateJson) &&
                    json.TryGetProperty("company", out JsonElement companyJson) &&
                    json.TryGetProperty("centro", out JsonElement centroJson))
                {
                    string candidateId = candidateJson.GetString();
                    string companyId = companyJson.GetString();
                    string centroId = centroJson.GetString();
                    string tmpPdfDir = GetTemporaryDirectory();
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT CA.dni, CO.id, CA.id as candidateDni FROM " +
                                "contratos CO INNER JOIN candidatos CA ON(CO.candidateId = CA.id) WHERE " +
                                "(@CANDIDATE IS NULL OR @CANDIDATE = CA.id) AND" +
                                "(@COMPANY IS NULL OR EXISTS(SELECT * FROM trabajos T INNER JOIN centros CE ON(T.centroId = CE.id) WHERE T.signLink = CA.lastSignLink AND CE.companyId = @COMPANY)) AND " +
                                "(@CENTRO IS NULL OR EXISTS(SELECT * FROM trabajos T WHERE T.signLink = CA.lastSignLink AND T.centroId = @CENTRO))";
                            command.Parameters.AddWithValue("@CANDIDATE", (object)candidateId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string sDni = reader.GetString(reader.GetOrdinal("dni"));
                                    string id = reader.GetString(reader.GetOrdinal("id"));
                                    string sCandidateId = reader.GetString(reader.GetOrdinal("candidateDni"));
                                    DateTime now = DateTime.Now;
                                    string sYear = now.Year.ToString();
                                    string sMonth = "0" + now.Month;
                                    string sDay = "0" + now.Day;
                                    sYear = sYear.Substring(sYear.Length - 4);
                                    sMonth = sMonth.Substring(sMonth.Length - 2);
                                    sDay = sDay.Substring(sDay.Length - 2);
                                    try
                                    {
                                        byte[] pdf = Convert.FromBase64String(ReadFile(new[] { "candidate", sCandidateId, "contrato", id, "pdf" }).Split(",")[1]);
                                        System.IO.File.WriteAllBytes(Path.Combine(tmpPdfDir, "CL_" + sYear + sMonth + sDay + "-00000-" + sDni + ".pdf"), pdf);
                                    }
                                    catch (Exception)
                                    {
                                        //Console.WriteLine(e);
                                    }
                                }
                            }
                        }
                    }
                    string tmpZipDir = GetTemporaryDirectory();
                    string zipName = "contratos.zip";
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
            }
            catch (Exception)
            {
                //Console.WriteLine(e);
            }
            return new NoContentResult();
        }






        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        public struct Contrato
        {
            public string id { get; set; }
            public int? weekHours { get; set; }
            public string pdf { get; set; }
            public bool downloaded { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
