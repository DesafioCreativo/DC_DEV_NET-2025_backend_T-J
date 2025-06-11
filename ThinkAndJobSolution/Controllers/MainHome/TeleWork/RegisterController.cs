using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using System.Text.Json;
using ThinkAndJobSolution.Utils;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;

namespace ThinkAndJobSolution.Controllers.MainHome.TeleWork
{
    [Route("api/v1/tw-register")]
    [ApiController]
    [Authorize]
    public class RegisterController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpPost]
        [Route("list/{userId}/")]
        public async Task<IActionResult> List(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("TeleworkRegister.List", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("start", out JsonElement startJson) && json.TryGetProperty("end", out JsonElement endJson))
            {
                DateTime? start = GetJsonDate(startJson), end = GetJsonDate(endJson);
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    try
                    {
                        string cUserId = FindUserIdBySecurityToken(securityToken, conn);
                        if (!access.EsJefe && userId != cUserId)
                        {
                            return Ok(new
                            {
                                error = "Error 1001, No se disponen de los privilegios suficientes."
                            });
                        }
                        result = new
                        {
                            error = false,
                            logs = listLogs(userId, start, end, conn)
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5434, no se ha podido listar los registros" };
                    }
                }
            }
            return Ok(result);
        }


        [HttpGet]
        [Route("report/{userId}/{year}/{month}/")]
        public IActionResult GenerateReport(string userId, int? year, int? month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            ResultadoAcceso access = HasPermission("TeleworkRegister.GenerateReport", securityToken);
            if (!access.Acceso)
            {
                return new ForbidResult();
            }
            try
            {
                string nombre = null, dni = null;
                Dictionary<int, Dictionary<int, Dictionary<int, List<TWLog>>>> dYears = new();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string cUserId = FindUserIdBySecurityToken(securityToken, conn);
                    if (!access.EsJefe && userId != cUserId)
                    {
                        return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT * FROM telework_register WHERE " +
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
                                TWLog shift = new()
                                {
                                    date = reader.GetDateTime(reader.GetOrdinal("date")),
                                    action = reader.GetString(reader.GetOrdinal("action"))
                                };
                                int cDay = shift.date.Day, cMonth = shift.date.Month, cYear = shift.date.Year;
                                if (!dYears.ContainsKey(cYear)) dYears[cYear] = new();
                                if (!dYears[cYear].ContainsKey(cMonth)) dYears[cYear][cMonth] = new();
                                if (!dYears[cYear][cMonth].ContainsKey(cDay)) dYears[cYear][cMonth][cDay] = new();
                                dYears[cYear][cMonth][cDay].Add(shift);
                            }
                        }
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT TRIM(CONCAT(name, ' ', surname)) as [fullName], DocID FROM users WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                nombre = reader.GetString(reader.GetOrdinal("fullName"));
                                dni = reader.GetString(reader.GetOrdinal("DocID"));
                            }
                        }
                    }
                    if (nombre == null) new NoContentResult();
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
                        cell.SetCellValue("DNI");
                        cell = row.CreateCell(2);
                        cell.CellStyle = styleHeader;
                        cell.SetCellValue("Nombre");
                        row = sheet.CreateRow(2);
                        cell = row.CreateCell(1);
                        cell.CellStyle = styleHeaderData;
                        cell.SetCellValue(dni);
                        cell = row.CreateCell(2);
                        cell.CellStyle = styleHeaderData;
                        cell.SetCellValue(nombre);

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
                            foreach (TWLog log in logs.Value.OrderBy(l => l.date))
                            {
                                row = sheet.CreateRow(nRow++);
                                cell = row.CreateCell(1);
                                cell.CellStyle = styleActionData;
                                cell.SetCellValue(log.date.ToString("dd/MM/yyyy HH:mm:ss"));
                                cell = row.CreateCell(2);
                                cell.CellStyle = styleActionData;
                                cell.SetCellValue(log.action);
                            }
                        }

                        FileStream file = new FileStream(Path.Combine(dirMonth, dni + " " + dMonths.Key + " " + dDays.Key + "- " + MESES[dDays.Key - 1] + ".xlsx"), FileMode.Create);
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
        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        public struct TWLog
        {
            public DateTime date { get; set; }
            public string action { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static List<TWLog> listLogs(string userId, DateTime? start, DateTime? end, SqlConnection conn, SqlTransaction transaction = null)
        {
            if (end.HasValue)
                end = end.Value.AddDays(1).AddSeconds(-1);

            List<TWLog> logs = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT * FROM telework_register WHERE " +
                    "(@USER IS NULL OR userId = @USER) AND " +
                    "(@START IS NULL OR date >= @START) AND " +
                    "(@END IS NULL OR date <= @END) " +
                    "ORDER BY date DESC";
                command.Parameters.AddWithValue("@USER", (object)userId ?? DBNull.Value);
                command.Parameters.AddWithValue("@START", start == null ? DBNull.Value : start.Value);
                command.Parameters.AddWithValue("@END", end == null ? DBNull.Value : end.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(new TWLog()
                        {
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            action = reader.GetString(reader.GetOrdinal("action"))
                        });
                    }
                }
            }
            return logs;
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
