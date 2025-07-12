using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Globalization;
using System.Text.Json;
using ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using System.Text;
using ThinkAndJobSolution.Controllers._Helper;

namespace ThinkAndJobSolution.Controllers.MainHome.RRHH
{
    [Route("api/v1/cdocuments")]
    [ApiController]
    [Authorize]
    public class CDocumentsController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        // Para candidatos
       
        /// <summary>
        /// Obtiene un documento de un candidato.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <param name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "get/{documentId}")]
        public IActionResult Get(string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("CDocuments.Get", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT * FROM cdocuments WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", documentId);
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result = new
                    {
                        error = false,
                        cDocument = new CDocument()
                        {
                            Id = reader.GetString(reader.GetOrdinal("id")),
                            CandidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            Category = reader.GetString(reader.GetOrdinal("category")),
                            Year = reader.GetInt32(reader.GetOrdinal("year")),
                            Month = reader.GetInt32(reader.GetOrdinal("month")),
                            Day = reader.GetInt32(reader.GetOrdinal("day")),
                            DownloadDate = reader.IsDBNull(reader.GetOrdinal("downloadDate")) ? null : reader.GetDateTime(reader.GetOrdinal("downloadDate")),
                            SignedDate = reader.IsDBNull(reader.GetOrdinal("signedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("signedDate")),
                        }
                    };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// Descarga un documento laboral de un candidato.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <param name="candidateId"> id del candidato </param>
        /// <param name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "downloadlaboral/{candidateId}/{documentId}")]
        public IActionResult DownloadLaboral(string candidateId, string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CDocuments.Download", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                result = new { error = false, data = ReadFile(new[] { "candidate", candidateId, "cdocuments", documentId + ".pdf" }) };
            }
            return Ok(result);
        }

        /// <summary>
        /// Descarga un documento de control horario de un candidato.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <param name="candidateId"> id del candidato </param>
        /// <param name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "downloadchorario/{candidateId}/{documentId}")]
        public IActionResult DownloadCHorarioDoc( string candidateId, string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            try
            {
                if (!HasPermission("CDocuments.Download", securityToken).Acceso)
                {
                    result = new
                    {
                        error = "Error 1001, No se disponen de los privilegios suficientes."
                    };
                }
                else
                {
                    result = new { error = false, data = ReadFile(new[] { "candidate", candidateId, "control_horario", documentId + ".pdf" }) };
                }
            }
            catch (Exception e)
            {
                result = new { error = "Error 5701, no han podido descargar el documento ", e.Message };
            }
            return Ok(result);
        }

        /// <summary>
        /// Descarga un documento personal de un candidato.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <param name="candidateId"> id del candidato </param>
        /// <param name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpPost]
        [Route(template: "downloadpersonal/")]
        public async Task<IActionResult> DownloadPersonalAsync()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CDocuments.Download", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;
                try
                {
                    if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson) &&
                        json.TryGetProperty("completeRoute", out JsonElement completeRouteJson))
                    {
                        string candidateId = candidateIdJson.GetString();
                        string completeRoute = completeRouteJson.GetString();
                        result = new { error = false, data = ReadFile(null, ComposePath(new[] { "candidate", candidateId }) + completeRoute) };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido descargar el documento" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// Lista documentos en base a un filtro.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpPost]
        [Route(template: "list-filtered/")]
        public async Task<IActionResult> ListFiltered()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CDocuments.List", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            try
            {
                if (json.TryGetProperty("searchKey", out JsonElement searchKeyJson) &&
                json.TryGetProperty("year", out JsonElement yearJson) &&
                json.TryGetProperty("month", out JsonElement monthJson) &&
                json.TryGetProperty("docType", out JsonElement docCategoryJson) &&
                json.TryGetProperty("downloaded", out JsonElement downloadedJson) &&
                json.TryGetProperty("signed", out JsonElement signedJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) &&
                json.TryGetProperty("perpage", out JsonElement perpageJson) &&
                json.TryGetProperty("sortColumn", out JsonElement sortColumnJson) &&
                json.TryGetProperty("sortDesc", out JsonElement sortDescJson))
                {
                    string searchKey = searchKeyJson.GetString();
                    int? year = yearJson.GetString() != null ? Int32.Parse(yearJson.GetString()) : null;
                    int? month = monthJson.GetString() != null ? Int32.Parse(monthJson.GetString()) : null;
                    string docCategory = docCategoryJson.GetString();
                    bool? downloaded = GetJsonBool(downloadedJson);
                    bool? signed = GetJsonBool(signedJson);
                    int page = pageJson.GetInt32();
                    int perpage = perpageJson.GetInt32();
                    string sort = FormatSort(new() {
                        { "dni", "C.dni" },
                        { "name", "CONCAT(C.apellidos, ' ', C.nombre)" },
                        { "company", "E.nombre" },
                        { "workCenter", "CE.alias" },
                        { "workCategory", "CA.name" },
                        { "category", "CD.category" },
                        { "year", "CD.year" },
                        { "month", "CD.month" },
                        { "day", "CD.day" },
                        { "downloaded", "CD.downloaded" },
                        { "signed", "CD.signed" },
                    }, "dni", sortColumnJson, sortDescJson);
                    List<CDocumentRow> cDocuments = listFiltered(searchKey, year, month, docCategory, signed, downloaded, page, perpage, sort);
                    result = new { error = false, cDocuments };

                }
            }
            catch (Exception e)
            {
                result = new { error = "Error 5701, no han podido listar documentos", e.Message };
            }

            return Ok(result);
        }

        /// <summary>
        /// Cuenta los documentos en base a un filtro.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpPost]
        [Route(template: "list-filtered-count/")]
        public async Task<IActionResult> ListFilteredCount()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CDocuments.List", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            try
            {
                if (json.TryGetProperty("searchKey", out JsonElement searchKeyJson) &&
                json.TryGetProperty("year", out JsonElement yearJson) &&
                json.TryGetProperty("month", out JsonElement monthJson) &&
                json.TryGetProperty("docType", out JsonElement docCategoryJson) &&
                json.TryGetProperty("downloaded", out JsonElement downloadedJson) &&
                json.TryGetProperty("signed", out JsonElement signedJson))
                {
                    string searchKey = searchKeyJson.GetString();
                    int? year = yearJson.GetString() != null ? Int32.Parse(yearJson.GetString()) : null;
                    int? month = monthJson.GetString() != null ? Int32.Parse(monthJson.GetString()) : null;
                    string docCategory = docCategoryJson.GetString();
                    bool? downloaded = GetJsonBool(downloadedJson);
                    bool? signed = GetJsonBool(signedJson);
                    int count = listFilteredCount(searchKey, year, month, docCategory, signed, downloaded);
                    result = new { error = false, count };

                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5701, no han podido listar documentos" };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list-filtered-dchorario/")]
        public async Task<IActionResult> ListFilteredDCHorario()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            try
            {
                if (json.TryGetProperty("searchKey", out JsonElement searchKeyJson) &&
                json.TryGetProperty("year", out JsonElement yearJson) &&
                json.TryGetProperty("month", out JsonElement monthJson) &&
                json.TryGetProperty("downloaded", out JsonElement downloadedJson) &&
                json.TryGetProperty("uploaded", out JsonElement uploadedJson) &&
                json.TryGetProperty("company", out JsonElement companyJson) &&
                json.TryGetProperty("workcenter", out JsonElement workcenterJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) &&
                json.TryGetProperty("perpage", out JsonElement perpageJson) &&
                json.TryGetProperty("sortColumn", out JsonElement sortColumnJson) &&
                json.TryGetProperty("sortDesc", out JsonElement sortDescJson))
                {
                    string searchKey = searchKeyJson.GetString();
                    int? year = yearJson.GetString() != null ? Int32.Parse(yearJson.GetString()) : null;
                    int? month = monthJson.GetString() != null ? Int32.Parse(monthJson.GetString()) : null;
                    bool? downloaded = GetJsonBool(downloadedJson);
                    bool? uploaded = GetJsonBool(uploadedJson);
                    string company = companyJson.GetString();
                    string workcenter = workcenterJson.GetString();
                    int page = pageJson.GetInt32();
                    int perpage = perpageJson.GetInt32();
                    string sort = FormatSort(new() {
                        { "dni", "C.dni" },
                        { "year", "CH.year" },
                        { "month", "CH.month" },
                        { "name", "CONCAT(C.apellidos, ' ', C.nombre)" },
                        { "company", "E.nombre" },
                        { "workcenter", "CE.alias" },
                        { "workCategory", "CA.name" },
                        { "day", "CH.day" },
                        { "downloaded", "CH.downloadDate" },
                        { "uploaded", "CH.uploadDate" },
                    }, "dni", sortColumnJson, sortDescJson);
                    List<CHorarioRow> cDocuments = listFilteredDCHorario(searchKey, year, month, uploaded, downloaded, company, workcenter, page, perpage, sort);
                    result = new { error = false, cDocuments };
                }
            }
            catch (Exception e)
            {
                result = new { error = "Error 5701, no han podido listar documentos", e.Message };
            }

            return Ok(result);
        }

        /// <summary>
        /// Cuenta los documentos  del control horario en base a un filtro.
        /// </summary>
        [HttpPost]
        [Route(template: "list-filtered-dchorario-count/")]
        public async Task<IActionResult> ListFilteredCountDCHorario()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            try
            {
                if (json.TryGetProperty("searchKey", out JsonElement searchKeyJson) &&
                  json.TryGetProperty("year", out JsonElement yearJson) &&
                  json.TryGetProperty("month", out JsonElement monthJson) &&
                  json.TryGetProperty("downloaded", out JsonElement downloadedJson) &&
                  json.TryGetProperty("uploaded", out JsonElement uploadedJson) &&
                  json.TryGetProperty("company", out JsonElement companyJson) &&
                  json.TryGetProperty("workCenter", out JsonElement workCenterJson))
                {
                    string searchKey = searchKeyJson.GetString();
                    int? year = yearJson.GetString() != null ? Int32.Parse(yearJson.GetString()) : null;
                    int? month = monthJson.GetString() != null ? Int32.Parse(monthJson.GetString()) : null;
                    bool? downloaded = GetJsonBool(downloadedJson);
                    bool? uploaded = GetJsonBool(uploadedJson);
                    string company = companyJson.GetString();
                    string workCenter = workCenterJson.GetString();
                    int count = listFilteredCountDCHorario(searchKey, year, month, uploaded, downloaded, company, workCenter);
                    result = new { error = false, count };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5701, no han podido listar documentos de Control Horario" };
            }
            return Ok(result);
        }

        /// <summary>
        /// Sube un pdf a la base de datos y lo asigna a un candidato.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpPost]
        [Route(template: "upload/")]
        public async Task<IActionResult> Upload()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("CDocuments.Upload", securityToken).Acceso)
            {
                return await Task.FromResult(Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."}));
            }
            else
            {
                // Para el encoding 1252 (windows-1252) en .NET Core
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                string tmpDirPDFBase = GetTemporaryDirectory();     // Dir. temporal donde se guarda el pdf base y derivados
                string tmpDirProcessed = GetTemporaryDirectory();    // Dir. temporal donde se guardan los pdfs generados
                string baseFile = Path.Combine(tmpDirPDFBase, "baseFile.pdf");
                try
                {
                    using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
                    string data = await readerBody.ReadToEndAsync();

                    JsonElement json = JsonDocument.Parse(data).RootElement;
                    if (json.TryGetProperty("pdf", out JsonElement pdfJson))
                    {
                        string pdf = pdfJson.GetString().Split("base64,")[1];
                        System.IO.File.WriteAllBytes(baseFile, Convert.FromBase64String(pdf));

                        int index = 0; // Este será el contador de posición a través de las páginas del pdf
                        PdfDocument mainDoc = PdfReader.Open(baseFile, PdfDocumentOpenMode.Import);
                        int numPages = mainDoc.PageCount;

                        // Por cada página del pdf...
                        while (index < numPages)
                        {
                            PDF.Create(
                                PDF.GetDocType(
                                    PDF.ExtractTextPDF(ref mainDoc, baseFile, index)
                                ),
                                baseFile,
                                tmpDirProcessed,
                                true,
                                ref index,
                                ref mainDoc
                            );
                        }

                        // Ahora que tenemos todos los pdfs generados en el directorio "tmpDirProcessed" con
                        // la estructura de nombre "{tipo de documento}_{dni}_{fecha}.pdf", procedemos a asignarlos cada
                        // uno a su respectivo candidato.
                        // La fecha de inicio sigue el formato "dd-MM-yyyy"
                        string[] files = Directory.GetFiles(tmpDirProcessed);
                        int certificados = 0, nominasYFiniquitos = 0, contratos = 0, seguridadLaboral = 0;
                        HashSet<string> candidatosNoRegistrados = new();
                        // Por cada pdf generado...
                        foreach (string file in files)
                        {
                            string[] fileSplit = file.Split("_");
                            if (fileSplit.Length < 3)
                            {
                                return await Task.FromResult(Ok(new{error = "Error 2935, problemas con el nombre del archivo."}));
                            }
                            // Obtenemos de fileSplit[0] el tipo de documento de la dirección completa del archivo
                            string tipoDocumento = fileSplit[0].Split(Path.DirectorySeparatorChar)[^1], dni = fileSplit[1], candidateId = null;
                            DateTime fecha = DateTime.ParseExact(fileSplit[2].Split(".pdf")[0], "dd-MM-yyyy", CultureInfo.InvariantCulture);

                            using SqlConnection conn = new(CONNECTION_STRING);
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
                                bool isSigned = PdfReader.Open(file, PdfDocumentOpenMode.Import).IsSigned();
                                switch (tipoDocumento)
                                {
                                    case "CertINEM":
                                    case "CertIRPF":
                                        certificados++;
                                        break;
                                    case "CBajaVoluntaria":
                                    case "CJCategoria":
                                    case "CLCEventual":
                                    case "CLCEventualParcial":
                                    case "CLCFijoDiscontinuo":
                                    case "CLCSustitucion":
                                    case "CLCSustitucionParcial":
                                    case "CLProrroga":
                                    case "CLTraFijoDiscontinuo":
                                    case "CLTraIndefinido":
                                    case "CLVencimiento":
                                    case "OrdenServicio":
                                        contratos++;
                                        break;
                                    case "Finiquito":
                                    case "Nomina":
                                        nominasYFiniquitos++;
                                        break;
                                    case "ExamenDeSalud":
                                    case "FichaPrevencionRiesgos":
                                        seguridadLaboral++;
                                        break;
                                    default:
                                        contratos++;
                                        break;
                                }
                                // Insertamos el documento en la base de datos si no existe
                                string id = ComputeStringHash(candidateId + fecha.Year + fecha.Month + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                                using SqlCommand command = conn.CreateCommand();
                                string docId = null;
                                command.CommandText = "SELECT id FROM cdocuments WHERE candidateId = @CANDIDATE_ID AND category = @CATEGORY AND year = @YEAR AND month = @MONTH AND day = @DAY";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@CATEGORY", tipoDocumento);
                                command.Parameters.AddWithValue("@YEAR", fecha.Year);
                                command.Parameters.AddWithValue("@MONTH", fecha.Month);
                                command.Parameters.AddWithValue("@DAY", fecha.Day);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        docId = reader.GetString(reader.GetOrdinal("id"));
                                    }
                                }

                                if (docId != null)
                                {
                                    SqlCommand command2 = conn.CreateCommand();
                                    command2.CommandText = "DELETE FROM cdocuments WHERE id = @ID";
                                    command2.Parameters.AddWithValue("@ID", docId);
                                    command2.ExecuteNonQuery();
                                    DeleteFile(new[] { "candidate", candidateId, "cdocuments", docId + ".pdf" });
                                }

                                SqlCommand command3 = conn.CreateCommand();
                                command3.CommandText = "INSERT INTO cdocuments (id, candidateId, category, year, month, day, signedDate) VALUES (@ID, @CANDIDATE_ID, @CATEGORY, @YEAR, @MONTH, @DAY, @SIGNEDDATE)";
                                command3.Parameters.AddWithValue("@ID", id);
                                command3.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command3.Parameters.AddWithValue("@CATEGORY", tipoDocumento);
                                command3.Parameters.AddWithValue("@YEAR", fecha.Year);
                                command3.Parameters.AddWithValue("@MONTH", fecha.Month);
                                command3.Parameters.AddWithValue("@DAY", fecha.Day);
                                command3.Parameters.AddWithValue("@SIGNEDDATE", isSigned ? new DateTime(fecha.Year, fecha.Month, fecha.Day) : DBNull.Value);
                                command3.ExecuteNonQuery();

                                // Movemos el pdf del documento del directorio temporal al directorio "cdocuments" del candidato.
                                // Si el directorio "cdocuments" no existe, lo creamos.
                                // Tras moverlo, le cambiaremos el nombre a "{id}.pdf"
                                string origen = file;
                                string destino = ComposePath(new[] { "candidate", candidateId, "cdocuments", id + ".pdf" });
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
                            else
                            {
                                candidatosNoRegistrados.Add(dni);
                            }
                        }
                        result = new
                        {
                            error = false,
                            info = new
                            {
                                certificados,
                                nominasYFiniquitos,
                                contratos,
                                seguridadLaboral,
                                // Array de candidatos no registrados (DNIS)
                                candidatosNoRegistrados
                            }
                        };
                    }
                    else
                    {
                        result = new
                        {
                            error = "Error 2934, no se ha encontrado el pdf"
                        };
                    }
                }
                catch (Exception e)
                {
                    return await Task.FromResult(Ok(new{error = "Error 2933, problemas con el archivo." + e.Message,}));
                }
                finally
                {
                    Directory.Delete(tmpDirPDFBase, true);
                    Directory.Delete(tmpDirProcessed, true);
                }
            }

            return await Task.FromResult(Ok(result));
        }

        /// <summary>
        /// Elimina un documento de un candidato
        /// 1º Elimina el documento de la base de datos (tabla cdocuments).
        /// 2º Elimina el documento del candidato (directorio "cdocuments" en cada directorio de candidato).
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <param name="candidateId"> id del candidato </param>
        /// <paran name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpDelete]
        [Route(template: "delete/{candidateId}/{documentId}")]
        public async Task<IActionResult> Delete(string candidateId, string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("CDocuments.Delete", securityToken).Acceso)
            {
                return await Task.FromResult(Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."}));
            }
            else
            {
                try
                {
                    // Borramos la entrada de la base de datos
                    using SqlConnection conn = new(CONNECTION_STRING);
                    conn.Open();
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "DELETE FROM cdocuments WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", documentId);
                    command.ExecuteNonQuery();
                    // Borramos el archivo
                    DeleteFile(new[] { "candidate", candidateId, "cdocuments", documentId + ".pdf" });
                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception e)
                {
                    result = new
                    {
                        error = "Error 2933, problemas con el archivo." + e.Message,
                    };
                }
            }
            return await Task.FromResult(Ok(result));
        }

        /// <summary>
        /// Elimina un documento de control horario de  un candidato
        /// 1º Elimina el documento de la base de datos (tabla ccontrol_horario).
        /// 2º Elimina el documento del candidato (directorio "control_horario" en cada directorio de candidato).
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <param name="candidateId"> id del candidato </param>
        /// <paran name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpDelete]
        [Route(template: "deleteCHorario/{candidateId}/{documentId}")]
        public async Task<IActionResult> DeleteCHorario( string candidateId, string documentId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("CDocuments.Delete", securityToken).Acceso)
            {
                return await Task.FromResult(Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."}));
            }
            else
            {
                try
                {
                    // Borramos la entrada de la base de datos
                    using SqlConnection conn = new(CONNECTION_STRING);
                    conn.Open();
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "DELETE FROM ccontrol_horario WHERE id_doc = @ID";
                    command.Parameters.AddWithValue("@ID", documentId);
                    command.ExecuteNonQuery();
                    string ext = "";
                    string[] files = ListFiles(new[] { "candidate", candidateId, "control_horario" });  //Directory.GetFiles(ComposePath(new[] { "candidate", candidateId, "control_horario" }));
                    string archivoBuscado = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).StartsWith(documentId));
                    if (archivoBuscado != null)
                    {
                        ext = Path.GetExtension(archivoBuscado);
                    }
                    // Borramos el archivo
                    DeleteFile(new[] { "candidate", candidateId, "control_horario", documentId });

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception e)
                {
                    result = new
                    {
                        error = "Error 2933, problemas con el archivo." + e.Message,
                    };
                }
            }
            return await Task.FromResult(Ok(result));
        }

        /// <summary>
        /// Elimina todos los documentos de todos los candidatos.
        /// 1º Elimina los documentos de la base de datos (tabla cdocuments).
        /// 2º Elimina los documentos de los candidatos (directorio "cdocuments" en cada directorio de candidato).
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpDelete]
        [Route(template: "deleteAll/")]
        public async Task<IActionResult> DeleteAll()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("CDocuments.DeleteAll", securityToken).Acceso)
            {
                return await Task.FromResult(Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."}));
            }
            else
            {
                try
                {
                    using SqlConnection conn = new(CONNECTION_STRING);
                    conn.Open();
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "DELETE FROM cdocuments";
                    command.ExecuteNonQuery();
                    string[] candidates = ListDirectories(new[] { "candidate" });
                    foreach (string candidateId in candidates)
                    {
                        string path = Path.Combine(candidateId, "cdocuments");
                        if (Directory.Exists(path))
                        {
                            string[] cDocuments = Directory.GetFiles(path);
                            foreach (string cDocument in cDocuments)
                                System.IO.File.Delete(cDocument);
                        }
                    }
                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception e)
                {
                    result = new
                    {
                        error = "Error 2933, problemas con el archivo." + e.Message,
                    };
                }
            }
            return await Task.FromResult(Ok(result));
        }

        /// <summary>
        /// Lista los documentos de un candidato.
        /// </summary>
        /// <param name="candidateId"> id del candidato </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "list-for-candidates/")]
        public IActionResult ListForCandidates()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new
                    {
                        error = false,
                        cdocuments = listForCandidates(conn, candidateId)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido listar los documentos." };
                }
            }

            return Ok(result);
        }

        /// <summary>
        /// Descarga un documento de un candidato.
        /// </summary>
        /// <param name="candidateId"> id del candidato </param>
        /// <param name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "download-for-candidates/{candidateId}/{documentId}")]
        public IActionResult DownloadForCandidates(string candidateId, string documentId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE cdocuments SET downloadDate = @DOWNLOADDATE WHERE id = @ID AND downloadDate IS NULL";
                        command.Parameters.AddWithValue("@ID", documentId);
                        command.Parameters.AddWithValue("@DOWNLOADDATE", DateTime.Now);
                        command.ExecuteNonQuery();
                    }
                    result = new { error = false, data = ReadFile(new[] { "candidate", candidateId, "cdocuments", documentId + ".pdf" }) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 2932, no se ha podido procesar la petición." };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 2932, no se ha podido procesar la petición." };
            }

            return Ok(result);
        }

        /// <summary>
        /// Descarga un documento de un candidato.
        /// </summary>
        /// <param name="candidateId"> id del candidato </param>
        /// <param name="documentId"> id del documento </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "/download-for-candidates-controlHorario/{candidateId}/{documentId}")]
        public IActionResult DownloadForCandidatesControlHorario(string candidateId, string documentId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE ccontrol_horario SET downloadDate = @DOWNLOADDATE WHERE id_doc = @ID AND downloadDate IS NULL";
                        command.Parameters.AddWithValue("@ID", documentId);
                        command.Parameters.AddWithValue("@DOWNLOADDATE", DateTime.Now);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false, data = ReadFile(new[] { "candidate", candidateId, "control_horario", documentId + ".png" }) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 2932, no se ha podido procesar la petición." };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 2932, no se ha podido procesar la petición." };
            }
            return Ok(result);
        }

        /// <summary>
        /// Lista los documentos de control horario que el candidato ha subido a la plataforma.
        /// Hace una consulta a la tabla de documentos de control horario. ccontrol_horario.
        /// </summary>
        /// <param name="candidateId"> id del candidato </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "list-control-horario/")]
        public IActionResult ListControlHorario()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new
                    {
                        error = false,
                        control_horario = listControlHorario(conn, candidateId)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido listar los documentos." };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-month-year-doc-ch/{candidateId}/{year}")]
        public IActionResult GetMonthYearDocCH(string candidateId, int year)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new
                    {
                        error = false,
                        control_horario = getMonthYearDocCH(conn, candidateId, year)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5710, no se han podido listar los documentos." };
                }
            }
            return Ok(result);
        }


        /// <summary>
        /// Lista documentos personales almacenados en sistema de los candidatos.
        /// </summary>
        /// <param name="securityToken"> Token de seguridad </param>
        /// <returns> Json con el resultado de la operación </returns>
        [HttpGet]
        [Route(template: "list-personal/")]
        public IActionResult ListPersonalDocs()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("CDocuments.ListPersonal", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            try
            {
                // 1º Indexación de documentos del sistema de ficheros
                Folder[] folders = ListDirCandidatos(ComposePath(new[] { "candidate" }), new[] { "cdocuments", "docs-template", "incidence-generic", "workshifts", "docs" }).folders;
                // 2º Búsqueda de trabajadores en la base de datos
                List<PersonalRow> personalrows = new();
                using (SqlConnection conn = new(CONNECTION_STRING))
                {
                    conn.Open();
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText =
                    "SELECT C.id as CandidateId, C.nombre as CandidateName, c.apellidos as CandidateSurname, " +
                    "C.dni as CandidateDni, E.id as CompanyId, E.nombre as CompanyName, CE.id as WorkCenterId, " +
                    "CE.alias as WorkCenterName, CA.id as WorkCategoryId, CA.name as WorkCategoryName " +
                    "FROM candidatos C LEFT OUTER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                    "LEFT OUTER JOIN categories CA ON(T.categoryId = CA.id) " +
                    "LEFT OUTER JOIN centros CE ON(T.centroId = CE.id) " +
                    "LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) ";
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        PersonalRow person = new();
                        person.Read(reader);
                        personalrows.Add(person);
                    }
                }
                result = new
                {
                    error = false,
                    personalrows,
                    folders
                };
            }
            catch (Exception)
            {
                result = new { error = "Error 5701, no han podido listar documentos personales." };
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "update-control-horario-data/{documentId}")]
        public async Task<IActionResult> UpdateControlHolrario(string documentId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            try
            {
                if (json.TryGetProperty("year", out JsonElement yearJson) &&
                json.TryGetProperty("month", out JsonElement monthJson))
                {
                    int year = yearJson.GetInt32();
                    int month = monthJson.GetInt32();
                    using SqlConnection conn = new(CONNECTION_STRING);
                    conn.Open();
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "UPDATE ccontrol_horario SET year = @YEAR, month = @MONTH WHERE id_doc = @ID";
                    command.Parameters.AddWithValue("@YEAR", year);
                    command.Parameters.AddWithValue("@MONTH", month);
                    command.Parameters.AddWithValue("@ID", documentId);
                    command.ExecuteNonQuery();
                    result = new { error = false };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5701, no han podido listar documentos." };
            }
            return Ok(result);
        }


        /// <summary>
        /// Sube el documento de control horario del candidato desde la plataforma
        /// </summary>
        /// <param name="candidateId"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(template: "api/v1/cdocuments/upload-control-horario/{candidateId}")]
        public async Task<IActionResult> UploadControlHorario(string candidateId)
        {
            /* object result = new
             {
                 error = "Error 5000, este servicio no está disponible actualmente, disculpe las molestias.",
             };
             return await Task.FromResult(Json(result));
             */
            // Obtener la fecha y hora actual del sistema para generar el id del documento
            DateTime fechaSubida = DateTime.Now;
            int añoDocumento = -1;
            int mesDocumento = -1;
            int diaDocumento = -1;
            bool failed = false;

            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                using StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await reader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("pdf", out JsonElement pdfJson))
                {
                    if (json.TryGetProperty("fecha", out JsonElement fechaJson))
                    {
                        DateTime fechaDocumentoControl = DateTime.Parse(fechaJson.GetString());
                        añoDocumento = fechaDocumentoControl.Year;
                        mesDocumento = fechaDocumentoControl.Month;
                        diaDocumento = fechaDocumentoControl.Day;
                    }
                    string base64String = pdfJson.GetString();
                    string id = ComputeStringHash(candidateId + añoDocumento + mesDocumento + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                    using (SqlConnection conn = new(CONNECTION_STRING))
                    {
                        conn.Open();
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "INSERT INTO ccontrol_horario (id_doc, candidateId, category, year, month, day, uploadDate) VALUES (@ID, @CANDIDATEID, @CATEGORY ,@YEAR, @MONTH, @DAY, @UPLOADDATE)";
                        command.Parameters.AddWithValue("@ID", id);
                        command.Parameters.AddWithValue("@CANDIDATEID", candidateId);
                        command.Parameters.AddWithValue("@CATEGORY", "ControlHorario");
                        command.Parameters.AddWithValue("@YEAR", añoDocumento);
                        command.Parameters.AddWithValue("@MONTH", mesDocumento);
                        command.Parameters.AddWithValue("@DAY", diaDocumento);
                        command.Parameters.AddWithValue("@UPLOADDATE", fechaSubida);
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception e)
                        {

                            failed = true;
                            result = new
                            {
                                error = "Error 2933, no se pudo insertar el registro en la BD." + e.Message,
                            };

                        }
                    }
                    if (base64String != null && !failed)
                    {
                        HelperMethods.SaveFile(new[] { "candidate", candidateId, "control_horario", id }, base64String);
                    }
                    result = new { error = false };
                }
            }
            catch (Exception e)
            {
                result = new
                {
                    error = "Error 2933, problemas con el archivo." + e.Message,
                };
            }
            return await Task.FromResult(Ok(result));
        }




        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        public struct CDocument
        {
            public string Id { get; set; }
            public string CandidateId { get; set; }
            public string Category { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
            public int Day { get; set; }
            public DateTime? DownloadDate { get; set; }
            public DateTime? SignedDate { get; set; }
            public DateTime? UploadDate { get; set; }
        }

        /// <summary>
        /// Estructura de un documento de un candidato para listar.
        /// </summary>
        public struct CDocumentRow
        {
            public string Id { get; set; }
            public string CandidateId { get; set; }
            public string CandidateName { get; set; }
            public string CandidateDni { get; set; }
            public string CompanyId { get; set; }
            public string CompanyName { get; set; }
            public string WorkCenterId { get; set; }
            public string WorkCenterName { get; set; }
            public string WorkCategoryId { get; set; }
            public string WorkCategoryName { get; set; }
            public string? Category { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
            public int Day { get; set; }
            public DateTime? DownloadDate { get; set; }
            public DateTime? SignedDate { get; set; }

            public void Read(SqlDataReader reader)
            {
                Id = reader.GetString(reader.GetOrdinal("Id"));
                CandidateId = reader.GetString(reader.GetOrdinal("CandidateId"));
                CandidateName = reader.GetString(reader.GetOrdinal("CandidateName")).ToUpper().Trim();
                CandidateDni = reader.GetString(reader.GetOrdinal("CandidateDni")).ToUpper().Trim();
                CompanyId = reader.GetString(reader.GetOrdinal("CompanyId"));
                CompanyName = reader.GetString(reader.GetOrdinal("CompanyName"));
                WorkCenterId = reader.GetString(reader.GetOrdinal("WorkCenterId"));
                WorkCenterName = reader.GetString(reader.GetOrdinal("WorkCenterName"));
                WorkCategoryId = reader.GetString(reader.GetOrdinal("WorkCategoryId"));
                WorkCategoryName = reader.GetString(reader.GetOrdinal("WorkCategoryName"));
                Category = reader.GetString(reader.GetOrdinal("Category"));
                SignedDate = reader.IsDBNull(reader.GetOrdinal("SignedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("SignedDate"));
                Year = reader.GetInt32(reader.GetOrdinal("Year"));
                Month = reader.GetInt32(reader.GetOrdinal("Month"));
                Day = reader.GetInt32(reader.GetOrdinal("Day"));
                DownloadDate = reader.IsDBNull(reader.GetOrdinal("DownloadDate")) ? null : reader.GetDateTime(reader.GetOrdinal("DownloadDate"));

            }
        }

        /// <summary>
        /// Estrucutra de un documento personal de un candidato.
        /// </summary>
        public struct PersonalRow
        {
            public string CandidateId { get; set; }
            public string CandidateName { get; set; }
            public string CandidateSurname { get; set; }
            public string CandidateDni { get; set; }
            public string CompanyId { get; set; }
            public string CompanyName { get; set; }
            public string WorkCenterId { get; set; }
            public string WorkCenterName { get; set; }
            public string WorkCategoryId { get; set; }
            public string WorkCategoryName { get; set; }

            internal void Read(SqlDataReader reader)
            {
                CandidateId = reader.GetString(reader.GetOrdinal("CandidateId"));
                CandidateName = reader.GetString(reader.GetOrdinal("CandidateName")).ToUpper().Trim();
                CandidateSurname = reader.GetString(reader.GetOrdinal("CandidateSurname")).ToUpper().Trim();
                CandidateDni = reader.GetString(reader.GetOrdinal("CandidateDni")).ToUpper().Trim();
                CompanyId = reader.IsDBNull(reader.GetOrdinal("CompanyId")) ? null : reader.GetString(reader.GetOrdinal("CompanyId"));
                CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName"));
                WorkCenterId = reader.IsDBNull(reader.GetOrdinal("WorkCenterId")) ? null : reader.GetString(reader.GetOrdinal("WorkCenterId"));
                WorkCenterName = reader.IsDBNull(reader.GetOrdinal("WorkCenterName")) ? null : reader.GetString(reader.GetOrdinal("WorkCenterName"));
                WorkCategoryId = reader.IsDBNull(reader.GetOrdinal("WorkCategoryId")) ? null : reader.GetString(reader.GetOrdinal("WorkCategoryId"));
                WorkCategoryName = reader.IsDBNull(reader.GetOrdinal("WorkCategoryName")) ? null : reader.GetString(reader.GetOrdinal("WorkCategoryName"));
            }
        }

        ///<summary> 
        ///Estructura de un documento de control horario de un candidato con mucha info. del candidato 
        ///</summary>
        public struct CHorarioRow
        {
            public string Id { get; set; }
            public string CandidateId { get; set; }
            public string CandidateName { get; set; }
            public string CandidateDni { get; set; }
            public string CompanyId { get; set; }
            public string CompanyName { get; set; }
            public string WorkCenterId { get; set; }
            public string WorkCenterName { get; set; }
            public string WorkCategoryId { get; set; }
            public string WorkCategoryName { get; set; }
            public string? Category { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
            public int Day { get; set; }
            public DateTime? DownloadDate { get; set; }
            public DateTime? UploadDate { get; set; }
            public DateTime? SignedDate { get; set; }

            public void Read(SqlDataReader reader)
            {
                Id = reader.IsDBNull(reader.GetOrdinal("Id")) ? null : reader.GetString(reader.GetOrdinal("Id"));
                CandidateId = reader.IsDBNull(reader.GetOrdinal("CandidateId")) ? null : reader.GetString(reader.GetOrdinal("CandidateId"));
                CandidateName = reader.IsDBNull(reader.GetOrdinal("CandidateName")) ? null : reader.GetString(reader.GetOrdinal("CandidateName")).ToUpper().Trim();
                CandidateDni = reader.IsDBNull(reader.GetOrdinal("CandidateDni")) ? null : reader.GetString(reader.GetOrdinal("CandidateDni")).ToUpper().Trim();
                CompanyId = reader.IsDBNull(reader.GetOrdinal("CompanyId")) ? null : reader.GetString(reader.GetOrdinal("CompanyId"));
                CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName"));
                WorkCenterId = reader.IsDBNull(reader.GetOrdinal("WorkCenterId")) ? null : reader.GetString(reader.GetOrdinal("WorkCenterId"));
                WorkCenterName = reader.IsDBNull(reader.GetOrdinal("WorkCenterName")) ? null : reader.GetString(reader.GetOrdinal("WorkCenterName"));
                WorkCategoryId = reader.IsDBNull(reader.GetOrdinal("WorkCategoryId")) ? null : reader.GetString(reader.GetOrdinal("WorkCategoryId"));
                WorkCategoryName = reader.IsDBNull(reader.GetOrdinal("WorkCategoryName")) ? null : reader.GetString(reader.GetOrdinal("WorkCategoryName"));
                Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category"));
                Year = reader.IsDBNull(reader.GetOrdinal("Year")) ? 0 : reader.GetInt32(reader.GetOrdinal("Year"));
                Month = reader.IsDBNull(reader.GetOrdinal("Month")) ? 0 : reader.GetInt32(reader.GetOrdinal("Month"));
                Day = reader.IsDBNull(reader.GetOrdinal("Day")) ? 0 : reader.GetInt32(reader.GetOrdinal("Day"));
                DownloadDate = reader.IsDBNull(reader.GetOrdinal("DownloadDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("DownloadDate"));
                UploadDate = reader.IsDBNull(reader.GetOrdinal("UploadDate")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("UploadDate"));
            }

        }

        public struct Folder
        {
            public string fullpath { get; set; }
            public Folder[] folders { get; set; }
            public string[] files { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        /// <summary>
        /// Lista los documentos de un candidato.
        /// </summary>
        /// <param name="conn"> Conexión a la base de datos </param>
        /// <param name="candidateId"> id del candidato </param>
        /// <returns> Lista de documentos para el candidato </returns>
        private static List<CDocument> listForCandidates(SqlConnection conn, string candidateId)
        {
            List<CDocument> cDocuments = new();
            using SqlCommand command = conn.CreateCommand();
            command.CommandText = "SELECT * FROM cdocuments WHERE candidateId = @CANDIDATE_ID";
            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                CDocument cdoc = new()
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    CandidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                    Category = reader.GetString(reader.GetOrdinal("category")),
                    Year = reader.GetInt32(reader.GetOrdinal("year")),
                    Month = reader.GetInt32(reader.GetOrdinal("month")),
                    Day = reader.GetInt32(reader.GetOrdinal("day")),
                    DownloadDate = reader.IsDBNull(reader.GetOrdinal("downloadDate")) ? null : reader.GetDateTime(reader.GetOrdinal("downloadDate")),
                    SignedDate = reader.IsDBNull(reader.GetOrdinal("signedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("signedDate ")),
                };
                cDocuments.Add(cdoc);
            }

            return cDocuments;
        }

        /// <summary>
        /// Lista los documentos de control horario que el candidato ha subido a la plataforma.
        /// </summary>
        /// <param name="conn"> Conexión a la base de datos </param>
        /// <param name="candidateId"> id del candidato </param>
        /// <returns> Lista de documentos de control horario para el candidato </returns>
        private static List<CDocument> listControlHorario(SqlConnection conn, string candidateId)
        {

            List<CDocument> cControlHorario = new();
            using SqlCommand command = conn.CreateCommand();
            command.CommandText = "SELECT * FROM ccontrol_horario WHERE candidateId = @CANDIDATE_ID order by uploadDate desc";
            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                CDocument cdoc = new()
                {
                    Id = reader.GetString(reader.GetOrdinal("id_doc")),
                    CandidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                    Category = reader.GetString(reader.GetOrdinal("category")),
                    Year = reader.GetInt32(reader.GetOrdinal("year")),
                    Month = reader.GetInt32(reader.GetOrdinal("month")),
                    Day = reader.GetInt32(reader.GetOrdinal("day")),
                    UploadDate = reader.IsDBNull(reader.GetOrdinal("uploadDate")) ? null : reader.GetDateTime(reader.GetOrdinal("uploadDate")),
                    DownloadDate = reader.IsDBNull(reader.GetOrdinal("downloadDate")) ? null : reader.GetDateTime(reader.GetOrdinal("downloadDate")),
                };
                cControlHorario.Add(cdoc);
            }

            return cControlHorario;
        }

        /// <summary>
        /// Lista documentos en base a un filtro.
        /// </summary>
        /// <param name="searchKey"> Palabra clave de búsqueda </param>
        /// <param name="year"> Año </param>
        /// <param name="month"> Mes </param>
        /// <param name="docCategory"> Categoría del documento </param>
        /// <param name="signed"> ¿Está firmado? </param>
        /// <param name="downloaded"> ¿Está descargado? </param>
        /// <param name="page"> Página </param>
        /// <param name="perpage"> Documentos por página </param>
        /// <param name="sort"> Orden </param>
        /// <returns> Lista de documentos </returns>
        private static List<CDocumentRow> listFiltered(string searchKey, int? year, int? month, string docCategory, bool? signed, bool? downloaded, int page, int perpage, string sort)
        {
            List<CDocumentRow> cDocuments = new();
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT CD.id as Id, CD.candidateId as CandidateId, CONCAT(C.apellidos, ', ', C.nombre) as CandidateName, " +
                    "C.dni as CandidateDni, E.id as CompanyId, E.nombre as CompanyName, CE.id as WorkCenterId, " +
                    "CE.alias as WorkCenterName, CA.id as WorkCategoryId, CA.name as WorkCategoryName, CD.category as Category, " +
                    "CD.year as Year, CD.month as Month, CD.day as Day, CD.downloadDate as DownloadDate, CD.signedDate as SignedDate " +
                    "FROM cdocuments CD LEFT OUTER JOIN candidatos C ON(CD.candidateId = C.id) " +
                    "LEFT OUTER JOIN trabajos T ON(C.lastSignLink = T.signLink) LEFT OUTER JOIN categories CA ON(T.categoryId = CA.id) " +
                    "LEFT OUTER JOIN centros CE ON(T.centroId = CE.id) LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE(@KEY IS NULL OR CONCAT(TRIM(C.nombre), ' ', TRIM(C.apellidos)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                    "OR CONCAT(TRIM(C.apellidos), ' ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                    "OR CONCAT(TRIM(C.apellidos), ', ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                    "OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY OR CE.alias LIKE @KEY) " +
                    "AND (@YEAR IS NULL OR CD.year = @YEAR) AND (@MONTH IS NULL OR CD.month = @MONTH) " +
                    "AND (@CATEGORY IS NULL OR CD.category ";
                if (SubCategories.Keys.ToList().Contains(docCategory))
                {
                    command.CommandText += "IN (";
                    foreach (string subCategory in SubCategories[docCategory])
                        command.CommandText += "'" + subCategory + "', ";
                    command.CommandText = command.CommandText[..^2] + ") ";
                }
                else
                    command.CommandText += "LIKE @CATEGORY";
                command.CommandText += ") AND ";
                if (downloaded != null)
                {
                    if (downloaded.Value)
                        command.CommandText += "(CD.downloadDate IS NOT NULL) ";
                    else
                        command.CommandText += "(CD.downloadDate IS NULL) ";
                }
                else
                    command.CommandText += "(@DOWNLOADED IS NULL) ";
                command.CommandText += "AND ";
                if (signed != null)
                {
                    if (signed.Value)
                        command.CommandText += "(CD.signedDate IS NOT NULL) ";
                    else
                        command.CommandText += "(CD.signedDate IS NULL) ";
                }
                else
                    command.CommandText += "(@SIGNED IS NULL) ";
                command.CommandText += sort + " ";
                command.CommandText += "OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY ";
                Console.WriteLine(command.CommandText);

                command.Parameters.AddWithValue("@KEY", ((object)(searchKey == null ? null : ("%" + searchKey.Trim() + "%"))) ?? DBNull.Value);
                command.Parameters.AddWithValue("@YEAR", ((object)year) ?? DBNull.Value);
                command.Parameters.AddWithValue("@MONTH", ((object)month) ?? DBNull.Value);
                command.Parameters.AddWithValue("@CATEGORY", ((object)docCategory) ?? DBNull.Value);
                if (downloaded == null) command.Parameters.AddWithValue("@DOWNLOADED", DBNull.Value);
                if (signed == null) command.Parameters.AddWithValue("@SIGNED", DBNull.Value);
                command.Parameters.AddWithValue("@OFFSET", page * perpage);
                command.Parameters.AddWithValue("@LIMIT", perpage);

                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    CDocumentRow cdoc = new();
                    cdoc.Read(reader);
                    cDocuments.Add(cdoc);
                }
            }

            return cDocuments;
        }

        private static readonly Dictionary<string, string[]> SubCategories = new()
        {
            { "certificados", new string[] { "CertINEM", "CertIRPF" } },
            { "nominasyfiniquitos", new string[] { "Nomina", "Finiquito" } },
            { "seguridadlaboral", new string[] { "ExamenDeSalud", "FichaPrevencionRiesgos" } },
            { "contratos", new string[] { "CLTraIndefinido", "CLCFijoDiscontinuo", "CLTraFijoDiscontinuo", "CLCEventual", "CLCSustitucion", "CLCEventualParcial", "CLCSustitucionParcial", "CJCategoria", "CLProrroga", "CLVencimiento", "OrdenServicio", "CBajaVoluntaria" } },
        };

        /// <summary>
        /// Cuenta los documentos en base a un filtro.
        /// </summary>
        /// <param name="searchKey"> Palabra clave de búsqueda </param>
        /// <param name="year"> Año </param>
        /// <param name="month"> Mes </param>
        /// <param name="docCategory"> Categoría del documento </param>
        /// <param name="signed"> ¿Está firmado? </param>
        /// <param name="downloaded"> ¿Está descargado? </param>
        /// <param name="page"> Página </param>
        /// <param name="perpage"> Documentos por página </param>
        /// <param name="sort"> Orden </param>
        /// <returns> Número de documentos </returns>
        private int listFilteredCount(string searchKey, int? year, int? month, string docCategory, bool? signed, bool? downloaded)
        {
            using SqlConnection conn = new(CONNECTION_STRING);
            conn.Open();

            using SqlCommand command = conn.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) " +
                "FROM cdocuments CD LEFT OUTER JOIN candidatos C ON(CD.candidateId = C.id) " +
                "LEFT OUTER JOIN trabajos T ON(C.lastSignLink = T.signLink) LEFT OUTER JOIN categories CA ON(T.categoryId = CA.id) " +
                "LEFT OUTER JOIN centros CE ON(T.centroId = CE.id) LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                "WHERE(@KEY IS NULL OR CONCAT(TRIM(C.nombre), ' ', TRIM(C.apellidos)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                "OR CONCAT(TRIM(C.apellidos), ' ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                "OR CONCAT(TRIM(C.apellidos), ', ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                "OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY OR CE.alias LIKE @KEY) " +
                "AND (@YEAR IS NULL OR CD.year = @YEAR) AND (@MONTH IS NULL OR CD.month = @MONTH) " +
                    "AND (@CATEGORY IS NULL OR CD.category ";
            if (SubCategories.Keys.ToList().Contains(docCategory))
            {
                command.CommandText += "IN (";
                foreach (string subCategory in SubCategories[docCategory])
                    command.CommandText += "'" + subCategory + "', ";
                command.CommandText = command.CommandText[..^2] + ") ";
            }
            else
                command.CommandText += "LIKE @CATEGORY";
            command.CommandText += ") AND ";
            if (downloaded != null)
            {
                if (downloaded.Value)
                    command.CommandText += "(CD.downloadDate IS NOT NULL) ";
                else
                    command.CommandText += "(CD.downloadDate IS NULL) ";
            }
            else
                command.CommandText += "(@DOWNLOADED IS NULL) ";
            command.CommandText += "AND ";
            if (signed != null)
            {
                if (signed.Value)
                    command.CommandText += "(CD.signedDate IS NOT NULL) ";
                else
                    command.CommandText += "(CD.signedDate IS NULL) ";
            }
            else
                command.CommandText += "(@SIGNED IS NULL) ";

            command.Parameters.AddWithValue("@KEY", ((object)(searchKey == null ? null : ("%" + searchKey.Trim() + "%"))) ?? DBNull.Value);
            command.Parameters.AddWithValue("@YEAR", ((object)year) ?? DBNull.Value);
            command.Parameters.AddWithValue("@MONTH", ((object)month) ?? DBNull.Value);
            command.Parameters.AddWithValue("@CATEGORY", ((object)docCategory) ?? DBNull.Value);
            command.Parameters.AddWithValue("@DOWNLOADED", downloaded == null ? DBNull.Value : (downloaded.Value ? 1 : 0));
            command.Parameters.AddWithValue("@SIGNED", signed == null ? DBNull.Value : (signed.Value ? 1 : 0));
            return (int)command.ExecuteScalar();
        }

        /// <summary>
        /// Lista los documentos de control horario de los candidatos en base a unos filtros
        /// 
        /// </summary>
        /// <param name="searchKey"></param>
        /// <param name="year"></param>
        /// <param name="month"></param>
        /// <param name="uploaded"></param>
        /// <param name="downloaded"></param>
        /// <param name="company"></param>
        /// <param name="workcenter"></param>
        /// <param name="page"></param>
        /// <param name="perpage"></param>
        /// <param name="sort"></param>
        /// <returns></returns>
        private static List<CHorarioRow> listFilteredDCHorario(string searchKey, int? year, int? month, bool? uploaded, bool? downloaded, string company, string workcenter, int page, int perpage, string sort)
        {
            {
                List<CHorarioRow> cDocuments = new();
                using (SqlConnection conn = new(CONNECTION_STRING))
                {
                    conn.Open();
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "SELECT CH.id_doc as Id, C.id as CandidateId, CONCAT(C.apellidos, ', ', C.nombre) as CandidateName, " +
                        " C.dni as CandidateDni, E.id as CompanyId, E.nombre as CompanyName, CE.id as WorkCenterId, CE.alias as WorkCenterName, CH.category as Category, " +
                        " CA.id as WorkCategoryId,CA.name as WorkCategoryName, CH.year as Year, CH.month as Month, CH.day as Day, CH.downloadDate as DownloadDate, CH.uploadDate as UploadDate, CH.signedDate as SignedDate " +
                        " FROM  candidatos C " +
                        " LEFT OUTER JOIN ccontrol_horario CH ON CH.candidateId = C.id " +
                        " LEFT OUTER JOIN trabajos T ON C.lastSignLink = T.signLink " +
                        " LEFT OUTER JOIN categories CA ON T.categoryId = CA.id " +
                        " LEFT OUTER JOIN centros CE ON T.centroId = CE.id " +
                        " LEFT OUTER JOIN empresas E ON CE.companyId = E.id " +
                        " WHERE (@KEY IS NULL OR CONCAT(TRIM(C.nombre), ' ', TRIM(C.apellidos)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI" +
                        " OR CONCAT(TRIM(C.apellidos), ' ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                        " OR CONCAT(TRIM(C.apellidos), ', ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                        " OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY OR CE.alias LIKE @KEY)" +
                        " AND (@COMPANY IS NULL OR E.id = @COMPANY) AND (@WORKCENTER IS NULL OR CE.id = @WORKCENTER)" +
                        " AND (@YEAR IS NULL OR CH.year = @YEAR)  AND (@MONTH IS NULL" +
                        " OR CH.month = @MONTH) ";

                    command.CommandText += " AND ";
                    if (downloaded != null)
                    {
                        if (downloaded.Value)
                            command.CommandText += "(CH.downloadDate IS NOT NULL) ";
                        else
                            command.CommandText += "(CH.downloadDate IS NULL) ";
                    }
                    else
                        command.CommandText += "(@DOWNLOADED IS NULL) ";
                    command.CommandText += "AND ";

                    if (uploaded != null)
                    {
                        if (uploaded.Value)
                            command.CommandText += "(CH.uploadDate IS NOT NULL) ";
                        else
                            command.CommandText += "(CH.uploadDate IS NULL) ";
                    }
                    else
                        command.CommandText += "(@UPLOADED IS NULL) ";
                    command.CommandText += sort;
                    command.CommandText += " OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY ";

                    command.Parameters.AddWithValue("@KEY", ((object)(searchKey == null ? null : ("%" + searchKey.Trim() + "%"))) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@YEAR", ((object)year) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@MONTH", ((object)month) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY", company == null ? DBNull.Value : company);
                    command.Parameters.AddWithValue("@WORKCENTER", workcenter == null ? DBNull.Value : workcenter);
                    if (downloaded == null) command.Parameters.AddWithValue("@DOWNLOADED", DBNull.Value);
                    if (uploaded == null) command.Parameters.AddWithValue("@UPLOADED", DBNull.Value);
                    command.Parameters.AddWithValue("@OFFSET", page * perpage);
                    command.Parameters.AddWithValue("@LIMIT", perpage);

                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CHorarioRow cdoc = new();
                        cdoc.Read(reader);
                        cDocuments.Add(cdoc);
                    }
                }
                return cDocuments;
            }
        }

        /// <summary>
        /// Cuenta los documentos de control horario en base a un filtro.
        /// </summary>
        /// <param name="searchKey"> Palabra clave de búsqueda </param>
        /// <param name="year"> Año </param>
        /// <param name="month"> Mes </param>
        /// <param name="uploaded"> ¿Está subido? </param>
        /// <param name="downloaded"> ¿Está descargado? </param>
        /// <param name="company"> Empresa </param>
        /// <param name="workcenter"> Centro de trabajo </param>
        /// <param name="page"> Página </param>
        /// <param name="perpage"> Documentos por página </param>
        /// <param name="sort"> Orden </param>
        /// <returns> Número de documentos </returns>
        /// 
        private int listFilteredCountDCHorario(string searchKey, int? year, int? month, bool? uploaded, bool? downloaded, string company, string workcenter)
        {

            using SqlConnection conn = new(CONNECTION_STRING);
            conn.Open();

            using SqlCommand command = conn.CreateCommand();
            command.CommandText = "SELECT COUNT(*) " +
                       "FROM candidatos C " +
                       "LEFT OUTER JOIN ccontrol_horario CH ON (CH.candidateId = C.id) " +
                       "LEFT OUTER JOIN trabajos T ON (C.lastSignLink = T.signLink) " +
                       "LEFT OUTER JOIN centros CE ON (T.centroId = CE.id) " +
                       "LEFT OUTER JOIN empresas E ON (CE.companyId = E.id) " +
                       "WHERE (@KEY IS NULL OR CONCAT(TRIM(C.nombre), ' ', TRIM(C.apellidos)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                       "OR CONCAT(TRIM(C.apellidos), ' ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                       "OR CONCAT(TRIM(C.apellidos), ', ', TRIM(C.nombre)) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI " +
                       "OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY OR E.nombre LIKE @KEY OR CE.alias LIKE @KEY) " +
                       "AND (@YEAR IS NULL OR CH.year = @YEAR) AND (@MONTH IS NULL OR CH.month = @MONTH) AND (@COMPANY IS NULL OR E.id = @COMPANY) AND (@WORKCENTER IS NULL OR CE.id= @WORKCENTER)";
            command.CommandText += "AND ";
            if (downloaded != null)
            {
                if (downloaded.Value)
                    command.CommandText += "(CH.downloadDate IS NOT NULL) ";
                else
                    command.CommandText += "(CH.downloadDate IS NULL) ";
            }
            else
                command.CommandText += "(@DOWNLOADED IS NULL) ";
            command.CommandText += "AND ";
            if (uploaded != null)
            {
                if (uploaded.Value)
                    command.CommandText += "(CH.uploadDate IS NOT NULL) ";
                else
                    command.CommandText += "(CH.uploadDate IS NULL) ";
            }
            else
                command.CommandText += "(@UPLOADED IS NULL) ";

            command.Parameters.AddWithValue("@KEY", ((object)(searchKey == null ? null : ("%" + searchKey.Trim() + "%"))) ?? DBNull.Value);
            command.Parameters.AddWithValue("@YEAR", ((object)year) ?? DBNull.Value);
            command.Parameters.AddWithValue("@MONTH", ((object)month) ?? DBNull.Value);
            command.Parameters.AddWithValue("@DOWNLOADED", downloaded == null ? DBNull.Value : (downloaded.Value ? 1 : 0));
            command.Parameters.AddWithValue("@UPLOADED", uploaded == null ? DBNull.Value : (uploaded.Value ? 1 : 0));
            command.Parameters.AddWithValue("@COMPANY", company == null ? DBNull.Value : company);
            command.Parameters.AddWithValue("@WORKCENTER", workcenter == null ? DBNull.Value : workcenter);
            return (int)command.ExecuteScalar();
        }
        private static int[] getMonthYearDocCH(SqlConnection conn, string candidateId, int year)
        {
            List<int> months = new List<int>();
            using SqlCommand command = conn.CreateCommand();
            command.CommandText = "SELECT distinct month FROM ccontrol_horario WHERE candidateId = @CANDIDATE_ID and year = @YEAR";
            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
            command.Parameters.AddWithValue("@YEAR", year);

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                int month = reader.GetInt32(0);
                months.Add(month);
            }
            return months.ToArray();
        }

        public static Folder ListDirCandidatos(string fullpath, string[] excludedFolders)
        {
            Folder folder = new()
            {
                fullpath = fullpath.Split(Path.DirectorySeparatorChar)[^1],
                files = Directory.GetFiles(fullpath)
            };
            for (int i = 0; i < folder.files.Length; i++)
                folder.files[i] = folder.files[i].Split(Path.DirectorySeparatorChar)[^1];
            string[] dirs = Directory.GetDirectories(fullpath);
            folder.folders = new Folder[dirs.Length];
            for (int i = 0; i < dirs.Length; i++)
            {
                if (!excludedFolders.Contains(dirs[i].Split(Path.DirectorySeparatorChar)[^1]))
                {
                    folder.folders[i] = ListDirCandidatos(dirs[i], excludedFolders);
                }
                else
                {
                    folder.folders[i] = new()
                    {
                        fullpath = dirs[i].Split(Path.DirectorySeparatorChar)[^1],
                        files = Array.Empty<string>(),
                        folders = Array.Empty<Folder>()
                    };
                }
            }
            return folder;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------

    }
}
