using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.Client
{
    [Route("api/v1/cl-register")]
    [ApiController]
    [Authorize]
    public class ClientRegisterController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpPost]
        [Route(template: "register/")]
        public async Task<IActionResult> Register()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, null, null, null, out MainHome.Comercial.ClientUserController.ClientUserAccess access) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("text", out string text))
            {

                using (SqlConnection conn = new SqlConnection(InstallationConstants.LOGS_CL_CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO logs (userId, text) VALUES (@USER, @TEXT)";
                            command.Parameters.AddWithValue("@USER", access.id);
                            command.Parameters.AddWithValue("@TEXT", text);
                            command.ExecuteNonQuery();
                        }

                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5234, no se ha podido insertar el registro." };
                    }
                }
            }

            return Ok(result);
        }

        // Obtencion
        [HttpPost]
        [Route("api/v1/cl-register/list-for-client/{userId}/")]
        public async Task<IActionResult> ListForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS) == null)
            {
                return Ok(new{error = "Error 1002, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetDateTime("day", out DateTime day))
            {
                using (SqlConnection conn = new SqlConnection(InstallationConstants.LOGS_CL_CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        result = new
                        {
                            error = false,
                            logs = listLogs(userId, day, day, conn)
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5234, no se ha podido listar los registros" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("api/v1/cl-register/list/{userId}/{securityToken}")]
        public async Task<IActionResult> List(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("CLRegister.List", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetDateTime("day", out DateTime day))
            {
                using (SqlConnection conn = new SqlConnection(InstallationConstants.LOGS_CL_CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        result = new
                        {
                            error = false,
                            logs = listLogs(userId, day, day, conn)
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5234, no se ha podido listar los registros" };
                    }
                }
            }
            return Ok(result);
        }

        // Reportes
        [HttpGet]
        [Route("api/v1/cl-register/report-for-client/{userId}/{year}/{month}/")]
        public IActionResult GenerateReportForClient(string userId, int? year, int? month)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS) == null)
            {
                return new ForbidResult();
            }
            return report(userId, year, month);
        }

        [HttpGet]
        [Route("api/v1/cl-register/report/{userId}/{year}/{month}/")]
        public IActionResult GenerateReport(string userId, int? year, int? month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            ResultadoAcceso access = HasPermission("CLRegister.GenerateReport", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }
            return report(userId, year, month);
        }



        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        // Ayuda
        public struct CLLog
        {
            public DateTime date { get; set; }
            public string text { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static List<CLLog> listLogs(string userId, DateTime? start, DateTime? end, SqlConnection conn, SqlTransaction transaction = null)
        {
            if (end.HasValue)
                end = end.Value.AddDays(1).AddSeconds(-1);

            List<CLLog> logs = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT * FROM logs WHERE " +
                    "(@USER IS NULL OR userId = @USER) AND " +
                    "(@START IS NULL OR date >= @START) AND " +
                    "(@END IS NULL OR date <= @END) " +
                    "ORDER BY date DESC";
                command.Parameters.AddWithValue("@USER", (object)userId ?? DBNull.Value);
                command.Parameters.AddWithValue("@START", start == null ? DBNull.Value : start.Value.Date);
                command.Parameters.AddWithValue("@END", end == null ? DBNull.Value : end.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(new CLLog()
                        {
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            text = reader.GetString(reader.GetOrdinal("text"))
                        });
                    }
                }
            }
            return logs;
        }
        private IActionResult report(string userId, int? year, int? month)
        {
            try
            {
                string username = FindUserClientUsernameByClientId(userId);
                Dictionary<int, Dictionary<int, Dictionary<int, List<CLLog>>>> dYears = new();
                using (SqlConnection conn = new SqlConnection(InstallationConstants.LOGS_CL_CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT * FROM logs WHERE " +
                            "userId = @USER AND " +
                            "(@YEAR IS NULL OR YEAR(date) = @YEAR) AND " +
                            "(@MONTH IS NULL OR MONTH(date) = @MONTH) " +
                            "ORDER BY date ASC";
                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@YEAR", year == null ? DBNull.Value : year.Value);
                        command.Parameters.AddWithValue("@MONTH", month == null ? DBNull.Value : month.Value);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                CLLog log = new()
                                {
                                    date = reader.GetDateTime(reader.GetOrdinal("date")),
                                    text = reader.GetString(reader.GetOrdinal("text"))
                                };
                                int cDay = log.date.Day, cMonth = log.date.Month, cYear = log.date.Year;
                                if (!dYears.ContainsKey(cYear)) dYears[cYear] = new();
                                if (!dYears[cYear].ContainsKey(cMonth)) dYears[cYear][cMonth] = new();
                                if (!dYears[cYear][cMonth].ContainsKey(cDay)) dYears[cYear][cMonth][cDay] = new();
                                dYears[cYear][cMonth][cDay].Add(log);
                            }
                        }
                    }
                }

                string tmpDir = GetTemporaryDirectory();
                foreach (var dMonths in dYears)
                {
                    string dirYear = Path.Combine(tmpDir, dMonths.Key.ToString());
                    Directory.CreateDirectory(dirYear);
                    foreach (var dDays in dMonths.Value)
                    {
                        string dirMonth = Path.Combine(dirYear, $"{dDays.Key.ToString("00")} {MESES[dDays.Key - 1]}");
                        Directory.CreateDirectory(dirMonth);

                        //Calcular fechas
                        var dateFirstDayOfMonth = new DateTime(dMonths.Key, dDays.Key, 1);
                        int firstDayOfMonth = dateFirstDayOfMonth.Day;
                        int lastDayOfMonth = dateFirstDayOfMonth.AddMonths(1).AddDays(-1).Day;

                        IWorkbook workbook = new XSSFWorkbook();
                        ISheet sheet = workbook.CreateSheet("Registro de acciones");

                        //Fuentes
                        IFont fontTitle = workbook.CreateFont();
                        fontTitle.FontName = "Calibri";
                        fontTitle.IsBold = true;
                        fontTitle.FontHeightInPoints = 14;

                        //Estilos
                        ICellStyle styleHeader = workbook.CreateCellStyle();
                        styleHeader.FillForegroundColor = HSSFColor.LightGreen.Index;
                        styleHeader.FillPattern = FillPattern.SolidForeground;
                        styleHeader.BorderTop = BorderStyle.Thin;
                        styleHeader.BorderBottom = BorderStyle.Thin;
                        styleHeader.BorderLeft = BorderStyle.Thin;
                        styleHeader.BorderRight = BorderStyle.Thin;
                        styleHeader.SetFont(fontTitle);
                        ICellStyle styleHeaderData = workbook.CreateCellStyle();
                        styleHeaderData.BorderTop = BorderStyle.Thin;
                        styleHeaderData.BorderBottom = BorderStyle.Thin;
                        styleHeaderData.BorderLeft = BorderStyle.Thin;
                        styleHeaderData.BorderRight = BorderStyle.Thin;
                        ICellStyle styleActionHeader = workbook.CreateCellStyle();
                        styleActionHeader.FillForegroundColor = HSSFColor.LightGreen.Index;
                        styleActionHeader.FillPattern = FillPattern.SolidForeground;
                        styleActionHeader.BorderTop = BorderStyle.Thin;
                        styleActionHeader.BorderBottom = BorderStyle.Thin;
                        styleActionHeader.BorderLeft = BorderStyle.Thin;
                        styleActionHeader.BorderRight = BorderStyle.Thin;
                        styleActionHeader.SetFont(fontTitle);
                        ICellStyle styleActionData = workbook.CreateCellStyle();
                        styleActionData.BorderTop = BorderStyle.Thin;
                        styleActionData.BorderBottom = BorderStyle.Thin;
                        styleActionData.BorderLeft = BorderStyle.Thin;
                        styleActionData.BorderRight = BorderStyle.Thin;

                        //Tamaños de filas y columnas
                        ICell cell;
                        IRow row;
                        sheet.SetColumnWidth(1, 20 * 256);
                        sheet.SetColumnWidth(2, 80 * 256);

                        //Cabecera
                        row = sheet.CreateRow(1);
                        cell = row.CreateCell(1);
                        cell.CellStyle = styleHeader;
                        cell.SetCellValue("USUARIO");
                        row = sheet.CreateRow(2);
                        cell = row.CreateCell(1);
                        cell.CellStyle = styleHeaderData;
                        cell.SetCellValue(username);

                        //Cabecera de datos
                        row = sheet.CreateRow(5);
                        cell = row.CreateCell(1);
                        cell.CellStyle = styleActionHeader;
                        cell.SetCellValue("Fecha y hora");
                        cell = row.CreateCell(2);
                        cell.CellStyle = styleActionHeader;
                        cell.SetCellValue("Acción");

                        int nRow = 6;
                        foreach (var logs in dDays.Value.OrderBy(vk => vk.Key))
                        {
                            foreach (CLLog log in logs.Value.OrderBy(r => r.date))
                            {
                                row = sheet.CreateRow(nRow++);
                                cell = row.CreateCell(1);
                                cell.CellStyle = styleActionData;
                                cell.SetCellValue(log.date.ToString("dd/MM/yyyy HH:mm:ss"));
                                cell = row.CreateCell(2);
                                cell.CellStyle = styleActionData;
                                cell.SetCellValue(log.text);
                            }
                        }

                        FileStream file = new FileStream(Path.Combine(dirMonth, username + " " + dMonths.Key + " " + dDays.Key + "- " + MESES[dDays.Key - 1] + ".xlsx"), FileMode.Create);
                        workbook.Write(file);
                        file.Close();
                    }
                }

                string tmpZipDir = GetTemporaryDirectory();
                string fileName = "acciones " + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + ".zip";
                string tmpOutZip = Path.Combine(tmpZipDir, fileName);
                ZipFile.CreateFromDirectory(tmpDir, tmpOutZip);

                string contentType = "application/zip";
                HttpContext.Response.ContentType = contentType;
                var response = new FileContentResult(System.IO.File.ReadAllBytes(tmpOutZip), contentType)
                {
                    FileDownloadName = fileName
                };

                Directory.Delete(tmpDir, true);
                Directory.Delete(tmpZipDir, true);

                return response;
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
