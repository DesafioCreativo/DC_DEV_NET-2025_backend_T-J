using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;


namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/nominas")]
    [ApiController]
    [Authorize]
    public class NominasController : ControllerBase
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
            if (!HasPermission("Nominas.Get", securityToken).Acceso)
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
                        command.CommandText = "SELECT id, year, month, day FROM nominas WHERE candidateId = @CANDIDATE_ID ORDER BY year DESC, month DESC";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            List<Nomina> nominas = new List<Nomina>();
                            while (reader.Read())
                            {
                                nominas.Add(new Nomina()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    year = reader.GetInt32(reader.GetOrdinal("year")),
                                    month = reader.GetInt32(reader.GetOrdinal("month")),
                                    day = reader.GetInt32(reader.GetOrdinal("day"))
                                });
                            }
                            result = new
                            {
                                error = false,
                                nominas
                            };
                        }
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
                    command.CommandText = "SELECT NO.id, NO.year, NO.month, NO.day, NO.downloaded FROM nominas NO INNER JOIN candidatos CA ON(CA.id = NO.candidateId) WHERE CA.id = @CANDIDATE_ID AND CA.lastSignLink = @SIGN_LINK ORDER BY year DESC, month DESC";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        List<Nomina> nominas = new List<Nomina>();
                        while (reader.Read())
                        {
                            nominas.Add(new Nomina()
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                year = reader.GetInt32(reader.GetOrdinal("year")),
                                month = reader.GetInt32(reader.GetOrdinal("month")),
                                day = reader.GetInt32(reader.GetOrdinal("day")),
                                downloaded = reader.GetInt32(reader.GetOrdinal("downloaded")) == 1
                            });
                        }
                        result = new
                        {
                            error = false,
                            nominas
                        };
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
                    command.CommandText = "SELECT COUNT(*) FROM nominas NO INNER JOIN candidatos CA ON(CA.id = NO.candidateId) WHERE CA.id = @CANDIDATE_ID AND CA.lastSignLink = @SIGN_LINK AND NO.downloaded = 0";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                    result = command.ExecuteScalar();
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "api/v1/nominas/download/{nominaId}/{candidate?}")]
        public IActionResult Download(string nominaId, bool candidate = false)
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
                    command.CommandText = "SELECT candidateId FROM nominas WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", nominaId);
                    using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                }

                if (candidate)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE nominas SET downloaded = 1 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", nominaId);
                        command.ExecuteNonQuery();
                    }
                }
            }

            if (candidateId == null)
            {
                result = new
                {
                    error = "Error 4802, nomina no encontrada"
                };
            }
            else
            {
                string fullPath = ComposePath(new[] { "candidate", candidateId, "nomina", nominaId + ".pdf" }, false);
                if (System.IO.File.Exists(fullPath))
                {
                    result = new
                    {
                        error = false,
                        file = Convert.ToBase64String(System.IO.File.ReadAllBytes(fullPath)),
                        ext = "pdf"
                    };
                }
                else
                {
                    result = new
                    {
                        error = "Error 4803, no se ha encontrado el pdf"
                    };
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "api/v1/nominas/{nominaId}/")]
        public IActionResult Delete(string nominaId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Nominas.Delete", securityToken).Acceso)
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
                        command.CommandText = "SELECT candidateId FROM nominas WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", nominaId);
                        using (SqlDataReader reader = command.ExecuteReader()) if (reader.Read()) candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                    }
                    if (candidateId == null)
                    {
                        result = new
                        {
                            error = "Error 4725, nomina no encontrada"
                        };
                    }
                    else
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM nominas WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", nominaId);
                            command.ExecuteNonQuery();
                            DeleteFile(new[] { "candidate", candidateId, "nomina", nominaId + ".pdf" });

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
            if (!HasPermission("Nominas.Create", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {

                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson) && json.TryGetProperty("year", out JsonElement yearJson) &&
                    json.TryGetProperty("month", out JsonElement monthJson) && json.TryGetProperty("day", out JsonElement dayJson) &&
                    json.TryGetProperty("pdf", out JsonElement pdfJson))
                {
                    string candidateId = candidateIdJson.GetString();
                    int year = yearJson.GetInt32();
                    int month = monthJson.GetInt32();
                    int day = dayJson.GetInt32();
                    string pdf = pdfJson.GetString();
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        try
                        {
                            bool failed = false;
                            //Comprobar que el candidato no tenga ya una nomina en ese mes y ano
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT COUNT(*) FROM nominas WHERE candidateId = @CANDIDATE_ID AND year = @YEAR AND month = @MONTH AND day = @DAY";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@YEAR", year);
                                command.Parameters.AddWithValue("@MONTH", month);
                                command.Parameters.AddWithValue("@DAY", day);
                                if ((int)command.ExecuteScalar() > 0)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 4726, este candidato ya tiene una nomina ese dia"
                                    };
                                }
                            }
                            if (!failed)
                            {
                                string id = ComputeStringHash(candidateId + year + month + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "INSERT INTO nominas (id, candidateId, year, month, day) VALUES (@ID, @CANDIDATE_ID, @YEAR, @MONTH, @DAY)";
                                    command.Parameters.AddWithValue("@ID", id);
                                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                    command.Parameters.AddWithValue("@YEAR", year);
                                    command.Parameters.AddWithValue("@MONTH", month);
                                    command.Parameters.AddWithValue("@DAY", day);
                                    command.ExecuteNonQuery();
                                    SaveFile(new[] { "candidate", candidateId, "nomina", id, "pdf" }, pdf);
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

        [HttpPost]
        [Route(template: "bulk-upload/")]
        public async Task<IActionResult> BulkUpload()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            string tmpPdfDir = GetTemporaryDirectory();     // Directorio temporal donde se guarda el pdf de la petición
            string tmpPdfDir2 = GetTemporaryDirectory();    // Directorio temporal donde se guardan las nóminas divididas
            string tmpPdfFile = Path.Combine(tmpPdfDir, "nominas.pdf");
            try
            {
                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("pdf", out JsonElement pdfJson))
                {
                    string pdf = pdfJson.GetString().Split("base64,")[1];

                    // Guardamos el pdf en el archivo temporal
                    System.IO.File.WriteAllBytes(tmpPdfFile, Convert.FromBase64String(pdf));
                    PdfDocument inputDocument = PdfReader.Open(tmpPdfFile, PdfDocumentOpenMode.Import);
                    string pdfText = ExtractTextPDF(tmpPdfFile);
                    string[] dnis = pdfText.Split("N.I.F.\r\n").Skip(1).ToArray();
                    string[] fechas = pdfText.Split("Periodo Liquidación\r\n").Skip(1).ToArray();
                    // Por cada pdf...
                    for (int i = 0; i < inputDocument.PageCount; i++)
                    {
                        // Creamos un nuevo documento
                        PdfDocument outputDocument = new PdfDocument();
                        outputDocument.Version = inputDocument.Version;
                        outputDocument.Info.Title = "Nómina";
                        outputDocument.Info.Creator = inputDocument.Info.Creator;
                        PdfPages pages = inputDocument.Pages;
                        PdfPage page = pages[i];
                        outputDocument.AddPage(page);
                        string dni = dnis[i].Split("\r\n")[0];
                        string[] f = fechas[i].Split("\r\n")[0].Replace("De ", "").Split(" a ");
                        string fIni = f[0].Replace("/", "-");
                        string fFin = f[1].Replace("/", "-");
                        outputDocument.Save(Path.Combine(tmpPdfDir2, $"nomina_{dni}_{fIni}_a_{fFin}.pdf"));
                    }

                    // Una vez generadas y divididas las nóminas, cada una de ellas las movemos al directorio de cada
                    // candidato. Si ya existe una nómina con el mismo nombre, la sobreescribimos.
                    // Por cada nómina del directorio temporal "tmpPdfDir2"
                    List<string> dniNoExistentes = new List<string>();
                    string[] nominas = Directory.GetFiles(tmpPdfDir2);
                    for (int i = 0; i < nominas.Length; i++)
                    {
                        string nomina = nominas[i];
                        string nombre = Path.GetFileName(nomina);
                        // La estructura del nombre del archivo es "nomina_{dni}_{fIni}_a_{fFin}.pdf"
                        // Sacamos el dni y la fecha de inicio. La fecha de inicio sigue el formato "dd-MM-yyyy"
                        string dni = nombre.Split("_")[1];
                        string fIni = nombre.Split("_")[2];
                        // Le añadimos a la fecha los dos primeros dígitos del año actual
                        fIni = fIni.Substring(0, 6) + DateTime.Now.Year.ToString().Substring(0, 2) + fIni.Substring(6);
                        DateTime dateTime = DateTime.ParseExact(fIni, "dd-MM-yyyy", CultureInfo.InvariantCulture);
                        // Obtenemos el candidateId del candidato con el dni
                        string candidateId = null;
                        using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                        {
                            conn.Open();
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT id FROM candidatos WHERE dni = @DNI";
                                command.Parameters.AddWithValue("@DNI", dni);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        candidateId = reader.GetString(reader.GetOrdinal("id"));
                                    }
                                }
                            }

                            if (candidateId != null)
                            {
                                // Insertamos la nómina en la base de datos si no existe
                                string id = ComputeStringHash(candidateId + dateTime.Year + dateTime.Month + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    string nominaId = null;
                                    command.CommandText = "SELECT id FROM nominas WHERE candidateId = @CANDIDATE_ID AND year = @YEAR AND month = @MONTH AND day = @DAY";
                                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                    command.Parameters.AddWithValue("@YEAR", dateTime.Year);
                                    command.Parameters.AddWithValue("@MONTH", dateTime.Month);
                                    command.Parameters.AddWithValue("@DAY", dateTime.Day);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            nominaId = reader.GetString(reader.GetOrdinal("id"));
                                        }
                                    }

                                    if (nominaId != null)
                                    {
                                        SqlCommand command2 = conn.CreateCommand();
                                        command2.CommandText = "DELETE FROM nominas WHERE id = @ID";
                                        command2.Parameters.AddWithValue("@ID", nominaId);
                                        command2.ExecuteNonQuery();
                                        DeleteFile(new[] { "candidate", candidateId, "nomina", nominaId + ".pdf" });
                                    }

                                    SqlCommand command3 = conn.CreateCommand();
                                    command3.CommandText = "INSERT INTO nominas (id, candidateId, year, month, day) VALUES (@ID, @CANDIDATE_ID, @YEAR, @MONTH, @DAY)";
                                    command3.Parameters.AddWithValue("@ID", id);
                                    command3.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                    command3.Parameters.AddWithValue("@YEAR", dateTime.Year);
                                    command3.Parameters.AddWithValue("@MONTH", dateTime.Month);
                                    command3.Parameters.AddWithValue("@DAY", dateTime.Day);
                                    command3.ExecuteNonQuery();

                                    // Movemos el pdf "nomina" del directorio temporal al directorio "nomina" del candidato.
                                    // Si el directorio "nomina" no existe, lo creamos.
                                    // Tras moverlo, le cambiaremos el nombre a "{id}.pdf"
                                    string origen = Path.Combine(tmpPdfDir2, nombre);
                                    string destino = ComposePath(new[] { "candidate", candidateId, "nomina", id + ".pdf" });
                                    string[] directorio = destino.Split(Path.DirectorySeparatorChar);
                                    string dir = "";
                                    for (int j = 0; j < directorio.Length - 1; j++)
                                    {
                                        dir += directorio[j] + Path.DirectorySeparatorChar;
                                    }
                                    if (!Directory.Exists(dir))
                                        Directory.CreateDirectory(dir);
                                    // Si el archivo ya existe, lo sobreescribimos
                                    if (System.IO.File.Exists(destino))
                                        System.IO.File.Delete(destino);
                                    System.IO.File.Move(origen, destino);
                                }
                                //Console.WriteLine("Nomina " + (i + 1) + ": Guardada");
                            }
                            else
                            {
                                dniNoExistentes.Add(dni);
                                //Console.WriteLine("Nomina " + (i + 1) + ": No guardada, dni no existente (" + dni + ")");
                            }
                        }
                    }
                    result = new
                    {
                        error = false,
                        msg = "Nominas guardadas correctamente.",
                        dniNoExistentes,
                    };
                }
                else
                {
                    result = new
                    {
                        error = "Error 4727, no se ha encontrado el pdf"
                    };
                }
            }
            catch (Exception e)
            {
                return Ok(new{error = "Error 2932, Error de fichero. " + e.Message,});
            }
            finally
            {
                Directory.Delete(tmpPdfDir, true);
                Directory.Delete(tmpPdfDir2, true);
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "bulk-delete/")]
        public IActionResult BulkDelete()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Nominas.Delete", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    List<string> candidatesId = new List<string>();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT candidateId FROM nominas";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                candidatesId.Add(reader.GetString(reader.GetOrdinal("candidateId")));
                            }
                        }
                    }
                    foreach (string id in candidatesId)
                    {
                        DeleteDir(new[] { "candidate", id, "nomina" });
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM nominas WHERE candidateId = @ID";
                            command.Parameters.AddWithValue("@ID", id);
                            command.ExecuteNonQuery();
                        }
                    }
                    result = new
                    {
                        error = false
                    };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "bulk-download/")]
        public async Task<IActionResult> BulkDownload()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Nominas.BulkDownload", securityToken).Acceso)
            {
                return new ForbidResult();
            }
            try
            {
                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("year", out JsonElement yearJson) &&
                    json.TryGetProperty("month", out JsonElement monthJson) &&
                    json.TryGetProperty("candidate", out JsonElement candidateJson) &&
                    json.TryGetProperty("company", out JsonElement companyJson) &&
                    json.TryGetProperty("centro", out JsonElement centroJson))
                {
                    int? year = GetJsonInt(yearJson);
                    int? month = GetJsonInt(monthJson);
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
                                "SELECT C.dni, N.year, N.month, N.day, N.id, C.id as candidateId FROM " +
                                "nominas N INNER JOIN candidatos C ON(N.candidateId = C.id) WHERE " +
                                "(@YEAR IS NULL OR @YEAR = N.year) AND " +
                                "(@MONTH IS NULL OR @MONTH = N.month) AND " +
                                "(@CANDIDATE IS NULL OR @CANDIDATE = C.id) AND " +
                                "(@COMPANY IS NULL OR EXISTS(SELECT * FROM trabajos T INNER JOIN centros CE ON(T.centroId = CE.id) WHERE T.signLink = C.lastSignLink AND CE.companyId = @COMPANY)) AND " +
                                "(@CENTRO IS NULL OR EXISTS(SELECT * FROM trabajos T WHERE T.signLink = C.lastSignLink AND T.centroId = @CENTRO))";
                            command.Parameters.AddWithValue("@YEAR", (object)year ?? DBNull.Value);
                            command.Parameters.AddWithValue("@MONTH", (object)month ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CANDIDATE", (object)candidateId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string sDni = reader.GetString(reader.GetOrdinal("dni"));
                                    string sYear = reader.GetInt32(reader.GetOrdinal("year")).ToString();
                                    string sMonth = "0" + reader.GetInt32(reader.GetOrdinal("month")).ToString();
                                    string sDay = "0" + reader.GetInt32(reader.GetOrdinal("day")).ToString();
                                    string id = reader.GetString(reader.GetOrdinal("id"));
                                    string sCandidateId = reader.GetString(reader.GetOrdinal("candidateId"));

                                    sYear = sYear.Substring(sYear.Length - 2);
                                    sMonth = sMonth.Substring(sMonth.Length - 2);
                                    sDay = sDay.Substring(sDay.Length - 2);

                                    try
                                    {
                                        byte[] pdf = System.IO.File.ReadAllBytes(ComposePath(new[] { "candidate", sCandidateId, "nomina", id + ".pdf" }, false));
                                        System.IO.File.WriteAllBytes(Path.Combine(tmpPdfDir, "Nomina_" + sDni + "_" + sDay + "-" + sMonth + "-" + sYear + ".pdf"), pdf);
                                    }
                                    catch (Exception) { }
                                }
                            }
                        }
                    }
                    string tmpZipDir = GetTemporaryDirectory();
                    string zipName = "nominas.zip";
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
            { }
            return new NoContentResult();
        }



        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        public struct Nomina
        {
            public string id { get; set; }
            public int year { get; set; }
            public int month { get; set; }
            public int day { get; set; }
            public string pdf { get; set; }
            public bool downloaded { get; set; }
        }
        public struct HoursAjust
        {
            public string candidateId { get; set; }
            public double hours { get; set; }
            public DateTime day { get; set; }
            public string incidenciaFaltaAsistenciaId { get; set; }
            public string incidenciaHorasExtraId { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
