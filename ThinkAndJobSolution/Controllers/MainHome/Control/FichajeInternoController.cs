using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static ThinkAndJobSolution.Controllers._Helper.AnvizTools.AnvizTools;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;
using System.Net;
using NPOI.HSSF.Model;
using System.Text.RegularExpressions;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;

namespace ThinkAndJobSolution.Controllers.MainHome.Control
{
    [Route("api/v1/internal-fichaje")]
    [ApiController]
    [Authorize]
    public class FichajeInternoController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        // Foto para la identificacion
        [HttpPost]
        [Route(template: "set-face/")]
        public async Task<IActionResult> SetFace()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            string userId = FindUserIdBySecurityToken(securityToken);

            if (userId == null)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            try
            {
                Face? face = await DetectFace(data);
                if (face == null)
                {
                    result = new { error = "Error 4320, No se ha encontrado ningún rostro en la imagen" };
                }
                else
                {
                    HelperMethods.SaveFile(new[] { "users", userId, "face" }, face.Value.image);

                    using SqlConnection conn = new(CONNECTION_STRING);
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE users SET hasFace = 1 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        command.ExecuteNonQuery();
                    }

                    setFace(userId, face.Value.image, conn);
                    result = new { error = false };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5320, No se ha podido procesar la imagen" };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "set-face-by-id/{userId}/")]
        public async Task<IActionResult> SetFaceById(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("FichajeInterno.SetFaceById", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            try
            {
                Face? face = await DetectFace(data);
                if (face == null)
                {
                    result = new { error = "Error 4320, No se ha encontrado ningún rostro en la imagen" };
                }
                else
                {
                    SaveFile(new[] { "users", userId, "face" }, face.Value.image);

                    using SqlConnection conn = new(CONNECTION_STRING);
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE users SET hasFace = 1 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        command.ExecuteNonQuery();
                    }

                    setFace(userId, face.Value.image, conn);
                    result = new { error = false };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5320, No se ha podido procesar la imagen" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-face/")]
        public IActionResult GetFace()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            _ = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            string userId = FindUserIdBySecurityToken(securityToken);

            if (userId == null)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            object result = new { error = false, face = ReadFile(new[] { "users", userId, "face" }) };

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-face-by-id/{userId}/")]
        public IActionResult GetFaceById(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            _ = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("FichajeInterno.GetFaceById", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            object result = new { error = false, face = ReadFile(new[] { "users", userId, "face" }) };

            return Ok(result);
        }

        // Obtencion

        [HttpGet]
        [Route("get-current/")]
        public IActionResult GetCurrent()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("FichajeInterno.GetCurrent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new
                    {
                        error = false,
                        current = getCurrent(FindUserIdBySecurityToken(securityToken, conn), conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5420, no se han podido obtener el fichaje actual" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("get-photo/{fichajeId}/")]
        public IActionResult GetPhoto(int fichajeId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("FichajeInterno.GetPhoto", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string userId;
                    using (SqlCommand command = conn.CreateCommand())
                    {

                        command.CommandText = "SELECT userId FROM internal_fichaje WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", fichajeId);
                        using SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                            userId = reader.GetString(reader.GetOrdinal("userId"));
                        else
                            return Ok(new { error = "Error 4424, No se ha encontrado el fichaje." });
                    }

                    result = new
                    {
                        error = false,
                        photo = ReadFile(new[] { "users", userId, "telework_shifts", fichajeId.ToString() })
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5425, no se ha podido obtener la imagen" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("list-users/")]
        public IActionResult ListHasToShiftUsers()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("FichajeInterno.ListHasToShiftUsers", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<object> users = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT U.id, CONCAT(U.name, ' ', U.surname) as fullName FROM users U WHERE U.hasToShift = 1 ORDER BY fullName";
                        using SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            users.Add(new
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("fullName"))
                            });
                        }
                    }

                    result = new
                    {
                        error = false,
                        users
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5434, no se ha podido listar los usuarios" };
                }
            }

            return Ok(result);
        }

        // Fichaje
        [HttpGet]
        [Route("start/")]
        public IActionResult Start()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("FichajeInterno.Start", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);
                    InternalShift? current = getCurrent(userId, conn);

                    if (current.HasValue)
                    {
                        return Ok(new { error = "Error 4421, ya tienes un fichaje activo" });
                    }

                    Int64 id;
                    using (SqlCommand command = conn.CreateCommand())
                    {

                        command.CommandText = "INSERT INTO internal_fichaje (userId, isfacial) OUTPUT INSERTED.ID VALUES (@USER, 0)";
                        command.Parameters.AddWithValue("@USER", userId);
                        id = (Int64)command.ExecuteScalar();
                    }

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5421, no se ha podido inciar el fichaje" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("stop/{forced}/")]
        public IActionResult Stop(bool forced)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("FichajeInterno.Stop", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    DateTime now = DateTime.Now;
                    string userId = FindUserIdBySecurityToken(securityToken, conn);
                    InternalShift? current = getCurrent(userId, conn);

                    if (!current.HasValue)
                    {
                        return Ok(new { error = "Error 4421, no tienes un fichaje activo" });
                    }

                    if (current.Value.isFacial)
                    {
                        return Ok(new { error = "Error 4422, no puedes detener un fichaje facial via web" });
                    }

                    //Limite de 12 horas
                    if ((now - current.Value.start).TotalHours >= 12)
                        now = current.Value.start.AddHours(12);

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE internal_fichaje SET endDate = @NOW, duration = DATEDIFF(second, startDate, @NOW), forceStopped = @FORCED WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", current.Value.id);
                        command.Parameters.AddWithValue("@FORCED", forced ? 1 : 0);
                        command.Parameters.AddWithValue("@NOW", now);
                        command.ExecuteNonQuery();
                    }

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5422, no se ha podido detener el fichaje" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("keep-alive/")]
        public IActionResult KeepAlive()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("FichajeInterno.KeepAlive", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);
                    InternalShift? current = getCurrent(userId, conn);

                    if (current.HasValue)
                    {
                        using SqlCommand command = conn.CreateCommand();

                        command.CommandText = "UPDATE internal_fichaje SET lastKeepAlive = getdate() WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", current.Value.id);
                        command.ExecuteNonQuery();
                    }

                    result = new
                    {
                        error = false,
                        current
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5423, no se ha podido renovar el fichaje" };
                }
            }

            return Ok(result);
        }

        // Listado y Reportes

        [HttpGet]
        [Route(template: "get-my-fichaje/{year}/{month}/")]
        public IActionResult GetMyFichaje(int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("FichajeInterno.GetMyFichaje", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();

                string userId = FindUserIdBySecurityToken(securityToken);
                DateTime start = new(year, month, 1);
                DateTime end = start.AddMonths(1);

                result = new { error = false, records = listRecords(userId, start, end, conn) };
            }
            catch (Exception)
            {
                result = new { error = "Error 5321, No se ha podido obtener el fichaje" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-fichaje/{userId}/{year}/{month}/")]
        public IActionResult GetFichaje(string userId, int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("FichajeInterno.GetFichaje", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();

                DateTime start = new(year, month, 1);
                DateTime end = start.AddMonths(1);

                result = new { error = false, records = listRecords(userId, start, end, conn) };
            }
            catch (Exception)
            {
                result = new { error = "Error 5321, No se ha podido obtener el fichaje" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-fichaje-dia/{year}/{month}/{day}/")]
        public IActionResult GetFichajeDia(int year, int month, int day)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("FichajeInterno.GetFichajeDia", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            try
            {
                DateTime start = new(year, month, day);
                DateTime end = start.AddDays(1);

                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                result = new { error = false, records = listRecords(null, start, end, conn) };
            }
            catch (Exception)
            {
                result = new { error = "Error 5321, No se ha podido obtener el fichaje" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "report/{userId}/{year}/{month}/")]
        public IActionResult GenerateReport(string userId, int? year, int? month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            ResultadoAcceso access = HasPermission("FichajeInterno.GenerateReport", securityToken);
            if (!access.Acceso)
            {
                return new ForbidResult();
            }

            try
            {
                DateTime? start = null, end = null;
                if (year != null && month != null)
                {
                    start = new DateTime(year.Value, month.Value, 1);
                    end = start.Value.AddMonths(1);
                }
                else if (year != null)
                {
                    start = new DateTime(year.Value, 1, 1);
                    end = start.Value.AddYears(1);
                }

                Dictionary<int, Dictionary<int, Dictionary<int, List<InternalShift>>>> dYears = new();
                List<InternalShift> records;

                string nombre = null, dni = null;
                using (SqlConnection conn = new(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT TRIM(CONCAT(name, ' ', surname)) as [fullName], DocID FROM users WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        using SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            nombre = reader.GetString(reader.GetOrdinal("fullName"));
                            dni = reader.GetString(reader.GetOrdinal("DocID"));
                        }
                    }

                    if (nombre == null) return new NoContentResult();

                    records = listRecords(userId, start, end, conn);
                }

                foreach (InternalShift record in records)
                {
                    int cDay = record.start.Day, cMonth = record.start.Month, cYear = record.start.Year;
                    if (!dYears.ContainsKey(cYear)) dYears[cYear] = new();
                    if (!dYears[cYear].ContainsKey(cMonth)) dYears[cYear][cMonth] = new();
                    if (!dYears[cYear][cMonth].ContainsKey(cDay)) dYears[cYear][cMonth][cDay] = new();
                    dYears[cYear][cMonth][cDay].Add(record);
                }

                string tmpDir = GetTemporaryDirectory();

                string empresa = "DEA ESTRATEGIAS LABORALES ETT S.L.";
                string cif = "B90238460";
                string ccc = "1812475719";

                foreach (var dMonths in dYears)
                {
                    string dirYear = Path.Combine(tmpDir, dMonths.Key.ToString());
                    Directory.CreateDirectory(dirYear);
                    foreach (var dDays in dMonths.Value)
                    {
                        string dirMonth = Path.Combine(dirYear, $"{dDays.Key:00} {MESES[dDays.Key - 1]}");
                        Directory.CreateDirectory(dirMonth);

                        //Calcular fechas
                        var dateFirstDayOfMonth = new DateTime(dMonths.Key, dDays.Key, 1);
                        int firstDayOfMonth = dateFirstDayOfMonth.Day;
                        int lastDayOfMonth = dateFirstDayOfMonth.AddMonths(1).AddDays(-1).Day;

                        IWorkbook workbook = new XSSFWorkbook();
                        ISheet sheet = workbook.CreateSheet("Control horario");

                        //Fuentes
                        IFont fontTitle = workbook.CreateFont();
                        fontTitle.FontName = "Century Gothic";
                        fontTitle.IsBold = true;
                        //fontTitle.Underline = FontUnderlineType.Single;
                        fontTitle.FontHeightInPoints = 14;
                        IFont fontBold = workbook.CreateFont();
                        fontBold.FontName = "Century Gothic";
                        fontBold.IsBold = true;
                        fontBold.FontHeightInPoints = 11;
                        IFont fontLegal = workbook.CreateFont();
                        fontLegal.FontName = "Century Gothic";
                        fontLegal.IsBold = true;
                        fontLegal.FontHeightInPoints = 10;
                        IFont fontDefault = workbook.CreateFont();
                        fontDefault.FontName = "Century Gothic";
                        fontDefault.FontHeightInPoints = 11;

                        ICellStyle style;

                        //Estilos
                        ICellStyle styleTitle = workbook.CreateCellStyle();
                        styleTitle.Alignment = HorizontalAlignment.Center;
                        styleTitle.SetFont(fontTitle);
                        ICellStyle styleSubtitle = workbook.CreateCellStyle();
                        styleSubtitle.Alignment = HorizontalAlignment.Center;
                        styleSubtitle.SetFont(fontBold);
                        styleSubtitle.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        ICellStyle styleAttribute = workbook.CreateCellStyle();
                        styleAttribute.Alignment = HorizontalAlignment.Right;
                        styleAttribute.SetFont(fontBold);
                        ICellStyle styleValue = workbook.CreateCellStyle();
                        styleValue.Alignment = HorizontalAlignment.Left;
                        styleValue.SetFont(fontBold);
                        ICellStyle styleNumeroDia = workbook.CreateCellStyle();
                        styleNumeroDia.Alignment = HorizontalAlignment.Right;
                        styleNumeroDia.SetFont(fontBold);
                        styleNumeroDia.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        ICellStyle styleLegal = workbook.CreateCellStyle();
                        styleLegal.Alignment = HorizontalAlignment.Left;
                        styleLegal.SetFont(fontLegal);
                        styleLegal.BorderTop = BorderStyle.Thick;
                        styleLegal.BorderBottom = BorderStyle.Thick;
                        styleLegal.BorderRight = BorderStyle.Thick;
                        styleLegal.BorderLeft = BorderStyle.Thick;
                        ICellStyle styleFirma = workbook.CreateCellStyle();
                        styleFirma.Alignment = HorizontalAlignment.Center;
                        styleFirma.SetFont(fontLegal);
                        styleFirma.BorderTop = BorderStyle.Thick;
                        styleFirma.BorderBottom = BorderStyle.Thick;
                        styleFirma.BorderRight = BorderStyle.Thick;
                        styleFirma.BorderLeft = BorderStyle.Thick;
                        ICellStyle styleDefault = workbook.CreateCellStyle();
                        styleDefault.Alignment = HorizontalAlignment.Center;
                        styleDefault.SetFont(fontDefault);

                        //Tamaños de filas y columnas
                        ICell cell;
                        IRow row;
                        sheet.SetColumnWidth(0, 30 * 256);
                        sheet.SetColumnWidth(1, 20 * 256);
                        sheet.SetColumnWidth(2, 20 * 256);
                        sheet.SetColumnWidth(3, 15 * 256);
                        sheet.SetColumnWidth(4, 30 * 256);

                        //Titulo
                        row = sheet.CreateRow(0);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontTitle);
                        style.BorderTop = BorderStyle.Thick;
                        style.BorderBottom = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        for (int i = 0; i <= 4; i++)
                        {
                            cell = row.CreateCell(i);
                            cell.CellStyle = style;
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(0, 0, 0, 4));
                        cell = sheet.GetRow(0).GetCell(0);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("REGISTRO DIARIO DE JORNADA EN TRABAJADORES A DISTANCIA");

                        //Empresa y trabajador titulo
                        row = sheet.CreateRow(1);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderTop = BorderStyle.Thick;
                        style.BorderBottom = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        for (int i = 0; i <= 4; i++)
                        {
                            cell = row.CreateCell(i);
                            cell.CellStyle = style;
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 0, 2));
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 3, 4));
                        cell = row.GetCell(0);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("EMPRESA");
                        cell = row.GetCell(3);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("TRABAJADOR");

                        //Fila de nombre
                        row = sheet.CreateRow(2);
                        ICellStyle styleHeaderLeft = workbook.CreateCellStyle();
                        styleHeaderLeft.Alignment = HorizontalAlignment.Right;
                        styleHeaderLeft.SetFont(fontBold);
                        styleHeaderLeft.BorderBottom = BorderStyle.Thin;
                        styleHeaderLeft.BorderLeft = BorderStyle.Thick;
                        styleHeaderLeft.BorderRight = BorderStyle.Thin;
                        ICellStyle styleHeaderRight = workbook.CreateCellStyle();
                        styleHeaderRight.Alignment = HorizontalAlignment.Left;
                        styleHeaderRight.SetFont(fontBold);
                        styleHeaderRight.BorderBottom = BorderStyle.Thin;
                        styleHeaderRight.BorderLeft = BorderStyle.Thin;
                        styleHeaderRight.BorderRight = BorderStyle.Thick;
                        ICellStyle styleHeaderMiddle = workbook.CreateCellStyle();
                        styleHeaderMiddle.Alignment = HorizontalAlignment.Left;
                        styleHeaderMiddle.SetFont(fontBold);
                        styleHeaderMiddle.BorderBottom = BorderStyle.Thin;
                        styleHeaderMiddle.BorderRight = BorderStyle.Thick;
                        for (int i = 0; i <= 4; i++)
                        {
                            cell = row.CreateCell(i);
                            cell.CellStyle = (i == 0 || i == 3) ? styleHeaderLeft : (i == 2 ? styleHeaderMiddle : styleHeaderRight);
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(2, 2, 1, 2));
                        cell = row.GetCell(0);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("NOMBRE O RAZON SOCIAL: ");
                        cell = row.GetCell(1);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(empresa);
                        cell = row.GetCell(3);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("NOMBRE: ");
                        cell = row.GetCell(4);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(nombre);

                        //Fila de cif o dni
                        row = sheet.CreateRow(3);
                        for (int i = 0; i <= 4; i++)
                        {
                            cell = row.CreateCell(i);
                            cell.CellStyle = (i == 0 || i == 3) ? styleHeaderLeft : (i == 2 ? styleHeaderMiddle : styleHeaderRight);
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(3, 3, 1, 2));
                        cell = row.GetCell(0);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("CIF: ");
                        cell = row.GetCell(1);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(cif);
                        cell = row.GetCell(3);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("NIF: ");
                        cell = row.GetCell(4);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(dni);

                        //Fila de ccc o naf
                        row = sheet.CreateRow(4);
                        for (int i = 0; i <= 4; i++)
                        {
                            cell = row.CreateCell(i);
                            cell.CellStyle = (i == 0 || i == 3) ? styleHeaderLeft : (i == 2 ? styleHeaderMiddle : styleHeaderRight);
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(4, 4, 1, 2));
                        cell = row.GetCell(0);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("C.C.C.: ");
                        cell = row.GetCell(1);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(ccc);
                        /*
                        cell = row.GetCell(3);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("NAF: ");
                        cell = row.GetCell(4);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(naf);
                        */

                        //Fila de fechas
                        row = sheet.CreateRow(5);
                        for (int i = 0; i <= 4; i++)
                        {
                            cell = row.CreateCell(i);
                            cell.CellStyle = (i == 0 || i == 3) ? styleHeaderLeft : (i == 2 ? styleHeaderMiddle : styleHeaderRight);
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(5, 5, 1, 2));
                        cell = row.GetCell(0);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("PERIODO DE LIQUIDACION: ");
                        cell = row.GetCell(1);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("DEL " + firstDayOfMonth + " AL " + lastDayOfMonth + " DE " + MESES[dDays.Key - 1].ToUpper() + " DE " + dMonths.Key);
                        cell = row.GetCell(3);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("FECHA: ");
                        cell = row.GetCell(4);
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(lastDayOfMonth + "/" + dDays.Key + "/" + dMonths.Key);

                        //Filas de cabecera
                        row = sheet.CreateRow(6);
                        for (int i = 0; i <= 4; i++)
                        {
                            row.CreateCell(i);
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(6, 7, 0, 0));
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(6, 6, 1, 2));
                        cell = row.GetCell(0);
                        cell.SetCellType(CellType.String);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.VerticalAlignment = VerticalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderTop = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("DIA DEL MES");
                        cell = row.GetCell(1);
                        cell.SetCellType(CellType.String);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderTop = BorderStyle.Thick;
                        style.BorderBottom = BorderStyle.Thin;
                        style.BorderLeft = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("HORARIO");
                        cell = row.GetCell(2);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderTop = BorderStyle.Thick;
                        style.BorderBottom = BorderStyle.Thin;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell = row.GetCell(3);
                        cell.SetCellType(CellType.String);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.VerticalAlignment = VerticalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderTop = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("TOTAL");
                        cell.CellStyle.WrapText = true;
                        cell = row.GetCell(4);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.VerticalAlignment = VerticalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderTop = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("TOTAL");
                        cell.CellStyle.WrapText = true;
                        row = sheet.CreateRow(7);
                        for (int i = 0; i <= 4; i++)
                        {
                            row.CreateCell(i);
                        }
                        cell = row.GetCell(0);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderBottom = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell = row.GetCell(1);
                        cell.SetCellType(CellType.String);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderBottom = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thin;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("ENTRADA");
                        cell = row.GetCell(2);
                        cell.SetCellType(CellType.String);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderBottom = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thin;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("SALIDA");
                        cell = row.GetCell(3);
                        cell.SetCellType(CellType.String);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderBottom = BorderStyle.Thick;
                        style.BorderLeft = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("POR TRAMO");
                        cell = row.GetCell(4);
                        style = workbook.CreateCellStyle();
                        style.Alignment = HorizontalAlignment.Center;
                        style.SetFont(fontBold);
                        style.BorderBottom = BorderStyle.Thick;
                        style.BorderRight = BorderStyle.Thick;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        cell.CellStyle = style;
                        cell.SetCellValue("POR DÍA");

                        ICellStyle styleFirst = workbook.CreateCellStyle();
                        styleFirst.Alignment = HorizontalAlignment.Center;
                        styleFirst.VerticalAlignment = VerticalAlignment.Center;
                        styleFirst.SetFont(fontBold);
                        styleFirst.BorderTop = BorderStyle.Thin;
                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                        style.FillPattern = FillPattern.SolidForeground;
                        int iRow = 8;

                        foreach (var lList in dDays.Value.OrderBy(vk => vk.Key))
                        {
                            if (lList.Value.Count == 0) continue;
                            row = sheet.CreateRow(iRow);
                            cell = row.CreateCell(0);
                            cell.CellStyle = styleFirst;
                            cell.SetCellValue(lList.Key);
                            if (lList.Value.Count > 1)
                                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow + lList.Value.Count - 1, 0, 0));

                            int segs = lList.Value.Select(shift => shift.duration).Sum();
                            int h = segs / 3600, m = (segs / 60) % 60;
                            cell = row.CreateCell(4);
                            cell.SetCellValue((h == 0 ? "" : (h + "h ")) + m + "m");
                            ICellStyle styleSubTotal = workbook.CreateCellStyle();
                            styleSubTotal.Alignment = HorizontalAlignment.Center;
                            styleSubTotal.VerticalAlignment = VerticalAlignment.Center;
                            styleSubTotal.SetFont(fontBold);
                            styleSubTotal.BorderBottom = BorderStyle.Thick;
                            styleSubTotal.BorderLeft = BorderStyle.Thick;
                            styleSubTotal.BorderRight = BorderStyle.Thick;
                            cell.CellStyle = styleSubTotal;
                            if (lList.Value.Count > 1)
                                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow + lList.Value.Count - 1, 4, 4));

                            int c = 0;
                            foreach (InternalShift record in lList.Value.OrderBy(r => r.start))
                            {
                                DateTime rStart = record.start;

                                cell = row.CreateCell(1);
                                style = workbook.CreateCellStyle();
                                style.Alignment = HorizontalAlignment.Center;
                                style.SetFont(fontBold);
                                style.BorderLeft = BorderStyle.Thin;
                                style.BorderRight = BorderStyle.Thin;
                                if (c == lList.Value.Count - 1)
                                {
                                    style.BorderBottom = BorderStyle.Thin;
                                }
                                cell.CellStyle = style;
                                cell.SetCellValue($"{rStart.Hour:00}:{rStart.Minute:00}");

                                cell = row.CreateCell(2);
                                cell.CellStyle = style;
                                if (record.end.HasValue)
                                {
                                    DateTime rEnd = record.end.Value;
                                    if (rStart.Day == rEnd.Day && rStart.Month == rEnd.Month && rStart.Year == rEnd.Year)
                                    {
                                        cell.SetCellValue($"{rEnd.Hour:00}:{rEnd.Minute:00}");
                                    }
                                    else
                                    {
                                        cell.SetCellValue($"{rEnd.Hour:00}:{rEnd.Minute:00} {rEnd.Day:00}/{rEnd.Month:00}/{rEnd.Year}");
                                    }

                                    cell = row.CreateCell(3);
                                    int ht = segs / 3600, mt = (record.duration / 60) % 60;
                                    cell.SetCellValue((ht == 0 ? "" : (ht + "h ")) + mt + "m");
                                    style = workbook.CreateCellStyle();
                                    style.Alignment = HorizontalAlignment.Center;
                                    style.SetFont(fontBold);
                                    cell.CellStyle = style;
                                }
                                else
                                {
                                    cell.SetCellValue("En curso");
                                }

                                if (c > 0)
                                {
                                    cell = row.CreateCell(4);
                                    cell.CellStyle = styleSubTotal;

                                    cell = row.CreateCell(0);
                                    cell.CellStyle = styleFirst;
                                }

                                iRow++;
                                c++;
                                row = sheet.CreateRow(iRow);
                            }
                            if (sheet.GetRow(iRow - 1)?.GetCell(3) != null)
                                sheet.GetRow(iRow - 1).GetCell(3).CellStyle.BorderBottom = BorderStyle.Thin;
                        }

                        //Pie de pagina
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow, 0, 4));
                        row = sheet.CreateRow(iRow);
                        row.Height = 600;
                        cell = row.CreateCell(0);
                        cell.CellStyle = styleLegal;
                        cell.CellStyle.WrapText = true;
                        cell.SetCellValue("En cumplimiento de la obligación establecida en el Art. 34.9 del,Real Decreto Legislativo 2/2015 de 23 de Octubre, por el que se aprueba el texto refundido de la Ley de Estatutos de los Trabajadores");
                        row.CreateCell(1).CellStyle = styleLegal;
                        row.CreateCell(2).CellStyle = styleLegal;
                        row.CreateCell(3).CellStyle = styleLegal;
                        row.CreateCell(4).CellStyle = styleLegal;
                        iRow++;

                        //Cabecera de las firmas
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow, 0, 1));
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow, 3, 4));
                        row = sheet.CreateRow(iRow);
                        cell = row.CreateCell(0);
                        cell.CellStyle = styleFirma;
                        cell.SetCellValue("FIRMA DE LA EMPRESA");
                        row.CreateCell(1).CellStyle = styleFirma;
                        cell = row.CreateCell(3);
                        cell.CellStyle = styleFirma;
                        cell.SetCellValue("FIRMA DEL TRABAJADOR");
                        row.CreateCell(4).CellStyle = styleFirma;
                        iRow++;

                        //Espacio para firmar
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow + 9, 0, 1));
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow + 9, 3, 4));
                        for (int i = 0; i <= 9; i++)
                        {
                            row = sheet.CreateRow(iRow + i);
                            cell = row.CreateCell(0);
                            cell.CellStyle = workbook.CreateCellStyle();
                            cell.CellStyle.BorderLeft = BorderStyle.Thick;
                            if (i == 9)
                            {
                                cell.CellStyle.BorderBottom = BorderStyle.Thick;
                            }
                            cell = row.CreateCell(1);
                            cell.CellStyle = workbook.CreateCellStyle();
                            cell.CellStyle.BorderRight = BorderStyle.Thick;
                            if (i == 9)
                            {
                                cell.CellStyle.BorderBottom = BorderStyle.Thick;
                            }
                            cell = row.CreateCell(3);
                            cell.CellStyle = workbook.CreateCellStyle();
                            cell.CellStyle.BorderLeft = BorderStyle.Thick;
                            if (i == 9)
                            {
                                cell.CellStyle.BorderBottom = BorderStyle.Thick;
                            }
                            cell = row.CreateCell(4);
                            cell.CellStyle = workbook.CreateCellStyle();
                            cell.CellStyle.BorderRight = BorderStyle.Thick;
                            if (i == 9)
                            {
                                cell.CellStyle.BorderBottom = BorderStyle.Thick;
                            }
                        }

                        //Firmas
                        string firmaEmpresa = DEA_FIRMA;
                        byte[] firmaEmpresaData = Convert.FromBase64String(firmaEmpresa);
                        int pictureIndex = workbook.AddPicture(firmaEmpresaData, PictureType.PNG);
                        ICreationHelper helperEmpresa = workbook.GetCreationHelper();
                        IDrawing drawingEmpresa = sheet.CreateDrawingPatriarch();
                        IClientAnchor anchorEmpresa = helperEmpresa.CreateClientAnchor();
                        anchorEmpresa.Col1 = 0;
                        anchorEmpresa.Row1 = iRow + 2;
                        IPicture pictureEmpresa = drawingEmpresa.CreatePicture(anchorEmpresa, pictureIndex);
                        pictureEmpresa.Resize();

                        /*
                        string firmaCandidato = HelperMethods.readFile(new[] { "candidate", id, "stored_sign" });
                        if (firmaCandidato != null && firmaCandidato.Contains(","))
                        {
                            firmaCandidato = firmaCandidato.Split(",")[1];
                            byte[] firmaCandidatoData = Convert.FromBase64String(firmaCandidato);
                            pictureIndex = workbook.AddPicture(firmaCandidatoData, PictureType.PNG);
                            ICreationHelper helperCandidato = workbook.GetCreationHelper();
                            IDrawing drawingCandidato = sheet.CreateDrawingPatriarch();
                            IClientAnchor anchorCandidato = helperCandidato.CreateClientAnchor();
                            anchorCandidato.Col1 = 3;
                            anchorCandidato.Row1 = iRow;
                            IPicture pictureCandidato = drawingCandidato.CreatePicture(anchorCandidato, pictureIndex);
                            pictureCandidato.Resize();
                        }
                        */

                        FileStream file = new(Path.Combine(dirMonth, dni + " " + dMonths.Key + " " + dDays.Key + "- " + MESES[dDays.Key - 1] + ".xlsx"), FileMode.Create);
                        workbook.Write(file);
                        file.Close();
                    }
                }

                string tmpZipDir = GetTemporaryDirectory();
                string fileName = "reporte " + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + ".zip";
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

        [HttpGet]
        [Route(template: "resumen/{year}/{month}/")]
        public IActionResult GenerateResumen(int? year, int? month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            ResultadoAcceso access = HasPermission("FichajeInterno.GenerateResumen", securityToken);
            if (!access.Acceso)
            {
                return new ForbidResult();
            }

            try
            {
                DateTime? start = null, end = null;
                if (year != null && month != null)
                {
                    start = new DateTime(year.Value, month.Value, 1);
                    end = start.Value.AddMonths(1);
                }
                else if (year != null)
                {
                    start = new DateTime(year.Value, 1, 1);
                    end = start.Value.AddYears(1);
                }

                Dictionary<int, Dictionary<int, Dictionary<string, Dictionary<int, int>>>> dYears = new();
                List<InternalShift> records;
                List<string> forcedUsers = new();

                using (SqlConnection conn = new(CONNECTION_STRING))
                {
                    conn.Open();
                    records = listRecords(null, start, end, conn);

                    //Obtener las personas que deben aparecer siempre en el resumen
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "SELECT CONCAT(U.name, ' ', U.surname) as fullName FROM users U WHERE U.hasToShift = 1";
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        forcedUsers.Add(reader.GetString(reader.GetOrdinal("fullName")));
                    }
                }

                if (records.Count == 0) return new NoContentResult();
                foreach (InternalShift record in records)
                {
                    string user = record.name;
                    int cDay = record.start.Day, cMonth = record.start.Month, cYear = record.start.Year;
                    if (!dYears.ContainsKey(cYear)) dYears[cYear] = new();
                    if (!dYears[cYear].ContainsKey(cMonth)) dYears[cYear][cMonth] = new();
                    if (!dYears[cYear][cMonth].ContainsKey(user)) dYears[cYear][cMonth][user] = new();
                    if (!dYears[cYear][cMonth][user].ContainsKey(cDay)) dYears[cYear][cMonth][user][cDay] = 0;
                    dYears[cYear][cMonth][user][cDay] += record.duration;
                }

                string tmpDir = GetTemporaryDirectory();
                XSSFWorkbook workbook = new();

                foreach (var dMonths in dYears.OrderBy(vk => vk.Key))
                {
                    foreach (var dUsers in dMonths.Value.OrderBy(vk => vk.Key))
                    {
                        //Insertar a los usuarios obligatorios que no esten
                        foreach (string user in forcedUsers)
                            if (!dUsers.Value.ContainsKey(user))
                                dUsers.Value[user] = new();

                        //Calcular fechas
                        DateTime dateFirstDayOfMonth = new(dMonths.Key, dUsers.Key, 1);
                        int daysInMonth = DateTime.DaysInMonth(dMonths.Key, dUsers.Key);
                        string monthName = MESES[dUsers.Key - 1] + " " + dMonths.Key;

                        ISheet sheet = workbook.CreateSheet(monthName);

                        //Estilos
                        XSSFCellStyle styleTitle = (XSSFCellStyle)workbook.CreateCellStyle();
                        styleTitle.Alignment = HorizontalAlignment.Center;
                        styleTitle.VerticalAlignment = VerticalAlignment.Center;
                        styleTitle.BorderTop = BorderStyle.Thin;
                        styleTitle.BorderBottom = BorderStyle.Thin;
                        styleTitle.BorderRight = BorderStyle.Thin;
                        styleTitle.BorderLeft = BorderStyle.Thin;

                        XSSFCellStyle styleName = (XSSFCellStyle)workbook.CreateCellStyle();
                        styleName.CloneStyleFrom(styleTitle);
                        styleName.Alignment = HorizontalAlignment.Left;

                        XSSFCellStyle styleWeekend = (XSSFCellStyle)workbook.CreateCellStyle();
                        styleWeekend.CloneStyleFrom(styleTitle);
                        styleWeekend.FillPattern = FillPattern.SolidForeground;
                        styleWeekend.FillForegroundColorColor = new XSSFColor(new byte[] { 217, 217, 217 });

                        XSSFCellStyle styleYes = (XSSFCellStyle)workbook.CreateCellStyle();
                        styleYes.CloneStyleFrom(styleTitle);
                        styleYes.FillPattern = FillPattern.SolidForeground;
                        styleYes.FillForegroundColorColor = new XSSFColor(new byte[] { 226, 239, 218 });

                        XSSFCellStyle styleNo = (XSSFCellStyle)workbook.CreateCellStyle();
                        styleNo.CloneStyleFrom(styleTitle);
                        styleNo.FillPattern = FillPattern.SolidForeground;
                        styleNo.FillForegroundColorColor = new XSSFColor(new byte[] { 244, 176, 132 });

                        //Tamaños de filas y columnas
                        ICell cell;
                        IRow row, rowA, rowB, rowC;
                        sheet.SetColumnWidth(1, 40 * 256);
                        for (int i = 0; i < daysInMonth; i++)
                            sheet.SetColumnWidth(i + 2, 6 * 256);

                        //Titulo
                        rowA = sheet.CreateRow(1);
                        rowB = sheet.CreateRow(2);
                        rowC = sheet.CreateRow(3);
                        cell = rowA.CreateCell(1);
                        cell.CellStyle = styleTitle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue("Nombre");
                        cell = rowB.CreateCell(1);
                        cell.CellStyle = styleTitle;
                        cell = rowC.CreateCell(1);
                        cell.CellStyle = styleTitle;

                        cell = rowA.CreateCell(2);
                        cell.CellStyle = styleTitle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(monthName);
                        DateTime cDay = dateFirstDayOfMonth.Date;
                        for (int i = 0; i < daysInMonth; i++)
                        {
                            if (i > 0)
                            {
                                cell = rowA.CreateCell(i + 2);
                                cell.CellStyle = styleTitle;
                            }

                            bool esFindesemana = cDay.DayOfWeek == DayOfWeek.Saturday || cDay.DayOfWeek == DayOfWeek.Sunday;
                            cell = rowB.CreateCell(i + 2);
                            cell.CellStyle = esFindesemana ? styleWeekend : styleTitle;
                            cell.SetCellType(CellType.String);
                            cell.SetCellValue(cDay.Day);
                            cell = rowC.CreateCell(i + 2);
                            cell.CellStyle = esFindesemana ? styleWeekend : styleTitle;
                            cell.SetCellType(CellType.String);
                            cell.SetCellValue(DIAS_SEMANA_LETRA[cDay.DayOfWeek].ToString());
                            cDay = cDay.AddDays(1);
                        }
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 3, 1, 1));
                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 2, 1 + daysInMonth));

                        //Trabajadores
                        int nRow = 4;
                        foreach (var dDays in dUsers.Value)
                        {
                            row = sheet.CreateRow(nRow++);
                            cell = row.CreateCell(1);
                            cell.SetCellType(CellType.String);
                            cell.SetCellValue(dDays.Key);
                            cell.CellStyle = styleName;

                            cDay = dateFirstDayOfMonth.Date;
                            for (int i = 1; i <= daysInMonth; i++)
                            {
                                bool esFindesemana = cDay.DayOfWeek == DayOfWeek.Saturday || cDay.DayOfWeek == DayOfWeek.Sunday;
                                bool present = dDays.Value.ContainsKey(i);
                                cell = row.CreateCell(i + 1);
                                cell.SetCellType(CellType.String);
                                cell.SetCellValue(present ? "X" : "");
                                cell.CellStyle = present ? styleYes : (esFindesemana ? styleWeekend : styleNo);
                                cDay = cDay.AddDays(1);
                            }
                        }
                    }
                }

                string tmpFile = Path.Combine(tmpDir, "Resumen.xlsx");
                FileStream file = new(tmpFile, FileMode.Create);
                workbook.Write(file);
                file.Close();

                string contentType = "application/zip";
                HttpContext.Response.ContentType = contentType;
                var response = new FileContentResult(System.IO.File.ReadAllBytes(tmpFile), contentType)
                {
                    FileDownloadName = "Resumen.xlsx"
                };

                Directory.Delete(tmpDir, true);

                return response;
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        // Control
        [HttpGet]
        [Route("/auto-stop/")]
        public IActionResult AutoStop()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            // Creamos el objeto de resultado con su error
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            // Comprobamos que tenga permiso
            ResultadoAcceso access = HasPermission("FichajeInterno.AutoStop", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    // Control normal para los usuarios de los que se requiera un seguimiento de si 
                    // están frente la pantalla via web. (requiresKeepAlive = 1 && isfacial = 0)
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE FI SET FI.endDate = FI.lastKeepAlive, FI.forceStopped = 1, duration = DATEDIFF(second, FI.startDate, FI.lastKeepAlive) " +
                            "FROM internal_fichaje FI INNER JOIN users U ON(FI.userId = U.id) " +
                            "WHERE U.requiresKeepAlive = 1 AND FI.isfacial = 0 AND FI.endDate IS NULL AND DATEDIFF(minute, FI.lastKeepAlive, @NOW) >= 5 ";
                        command.Parameters.AddWithValue("@NOW", DateTime.Now);
                        command.ExecuteNonQuery();
                    }

                    // Controla que nadie de la oficina o via web se pase más de 12 horas.
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE FI SET FI.endDate = FI.lastKeepAlive, FI.forceStopped = 1, duration = DATEDIFF(second, FI.startDate, FI.lastKeepAlive) " +
                            "FROM internal_fichaje FI INNER JOIN users U ON(FI.userId = U.id) " +
                            "WHERE FI.endDate IS NULL AND DATEDIFF(minute, FI.lastKeepAlive, @NOW) >= 720";
                        command.Parameters.AddWithValue("@NOW", DateTime.Now);
                        command.ExecuteNonQuery();
                    }

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5426, no se han podido detener los fichajes" };
                }
            }

            return Ok(result);
        }

        // Sincronizacion
        [HttpGet]
        [Route(template: "sync/")]
        public IActionResult Sync()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            // Creamos el objeto de resultado con su error
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            // Comprobamos que tenga permiso
            if (!HasPermission("FichajeInterno.Sync", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                using SqlTransaction transaction = conn.BeginTransaction();
                try
                {
                    //await Sync(conn, transaction);

                    transaction.Commit();
                    result = new { error = false };
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    result = new { error = "Error 5322, No se ha podido sincronizar" };
                }
            }

            return Ok(result);
        }

        //Webhooks

        [HttpGet]
        [Route(template: "auto-tick/{idd}/{timestamp}")]
        public IActionResult AutoTick(string idd, int timestamp)
        {
            try
            {
                autoTick(idd, EpochToDateTime(timestamp));
                return Ok(new { error = false });
            }
            catch (Exception e)
            {
                return Ok(new { error = e.Message });
            }
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------

        // Ayuda

        public struct InternalShift
        {
            public long id { get; set; }
            public string userId { get; set; }
            public string name { get; set; }
            public DateTime start { get; set; }
            public DateTime? end { get; set; }
            public int duration { get; set; }
            public bool isFacial { get; set; }
            public bool forceStopped { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static InternalShift? getCurrent(string userId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using SqlCommand command = conn.CreateCommand();
            if (transaction != null)
            {
                command.Connection = conn;
                command.Transaction = transaction;
            }

            command.CommandText = "SELECT TOP 1 * FROM internal_fichaje WHERE userId = @USER ORDER BY startDate DESC";
            command.Parameters.AddWithValue("@USER", userId);

            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read() && reader.IsDBNull(reader.GetOrdinal("endDate")))
            {
                return new InternalShift()
                {
                    id = reader.GetInt64(reader.GetOrdinal("id")),
                    userId = userId,
                    start = reader.GetDateTime(reader.GetOrdinal("startDate")),
                    end = null,
                    isFacial = reader.GetInt32(reader.GetOrdinal("isfacial")) == 1
                };
            }
            else
            {
                return null;
            }
        }
        public static List<InternalShift> listRecords(string userId, DateTime? start, DateTime? end, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<InternalShift> records = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT FI.id, FI.startDate, FI.endDate, FI.duration, FI.isfacial, FI.forceStopped, U.id as userId, TRIM(CONCAT(U.name, ' ', U.surname)) as [fullName] " +
                    "FROM internal_fichaje FI INNER JOIN users U ON(FI.userId = U.id) " +
                    "WHERE (@USER IS NULL OR @USER = U.id) AND (@START IS NULL OR @START <= FI.startDate) AND (@END IS NULL OR @END >= FI.startDate)";
                command.Parameters.AddWithValue("@USER", (object)userId ?? DBNull.Value);
                command.Parameters.AddWithValue("@START", start == null ? DBNull.Value : start.Value);
                command.Parameters.AddWithValue("@END", end == null ? DBNull.Value : end.Value);
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new InternalShift()
                    {
                        id = reader.GetInt64(reader.GetOrdinal("id")),
                        start = reader.GetDateTime(reader.GetOrdinal("startDate")),
                        end = reader.IsDBNull(reader.GetOrdinal("endDate")) ? null : reader.GetDateTime(reader.GetOrdinal("endDate")),
                        duration = reader.GetInt32(reader.GetOrdinal("duration")),
                        isFacial = reader.GetInt32(reader.GetOrdinal("isfacial")) == 1,
                        userId = reader.GetString(reader.GetOrdinal("userId")),
                        name = reader.GetString(reader.GetOrdinal("fullName")),
                        forceStopped = reader.GetInt32(reader.GetOrdinal("forceStopped")) == 1
                    });
                }
            }

            return records;
        }
        public static void autoTick(string idd, DateTime timestamp)
        {
            if (!Regex.IsMatch(idd, @"^[0-9]"))
                throw new Exception("Error 4630, El codigo de trabajador no es valido");

            using SqlConnection conn = new(CONNECTION_STRING);
            conn.Open();

            //Obtener la ID del usuario
            string userId;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT id FROM users WHERE dbo.DNI2IDD(DocID) = @IDD";
                command.Parameters.AddWithValue("@IDD", idd);
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                    userId = reader.GetString(reader.GetOrdinal("id"));
                else
                    throw new Exception("Error 4631, El codigo de trabajador no existe");
            }

            //Si existe un shift finalizado y T está dentro, finalizar
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM internal_fichaje WHERE userId = @USER AND (@T between startDate and endDate)";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@T", timestamp);
                if ((int)command.ExecuteScalar() > 0)
                    throw new Exception("Error 4632, Ya hay un fichaje durante este periodo, ignorado");
            }

            //Buscar el primer shift previo a T que no haya finalizado
            InternalShift? currentTmp = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT TOP 1 * FROM internal_fichaje WHERE userId = @USER AND endDate IS NULL AND startDate <= @T ORDER BY startDate DESC";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@T", timestamp);
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    currentTmp = new InternalShift()
                    {
                        id = reader.GetInt64(reader.GetOrdinal("id")),
                        start = reader.GetDateTime(reader.GetOrdinal("startDate")),
                        isFacial = reader.GetInt32(reader.GetOrdinal("isfacial")) == 1
                    };
                }
            }

            if (currentTmp.HasValue)
            {
                //Hay un shift abierto
                InternalShift current = currentTmp.Value;

                //Comprobar que no sea via web
                if (!current.isFacial)
                    throw new Exception("Error 4633, El fichaje facial no puede finalizar un turno via web, ignorado");

                //Comprobar que hayan transcurrido al menos 5 minutos
                if ((timestamp - current.start).TotalMinutes < 5)
                    throw new Exception("Error 4634, Han pasado menos de 5 minutos desde el inicio del turno, ignorado");

                //Comprobar que no haya fichajes que se solapen entre current.start y timestamp
                List<InternalShift> solapados = new();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT id, startDate, endDate FROM internal_fichaje WHERE userId = @USER AND (startDate between @START AND @END OR endDate between @START AND @END) AND id <> @ID";
                    command.Parameters.AddWithValue("@USER", userId);
                    command.Parameters.AddWithValue("@ID", current.id);
                    command.Parameters.AddWithValue("@START", current.start);
                    command.Parameters.AddWithValue("@END", timestamp);
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        solapados.Add(new()
                        {
                            id = reader.GetInt64(reader.GetOrdinal("id")),
                            start = reader.GetDateTime(reader.GetOrdinal("startDate")),
                            end = reader.GetDateTime(reader.GetOrdinal("endDate"))
                        });
                    }
                }

                //Si uno de los shift solapados es anterior a current.start, eliminar current e ignorar el tick
                if (solapados.Any(s => s.start <= current.start))
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM internal_fichaje WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", current.id);
                        command.ExecuteNonQuery();
                    }
                    throw new Exception("Error 4635, Hay un fichaje anterior que se solapa con este, inicio borrado e ignorado");
                }

                //Si hay solapados (pero todos son posteriores), eliminar esos solapados
                if (solapados.Count > 0)
                {
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "DELETE FROM internal_fichaje WHERE id = @ID";
                    command.Parameters.Add("@ID", System.Data.SqlDbType.BigInt);
                    foreach (InternalShift solapado in solapados)
                    {
                        command.Parameters["@ID"].Value = solapado.id;
                        command.ExecuteNonQuery();
                    }
                }

                if ((timestamp - current.start).TotalHours > 12)
                {
                    //Han pasado más de 12 horas
                    //Terminar el turno abierto con un máximo de 12 horas
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE internal_fichaje SET endDate = @T, duration = DATEDIFF(second, startDate, @T) WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", current.id);
                        command.Parameters.AddWithValue("@T", current.start.AddHours(12));
                        command.ExecuteNonQuery();
                    }

                    //Empezar uno nuevo
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO internal_fichaje (userId, startDate, isfacial) VALUES (@USER, @T, 1)";
                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@T", timestamp);
                        command.ExecuteNonQuery();
                    }
                }
                else
                {
                    //Han pasado menos de 12 horas
                    //Terminar el turno
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "UPDATE internal_fichaje SET endDate = @T, duration = DATEDIFF(second, startDate, @T) WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", current.id);
                    command.Parameters.AddWithValue("@T", timestamp);
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                //No hay ninguno abierto
                //Comprobar que no se acabe de cerrar un shift hace menos de 5 minutos
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM internal_fichaje WHERE userId = @USER AND endDate > @CUTOFF AND endDate < @T";
                    command.Parameters.AddWithValue("@USER", userId);
                    command.Parameters.AddWithValue("@T", timestamp);
                    command.Parameters.AddWithValue("@CUTOFF", timestamp.AddMinutes(-5));
                    if ((int)command.ExecuteScalar() > 0)
                        throw new Exception("Error 4636, Hay un fichaje terminado a menos de 5 minutos, ignorando");
                }

                //Insertar un shift nuevo
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "INSERT INTO internal_fichaje (userId, startDate, isfacial) VALUES (@USER, @T, 1)";
                    command.Parameters.AddWithValue("@USER", userId);
                    command.Parameters.AddWithValue("@T", timestamp);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void setFace(string userId, string face, SqlConnection conn, SqlTransaction transaction = null)
        {
            _ = FindDocIdByUserId(userId, conn, transaction);
            _ = FindUsernameById(userId, conn, transaction);
            //await AnvizTools.InsertEmployees(new List<DeviceUser>() { new DeviceUser() { idd = HelperMethods.Dni2idd(dni), image = face, name = username } });
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
