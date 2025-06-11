using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Helper.AnvizTools;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers._Model.Horario;
using ThinkAndJobSolution.Controllers.Commons;
using ThinkAndJobSolution.Utils;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    [Route("api/v1/workshift")]
    [ApiController]
    [Authorize]
    public class WorkshiftController : ControllerBase
    {

        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpGet]
        [Route(template: "api/v1/workshift/get/{candidateId}/{localId}")]
        public IActionResult Get(string candidateId, int localId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                            "SELECT W.* " +
                            "FROM workshifts W " +
                            "WHERE W.candidateId = @CANDIDATE_ID AND " +
                            "CAST(DATEDIFF(s, '1970-01-01 00:00:00', startTime) AS BIGINT) = @LOCAL_ID";

                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@LOCAL_ID", localId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = new { error = false, shift = shiftFromReader(reader, true) };
                        }
                        else
                        {
                            result = new { error = "Error 4782, turno no encontrado" };
                        }
                    }
                }
            }
            return Ok(result);
        }

        //Borrado
        [HttpGet]
        [Route(template: "api/v1/workshift/remove/{candidateId}/{localId}")]
        public IActionResult Remove(string candidateId, int localId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                bool failed = false;
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.CommandText =
                            "DELETE FROM workshifts WHERE candidateId = @CANDIDATE_ID AND " +
                            "CAST(DATEDIFF(s, '1970-01-01 00:00:00', startTime) AS BIGINT) = @LOCAL_ID";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        command.Parameters.AddWithValue("@LOCAL_ID", localId);
                        command.ExecuteNonQuery();
                        HelperMethods.DeleteDir(new[] { "candidate", candidateId, "workshifts", localId.ToString() });
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new { error = "Error 5813, no se ha podido borrar el trabajo" };
                    }

                }
                if (!failed)
                {
                    result = new { error = false };
                }
                result = new { error = false };
            }
            return Ok(result);
        }

        //Listado
        [HttpGet]
        [Route(template: "api/v1/workshift/candidate/{candidateId}/{page?}/{year?}/{month?}")]
        public IActionResult GetByCandidate(string candidateId, int? year, int? month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HelperMethods.HasPermission("Workshift.GetByCandidate", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    List<Shift> shifts = new List<Shift>();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "SELECT W.* " +
                                "FROM workshifts W INNER JOIN candidatos C ON W.candidateId = C.id " +
                                "WHERE C.id = @CANDIDATE_ID AND " +
                                "(@YEAR IS NULL OR YEAR(w.startTime) = @YEAR) AND " +
                                "(@MONTH IS NULL OR MONTH(w.startTime) = @MONTH) " +
                                "ORDER BY startTime DESC";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        command.Parameters.AddWithValue("@YEAR", (object)year ?? DBNull.Value);
                        command.Parameters.AddWithValue("@MONTH", (object)month ?? DBNull.Value);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                shifts.Add(shiftFromReader(reader));
                            }
                        }
                    }

                    result = shifts;
                }
            }
            return Ok(result);
        }

        //Listado ordenado
        [HttpGet]
        [Route(template: "api/v1/workshift/candidate-sorted/{candidateId}/{page?}/{year?}/{month?}")]
        public IActionResult GetByCandidateSorted(string candidateId, int? year, int? month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HelperMethods.HasPermission("Workshift.GetByCandidateSorted", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    result = getShiftsOfCandidate(conn, candidateId, year, month);
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "current/{signLink}")]
        public IActionResult GetCurrentShift(string signLink)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                //Comprobar que existe el candidato y que este es su lastSignLink
                bool failed = false;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE id = @CANDIDATE_ID AND lastSignLink = @SIGN_LINK";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@SIGN_LINK", signLink);

                    try
                    {
                        if (command.ExecuteScalar() as Int32? != 1)
                        {
                            failed = true;
                            result = new { error = "Error 4810, no existe el candidato o no tiene tal trabajo actual asignado" };
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new { error = "Error 5810, no se ha podido determinar si el candidato existe" };
                    }
                }

                //Obtener el shift actual
                if (!failed)
                {
                    try
                    {
                        result = new { error = false, shift = getCurrent(candidateId, conn) };
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new { error = "Error 5811, no se ha podido determinar si el trabajador tiene ya iniciado un turno" };
                    }
                }
            }

            return Ok(result);
        }

        //Acciones
        [HttpPost]
        [Route(template: "start/{candidateId}/{signLink}")]
        public async Task<IActionResult> StartShift(string candidateId, string signLink)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("picture", out JsonElement pictureJson) &&
                json.TryGetProperty("lat", out JsonElement latJson) &&
                json.TryGetProperty("lon", out JsonElement lonJson))
            {
                string picture = pictureJson.GetString();
                double? lat = HelperMethods.GetJsonDouble(latJson);
                double? lon = HelperMethods.GetJsonDouble(lonJson);

                try
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();

                        //Comprobar que existe el candidato, que este es su lastSignLink y que esta activo
                        string candidateName, groupName, workName;
                        bool active;
                        DateTime? fechaComienzoTrabajo;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT C.active, C.fechaComienzoTrabajo, GE.nombre as groupName, nombre = TRIM(CONCAT(C.nombre, ' ', C.apellidos)), CA.name as category " +
                                "FROM candidatos C " +
                                "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                                "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                                "LEFT OUTER JOIN grupos_empresariales GE ON(E.grupoId = GE.id) " +
                                "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                                "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                                "WHERE C.id = @CANDIDATE_ID AND C.lastSignLink = @SIGN_LINK";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@SIGN_LINK", signLink);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    active = reader.GetInt32(reader.GetOrdinal("active")) == 1;
                                    fechaComienzoTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo"));
                                    candidateName = reader.GetString(reader.GetOrdinal("nombre"));
                                    groupName = reader.IsDBNull(reader.GetOrdinal("groupName")) ? null : reader.GetString(reader.GetOrdinal("groupName"));
                                    workName = reader.GetString(reader.GetOrdinal("category"));
                                }
                                else
                                    return Ok(new { error = "Error 4810, no existe el candidato o no tiene tal trabajo actual asignado" });
                            }
                        }

                        if (!active)
                            return Ok(new { error = "Error 4310, no tienes permitido comenzar a trabajar" });

                        if (fechaComienzoTrabajo != null && fechaComienzoTrabajo.Value.Date > DateTime.Now.Date)
                            return Ok(new { error = "Error 4310, aún no puedes empezar a trabajar", fechaComienzoTrabajo });

                        CentroRequirements? requirements = getCentroRequirements(candidateId, conn);

                        if (requirements == null)
                            return Ok(new { error = "Error 4811, no se ha encontrado el centro" });

                        if (requirements.Value.foto && picture == null)
                            return Ok(new { error = "Error 4815, no se ha proporcionado la foto" });

                        if (requirements.Value.ubicacion && (lat == null || lon == null))
                            return Ok(new { error = "Error 4815, no se ha proporcionado la ubicacion" });

                        //Comprobar que no tenga un workshift ya iniciado
                        Shift? current = getCurrent(candidateId, conn);
                        if (current != null)
                            return Ok(new { error = "Error 4811, no se puede iniciar el turno, hay uno sin acabar" });

                        //Comprobar que no tenga una baja vigente
                        DateTime now = DateTime.Now;
                        Dia dia = HorariosController.getCandidateDia(candidateId, now.Date, out bool empty, conn);

                        if (dia.baja)
                            return Ok(new { error = "Error 4812, no se puede iniciar el turno, hay una baja activa" });

                        if (dia.vacaciones)
                            return Ok(new { error = "Error 4812, no se puede iniciar el turno en periodo de vacaciones" });

                        //Intentar obtener la dirección IP de origen
                        string ip = null;
                        HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var headerIPAddress);
                        if (headerIPAddress.Count > 0)
                            ip = headerIPAddress[0];
                        else
                            ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                        //Insertar
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO workshifts (candidateId, workName, signLink, hasPicture, lat, lon, ipAddress, startTime) " +
                                                  "VALUES (@CANDIDATE_ID, @WORK_NAME, @SIGN_LINK, @PICTURE, @LAT, @LON, @IP, @START_TIME)";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                            command.Parameters.AddWithValue("@WORK_NAME", workName);
                            command.Parameters.AddWithValue("@PICTURE", picture == null ? 0 : 1);
                            command.Parameters.AddWithValue("@LAT", (object)lat ?? DBNull.Value);
                            command.Parameters.AddWithValue("@LON", (object)lon ?? DBNull.Value);
                            command.Parameters.AddWithValue("@IP", (object)ip ?? DBNull.Value);
                            command.Parameters.AddWithValue("@START_TIME", now);
                            command.ExecuteNonQuery();
                        }

                        //Guardar la imagen si la tiene
                        if (picture != null)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT * FROM workshifts WHERE candidateId = @CANDIDATE_ID AND endTime IS NULL";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        Shift shift = shiftFromReader(reader);
                                        picture = HelperMethods.LimitImageSize(picture, true, 512);
                                        HelperMethods.SaveFile(new[] { "candidate", shift.candidateId, "workshifts", shift.localId.ToString(), "picture" }, picture);
                                    }
                                }
                            }
                        }

                        //Comprobar si tiene firma guardada
                        bool hasToSign = false;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT lastStoredSign FROM candidatos WHERE id = @CANDIDATE_ID";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    if (reader.IsDBNull(reader.GetOrdinal("lastStoredSign")))
                                    {
                                        hasToSign = true;
                                    }
                                    else
                                    {
                                        DateTime lastStoredSign = reader.GetDateTime(reader.GetOrdinal("lastStoredSign"));
                                        hasToSign = (lastStoredSign.Month != now.Month || lastStoredSign.Year != now.Year) && now.Day >= 20;
                                    }
                                }
                            }
                        }

                        result = new { error = false, hasToSign };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5810, no se ha podido comenzar el turno" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "stop/{candidateId}/{signLink}")]
        public async Task<IActionResult> StopShift(string candidateId, string signLink)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    //Comprobar que existe el candidato y que este es su lastSignLink
                    string candidateName, groupName;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT GE.nombre as groupName, nombre = TRIM(CONCAT(C.nombre, ' ', C.apellidos)) " +
                            "FROM candidatos C " +
                            "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                            "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                            "LEFT OUTER JOIN grupos_empresariales GE ON(E.grupoId = GE.id) " +
                            "WHERE C.id = @CANDIDATE_ID AND C.lastSignLink = @SIGN_LINK";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                candidateName = reader.GetString(reader.GetOrdinal("nombre"));
                                groupName = reader.IsDBNull(reader.GetOrdinal("groupName")) ? null : reader.GetString(reader.GetOrdinal("groupName"));
                            }
                            else
                                return Ok(new { error = "Error 4810, no existe el candidato o no tiene tal trabajo actual asignado" });
                        }
                    }

                    //Comprobar que tenga un trabajo ya iniciado
                    Shift? current = getCurrent(candidateId, conn);
                    if (current == null)
                        return Ok(new { error = "Error 4811, no se puede detener el turno, no hay ninguno activo" });

                    DateTime shiftStart = current.Value.start;

                    //Obtener su horario
                    DateTime now = DateTime.Now;
                    Dia dia = HorariosController.getCandidateDia(candidateId, shiftStart.Date, out bool empty, conn);

                    //Comprobar en qué turno está
                    Tuple<DateTime, DateTime> turno = getCurrentTurno(candidateId, dia, shiftStart, conn);
                    DateTime? turnoStart = null, turnoEnd = null;
                    if (turno != null)
                    {
                        turnoStart = turno.Item1;
                        turnoEnd = turno.Item2;
                    }

                    Tuple<DateTime?, DateTime?> shift;
                    try
                    {
                        string deviceId = HelperMethods.FindRFDeviceIdbyCandidateId(candidateId, conn);
                        shift = await AnvizTools.Workshift(deviceId, groupName, candidateName, 1, shiftStart, now, turnoStart, turnoEnd);

                        //Si no tiene horario definido, ignorar
                        if (empty && (!shift.Item1.HasValue || !shift.Item2.HasValue))
                            shift = new(shiftStart, now);
                    }
                    catch (Exception)
                    {
                        //Si faya, suponer que no debe haber cambios
                        shift = new(shiftStart, now);
                    }

                    //Completar la fila 
                    if (shift.Item1.HasValue && shift.Item2.HasValue)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE workshifts SET startTime = @START, endTime = @END WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@OLD_START", shiftStart);
                            command.Parameters.AddWithValue("@START", shift.Item1.Value);
                            command.Parameters.AddWithValue("@END", shift.Item2.Value);

                            command.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM workshifts WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@OLD_START", shiftStart);

                            command.ExecuteNonQuery();
                        }
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5812, no se ha podido detener el turno" };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "report/{year?}/{month?}")]
        public async Task<IActionResult> GenerateReport( int? year, int? month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HelperMethods.HasPermission("Workshift.GenerateReport", securityToken).Acceso)
            {
                return new ForbidResult();
            }
            else
            {
                try
                {
                    using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                    string data = await readerBody.ReadToEndAsync();
                    JsonElement json = JsonDocument.Parse(data).RootElement;
                    if (json.TryGetProperty("all", out JsonElement allJson) &&
                        json.TryGetProperty("ids", out JsonElement idsJson))
                    {
                        bool all = allJson.GetBoolean();
                        List<string> ids = new List<string>();
                        foreach (JsonElement idJson in idsJson.EnumerateArray())
                        {
                            ids.Add(idJson.GetString());
                        }
                        using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                        {
                            conn.Open();
                            //Si se seleccionan todos, la lista ids tiene las exclusiones
                            if (all)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    if (ids.Count > 0)
                                    {
                                        command.CommandText = "SELECT id FROM candidatos WHERE id NOT IN ({diff})";
                                        HelperMethods.AddArrayParameters(command, "diff", ids.ToArray());
                                    }
                                    else
                                    {
                                        command.CommandText = "SELECT id FROM candidatos";
                                    }

                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        ids.Clear();
                                        while (reader.Read())
                                        {
                                            ids.Add(reader.GetString(reader.GetOrdinal("id")));
                                        }
                                    }
                                }
                            }

                            string tmpDir = HelperMethods.GetTemporaryDirectory();

                            foreach (string id in ids)
                            {
                                string nombre = null, nif = null, naf = null;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText =
                                        "SELECT TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as [fullName], C.dni, C.numero_seguridad_social " +
                                        "FROM workshifts W INNER JOIN candidatos C ON W.candidateId = C.id " +
                                        "WHERE C.id = @ID";
                                    command.Parameters.AddWithValue("@ID", id);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            nombre = reader.GetString(reader.GetOrdinal("fullName"));
                                            nif = reader.GetString(reader.GetOrdinal("dni"));
                                            naf = reader.IsDBNull(reader.GetOrdinal("numero_seguridad_social")) ? "" : reader.GetString(reader.GetOrdinal("numero_seguridad_social"));
                                        }
                                    }
                                }

                                if (nombre == null) continue;

                                Dictionary<int, Dictionary<int, Dictionary<int, List<Shift>>>> dYears = getShiftsOfCandidate(conn, id, year, month);
                                if (dYears.Count == 0) continue;

                                string empresa = "DEA ESTRATEGIAS LABORALES ETT S.L.";
                                string cif = "B90238460";
                                string ccc = "1812475719";

                                string dirCandidate = System.IO.Path.Combine(tmpDir, nif);
                                Directory.CreateDirectory(dirCandidate);
                                foreach (var dMonths in dYears)
                                {
                                    string dirYear = System.IO.Path.Combine(dirCandidate, dMonths.Key.ToString());
                                    Directory.CreateDirectory(dirYear);
                                    foreach (var dDays in dMonths.Value)
                                    {

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
                                        cell.SetCellValue("REGISTRO DIARIO DE JORNADA EN TRABAJADORES A TIEMPO COMPLETO");

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
                                        cell.SetCellValue(nif);

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
                                        cell = row.GetCell(3);
                                        cell.SetCellType(CellType.String);
                                        cell.SetCellValue("NAF: ");
                                        cell = row.GetCell(4);
                                        cell.SetCellType(CellType.String);
                                        cell.SetCellValue(naf);

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
                                        cell.SetCellValue("DEL " + firstDayOfMonth + " AL " + lastDayOfMonth + " DE " + HelperMethods.MESES[dDays.Key - 1].ToUpper() + " DE " + dMonths.Key);
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
                                        sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(6, 7, 3, 4));
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
                                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                                        style.FillPattern = FillPattern.SolidForeground;
                                        cell.CellStyle = style;
                                        cell.SetCellValue("TOTAL HORAS");
                                        cell = row.GetCell(4);
                                        style = workbook.CreateCellStyle();
                                        style.Alignment = HorizontalAlignment.Center;
                                        style.SetFont(fontBold);
                                        style.BorderTop = BorderStyle.Thick;
                                        style.BorderRight = BorderStyle.Thick;
                                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                                        style.FillPattern = FillPattern.SolidForeground;
                                        cell.CellStyle = style;
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
                                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                                        style.FillPattern = FillPattern.SolidForeground;
                                        cell.CellStyle = style;
                                        cell.SetCellValue("TOTAL HORAS");
                                        cell = row.GetCell(4);
                                        style = workbook.CreateCellStyle();
                                        style.Alignment = HorizontalAlignment.Center;
                                        style.SetFont(fontBold);
                                        style.BorderBottom = BorderStyle.Thick;
                                        style.BorderRight = BorderStyle.Thick;
                                        style.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                                        style.FillPattern = FillPattern.SolidForeground;
                                        cell.CellStyle = style;

                                        ICellStyle styleFirst = workbook.CreateCellStyle();
                                        styleFirst.Alignment = HorizontalAlignment.Right;
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

                                            int c = 0;
                                            foreach (Shift shift in lList.Value.OrderBy(r => r.start))
                                            {
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
                                                cell.SetCellValue(number2twodigits(shift.start.Hour) + ":" + number2twodigits(shift.start.Minute));

                                                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(iRow, iRow, 3, 4));

                                                cell = row.CreateCell(2);
                                                cell.CellStyle = style;
                                                if (shift.end.HasValue)
                                                {
                                                    DateTime end = shift.end.Value;
                                                    DateTime start = shift.start;
                                                    if (start.Day == end.Day && start.Month == end.Month && start.Year == end.Year)
                                                    {
                                                        cell.SetCellValue(number2twodigits(end.Hour) + ":" + number2twodigits(end.Minute));
                                                    }
                                                    else
                                                    {
                                                        cell.SetCellValue(number2twodigits(end.Hour) + ":" + number2twodigits(end.Minute) + " " + number2twodigits(end.Day) + "/" + number2twodigits(end.Month) + "/" + end.Year);
                                                    }
                                                    end = end.AddSeconds(-end.Second);
                                                    end = end.AddMilliseconds(-end.Millisecond);
                                                    start = start.AddSeconds(-start.Second);
                                                    start = start.AddMilliseconds(-start.Millisecond);
                                                    var diff = end - start;
                                                    cell = row.CreateCell(3);
                                                    cell.CellStyle = style;
                                                    cell.SetCellValue(string.Format("{0:0.##}", (diff.TotalHours == 0 ? "" : (((int)diff.TotalHours) + "h ")) + diff.Minutes + "m"));
                                                    cell = row.CreateCell(4);
                                                    cell.CellStyle = style;
                                                }
                                                else
                                                {
                                                    cell.SetCellValue("En curso");
                                                }

                                                iRow++;
                                                c++;
                                                row = sheet.CreateRow(iRow);
                                            }
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
                                        cell.SetCellValue("FIRMA DEL CANDIDATO");
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
                                        string firmaEmpresa = HelperMethods.DEA_FIRMA;
                                        byte[] firmaEmpresaData = Convert.FromBase64String(firmaEmpresa);
                                        int pictureIndex = workbook.AddPicture(firmaEmpresaData, PictureType.PNG);
                                        ICreationHelper helperEmpresa = workbook.GetCreationHelper();
                                        IDrawing drawingEmpresa = sheet.CreateDrawingPatriarch();
                                        IClientAnchor anchorEmpresa = helperEmpresa.CreateClientAnchor();
                                        anchorEmpresa.Col1 = 0;
                                        anchorEmpresa.Row1 = iRow + 2;
                                        IPicture pictureEmpresa = drawingEmpresa.CreatePicture(anchorEmpresa, pictureIndex);
                                        pictureEmpresa.Resize();

                                        string firmaCandidato = HelperMethods.ReadFile(new[] { "candidate", id, "stored_sign" });
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


                                        FileStream file = new FileStream(System.IO.Path.Combine(dirYear, nif + " " + dMonths.Key + " " + dDays.Key + "- " + HelperMethods.MESES[dDays.Key - 1] + ".xlsx"), FileMode.Create);
                                        workbook.Write(file);
                                        file.Close();
                                    }
                                }
                            }

                            string tmpZipDir = HelperMethods.GetTemporaryDirectory();
                            string fileName = "reporte " + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + ".zip";
                            string tmpOutZip = System.IO.Path.Combine(tmpZipDir, fileName);
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
                    }
                }
                catch (Exception)
                {
                    //Console.WriteLine(e.StackTrace);
                }
            }

            return new NoContentResult();
        }

        [HttpGet]
        [Route(template: "get-centro-requirements-for-candidate/{candidateId}")]
        public IActionResult GetCentroRequirementsForCandidate(string candidateId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                CentroRequirements? requirements = getCentroRequirements(candidateId, conn);
                if (requirements != null)
                {
                    result = new
                    {
                        error = false,
                        requiereFoto = requirements.Value.foto,
                        requiereUbicacion = requirements.Value.ubicacion
                    };
                }
                else
                {
                    result = new { error = "Error 4783, centro no asignado" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-at-work-day/{year}/{month}/{day}/")]
        public IActionResult ListCandidatesAtWorkDay(int year, int month, int day)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HelperMethods.HasPermission("Workshift.ListCandidatesAtWorkDay", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    result = new { error = false, candidates = listCandidatesAtWorkDay(new DateTime(year, month, day), conn) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5785, no se han podido obtener los candidatos que estan trabajando." };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-at-work-instant/{year}/{month}/{day}/{hour}/{minute}/{second}/")]
        public IActionResult ListCandidatesAtWorkInstant(int year, int month, int day, int hour, int minute, int second)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HelperMethods.HasPermission("Workshift.ListCandidatesAtWorkInstant", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    result = new { error = false, candidates = listCandidatesAtWorkInstant(new DateTime(year, month, day, hour, minute, second), conn) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5786, no se han podido obtener online los candidatos que estan trabajando." };
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "notify-candidates/")]
        public async Task<IActionResult> NotifyCandidates()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("Workshift.NotifyCandidates", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("title", out string title) &&
                json.TryGetString("body", out string body) &&
                json.TryGetStringList("candidates", out List<string> candidates))
            {
                try
                {
                    await PushNotificationController.sendNotifications(candidates.Select(id => new PushNotificationController.UID() { type = "ca", id = id }), new() { title = title, body = body, type = "candidate-shift-start" });
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5787, no se han podido enviar las notificaciones" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "auto-tick/{idd}/{timestamp?}")]
        public async Task<IActionResult> AutoTick(string idd, int? timestamp)
        {
            try
            {
                await autoTick(idd, timestamp.HasValue ? HelperMethods.EpochToDateTime(timestamp.Value) : DateTime.Now);
                return Ok(new { error = false });
            }
            catch (Exception e)
            {
                return Ok(new { error = e.Message });
            }
        }




        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        #region"Clases"
        public struct Shift
        {
            public string candidateId { get; set; }
            public int localId { get; set; }
            public string workName { get; set; }
            public DateTime start { get; set; }
            public DateTime? end { get; set; }
            public bool finished { get; set; }
            public bool hasPicture { get; set; }
            public string picture { get; set; }
            public double? lat { get; set; }
            public double? lon { get; set; }
            public string signLink { get; set; }
        }

        public class CandidateAtWork
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public string dni { get; set; }
            public string email { get; set; }
            public string telefono { get; set; }
            public string provincia { get; set; }
            public string localidad { get; set; }
            public string empresaId { get; set; }
            public string empresa { get; set; }
            public string centroId { get; set; }
            public string centro { get; set; }
            public int state { get; set; }
            public List<TimeInterval> shifts { get; set; }
            public Dia dia { get; set; }
            public DateTime? shiftStart { get; set; }
            public DateTime? shiftEnd { get; set; }
            public DateTime? turnoStart { get; set; }
            public DateTime? turnoEnd { get; set; }
        }
        public struct TimeInterval
        {
            public DateTime start { get; set; }
            public DateTime? end { get; set; }
            public bool overlap { get; set; }
        }
        public struct CentroRequirements
        {
            public bool foto { get; set; }
            public bool ubicacion { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        private string number2twodigits(int n)
        {
            return n < 10 ? ("0" + n) : n.ToString();
        }
        private Dictionary<int, Dictionary<int, Dictionary<int, List<Shift>>>> getShiftsOfCandidate(SqlConnection conn, string candidateId, int? year = null, int? month = null)
        {
            Dictionary<int, Dictionary<int, Dictionary<int, List<Shift>>>> dYears = new Dictionary<int, Dictionary<int, Dictionary<int, List<Shift>>>>();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT W.*, YEAR(w.startTime) as [year], MONTH(w.startTime) as [month], DAY(w.startTime) as [day] " +
                    "FROM workshifts W WHERE W.candidateId = @ID AND " +
                    "(@YEAR IS NULL OR YEAR(w.startTime) = @YEAR) AND " +
                    "(@MONTH IS NULL OR MONTH(w.startTime) = @MONTH) AND " +
                    "W.endTime IS NOT NULL " +
                    "ORDER BY W.startTime ASC ";
                command.Parameters.AddWithValue("@ID", candidateId);
                command.Parameters.AddWithValue("@YEAR", (object)year ?? DBNull.Value);
                command.Parameters.AddWithValue("@MONTH", (object)month ?? DBNull.Value);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int wYear = reader.GetInt32(reader.GetOrdinal("year"));
                        int wMonth = reader.GetInt32(reader.GetOrdinal("month"));
                        int wDay = reader.GetInt32(reader.GetOrdinal("day"));
                        if (!dYears.ContainsKey(wYear))
                        {
                            dYears.Add(wYear, new Dictionary<int, Dictionary<int, List<Shift>>>());
                        }
                        Dictionary<int, Dictionary<int, List<Shift>>> dMonths = dYears[wYear];
                        if (!dMonths.ContainsKey(wMonth))
                        {
                            dMonths.Add(wMonth, new Dictionary<int, List<Shift>>());
                        }
                        Dictionary<int, List<Shift>> dDays = dMonths[wMonth];
                        if (!dDays.ContainsKey(wDay))
                        {
                            dDays.Add(wDay, new List<Shift>());
                        }
                        List<Shift> lShifts = dDays[wDay];

                        lShifts.Add(shiftFromReader(reader));
                    }
                }
            }
            return dYears;
        }
        private Shift shiftFromReader(SqlDataReader reader, bool full = false)
        {
            Shift shift = new Shift
            {
                candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                workName = reader.GetString(reader.GetOrdinal("workName")),
                start = reader.GetDateTime(reader.GetOrdinal("startTime")),
                end = reader.IsDBNull(reader.GetOrdinal("endTime")) ? null : reader.GetDateTime(reader.GetOrdinal("endTime")),
                finished = !reader.IsDBNull(reader.GetOrdinal("endTime")),
                hasPicture = reader.GetInt32(reader.GetOrdinal("hasPicture")) == 1,
                lat = reader.IsDBNull(reader.GetOrdinal("lat")) ? null : reader.GetDouble(reader.GetOrdinal("lat")),
                lon = reader.IsDBNull(reader.GetOrdinal("lon")) ? null : reader.GetDouble(reader.GetOrdinal("lon")),
                signLink = reader.GetString(reader.GetOrdinal("signLink"))
            };
            shift.localId = HelperMethods.GetEpoch(shift.start);
            if (full && shift.hasPicture)
                shift.picture = HelperMethods.ReadFile(new[]
                    {"candidate", shift.candidateId, "workshifts", shift.localId.ToString(), "picture"}) ?? HelperMethods.PLACEHOLDER_PIC;
            return shift;
        }
        private static Shift? getCurrent(string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            Shift? current = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT TOP 1 W.workName, W.startTime, W.endTime FROM workshifts W INNER JOIN candidatos C ON(W.candidateId = C.id) WHERE C.id = @CANDIDATE_ID AND startTime <= @START  ORDER BY startTime DESC";
                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                command.Parameters.AddWithValue("@START", DateTime.Now);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read() && reader.IsDBNull(reader.GetOrdinal("endTime")))
                    {
                        current = new Shift
                        {
                            workName = reader.GetString(reader.GetOrdinal("workName")),
                            start = reader.GetDateTime(reader.GetOrdinal("startTime")),
                            end = null,
                            finished = false
                        };
                    }
                }
            }
            return current;
        }

        private List<CandidateAtWork> listCandidatesAtWorkInstant(DateTime time, SqlConnection conn)
        {
            List<CandidateAtWork> candidates = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.id, nombre = CONCAT(C.nombre, ' ', C.apellidos), C.dni, C.email, C.telefono, C.provincia, C.localidad, " +
                    "CE.id as centroId, CE.alias as centroAlias, E.id as empresaId, E.nombre as empresaNombre " +
                    "FROM candidatos C " +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE C.cesionActiva = 1 AND " +
                    "( EXISTS(SELECT * FROM horarios_asignacion HA INNER JOIN horarios H ON(HA.horarioId = H.id) WHERE HA.candidateId = C.id AND H.semana = @MONDAY) OR " +
                    "  EXISTS(SELECT * FROM workshifts WHERE candidateId = C.id AND startTime <= @TIME AND (endTime IS NULL OR endTime >= @TIME)) ) " +
                    "ORDER BY CE.alias, C.nombre, C.apellidos";
                command.Parameters.AddWithValue("@TIME", time);
                command.Parameters.AddWithValue("@MONDAY", time.GetMonday());
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        candidates.Add(new()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            nombre = reader.GetString(reader.GetOrdinal("nombre")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            email = reader.GetString(reader.GetOrdinal("email")),
                            telefono = reader.GetString(reader.GetOrdinal("telefono")),
                            provincia = reader.IsDBNull(reader.GetOrdinal("provincia")) ? null : reader.GetString(reader.GetOrdinal("provincia")),
                            localidad = reader.IsDBNull(reader.GetOrdinal("localidad")) ? null : reader.GetString(reader.GetOrdinal("localidad")),
                            centroId = reader.GetString(reader.GetOrdinal("centroId")),
                            centro = reader.GetString(reader.GetOrdinal("centroAlias")),
                            empresaId = reader.GetString(reader.GetOrdinal("empresaId")),
                            empresa = reader.GetString(reader.GetOrdinal("empresaNombre"))
                        });
                    }
                }
            }

            candidates.ForEach(candidate => {
                candidate.dia = HorariosController.getCandidateDia(candidate.id, time.Date, out bool empty, conn);

                bool hasHorarioWithMargin = false;
                if (empty)
                {
                    candidate.dia = null;
                }
                else
                {
                    Console.WriteLine(candidate.dni + " " + candidate.dia);
                    if (candidate.dia.vacaciones || candidate.dia.baja)
                    {
                        candidate.dia = null;
                    }
                    else
                    {
                        TimeSpan margin = new(0, 15, 0);
                        if (candidate.dia.manana != null)
                        {
                            DateTime start = time + candidate.dia.manana.entrada;
                            DateTime end = time + candidate.dia.manana.salida;
                            if (start <= time && time <= end)
                            {
                                candidate.turnoStart = start;
                                candidate.turnoEnd = end;
                            }
                            if (start - margin <= time && time <= end + margin)
                                hasHorarioWithMargin = true;
                        }
                        if (candidate.dia.tarde != null && candidate.turnoStart == null)
                        {
                            DateTime start = time + candidate.dia.tarde.entrada;
                            DateTime end = time + candidate.dia.tarde.salida;
                            if (start <= time && time <= end)
                            {
                                candidate.turnoStart = start;
                                candidate.turnoEnd = end;
                            }
                            if (start - margin <= time && time <= end + margin)
                                hasHorarioWithMargin = true;
                        }
                        if (candidate.dia.noche != null && candidate.turnoStart == null)
                        {
                            DateTime start = time + candidate.dia.noche.entrada;
                            DateTime end = time.AddDays(1) + candidate.dia.noche.salida;
                            if (start <= time && time <= end)
                            {
                                candidate.turnoStart = start;
                                candidate.turnoEnd = end;
                            }
                            if (start - margin <= time && time <= end + margin)
                                hasHorarioWithMargin = true;
                        }
                        if (!hasHorarioWithMargin)
                            candidate.dia = null;
                    }
                }

                candidate.shifts = new();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT startTime, endTime FROM workshifts WHERE candidateId = @ID AND startTime <= @TIME AND (endTime IS NULL OR endTime >= @TIME)";
                    command.Parameters.AddWithValue("@ID", candidate.id);
                    command.Parameters.AddWithValue("@TIME", time);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            candidate.shiftStart = reader.GetDateTime(reader.GetOrdinal("startTime"));
                            candidate.shiftEnd = reader.IsDBNull(reader.GetOrdinal("endTime")) ? null : reader.GetDateTime(reader.GetOrdinal("endTime"));
                            candidate.shifts.Add(new() { start = candidate.shiftStart.Value, end = candidate.shiftEnd, overlap = true });
                            candidate.state = hasHorarioWithMargin ? 2 : 1;
                        }
                        else
                        {
                            candidate.state = 0;
                        }
                    }
                }
            });

            return candidates.Where(candidate => candidate.dia != null || candidate.shifts.Count > 0).ToList();
        }
        private List<CandidateAtWork> listCandidatesAtWorkDay(DateTime date, SqlConnection conn)
        {
            List<CandidateAtWork> candidates = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.id, nombre = CONCAT(C.nombre, ' ', C.apellidos), C.dni, C.email, C.telefono, C.provincia, C.localidad, " +
                    "CE.id as centroId, CE.alias as centroAlias, E.id as empresaId, E.nombre as empresaNombre " +
                    "FROM candidatos C " +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE " +
                    "( EXISTS(SELECT * FROM horarios_asignacion HA INNER JOIN horarios H ON(HA.horarioId = H.id) WHERE HA.candidateId = C.id AND H.semana = @MONDAY) OR " +
                    "  EXISTS(SELECT * FROM workshifts WHERE candidateId = C.id AND startTime <= @END AND (endTime IS NULL OR endTime >= @START)) ) " +
                    "ORDER BY CE.alias, C.nombre, C.apellidos";
                command.Parameters.AddWithValue("@MONDAY", date.GetMonday());
                command.Parameters.AddWithValue("@START", date.Date);
                command.Parameters.AddWithValue("@END", date.Date.AddDays(1).AddMilliseconds(-1));
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        candidates.Add(new()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            nombre = reader.GetString(reader.GetOrdinal("nombre")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            email = reader.GetString(reader.GetOrdinal("email")),
                            telefono = reader.GetString(reader.GetOrdinal("telefono")),
                            provincia = reader.IsDBNull(reader.GetOrdinal("provincia")) ? null : reader.GetString(reader.GetOrdinal("provincia")),
                            localidad = reader.IsDBNull(reader.GetOrdinal("localidad")) ? null : reader.GetString(reader.GetOrdinal("localidad")),
                            centroId = reader.GetString(reader.GetOrdinal("centroId")),
                            centro = reader.GetString(reader.GetOrdinal("centroAlias")),
                            empresaId = reader.GetString(reader.GetOrdinal("empresaId")),
                            empresa = reader.GetString(reader.GetOrdinal("empresaNombre"))
                        });
                    }
                }
            }

            candidates.ForEach(candidate => {
                candidate.dia = HorariosController.getCandidateDia(candidate.id, date.Date, out bool empty, conn);
                List<TimeInterval> turnos = new();
                TimeSpan margin = new(0, 15, 0);

                if (empty)
                {
                    candidate.dia = null;
                }
                else
                {
                    if ((candidate.dia.manana == null && candidate.dia.tarde == null && candidate.dia.noche == null) || candidate.dia.vacaciones || candidate.dia.baja)
                    {
                        candidate.dia = null;
                    }
                    else
                    {
                        if (candidate.dia.manana != null)
                        {
                            DateTime start = date + candidate.dia.manana.entrada;
                            DateTime end = date + candidate.dia.manana.salida;
                            turnos.Add(new() { start = start - margin, end = end + margin });
                            candidate.turnoStart = start;
                            candidate.turnoEnd = end;
                        }
                        if (candidate.dia.tarde != null)
                        {
                            DateTime start = date + candidate.dia.tarde.entrada;
                            DateTime end = date + candidate.dia.tarde.salida;
                            turnos.Add(new() { start = start - margin, end = end + margin });
                            if (candidate.turnoStart == null)
                                candidate.turnoStart = start;
                            candidate.turnoEnd = end;
                        }
                        if (candidate.dia.noche != null)
                        {
                            DateTime start = date + candidate.dia.noche.entrada;
                            DateTime end = date.AddDays(1) + candidate.dia.noche.salida;
                            turnos.Add(new() { start = start - margin, end = end + margin });
                            if (candidate.turnoStart == null)
                                candidate.turnoStart = start;
                            candidate.turnoEnd = end;
                        }
                    }
                }

                candidate.shifts = new();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT startTime, endTime FROM workshifts WHERE candidateId = @ID AND startTime <= @END AND (endTime IS NULL OR endTime >= @START) ORDER BY startTime";
                    command.Parameters.AddWithValue("@ID", candidate.id);
                    command.Parameters.AddWithValue("@START", date.Date);
                    command.Parameters.AddWithValue("@END", date.Date.AddDays(1).AddMilliseconds(-1));
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime start = reader.GetDateTime(reader.GetOrdinal("startTime"));
                            DateTime? end = reader.IsDBNull(reader.GetOrdinal("endTime")) ? null : reader.GetDateTime(reader.GetOrdinal("endTime"));
                            candidate.shifts.Add(new() { start = start, end = end, overlap = turnos.Any(t => start <= t.end && (end == null || end >= t.start)) });
                        }
                    }
                }

                if (candidate.shifts.Count > 0)
                {
                    candidate.shiftStart = candidate.shifts[0].start;
                    candidate.shiftEnd = candidate.shifts[^1].end;
                    candidate.state = candidate.dia == null ? 1 : 2;
                }
                else
                {
                    candidate.state = 0;
                }
            });

            return candidates.Where(candidate => candidate.dia != null || candidate.shifts.Count > 0).ToList();
        }
        private static Tuple<DateTime, DateTime> getCurrentTurno(string candidateId, Dia dia, DateTime today, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<string> turnos = new();
            if (dia.manana != null) turnos.Add("manana");
            if (dia.tarde != null) turnos.Add("tarde");
            if (dia.noche != null) turnos.Add("noche");

            int nFinalizados;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT COUNT(*) " +
                    "FROM workshifts " +
                    "WHERE candidateId = @CANDIDATE_ID AND endTime IS NOT NULL AND " +
                    "startTime >= @DAY_START AND startTime <= @DAY_END";
                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                command.Parameters.AddWithValue("@DAY_START", today.Date);
                command.Parameters.AddWithValue("@DAY_END", today.Date.AddDays(1).AddSeconds(-1));
                nFinalizados = (int)command.ExecuteScalar();
            }
            if (nFinalizados >= turnos.Count)
                return null;
            else
            {
                switch (turnos[nFinalizados])
                {
                    case "manana":
                        return new Tuple<DateTime, DateTime>(today + dia.manana.entrada, today + dia.manana.salida);
                    case "tarde":
                        return new Tuple<DateTime, DateTime>(today + dia.tarde.entrada, today + dia.tarde.salida);
                    case "noche":
                        return new Tuple<DateTime, DateTime>(today + dia.noche.entrada, today.AddDays(1) + dia.noche.salida);
                    default:
                        return null;
                }
            }
        }

        public static CentroRequirements? getCentroRequirements(string candidateId, SqlConnection conn)
        {
            CentroRequirements? requirements = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT CE.workshiftRequiereFoto, CE.workshiftRequiereUbicacion " +
                    "FROM candidatos C INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "WHERE C.id = @CANDIDATE_ID";

                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        requirements = new CentroRequirements()
                        {
                            foto = reader.GetInt32(reader.GetOrdinal("workshiftRequiereFoto")) == 1,
                            ubicacion = reader.GetInt32(reader.GetOrdinal("workshiftRequiereUbicacion")) == 1
                        };
                    }
                }
            }

            return requirements;
        }
        public static async Task autoTick(string idd, DateTime timestamp)
        {
            if (!Regex.IsMatch(idd, @"^[0-9]"))
                throw new Exception("Error 4630, El codigo de trabajador no es valido");

            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                //Comprobar que existe el candidato y obtener sus datos
                string candidateId, signLink, candidateName, groupName, workName;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT C.id, C.lastSignLink, GE.nombre as groupName, nombre = TRIM(CONCAT(C.nombre, ' ', C.apellidos)), CA.name as category " +
                        "FROM candidatos C " +
                        "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                        "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                        "LEFT OUTER JOIN grupos_empresariales GE ON(E.grupoId = GE.id) " +
                        "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                        "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                        "WHERE dbo.DNI2IDD(C.dni) = @IDD";

                    command.Parameters.AddWithValue("@IDD", idd);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            candidateId = reader.GetString(reader.GetOrdinal("id"));
                            signLink = reader.GetString(reader.GetOrdinal("lastSignLink"));
                            candidateName = reader.GetString(reader.GetOrdinal("nombre"));
                            groupName = reader.IsDBNull(reader.GetOrdinal("groupName")) ? null : reader.GetString(reader.GetOrdinal("groupName"));
                            workName = reader.GetString(reader.GetOrdinal("category"));
                        }
                        else
                            throw new Exception("Error 4810, no existe el candidato o no tiene tal trabajo actual asignado");
                    }
                }
                string deviceId = HelperMethods.FindRFDeviceIdbyCandidateId(candidateId, conn);

                //Si existe un shift finalizado y T está dentro, finalizar
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM workshifts WHERE candidateId = @CANDIDATE_ID AND (@T between startTime and endTime)";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@T", timestamp);
                    if ((int)command.ExecuteScalar() > 0)
                        throw new Exception("Error 4832, Ya hay un fichaje durante este periodo, ignorado");
                }

                //Desde la maquina no se tiene en cuena vacaciones o bajas

                //Buscar el primer shift previo a T que no haya finalizado
                DateTime? shiftStart = null;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT TOP 1 * FROM workshifts WHERE candidateId = @CANDIDATE_ID AND endTime IS NULL AND startTime <= @T ORDER BY startTime DESC";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    command.Parameters.AddWithValue("@T", timestamp);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            shiftStart = reader.GetDateTime(reader.GetOrdinal("startTime"));
                        }
                    }
                }

                if (shiftStart.HasValue)
                {
                    //Comprobar que hayan transcurrido al menos 1 minuto
                    if ((timestamp - shiftStart.Value).TotalMinutes < 1)
                        throw new Exception("Error 4834, Han pasado menos de 1 minuto desde el inicio del turno, ignorado");

                    //Comprobar en qué turno está
                    Dia dia = HorariosController.getCandidateDia(candidateId, shiftStart.Value.Date, out bool empty, conn);
                    Tuple<DateTime, DateTime> lastTurno = getCurrentTurno(candidateId, dia, shiftStart.Value, conn);
                    DateTime? lastTurnoStart = null, lastTurnoEnd = null;
                    if (lastTurno != null)
                    {
                        lastTurnoStart = lastTurno.Item1;
                        lastTurnoEnd = lastTurno.Item2;
                    }

                    if ((timestamp - shiftStart.Value).TotalHours > 12)
                    {
                        //Han pasado más de 12 horas Terminar el turno abierto con un máximo de 12 horas
                        DateTime shouldEndTime = shiftStart.Value.AddHours(12);

                        //Mostrar los datos del fichaje
                        Tuple<DateTime?, DateTime?> shift;
                        try
                        {
                            shift = await AnvizTools.Workshift(deviceId, groupName, candidateName, 1, shiftStart.Value, shouldEndTime, lastTurnoStart, lastTurnoEnd);

                            //Si no tiene horario definido, o faltan datos, se ignora el cambio
                            if (empty && (!shift.Item1.HasValue || !shift.Item2.HasValue))
                                shift = new(shiftStart, lastTurnoStart);
                        }
                        catch (Exception)
                        {
                            //Si faya, suponer que no debe haber cambios
                            shift = new(shiftStart, lastTurnoStart);
                        }

                        if (shift.Item1.HasValue && shift.Item2.HasValue)
                        {
                            //Usar los nuevos valores de finidos en shift

                            //Comprobar que no haya fichajes que se solapen emtre current.start y timestamp
                            bool solape;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT COUNT(*) FROM workshifts WHERE candidateId = @CANDIDATE_ID AND (startTime between @START AND @END OR endTime between @START AND @END) AND startTime <> @OLD_START";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                command.Parameters.AddWithValue("@START", shift.Item1.Value);
                                command.Parameters.AddWithValue("@END", shift.Item2.Value);
                                solape = (int)command.ExecuteScalar() > 0;
                            }
                            //Si hay solapamiento se borra current y se ignora el tick
                            if (solape)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "DELETE FROM workshifts WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                    command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                    command.ExecuteNonQuery();
                                }
                                throw new Exception("Error 4835, Hay un fichaje que se solapa con este, inicio borrado e ignorado");
                            }

                            //Actualizar el fichaje
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE workshifts SET startTime = @START, endTime = @END WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                command.Parameters.AddWithValue("@START", shift.Item1.Value);
                                command.Parameters.AddWithValue("@END", shift.Item2.Value);
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            //Se cancela el fichaje, hay que eliminarlo

                            //Eliminar el fichaje
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "DELETE FROM workshifts WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                command.ExecuteNonQuery();
                            }
                        }

                        //Empezar un fichaje nuevo
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO workshifts (candidateId, startTime, signLink, workName, hasPicture) VALUES (@USER, @T, @SIGN_LINK, @WORK_NAME, 0)";
                            command.Parameters.AddWithValue("@USER", candidateId);
                            command.Parameters.AddWithValue("@T", timestamp);
                            command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                            command.Parameters.AddWithValue("@WORK_NAME", workName);
                            command.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        //Han pasado menos de 12 horas Terminar el turno

                        //Mostrar los datos del fichaje
                        Tuple<DateTime?, DateTime?> shift;
                        try
                        {
                            shift = await AnvizTools.Workshift(deviceId, groupName, candidateName, 1, shiftStart.Value, timestamp, lastTurnoStart, lastTurnoEnd);

                            //Si no tiene horario definido, o faltan datos, se ignora el cambio
                            if (empty && (!shift.Item1.HasValue || !shift.Item2.HasValue))
                                shift = new(shiftStart, timestamp);
                        }
                        catch (Exception)
                        {
                            //Si faya, suponer que no debe haber cambios
                            shift = new(shiftStart, timestamp);
                        }

                        if (shift.Item1.HasValue && shift.Item2.HasValue)
                        {
                            //El fichaje es redefinido en shift

                            //Comprobar que no haya fichajes que se solapen emtre current.start y timestamp
                            bool solape;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT COUNT(*) FROM workshifts WHERE candidateId = @CANDIDATE_ID AND (startTime between @START AND @END OR endTime between @START AND @END) AND startTime <> @OLD_START";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                command.Parameters.AddWithValue("@START", shift.Item1.Value);
                                command.Parameters.AddWithValue("@END", shift.Item2.Value);
                                solape = (int)command.ExecuteScalar() > 0;
                            }
                            //Si hay solapamiento se borra current y se ignora el tick
                            if (solape)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "DELETE FROM workshifts WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                    command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                    command.ExecuteNonQuery();
                                }
                                throw new Exception("Error 4835, Hay un fichaje que se solapa con este, inicio borrado e ignorado");
                            }

                            //Actualizar el fichaje
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE workshifts SET startTime = @START, endTime = @END WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                command.Parameters.AddWithValue("@START", shift.Item1.Value);
                                command.Parameters.AddWithValue("@END", shift.Item2.Value);
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            //Se cancela el fichaje, hay que eliminarlo

                            //Eliminar el fichaje
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "DELETE FROM workshifts WHERE candidateId = @CANDIDATE_ID AND startTime = @OLD_START";
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                                command.Parameters.AddWithValue("@OLD_START", shiftStart.Value);
                                command.ExecuteNonQuery();
                            }
                        }

                    }

                }
                else
                {
                    //No hay ninguno abierto
                    //Comprobar que no se acabe de cerrar un shift hace menos de 1 minutos
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM workshifts WHERE candidateId = @CANDIDATE_ID AND endTime > @CUTOFF AND endTime < @T";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        command.Parameters.AddWithValue("@T", timestamp);
                        command.Parameters.AddWithValue("@CUTOFF", timestamp.AddMinutes(-1));
                        if ((int)command.ExecuteScalar() > 0)
                            throw new Exception("Error 4836, Hay un fichaje terminado a menos de 1 minuto, ignorando");
                    }

                    //Insertar un shift nuevo
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO workshifts (candidateId, startTime, signLink, workName, hasPicture) VALUES (@CANDIDATE_ID, @T, @SIGN_LINK, @WORK_NAME, 0)";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        command.Parameters.AddWithValue("@T", timestamp);
                        command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                        command.Parameters.AddWithValue("@WORK_NAME", workName);
                        command.ExecuteNonQuery();
                    }
                }

            }
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------


    }
}
