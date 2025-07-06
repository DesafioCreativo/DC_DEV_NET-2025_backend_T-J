using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Text;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Model.Candidate;
using ThinkAndJobSolution.Controllers.Candidate;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.Constants;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    struct Candidate
    {
        public string dni;
        public string name;
        public string lastname;
    }

    [Route("api/v1/PRL")]
    [ApiController]
    [Authorize]
    public class PrlController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpPost]
        [Route(template: "update-altas-bajas/")]
        public async Task<IActionResult> UpdateAltasBajas()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRL.UpdateAltasBajas", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                result = new
                {
                    error = false,
                };
            }
            return Ok(result);
        }

        //------------------PrlFormController Inicio------------------
        #region "PrlFormController"
        [HttpGet]
        [Route(template: "form/list/{type?}")]
        public IActionResult List(string type = null)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRL.List", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM formularios WHERE (@TYPE IS NULL OR @TYPE = tipo)";
                        command.Parameters.AddWithValue("@TYPE", HelperMethods.TestTypeFilter(type));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            List<object> forms = new List<object>();
                            while (reader.Read())
                            {
                                forms.Add(new
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    name = reader.GetString(reader.GetOrdinal("nombre")),
                                    details = reader.GetString(reader.GetOrdinal("detalles")),
                                    type = HelperMethods.PrettyCase(reader.GetString(reader.GetOrdinal("tipo")))
                                });
                            }
                            result = forms;
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "form/create/")]
        public async Task<IActionResult> CreateForm()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRL.CreateForm", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using System.IO.StreamReader reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await reader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("name", out JsonElement formNameObj) &&
                    json.TryGetProperty("details", out JsonElement formDetails) &&
                    json.TryGetProperty("type", out JsonElement formType))
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            string name = formNameObj.GetString();
                            string details = formDetails.GetString();
                            string id = HelperMethods.ComputeStringHash(name + details + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                            object type = HelperMethods.TestTypeFilter(formType.GetString());
                            if (type == DBNull.Value) type = "PRL";
                            command.CommandText = "INSERT INTO formularios(id, nombre, detalles, tipo, preguntas) VALUES " +
                                "(@ID, @NAME, @DETAILS, @TYPE, '[]')";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@NAME", name);
                            command.Parameters.AddWithValue("@DETAILS", details);
                            command.Parameters.AddWithValue("@TYPE", type);
                            if (command.ExecuteNonQuery() > 0)
                            {
                                result = new
                                {
                                    error = false
                                };
                            }
                            else
                            {
                                result = new
                                {
                                    error = "Error 5491, no se ha podido crear el formulario"
                                };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "quest/list/{formId}/")]
        public IActionResult ListFormQuestions(string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRL.ListFormQuestions", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT preguntas FROM formularios WHERE id = @FORM_ID";
                        command.Parameters.AddWithValue("@FORM_ID", formId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = parseQuestions(reader.GetString(reader.GetOrdinal("preguntas")));
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "form/update/{formId}/")]
        public async Task<IActionResult> UpdateFormQuestions(string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRL.UpdateFormQuestions", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using System.IO.StreamReader reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await reader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("questions", out JsonElement questionsArrayObj) &&
                    json.TryGetProperty("formName", out JsonElement formNameObj) &&
                    json.TryGetProperty("formDetails", out JsonElement formDetailsObj) &&
                    json.TryGetProperty("formType", out JsonElement formType) &&
                    json.TryGetProperty("certNeeded", out JsonElement certNeeded))
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                object type = HelperMethods.TestTypeFilter(formType.GetString());
                                if (type == DBNull.Value) type = "PRL";
                                command.CommandText = "UPDATE formularios SET nombre = @F_NAME, detalles = @F_DETAILS, tipo = @TYPE, preguntas = @QUESTIONS, requiereCertificado = @CERT_NEEDED WHERE id = @ID";
                                command.Parameters.AddWithValue("@id", formId);
                                command.Parameters.AddWithValue("@F_NAME", formNameObj.GetString());
                                command.Parameters.AddWithValue("@F_DETAILS", formDetailsObj.GetString());
                                command.Parameters.AddWithValue("@CERT_NEEDED", certNeeded.GetBoolean() ? 1 : 0);
                                command.Parameters.AddWithValue("@TYPE", type);
                                command.Parameters.AddWithValue("@QUESTIONS", JsonSerializer.Serialize(parseQuestions(questionsArrayObj)));
                                command.ExecuteNonQuery();
                                result = new
                                {
                                    error = false
                                };
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "form/details/{formId}/")]
        public IActionResult FormDetails(string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRL.FormDetails", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM formularios WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", formId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {

                            if (reader.Read())
                            {
                                result = new
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    name = reader.GetString(reader.GetOrdinal("nombre")),
                                    details = reader.GetString(reader.GetOrdinal("detalles")),
                                    type = reader.GetString(reader.GetOrdinal("tipo")),
                                    certNeeded = reader.GetInt32(reader.GetOrdinal("requiereCertificado")) == 1

                                };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }


        [HttpPost]
        [Route(template: "form/delete/{formId}/")]
        public IActionResult DeleteForm(string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("PRL.DeleteForm", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {

                        bool failed = false;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM emision_cuestionarios WHERE formularioId LIKE @ID";
                                command.Parameters.AddWithValue("@ID", formId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                            }
                        }
                        if (!failed)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM vinculos_categorias_formularios WHERE formularioId LIKE @ID";
                                    command.Parameters.AddWithValue("@ID", formId);
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                }
                            }
                        }
                        if (!failed)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM formularios WHERE id LIKE @ID";
                                    command.Parameters.AddWithValue("@ID", formId);
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                }
                            }
                        }
                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            transaction.Commit();
                            result = new
                            {
                                error = false
                            };

                            HelperMethods.LogToDB(HelperMethods.LogType.DELETION, "Eliminado formulario " + HelperMethods.FindFormNameById(formId, conn) + " modificado", HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                        }
                    }
                }
            }
            return Ok(result);
        }
        #endregion

        #region "XlsxController"
        [HttpGet]
        [Route(template: "download-update-candidatos-template/")]
        public IActionResult GenerateTemplateUpdateCandidatos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HelperMethods.HasPermission("XSLX.GenerateTemplateUpdateCandidatos", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            string fileName = "PlantillaActualizacionThinkAndJob.xlsx";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ExcelTemplates", fileName);
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            HttpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(filePath), contentType)
            {
                FileDownloadName = fileName
            };
            return response;
        }

        [HttpPost]
        [Route(template: "upload-update-candidatos-template/")]
        public async Task<IActionResult> UploadTemplateUpdateCandidatos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("XSLX.UploadTemplateUpdateCandidatos", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("xlsx", out JsonElement xlsxJson))
            {
                List<CandidateStats> candidates = new();
                List<string> errorList = new();
                try
                {
                    string xlsxString = xlsxJson.GetString();
                    byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
                    string tmpDir = HelperMethods.GetTemporaryDirectory();
                    string tmpFile = Path.Combine(tmpDir, "template.xlsx");
                    System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);
                    XSSFWorkbook workbook = new XSSFWorkbook(tmpFile);
                    ISheet sheet = workbook.GetSheetAt(0);
                    DataFormatter formatter = new DataFormatter();
                    if (!HelperMethods.CheckExcelTemplateId(workbook, "update-candidates"))
                        return Ok(new { error = "Error 4010, plantilla incorrecta." });
                    //Obtener los candidatos
                    int r = 1;
                    while (true)
                    {
                        IRow row = sheet.GetRow(r++);
                        if (row == null || row.GetCell(0) == null) break;

                        CandidateStats candidate = new CandidateStats()
                        {
                            dni = formatter.FormatCellValue(row.GetCell(0))?.Trim(),
                            name = formatter.FormatCellValue(row.GetCell(1))?.Trim(),
                            surname = ((formatter.FormatCellValue(row.GetCell(2))?.Trim() ?? "") + " " + (formatter.FormatCellValue(row.GetCell(3))?.Trim() ?? "")).Trim(),
                            numeroSeguridadSocial = formatter.FormatCellValue(row.GetCell(4))?.Trim(),
                            birth = row.GetCell(6)?.DateCellValue ?? null,
                            nacionalidad = formatter.FormatCellValue(row.GetCell(7))?.Trim(),
                            phone = formatter.FormatCellValue(row.GetCell(8))?.Trim(),
                            email = formatter.FormatCellValue(row.GetCell(9))?.Trim(),
                            direccion = formatter.FormatCellValue(row.GetCell(10))?.Trim(),
                            cp = formatter.FormatCellValue(row.GetCell(11))?.Trim(),
                            localidad = formatter.FormatCellValue(row.GetCell(12))?.Trim(),
                            provincia = formatter.FormatCellValue(row.GetCell(13))?.Trim(),
                            cuentaBancaria = formatter.FormatCellValue(row.GetCell(14))?.Trim(),
                            fechaComienzoTrabajo = row.GetCell(15)?.DateCellValue ?? null,
                            permisoTrabajoCaducidad = row.GetCell(16)?.DateCellValue ?? null,
                            centroAlias = formatter.FormatCellValue(row.GetCell(17))?.Trim(),
                            workName = formatter.FormatCellValue(row.GetCell(18))?.Trim(),
                        };
                        if (string.IsNullOrWhiteSpace(candidate.name)) candidate.name = null;
                        if (string.IsNullOrWhiteSpace(candidate.surname)) candidate.surname = null;
                        if (string.IsNullOrWhiteSpace(candidate.numeroSeguridadSocial)) candidate.numeroSeguridadSocial = null;
                        string sexoString = formatter.FormatCellValue(row.GetCell(5))?.Trim();
                        char? sexo = (sexoString != null && sexoString.Length > 0) ? sexoString[0] : null;
                        if (sexo != null)
                        {
                            if (sexo.Value == 'M' || sexo.Value == 'F')
                                candidate.sexo = sexo.Value;
                            else
                                errorList.Add($"Error en la fila {r}: El sexo debe ser M o F.");
                        }
                        if (string.IsNullOrWhiteSpace(candidate.nacionalidad)) candidate.nacionalidad = null;
                        if (candidate.nacionalidad != null)
                        {
                            if (candidate.nacionalidad.Contains(","))
                                candidate.nacionalidad = String.Join(" ", candidate.nacionalidad.Replace(" ", "").Split(",").Reverse());

                            var pais = getPaisByName(candidate.nacionalidad);
                            if (pais == null)
                            {
                                errorList.Add($"Error en la fila {r}: Nacionalidad '{candidate.nacionalidad}' no encontrada.");
                                candidate.nacionalidad = null;
                            }
                            else
                                candidate.nacionalidad = pais.iso3;
                        }
                        if (string.IsNullOrWhiteSpace(candidate.phone)) candidate.phone = null;
                        if (candidate.phone != null) candidate.phone = candidate.phone.Replace(" ", "");
                        if (candidate.phone != null && !HelperMethods.ValidatePhone(candidate.phone))
                        {
                            errorList.Add($"Error en la fila {r}: Teléfono '{candidate.phone}' no válido.");
                            candidate.phone = null;
                        }
                        if (string.IsNullOrWhiteSpace(candidate.email)) candidate.email = null;
                        if (candidate.email != null) candidate.email = candidate.email.Replace(" ", "");
                        if (candidate.email != null && !HelperMethods.ValidateEmail(candidate.email))
                        {
                            errorList.Add($"Error en la fila {r}: Email '{candidate.email}' no válido.");
                            candidate.email = null;
                        }
                        if (string.IsNullOrWhiteSpace(candidate.direccion)) candidate.direccion = null;
                        if (string.IsNullOrWhiteSpace(candidate.cp)) candidate.cp = null;
                        if (string.IsNullOrWhiteSpace(candidate.provincia)) candidate.provincia = null;
                        if (string.IsNullOrWhiteSpace(candidate.localidad)) candidate.localidad = null;
                        //El CP se revisa una vez se ha establecido la conexion
                        if (string.IsNullOrWhiteSpace(candidate.cuentaBancaria)) candidate.cuentaBancaria = null;
                        if (candidate.cuentaBancaria != null) candidate.cuentaBancaria = candidate.cuentaBancaria.Replace(" ", "");
                        if (string.IsNullOrWhiteSpace(candidate.centroAlias)) candidate.centroAlias = null;
                        if (string.IsNullOrWhiteSpace(candidate.workName)) candidate.workName = null;
                        candidates.Add(candidate);
                    }
                    workbook.Close();
                    Directory.Delete(tmpDir, true);
                }
                catch (Exception)
                {
                    return Ok(new { error = "Error 5001, No se ha podido procesar el documento." });
                }

                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();
                using SqlTransaction transaction = conn.BeginTransaction();
                try
                {
                    foreach (CandidateStats candidate in candidates)
                    {
                        candidate.id = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT id FROM candidatos WHERE dni = @DNI";
                            command.Parameters.AddWithValue("@DNI", candidate.dni);
                            using SqlDataReader reader = command.ExecuteReader();
                            if (reader.Read())
                                candidate.id = reader.GetString(reader.GetOrdinal("id"));
                        }

                        if (candidate.id == null)
                        {
                            errorList.Add($"Candidate con DNI '{candidate.dni}' no encontrado.");
                            continue;
                        }

                        //Procesado del CP
                        if (candidate.cp != null)
                        {
                            if (searchCP(candidate.cp, out Provincia provincia, out Localidad localidad))
                            {
                                if (candidate.provincia == null) candidate.provincia = provincia.nombre;
                                if (candidate.localidad == null) candidate.localidad = localidad.nombre;
                            }
                            else
                            {
                                errorList.Add($"Aviso: Código postal '{candidate.cp}' no encontrado.");
                            }
                        }

                        CandidateController.UpdateResult update = CandidateController.updateCandidateData(conn, transaction, candidate);
                        if (update.failed)
                        {
                            result = update.result;
                            throw new Exception();
                        }

                        //Intentar actualizar el puesto de trabajo
                        try
                        {
                            if (!String.IsNullOrWhiteSpace(candidate.centroAlias) && !String.IsNullOrWhiteSpace(candidate.workName))
                            {
                                string centroId = getCentroIdByAlias(candidate.centroAlias, conn, transaction);
                                string categoryId = getCategoryIdByName(candidate.workName, conn, transaction);
                                if (centroId == null) errorList.Add($"Centro de trabajo '{candidate.centroAlias}' no encontrado.");
                                if (categoryId == null) errorList.Add($"Puesto de trabajo '{candidate.workName}' no encontrado.");
                                if (centroId != null && categoryId != null)
                                {
                                    string workId = getWorkIdByCentroIdAndCategoryId(centroId, categoryId, conn, transaction);
                                    if (workId == null)
                                    {
                                        errorList.Add($"El centro de trabajo '{candidate.centroAlias}' no tiene el puesto de trabajo '{candidate.workName}'.");
                                    }
                                    else
                                    {
                                        //Actualizar el centro y el lastSignLink del candidato
                                        using SqlCommand command = conn.CreateCommand();
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText =
                                            "UPDATE candidatos " +
                                            "SET centroId = @CENTRO, lastSignLink = @SIGNLINK " +
                                            "WHERE id = @CANDIDATE";
                                        command.Parameters.AddWithValue("@CENTRO", centroId);
                                        command.Parameters.AddWithValue("@SIGNLINK", workId.Substring(0, 10));
                                        command.Parameters.AddWithValue("@CANDIDATE", candidate.id);
                                        command.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            errorList.Add($"Error interno intentando establecer el trabajo del candidato con DNI '{candidate.dni}'.");
                        }
                    }

                    transaction.Commit();
                    result = new { error = false, errorList };
                }
                catch (Exception)
                {
                    transaction.Rollback();
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-prerregistro-template/")]
        public IActionResult GenerateTemplatePrerregistro()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HelperMethods.HasPermission("XSLX.GenerateTemplatePrerregistro", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            string fileName = "PlantillaVolcadoThinkAndJob.xlsx";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ExcelTemplates", fileName);
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            HttpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(filePath), contentType)
            {
                FileDownloadName = fileName
            };
            return response;
        }

        [HttpPost]
        [Route(template: "upload-prerregistro-template/")]
        public async Task<IActionResult> UploadTemplatePrerregistro()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("XSLX.UploadTemplatePrerregistro", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("xlsx", out JsonElement xlsxJson))
            {
                List<PreCandidateData> candidates = new();
                List<string> errorList = new();
                try
                {
                    string xlsxString = xlsxJson.GetString();
                    byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
                    string tmpDir = HelperMethods.GetTemporaryDirectory();
                    string tmpFile = Path.Combine(tmpDir, "template.xlsx");
                    System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);

                    XSSFWorkbook workbook = new XSSFWorkbook(tmpFile);
                    ISheet sheet = workbook.GetSheetAt(0);
                    DataFormatter formatter = new DataFormatter();

                    if (!HelperMethods.CheckExcelTemplateId(workbook, "preregister-candidates"))
                        return Ok(new { error = "Error 4010, plantilla incorrecta." });

                    //Obtener los precandidatos
                    int r = 1;
                    while (true)
                    {
                        IRow row = sheet.GetRow(r++);
                        if (row == null || row.GetCell(0) == null) break;

                        PreCandidateData candidate = new()
                        {
                            dni = formatter.FormatCellValue(row.GetCell(0))?.Trim(),
                            name = formatter.FormatCellValue(row.GetCell(1))?.Trim(),
                            lastname = ((formatter.FormatCellValue(row.GetCell(2))?.Trim() ?? "") + " " + (formatter.FormatCellValue(row.GetCell(3))?.Trim() ?? "")).Trim(),
                            telf = formatter.FormatCellValue(row.GetCell(4))?.Trim(),
                        };

                        if (string.IsNullOrWhiteSpace(candidate.name))
                        {
                            errorList.Add($"Error en la fila {r}: El nombre es obligatorio.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(candidate.lastname))
                        {
                            errorList.Add($"Error en la fila {r}: Los apellidos son obligatorios.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(candidate.telf))
                        {
                            errorList.Add($"Error en la fila {r}: El teléfono es obligatorio.");
                            continue;
                        }
                        if (candidate.telf != null) candidate.telf = candidate.telf.Replace(" ", "");
                        if (candidate.telf != null && !HelperMethods.ValidatePhone(candidate.telf))
                        {
                            errorList.Add($"Error en la fila {r}: Teléfono '{candidate.telf}' no válido.");
                            continue;
                        }

                        candidates.Add(candidate);
                    }

                    workbook.Close();
                    Directory.Delete(tmpDir, true);
                }
                catch (Exception)
                {
                    return Ok(new { error = "Error 5003, No se ha podido procesar el documento." });
                }

                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (PreCandidateData candidate in candidates)
                            {
                                PreCandidateData preCandidate = candidate;
                                preCandidate.id = HelperMethods.ComputeStringHash(preCandidate.dni + preCandidate.telf + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                                // Comprobar si ya existe un candiato con el mismo DNI
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE dni = @DNI";
                                    command.Parameters.AddWithValue("@DNI", preCandidate.dni);
                                    if ((Int32)command.ExecuteScalar() > 0) continue;
                                }

                                // Comprobar si ya existe un precandidato con el mismo DNI
                                bool exists;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "SELECT COUNT(*) FROM pre_candidates WHERE dni = @DNI";
                                    command.Parameters.AddWithValue("@DNI", preCandidate.dni);
                                    exists = (Int32)command.ExecuteScalar() > 0;
                                }

                                // Crear el precandidato o actualizarlo
                                if (exists)
                                {
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;

                                        command.CommandText =
                                            "UPDATE pre_candidates SET name = @NAME, lastname = @LAST_NAME, telf = @TELF " +
                                            "WHERE dni = @DNI";

                                        command.Parameters.AddWithValue("@ID", preCandidate.id);
                                        command.Parameters.AddWithValue("@NAME", preCandidate.name);
                                        command.Parameters.AddWithValue("@LAST_NAME", preCandidate.lastname);
                                        command.Parameters.AddWithValue("@DNI", preCandidate.dni);
                                        command.Parameters.AddWithValue("@TELF", preCandidate.telf);

                                        command.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;

                                        command.CommandText =
                                            "INSERT INTO pre_candidates (id, name, lastname, dni, telf) VALUES " +
                                            "(@ID, @NAME, @LAST_NAME, @DNI, @TELF)";

                                        command.Parameters.AddWithValue("@ID", preCandidate.id);
                                        command.Parameters.AddWithValue("@NAME", preCandidate.name);
                                        command.Parameters.AddWithValue("@LAST_NAME", preCandidate.lastname);
                                        command.Parameters.AddWithValue("@DNI", preCandidate.dni);
                                        command.Parameters.AddWithValue("@TELF", preCandidate.telf);

                                        command.ExecuteNonQuery();
                                    }

                                    HelperMethods.LogToDB(HelperMethods.LogType.CANDIDATE_PREREGISTER,
                                                "Candidato prerregistrado " + preCandidate.dni,
                                                HelperMethods.FindUsernameBySecurityToken(securityToken, conn,
                                                    transaction), conn, transaction);
                                }
                            }

                            transaction.Commit();
                            result = new { error = false, errorList };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5004, no se han podido actualizar los precandidatos." };
                        }
                    }
                }
            }
            return Ok(result);
        }

        #endregion





        //------------------PrlFormController Fin---------------------
        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------PrlFormController Inicio------------------
        #region "Clases PrlFormController"
        public struct Question
        {
            public string type { get; set; }
            public string question { get; set; }
            public bool answer { get; set; } //Used for truefalse
            public bool candidateAnswer { get; set; } //Used for truefalse (OPCIONAL)
            public List<Answer> answers { get; set; } //Used for select and selectmulti
        }
        public struct Answer
        {
            public string text { get; set; }
            public bool correct { get; set; }
            public bool candidateCorrect { get; set; } // (OPTIONAL)
        }
        #endregion
        #region "Clases XlsxController"
        
        private struct PreCandidateData
        {
            public string id { get; set; }
            public string name { get; set; }
            public string lastname { get; set; }
            public string dni { get; set; }
            public string telf { get; set; }
        }
        #endregion

        //------------------PrlFormController Fin---------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------PrlFormController Inicio------------------
        #region "PrlFormController"
        public static List<Question> parseQuestions(JsonElement json)
        {
            List<Question> questions = new();
            if (json.ValueKind != JsonValueKind.Array) return questions;

            foreach (JsonElement itemJson in json.EnumerateArray())
            {
                if (itemJson.TryGetProperty("type", out JsonElement typeJson) && itemJson.TryGetProperty("question", out JsonElement questionJson))
                {
                    Question question = new Question()
                    {
                        type = typeJson.GetString(),
                        question = questionJson.GetString()
                    };

                    switch (question.type)
                    {
                        case "truefalse":
                            if (itemJson.TryGetProperty("answer", out JsonElement answerJson))
                            {
                                question.answer = answerJson.GetBoolean();
                            }
                            else continue;
                            if (itemJson.TryGetProperty("candidateAnswer", out JsonElement candidateAnswerJson))
                                question.candidateAnswer = candidateAnswerJson.GetBoolean();
                            break;
                        case "select":
                        case "selectmulti":
                            if (itemJson.TryGetProperty("answers", out JsonElement answersJson) && answersJson.ValueKind == JsonValueKind.Array)
                            {
                                question.answers = new();
                                foreach (JsonElement answareJson in answersJson.EnumerateArray())
                                {
                                    if (answareJson.TryGetProperty("text", out JsonElement textJson) && answareJson.TryGetProperty("correct", out JsonElement correctJson))
                                    {
                                        Answer answare = new Answer()
                                        {
                                            text = textJson.GetString(),
                                            correct = correctJson.GetBoolean()
                                        };
                                        if (itemJson.TryGetProperty("candidateCorrect", out JsonElement candidateCorrectJson))
                                            answare.candidateCorrect = candidateCorrectJson.GetBoolean();
                                        question.answers.Add(answare);
                                    }
                                }
                            }
                            else continue;
                            break;
                        default:
                            continue;
                    }

                    questions.Add(question);
                }
            }

            return questions;
        }
        public static List<Question> parseQuestions(string jsonString)
        {
            return parseQuestions(JsonDocument.Parse(jsonString).RootElement);
        }
        #endregion

        #region "XlsxController"
        private static string getCentroIdByAlias(string alias, SqlConnection conn, SqlTransaction transaction)
        {
            if (alias == null) return null;
            string centroId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "SELECT id FROM centros WHERE (alias COLLATE Latin1_General_CI_AI) = (@ALIAS COLLATE Latin1_General_CI_AI)";
                command.Parameters.AddWithValue("@ALIAS", alias);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        centroId = reader.GetString(reader.GetOrdinal("id"));
            }
            return centroId;
        }
        private static string getCategoryIdByName(string name, SqlConnection conn, SqlTransaction transaction)
        {
            if (name == null) return null;
            string centroId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "SELECT id FROM categories WHERE (name COLLATE Latin1_General_CI_AI) = (@NAME COLLATE Latin1_General_CI_AI)";
                command.Parameters.AddWithValue("@NAME", name);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        centroId = reader.GetString(reader.GetOrdinal("id"));
            }
            return centroId;
        }
        private static string getWorkIdByCentroIdAndCategoryId(string centroId, string categoryId, SqlConnection conn, SqlTransaction transaction)
        {
            if (centroId == null || categoryId == null) return null;
            string workId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "SELECT id FROM trabajos WHERE categoryId = @CATEGORY AND centroId = @CENTRO";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@CATEGORY", categoryId);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        workId = reader.GetString(reader.GetOrdinal("id"));
            }
            return workId;
        }
        #endregion
        //------------------PrlFormController Fin---------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
