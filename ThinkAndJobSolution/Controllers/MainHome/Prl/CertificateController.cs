using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;
using System.IO.Compression;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/certificate")]
    [ApiController]
    [Authorize]
    public class CertificateController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------


        [HttpPost]
        [Route(template: "upload/{submitId}")]
        public async Task<IActionResult> Upload( string submitId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Certificate.Upload", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body);
                string data = await bodyReader.ReadToEndAsync();
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    /**
                     * Get the target candidateId, formId from the submitId
                     */
                    bool failed = false;
                    string formId = null;
                    string candidateId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT candidatoId, formularioId FROM emision_cuestionarios WHERE id LIKE @SUBMIT_ID";
                        command.Parameters.AddWithValue("@SUBMIT_ID", submitId);

                        try
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    candidateId = reader.GetString(reader.GetOrdinal("candidatoId"));
                                    formId = reader.GetString(reader.GetOrdinal("formularioId"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 3194, la solicitud seleccionada no está registrada."
                                    };
                                }
                            }
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5193, error en la solicitud."
                            };
                        }

                    }
                    if (!failed)
                    {
                        HelperMethods.SaveFile(new[] { "questionnaire_submissions", submitId, "certificado" }, "data:application/pdf;base64," + data);
                        HelperMethods.LogToDB(HelperMethods.LogType.CANDIDATE_CERTIFIED, "Certificado agregado a " + HelperMethods.FindNameBySubmitId(submitId, conn), HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                    }

                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE emision_cuestionarios SET certificado = 1 WHERE id LIKE @SUBMIT_ID";
                            command.Parameters.AddWithValue("@SUBMIT_ID", submitId);

                            try
                            {
                                command.ExecuteNonQuery();
                                result = new { error = false };
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5193, error al actualizar el estado."
                                };
                            }

                        }
                    }
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "download/{submitId}")]
        public IActionResult Download( string submitId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Certificate.Download", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                result = new { data = HelperMethods.ReadFile(new[] { "questionnaire_submissions", submitId, "certificado" }) };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "download-by-dni/")]
        public async Task<IActionResult> DownloadCertificatesByDni()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            try
            {
                // 1º Recogemos el excel que se nos envía en la request
                using StreamReader readerBody = new(Request.Body, Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                json.TryGetProperty("excel", out JsonElement excelJson);
                string excelString = excelJson.GetString();

                // 2º Creamos una carpeta temporal para guardar el excel y almacenamos su ruta.
                string tempDir = HelperMethods.GetTemporaryDirectory();
                string ext = HelperMethods.getExtFromBase64(excelString)[0];
                string tempFile = tempDir + "/temp." + ext;

                // 3º Guardamos el excel en la carpeta temporal sin usar HelperMethods.SaveFile
                System.IO.File.WriteAllBytes(tempFile, Convert.FromBase64String(excelString.Split(",")[1]));

                // 4º Abrimos el excel y recorremos las filas. Todos aquellos DNIs válidos los guardamos en una lista.
                // Al acabar, borramos el excel de la carpeta temporal.
                List<string> validDnis = new();
                switch (ext)
                {
                    case "xlsx":
                        XSSFWorkbook workbook1 = new(tempFile);
                        ISheet sheet = workbook1.GetSheetAt(0);
                        DataFormatter formatter = new();
                        for (int i = 1; i <= sheet.LastRowNum; i++)
                        {
                            IRow row = sheet.GetRow(i);
                            if (row != null)
                            {
                                ICell cell = row.GetCell(0);
                                if (cell != null)
                                {
                                    string dni = formatter.FormatCellValue(cell).Trim();
                                    if (HelperMethods.validDNI(dni) || HelperMethods.validNIE(dni))
                                    {
                                        validDnis.Add(dni);
                                    }
                                    else
                                    {
                                        Console.WriteLine("DNI no válido: " + dni);
                                    }
                                }
                            }
                        }
                        workbook1.Close();
                        System.IO.File.Delete(tempFile);
                        break;
                    case "xls":
                        HSSFWorkbook workbook2;
                        using (FileStream file = new(tempFile, FileMode.Open, FileAccess.Read))
                            workbook2 = new HSSFWorkbook(file);
                        ISheet sheet2 = workbook2.GetSheetAt(0);
                        DataFormatter formatter2 = new();
                        for (int i = 1; i <= sheet2.LastRowNum; i++)
                        {
                            IRow row = sheet2.GetRow(i);
                            if (row != null)
                            {
                                ICell cell = row.GetCell(0);
                                if (cell != null)
                                {
                                    string dni = formatter2.FormatCellValue(cell).Trim();
                                    if (HelperMethods.validDNI(dni) || HelperMethods.validNIE(dni))
                                    {
                                        validDnis.Add(dni);
                                    }
                                }
                            }
                        }
                        break;
                    case "csv":
                        string[] lines = System.IO.File.ReadAllLines(tempFile);
                        for (int i = 1; i < lines.Length; i++)
                        {
                            string[] values = lines[i].Split(';');
                            string dni = values[0].Trim();
                            if (HelperMethods.validDNI(dni) || HelperMethods.validNIE(dni))
                            {
                                validDnis.Add(dni);
                            }
                        }
                        System.IO.File.Delete(tempFile);
                        break;
                }

                // 5º Obtenemos un hash que relaciona los dnis con los submitIds.
                Dictionary<string, string[]> dnisToSubmitIds = new();
                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT EC.id as submitId, CA.dni, CA.nombre, CA.apellidos" +
                    " FROM emision_cuestionarios EC INNER JOIN candidatos CA" +
                    " ON(EC.candidatoId = CA.id) WHERE EC.certificado = 1";
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string dni = reader.GetString(reader.GetOrdinal("dni"));
                    string submitId = reader.GetString(reader.GetOrdinal("submitId"));
                    string name = reader.GetString(reader.GetOrdinal("apellidos")) + " " + reader.GetString(reader.GetOrdinal("nombre"));
                    dnisToSubmitIds[dni] = new string[] { submitId, name };
                }

                // 6º Por cada dni de "validDnis", si existe en el hash, copiamos el .pdf a la carpeta temporal.
                // También guardamos en una lista los DNIs que no tienen certificado.
                List<string> dnisWithoutCertificate = new();
                foreach (string dni in validDnis)
                {
                    if (dnisToSubmitIds.ContainsKey(dni))
                    {
                        string submitId = dnisToSubmitIds[dni][0];
                        string name = dnisToSubmitIds[dni][1];
                        string path = HelperMethods.ComposePath(new[] { "questionnaire_submissions", submitId, "certificado.pdf" });
                        byte[] pdf = System.IO.File.ReadAllBytes(path);
                        string pdfPath = $"{tempDir}/{dni}_{name.Replace(' ', '_')}_certPRL.pdf";
                        System.IO.File.WriteAllBytes(pdfPath, pdf);
                    }
                    else
                    {
                        dnisWithoutCertificate.Add(dni);
                    }
                }

                // 7º Comprimimos la carpeta temporal en un .zip y lo devolvemos.
                System.IO.File.Delete(tempFile);
                string zipMIMETYPE = "";
                string zipPath = HelperMethods.GetTemporaryDirectory() + "/certificadosPRL.zip";
                ZipFile.CreateFromDirectory(tempDir, zipPath);
                byte[] zipFile = System.IO.File.ReadAllBytes(zipPath);
                zipMIMETYPE = HelperMethods.getBase64Header(zipPath);
                System.IO.File.Delete(zipPath);
                Directory.Delete(tempDir, true);

                // 8º Si dnisWithoutCertificate.count > 0, devolvemos un excel listando los DNIs sin certificado.
                byte[] excelFile = null;
                string excelMIMETYPE = "";
                if (dnisWithoutCertificate.Count > 0)
                {
                    string excelPath = null;
                    XSSFWorkbook excel = new();
                    ISheet excelSheet = excel.CreateSheet("DNIs sin certificado");
                    IRow header = excelSheet.CreateRow(0);
                    header.CreateCell(0).SetCellValue("DNI");
                    for (int i = 0; i < dnisWithoutCertificate.Count; i++)
                    {
                        IRow row = excelSheet.CreateRow(i + 1);
                        row.CreateCell(0).SetCellValue(dnisWithoutCertificate[i]);
                    }
                    excelPath = HelperMethods.GetTemporaryDirectory() + "/DNIs_sin_certificado.xlsx";
                    using FileStream file = new(excelPath, FileMode.Create, FileAccess.Write);
                    excel.Write(file);
                    excel.Close();
                    excelFile = System.IO.File.ReadAllBytes(excelPath);
                    excelMIMETYPE = HelperMethods.getBase64Header(excelPath);
                    System.IO.File.Delete(excelPath);
                }

                result = new
                {
                    error = false,
                    zip = zipMIMETYPE + Convert.ToBase64String(zipFile),
                    excel = excelFile != null ? excelMIMETYPE + Convert.ToBase64String(excelFile) : null
                };
            }
            catch (Exception)
            {
                result = new { error = "No se pudo procesar correctamente el excel." };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "revoke/{submitId}")]
        public IActionResult Revoke( string submitId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Certificate.Revoke", securityToken).Acceso)
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

                    bool failed = false;
                    string submitName = HelperMethods.FindNameBySubmitId(submitId, conn);
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE emision_cuestionarios SET certificado = 0 WHERE id LIKE @SUBMIT_ID";
                        command.Parameters.AddWithValue("@SUBMIT_ID", submitId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5194, no se ha podido eliminar el certificado."
                            };
                        }
                    }
                    if (!failed)
                    {
                        HelperMethods.DeleteFile(new[] { "questionnaire_submissions", submitId, "certificado" });
                        HelperMethods.LogToDB(HelperMethods.LogType.CANDIDATE_CERTIFICATE_REVOKE, "Certificado revocado " + submitName, HelperMethods.FindUsernameBySecurityToken(securityToken), conn);
                        result = new { error = false };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "revoke-test/{submitId}")]
        public IActionResult RevokeTest(string submitId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Certificate.RevokeTest", securityToken).Acceso)
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
                    bool failed = false;
                    string submitName = HelperMethods.FindNameBySubmitId(submitId, conn);
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM emision_cuestionarios WHERE id LIKE @SUBMIT_ID";
                        command.Parameters.AddWithValue("@SUBMIT_ID", submitId);
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5193, no se ha podido eliminar la entrega."
                            };
                        }
                    }
                    if (!failed)
                    {
                        HelperMethods.DeleteDir(new[] { "questionnaire_submissions", submitId });
                        HelperMethods.LogToDB(HelperMethods.LogType.CANDIDATE_CERTIFICATE_REVOKE, "Emisión revocada " + submitName, HelperMethods.FindUsernameBySecurityToken(securityToken), conn);
                        result = new { error = false };
                    }
                }
            }
            return Ok(result);
        }

        //Para candidato
        [HttpGet]
        [Route(template: "list/{signLink}")]
        public IActionResult GetCertificates(string signLink)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                bool existsCandidate = false;

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT id FROM candidatos WHERE id LIKE @CANDIDATE_ID";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                    using SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        existsCandidate = true;
                    }
                    else
                    {
                        result = new
                        {
                            error = "Error 3934, el candidato no esta registrado."
                        };
                    }
                }

                if (existsCandidate)
                {
                    using SqlCommand command = conn.CreateCommand();

                    command.CommandText =
                        "SELECT EC.id, F.nombre as nombreFormulario, CE.alias as aliasCentro " +
                        "FROM formularios F " +
                        "INNER JOIN emision_cuestionarios EC ON F.id = EC.formularioId " +
                        "INNER JOIN trabajos T ON T.id = EC.trabajoId " +
                        "INNER JOIN centros CE ON T.centroId = CE.id " +
                        "WHERE EC.candidatoId = @CANDIDATE_ID AND EC.certificado = 1";
                    command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                    List<object> certificates = new();

                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        certificates.Add(new
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            formulario = reader.GetString(reader.GetOrdinal("nombreFormulario")),
                            centro = reader.GetString(reader.GetOrdinal("aliasCentro"))
                        });
                    }
                    result = new { error = false, certificates };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "download-for-candidate/{submitId}")]
        public IActionResult DownloadForCandidate(string submitId)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                /**
                 * Get the target candidateId, formId from the submitId
                 */
                bool failed = false;
                string formId = null;

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT candidatoId, formularioId FROM emision_cuestionarios WHERE id LIKE @SUBMIT_ID";
                    command.Parameters.AddWithValue("@SUBMIT_ID", submitId);

                    try
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                candidateId = reader.GetString(reader.GetOrdinal("candidatoId"));
                                formId = reader.GetString(reader.GetOrdinal("formularioId"));
                            }
                            else
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 3194, la solicitud seleccionada no está registrada."
                                };
                            }
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 3193, error en la solicitud."
                        };
                    }

                }

                if (!failed)
                {
                    result = new { data = HelperMethods.ReadFile(new[] { "questionnaire_submissions", submitId, "certificado" }) };
                }
            }
            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
