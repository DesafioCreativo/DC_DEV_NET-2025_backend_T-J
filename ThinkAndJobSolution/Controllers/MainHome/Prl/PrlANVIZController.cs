using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Helper.AnvizTools;
using ThinkAndJobSolution.Utils;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/ANVIZ")]
    [ApiController]
    [Authorize]
    public class PrlANVIZController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpGet]
        [Route(template: "download-candidates-cardid-template/{workCenterId}")]
        public IActionResult GenerateTemplateCandidateCardId(string workCenterId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HelperMethods.HasPermission("XSLX.GenerateTemplateUploadCandidateCardId", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            // Load the data from "candidatos" table (nombre, apellidos, dni).
            List<Candidate> candidates = new();
            using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
            conn.Open();
            using SqlCommand command = new("SELECT dni, nombre, apellidos FROM candidatos WHERE centroId = @CENTROID ORDER BY apellidos ASC, nombre ASC, dni ASC", conn);
            command.Parameters.AddWithValue("@CENTROID", workCenterId);
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                Candidate candidate = new()
                {
                    dni = reader.GetString(reader.GetOrdinal("dni")),
                    name = reader.GetString(reader.GetOrdinal("nombre")),
                    lastname = reader.GetString(reader.GetOrdinal("apellidos"))
                };
                candidates.Add(candidate);
            }
            // Load the workbook. After loading the workbook, write all the candidates to the workbook.
            // The workbook already has a sheet called "NºIdentificación" where the candidates will be written.
            // The workbook already has the headers, so we start from the second row.
            // Headers: "{apellidos}, {nombre}", "{dni}"
            string fileName = "PlantillaVolcadoCandidateCardId.xlsx";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ExcelTemplates", fileName);
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            XSSFWorkbook workbook = new();
            using FileStream file = new(filePath, FileMode.Open, FileAccess.Read);
            workbook = new XSSFWorkbook(file);
            ISheet sheet = workbook.GetSheetAt(0);
            int r = 1;
            foreach (Candidate candidate in candidates)
            {
                IRow row = sheet.CreateRow(r++);
                row.CreateCell(0).SetCellValue(candidate.lastname.ToUpper() + ", " + candidate.name.ToUpper());
                row.CreateCell(1).SetCellValue(candidate.dni.ToUpper());
            }

            // Save the workbook to a temporary file and return it to the user.
            string tmpDir = HelperMethods.GetTemporaryDirectory();
            string tmpFile = Path.Combine(tmpDir, "template.xlsx");
            using FileStream file2 = new(tmpFile, FileMode.Create, FileAccess.Write);
            workbook.Write(file2);
            workbook.Close();
            file.Close();
            file2.Close();

            HttpContext.Response.ContentType = contentType;
            FileContentResult response = new(System.IO.File.ReadAllBytes(tmpFile), contentType)
            {
                FileDownloadName = fileName
            };

            return response;
        }


        [HttpPost]
        [Route(template: "upload-candidates-cardid-template/{securityToken}")]
        public async Task<IActionResult> UploadCandidateCardIdExcel()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("XSLX.UploadTemplateUploadCandidateCardId", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            using StreamReader readerBody = new(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("xlsx", out JsonElement xlsxJson))
            {
                //Diccionario de dni, cardId
                Dictionary<string, string> candidatesIds = new();
                List<string> errorList = new();
                try
                {
                    string xlsxString = xlsxJson.GetString();
                    byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
                    string tmpDir = HelperMethods.GetTemporaryDirectory();
                    string tmpFile = Path.Combine(tmpDir, "template.xlsx");
                    System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);
                    XSSFWorkbook workbook = new(tmpFile);
                    ISheet sheet = workbook.GetSheetAt(0);
                    DataFormatter formatter = new();
                    //Obtener los candidatos
                    int r = 1;
                    // number of rows
                    int rows = sheet.LastRowNum;
                    while (r < rows)
                    {
                        IRow row = sheet.GetRow(r++);
                        if (row != null && row.GetCell(1) != null && row.GetCell(2) != null)
                        {
                            candidatesIds.Add(formatter.FormatCellValue(row.GetCell(1))?.Trim().ToUpper(), formatter.FormatCellValue(row.GetCell(2))?.Trim().ToUpper());
                        }
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
                    // DNI, cardId
                    foreach (KeyValuePair<string, string> candidate in candidatesIds)
                    {
                        // Comprobamos si ya existe algún candidato con esa tarjeta
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "SELECT id, dni FROM candidatos WHERE cardId = @CARDID";
                        command.Parameters.AddWithValue("@CARDID", candidate.Value);
                        using SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            // Obtenemos el id y dni del candidato que la tiene
                            string cId = reader.GetString(reader.GetOrdinal("id"));
                            string cDni = reader.GetString(reader.GetOrdinal("dni"));
                            reader.Close();

                            // Si existe: 1º se la quitamos al candidato que la tiene
                            using SqlCommand command2 = conn.CreateCommand();
                            command2.Connection = conn;
                            command2.Transaction = transaction;
                            command2.CommandText = "UPDATE candidatos SET cardId = NULL WHERE id = @ID";
                            command2.Parameters.AddWithValue("@ID", cId);
                            command2.ExecuteNonQuery();

                            // 2º eliminiamos al candidato del dispositivo en el que se encuentre
                            // Si el centro al que pertenece este candidato tiene asociado un dispositivo anviz,
                            // lo eliminamos del dispositivo
                            using SqlCommand command3 = conn.CreateCommand();
                            command3.Connection = conn;
                            command3.Transaction = transaction;
                            command3.CommandText = "SELECT RDC.deviceId FROM rf_devices_centros RDC INNER JOIN candidatos C ON(RDC.centroId = C.centroId) WHERE C.id = @ID";
                            command3.Parameters.AddWithValue("@ID", cId);
                            using SqlDataReader reader2 = command3.ExecuteReader();
                            List<string> deviceIds = new();
                            while (reader2.Read())
                                deviceIds.Add(reader2.GetString(reader2.GetOrdinal("deviceId")));
                            reader2.Close();
                            if (deviceIds.Count > 0)
                            {
                                foreach (string deviceId in deviceIds)
                                {
                                    string idd = HelperMethods.Dni2idd(cDni);
                                    AnvizTools.InsertRemoveTask(deviceId, idd);
                                }
                            }
                        }
                        reader.Close();
                        // 3º se la asignamos al nuevo candidato
                        using SqlCommand command4 = conn.CreateCommand();
                        command4.Connection = conn;
                        command4.Transaction = transaction;
                        command4.CommandText = "UPDATE candidatos SET cardId = @CARDID WHERE dni = @DNI";
                        command4.Parameters.AddWithValue("@DNI", candidate.Key);
                        command4.Parameters.AddWithValue("@CARDID", candidate.Value);
                        command4.ExecuteNonQuery();

                        // 4º lo añadimos al dispositivo en el que está (si está en alguno)
                        string cId2 = null; // id del candidato
                        using SqlCommand command5 = conn.CreateCommand();
                        command5.Connection = conn;
                        command5.Transaction = transaction;
                        command5.CommandText = "SELECT id FROM candidatos WHERE dni = @DNI";
                        command5.Parameters.AddWithValue("@DNI", candidate.Key);
                        using SqlDataReader reader3 = command5.ExecuteReader();
                        if (reader3.Read())
                            cId2 = reader3.GetString(reader3.GetOrdinal("id"));
                        reader3.Close();

                        if (cId2 == null)
                        {
                            return Ok(new { error = "Error 5002, El candidato con DNI " + candidate.Key + " no existe." });
                        }
                        using SqlCommand command6 = conn.CreateCommand();
                        command6.Connection = conn;
                        command6.Transaction = transaction;
                        command6.CommandText = "SELECT RDC.deviceId FROM rf_devices_centros RDC INNER JOIN candidatos C ON(RDC.centroId = C.centroId) WHERE C.id = @ID";
                        command6.Parameters.AddWithValue("@ID", cId2);
                        using SqlDataReader reader4 = command6.ExecuteReader();
                        List<string> deviceIds2 = new();
                        while (reader4.Read())
                            deviceIds2.Add(reader4.GetString(reader4.GetOrdinal("deviceId")));
                        reader4.Close();

                        if (deviceIds2.Count > 0)
                        {
                            foreach (string deviceId in deviceIds2)
                            {
                                string idd = HelperMethods.Dni2idd(candidate.Key);
                                AnvizTools.InsertRegisterTask(deviceId, new AnvizTools.DeviceUser()
                                {
                                    rjId = cId2,
                                    dni = candidate.Key,
                                    idd = idd,
                                    name = candidate.Key,
                                    cardid = candidate.Value,
                                    pass = candidate.Value.ToString()[^4..],
                                    identity_type = AnvizTools.GetAnvizIdentityType(deviceId)
                                });
                            }
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

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
