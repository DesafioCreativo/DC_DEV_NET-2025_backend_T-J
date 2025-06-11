using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers._Model.Horario;
using ThinkAndJobSolution.Controllers.Commons;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateGroupController;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.ClientUserController;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    using Horario = List<Dia>;

    [Route("api/v1/horario")]
    [ApiController]
    [Authorize]
    public class HorariosController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        // Plantilla por centro

        [HttpGet]
        [Route(template: "get-template-for-client/{centroId}/")]
        public IActionResult GetTemplateForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn) == null)
                    {
                        return Ok(new { error = "Error 1002, permisos insuficientes" });
                    }

                    result = new { error = false, template = getTemplate(centroId, conn), lockDate = getLockDate() };
                }
                catch (Exception)
                {
                    //result = new { error = ErrorHandle.newError(5780, e) };
                    result = new { error = "Error 5780, no se ha podido obtener la plantilla" };
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route("set-template-for-client/{centroId}/")]
        public async Task<IActionResult> SetTemplateForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn) == null)
                    {
                        return Ok(new { error = "Error 1002, permisos insuficientes" });
                    }

                    setTemplate(centroId, parseDia(json), conn);

                    result = new { error = false };
                }
                catch (Exception e)
                {
                    result = new { error = ErrorHandle.newError(5781, e) };
                    //result = new { error = "Error 5780, no se ha podido obtener la plantilla" };
                }
            }

            return Ok(result);
        }

        // Definición del horario

        [HttpPost]
        [Route("set-semana-for-client/{centroId}/{year}/{month}/{day}/")]
        public async Task<IActionResult> SetSemanaForClient(string centroId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            DateTime monday = new DateTime(year, month, day).GetMonday();
            if (monday < getLockDate())
            {
                return Ok(new { error = "Error 4782, no puedes establecer el horario de una semana pasada" });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!(json.TryGetProperty("semana", out JsonElement semanaJson) && json.TryGetStringList("afectados", out List<string> afectados)))
                return Ok(result);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn, transaction) == null)
                            return Ok(new { error = "Error 1002, permisos insuficientes" });

                        Semana semana = parseSemana(semanaJson);

                        await setSemana(centroId, monday, semana, afectados.ToHashSet(), conn, transaction);

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5782, no se ha podido establecer el horario" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("repeat-semana-for-client/{centroId}/{year}/{month}/{day}/")]
        public async Task<IActionResult> RepeatSemanaForClient(string centroId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            DateTime monday = new DateTime(year, month, day).GetMonday();
            if (monday < getLockDate())
            {
                return Ok(new { error = "Error 4782, no puedes establecer el horario de una semana pasada" });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn, transaction) == null)
                            return Ok(new { error = "Error 1002, permisos insuficientes" });

                        Semana semana = getSemana(centroId, monday.AddDays(-7), conn, transaction);
                        if (semana == null)
                            return Ok(new { error = "Error 4785, no hay horario definido para la semana pasada." });

                        //Eliminar a los trabajadores que ya no estén
                        List<string> candidates = new();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT id FROM candidatos WHERE centroId = @CENTRO";
                            command.Parameters.AddWithValue("@CENTRO", centroId);
                            using (SqlDataReader reader = command.ExecuteReader())
                                while (reader.Read())
                                    candidates.Add(reader.GetString(reader.GetOrdinal("id")));
                        }
                        semana.candidatos = semana.candidatos.Where(i => candidates.Contains(i.Key)).ToDictionary(i => i.Key, i => i.Value);

                        //Eliminar los grupos que ya no existan
                        List<CandidateGroup> groups = listGroupsWithMembers(centroId, conn, transaction);
                        semana.grupos = semana.grupos.Where(i => groups.Any(g => i.nombre == g.name)).ToList();

                        //Actualizar los participantes de los grupos actuales
                        semana.grupos.ForEach(g =>
                        {
                            CandidateGroup group = groups.Find(i => i.name == g.nombre);
                            g.miembros = group.candidates.Select(c => c.id).ToList();
                        });

                        await setSemana(centroId, monday, semana, null, conn, transaction);

                        transaction.Commit();
                        result = new { error = false, semana };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5788, no se ha podido copiar el horario" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route("delete-semana-for-client/{centroId}/{year}/{month}/{day}/")]
        public IActionResult DeleteSemanaForClient(string centroId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            DateTime monday = new DateTime(year, month, day).GetMonday();
            if (monday < getLockDate())
            {
                return Ok(new { error = "Error 4782, no puedes borrar el horario de una semana actual o pasada" });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn, transaction) == null)
                            return Ok(new { error = "Error 1002, permisos insuficientes" });

                        deleteSemana(centroId, monday, conn, transaction);

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5783, no se ha podido borrar el horario" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-semana-for-client/{centroId}/{year}/{month}/{day}/")]
        public IActionResult GetSemanaForClient(string centroId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn) == null)
                        return Ok(new { error = "Error 1002, permisos insuficientes" });

                    DateTime monday = new DateTime(year, month, day).GetMonday();

                    result = new { error = false, semana = getSemana(centroId, monday, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5784, no se ha podido obtener el horario" };
                }
            }

            return Ok(result);
        }

        //Extras de la asignacion

        [HttpGet]
        [Route("list-candidates-and-groups-for-client/{centroId}/")]
        public IActionResult ListCandidatesAndGroupsForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn, null, out ClientUserAccess access) == null)
                        return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });

                    result = new
                    {
                        error = false,
                        candidates = listForClient(centroId, conn),
                        groups = listGroupsWithMembers(centroId, conn)
                    };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5797, no han podido listar candidatos" };
            }

            return Ok(result);
        }

        //Obtencion de la asignacion

        [HttpGet]
        [Route("check-candidate-has-horario/")]
        public IActionResult CheckCandidateHasHorario()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    bool empty = true;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) " +
                            "FROM horarios_asignacion HA " +
                            "WHERE HA.candidateId = @CANDIDATE ";
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        empty = (int)command.ExecuteScalar() == 0;
                    }

                    result = new { error = false, empty };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5795, no se ha podido obtener el horario" };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route("get-candidate-defined-semanas/")]
        public IActionResult GetDefinedSemanas()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new { error = false, semanas = getCandidateDefinedSemanas(candidateId, conn), sinver = getCandidateUnseenSemanas(candidateId, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5786, no se ha podido obtener el horario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("get-candidate-semana/{year}/{month}/{day}")]
        public IActionResult GetCandidateSemana(int year, int month, int day)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    DateTime monday = new DateTime(year, month, day).GetMonday();
                    Horario horario = getCandidateSemana(candidateId, monday, out bool empty, conn);

                    //Marcar como visto
                    if (!empty)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "UPDATE horarios_asignacion " +
                                "SET horarios_asignacion.seen = 1 " +
                                "FROM horarios_asignacion HA " +
                                "INNER JOIN horarios H ON(HA.horarioId = H.id) " +
                                "WHERE HA.candidateId = @CANDIDATE AND H.semana = @SEMANA";
                            command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                            command.Parameters.AddWithValue("@SEMANA", monday);
                            command.ExecuteNonQuery();
                        }
                    }

                    result = new { error = false, horario, empty };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5787, no se ha podido obtener el horario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("get-centro-horario/{centroId}/{workId}/{year}/{month}/{day}/")]
        public IActionResult GetCentroHorario(string centroId, string workId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn) == null)
                        return Ok(new { error = "Error 1002, permisos insuficientes" });

                    DateTime start = new DateTime(year, month, day).GetMonday();
                    if (workId == "null") workId = null;

                    result = new { error = false, lines = getHorarioCentroSemana(centroId, start, workId, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5795, no se ha podido obtener el horario del centro" };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "download-centro-horario/{centroId}/{workId}/{year}/{month}/{day}/")]
        public IActionResult DownloadCentroHorario(string centroId, string workId, int year, int month, int day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            try
            {
                if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS) == null)
                    return new NoContentResult();

                DateTime start = new DateTime(year, month, day).GetMonday();
                DateTime end = start.GetSunday();
                if (workId == "null") workId = null;

                //Obtener los datos
                string companyNombre = null, centroAlias = null;
                List<ExcelLine> lines = new List<ExcelLine>();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Obtener datos del centro y comprobar que existe
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT CE.alias, E.nombre FROM centros CE INNER JOIN empresas E ON(CE.companyId = E.id) WHERE CE.id = @CENTRO";
                        command.Parameters.AddWithValue("@CENTRO", centroId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                companyNombre = reader.GetString(reader.GetOrdinal("nombre"));
                                centroAlias = reader.GetString(reader.GetOrdinal("alias"));
                            }
                        }
                    }

                    lines = getHorarioCentroSemana(centroId, start, workId, conn);
                }

                //Crear el excel
                string tmpDir = GetTemporaryDirectory();
                var thicc = BorderStyle.Medium;
                var thin = BorderStyle.Thin;
                var none = BorderStyle.None;
                var light = new XSSFColor(new byte[] { 242, 242, 242 });
                var descanso = new XSSFColor(new byte[] { 238, 238, 238 });
                var descansoLight = new XSSFColor(new byte[] { 245, 245, 245 });
                var manana = new XSSFColor(new byte[] { 188, 228, 236 });
                var mananaLight = new XSSFColor(new byte[] { 220, 240, 244 });
                var tarde = new XSSFColor(new byte[] { 130, 147, 186 });
                var tardeLight = new XSSFColor(new byte[] { 156, 170, 200 });
                var noche = new XSSFColor(new byte[] { 54, 79, 135 });
                var nocheLight = new XSSFColor(new byte[] { 65, 95, 161 });
                var bluwy = new XSSFColor(new byte[] { 119, 136, 153 });

                var workbook = new XSSFWorkbook();

                //Fuentes
                var fontTitleBlack = workbook.CreateFont();
                fontTitleBlack.FontName = "Bahnschrift";
                fontTitleBlack.IsBold = true;
                fontTitleBlack.FontHeightInPoints = 14;
                fontTitleBlack.Color = IndexedColors.Black.Index;

                var fontTitle = workbook.CreateFont();
                fontTitle.FontName = "Bahnschrift";
                fontTitle.FontHeightInPoints = 14;
                fontTitle.Color = IndexedColors.Black.Index;

                var fontNormal = workbook.CreateFont();
                fontNormal.FontName = "Bahnschrift";
                fontNormal.FontHeightInPoints = 11;
                fontNormal.Color = IndexedColors.Black.Index;

                var fontNormalBluwy = (XSSFFont)workbook.CreateFont();
                fontNormalBluwy.FontName = "Bahnschrift";
                fontNormalBluwy.FontHeightInPoints = 11;
                fontNormalBluwy.SetColor(bluwy);

                var fontNormalWhite = workbook.CreateFont();
                fontNormalWhite.FontName = "Bahnschrift";
                fontNormalWhite.FontHeightInPoints = 11;
                fontNormalWhite.Color = IndexedColors.White.Index;

                //Formatos
                XSSFCellStyle headerHorarioStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerHorarioStyle.SetFont(fontTitleBlack);
                headerHorarioStyle.BorderTop = thicc;
                headerHorarioStyle.BorderBottom = thicc;
                headerHorarioStyle.BorderRight = none;
                headerHorarioStyle.BorderLeft = thicc;
                headerHorarioStyle.Alignment = HorizontalAlignment.Center;
                headerHorarioStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle headerDateStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerDateStyle.SetFont(fontNormal);
                headerDateStyle.BorderTop = thicc;
                headerDateStyle.BorderBottom = thicc;
                headerDateStyle.BorderRight = none;
                headerDateStyle.BorderLeft = none;
                headerDateStyle.Alignment = HorizontalAlignment.Center;
                headerDateStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle headerCompanyStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerCompanyStyle.SetFont(fontTitle);
                headerCompanyStyle.BorderTop = thicc;
                headerCompanyStyle.BorderBottom = thicc;
                headerCompanyStyle.BorderRight = none;
                headerCompanyStyle.BorderLeft = none;
                headerCompanyStyle.Alignment = HorizontalAlignment.Center;
                headerCompanyStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle headerCentroStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerCentroStyle.SetFont(fontTitle);
                headerCentroStyle.BorderTop = thicc;
                headerCentroStyle.BorderBottom = thicc;
                headerCentroStyle.BorderRight = thicc;
                headerCentroStyle.BorderLeft = none;
                headerCentroStyle.Alignment = HorizontalAlignment.Center;
                headerCentroStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle headerTrabajadorStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerTrabajadorStyle.SetFont(fontNormal);
                headerTrabajadorStyle.BorderTop = thicc;
                headerTrabajadorStyle.BorderBottom = thin;
                headerTrabajadorStyle.BorderRight = thicc;
                headerTrabajadorStyle.BorderLeft = thicc;
                headerTrabajadorStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerDniStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerDniStyle.SetFont(fontNormal);
                headerDniStyle.BorderTop = thin;
                headerDniStyle.BorderBottom = thicc;
                headerDniStyle.BorderRight = thin;
                headerDniStyle.BorderLeft = thicc;
                headerDniStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerNombreStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerNombreStyle.SetFont(fontNormal);
                headerNombreStyle.BorderTop = thin;
                headerNombreStyle.BorderBottom = thicc;
                headerNombreStyle.BorderRight = thicc;
                headerNombreStyle.BorderLeft = thin;
                headerNombreStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerTurnoStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerTurnoStyle.SetFont(fontNormal);
                headerTurnoStyle.FillForegroundColorColor = light;
                headerTurnoStyle.FillPattern = FillPattern.SolidForeground;
                headerTurnoStyle.BorderTop = thicc;
                headerTurnoStyle.BorderBottom = thicc;
                headerTurnoStyle.BorderRight = thicc;
                headerTurnoStyle.BorderLeft = thicc;
                headerTurnoStyle.Alignment = HorizontalAlignment.Center;
                headerTurnoStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle headerDiaClaroStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerDiaClaroStyle.SetFont(fontNormal);
                headerDiaClaroStyle.BorderTop = thicc;
                headerDiaClaroStyle.BorderBottom = thin;
                headerDiaClaroStyle.BorderRight = thicc;
                headerDiaClaroStyle.BorderLeft = thicc;
                headerDiaClaroStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerEntradaClaroStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerEntradaClaroStyle.SetFont(fontNormal);
                headerEntradaClaroStyle.BorderTop = thin;
                headerEntradaClaroStyle.BorderBottom = thicc;
                headerEntradaClaroStyle.BorderRight = thin;
                headerEntradaClaroStyle.BorderLeft = thicc;
                headerEntradaClaroStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerSalidaClaroStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerSalidaClaroStyle.SetFont(fontNormal);
                headerSalidaClaroStyle.BorderTop = thin;
                headerSalidaClaroStyle.BorderBottom = thicc;
                headerSalidaClaroStyle.BorderRight = thicc;
                headerSalidaClaroStyle.BorderLeft = thin;
                headerSalidaClaroStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerDiaOscuroStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerDiaOscuroStyle.SetFont(fontNormal);
                headerDiaOscuroStyle.FillForegroundColorColor = light;
                headerDiaOscuroStyle.FillPattern = FillPattern.SolidForeground;
                headerDiaOscuroStyle.BorderTop = thicc;
                headerDiaOscuroStyle.BorderBottom = thin;
                headerDiaOscuroStyle.BorderRight = thicc;
                headerDiaOscuroStyle.BorderLeft = thicc;
                headerDiaOscuroStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerEntradaOscuroStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerEntradaOscuroStyle.SetFont(fontNormal);
                headerEntradaOscuroStyle.FillForegroundColorColor = light;
                headerEntradaOscuroStyle.FillPattern = FillPattern.SolidForeground;
                headerEntradaOscuroStyle.BorderTop = thin;
                headerEntradaOscuroStyle.BorderBottom = thicc;
                headerEntradaOscuroStyle.BorderRight = thin;
                headerEntradaOscuroStyle.BorderLeft = thicc;
                headerEntradaOscuroStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle headerSalidaOscuroStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerSalidaOscuroStyle.SetFont(fontNormal);
                headerSalidaOscuroStyle.FillForegroundColorColor = light;
                headerSalidaOscuroStyle.FillPattern = FillPattern.SolidForeground;
                headerSalidaOscuroStyle.BorderTop = thin;
                headerSalidaOscuroStyle.BorderBottom = thicc;
                headerSalidaOscuroStyle.BorderRight = thicc;
                headerSalidaOscuroStyle.BorderLeft = thin;
                headerSalidaOscuroStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyDniStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyDniStyle.SetFont(fontNormal);
                bodyDniStyle.BorderTop = thicc;
                bodyDniStyle.BorderBottom = thicc;
                bodyDniStyle.BorderRight = thin;
                bodyDniStyle.BorderLeft = thicc;
                bodyDniStyle.Alignment = HorizontalAlignment.Left;
                bodyDniStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle bodyNombreStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyNombreStyle.SetFont(fontNormal);
                bodyNombreStyle.BorderTop = thicc;
                bodyNombreStyle.BorderBottom = thicc;
                bodyNombreStyle.BorderRight = thicc;
                bodyNombreStyle.BorderLeft = thin;
                bodyNombreStyle.Alignment = HorizontalAlignment.Left;
                bodyNombreStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle bodyMananaStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyMananaStyle.SetFont(fontNormal);
                bodyMananaStyle.FillForegroundColorColor = manana;
                bodyMananaStyle.FillPattern = FillPattern.SolidForeground;
                bodyMananaStyle.BorderTop = thicc;
                bodyMananaStyle.BorderBottom = thin;
                bodyMananaStyle.BorderRight = thicc;
                bodyMananaStyle.BorderLeft = thicc;
                bodyMananaStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyTardeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyTardeStyle.SetFont(fontNormal);
                bodyTardeStyle.FillForegroundColorColor = tarde;
                bodyTardeStyle.FillPattern = FillPattern.SolidForeground;
                bodyTardeStyle.BorderTop = thin;
                bodyTardeStyle.BorderBottom = thin;
                bodyTardeStyle.BorderRight = thicc;
                bodyTardeStyle.BorderLeft = thicc;
                bodyTardeStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyNocheStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyNocheStyle.SetFont(fontNormalWhite);
                bodyNocheStyle.FillForegroundColorColor = noche;
                bodyNocheStyle.FillPattern = FillPattern.SolidForeground;
                bodyNocheStyle.BorderTop = thin;
                bodyNocheStyle.BorderBottom = thicc;
                bodyNocheStyle.BorderRight = thicc;
                bodyNocheStyle.BorderLeft = thicc;
                bodyNocheStyle.Alignment = HorizontalAlignment.Center;

                //Dia claro
                XSSFCellStyle bodyEntradaMananaStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaMananaStyle.SetFont(fontNormal);
                bodyEntradaMananaStyle.FillForegroundColorColor = mananaLight;
                bodyEntradaMananaStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaMananaStyle.BorderTop = thicc;
                bodyEntradaMananaStyle.BorderBottom = thin;
                bodyEntradaMananaStyle.BorderRight = thin;
                bodyEntradaMananaStyle.BorderLeft = thicc;
                bodyEntradaMananaStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaMananaStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaMananaStyle.SetFont(fontNormal);
                bodySalidaMananaStyle.FillForegroundColorColor = mananaLight;
                bodySalidaMananaStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaMananaStyle.BorderTop = thicc;
                bodySalidaMananaStyle.BorderBottom = thin;
                bodySalidaMananaStyle.BorderRight = thicc;
                bodySalidaMananaStyle.BorderLeft = thin;
                bodySalidaMananaStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaTardeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaTardeStyle.SetFont(fontNormal);
                bodyEntradaTardeStyle.FillForegroundColorColor = tardeLight;
                bodyEntradaTardeStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaTardeStyle.BorderTop = thin;
                bodyEntradaTardeStyle.BorderBottom = thin;
                bodyEntradaTardeStyle.BorderRight = thin;
                bodyEntradaTardeStyle.BorderLeft = thicc;
                bodyEntradaTardeStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaTardeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaTardeStyle.SetFont(fontNormal);
                bodySalidaTardeStyle.FillForegroundColorColor = tardeLight;
                bodySalidaTardeStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaTardeStyle.BorderTop = thin;
                bodySalidaTardeStyle.BorderBottom = thin;
                bodySalidaTardeStyle.BorderRight = thicc;
                bodySalidaTardeStyle.BorderLeft = thin;
                bodySalidaTardeStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaNocheStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaNocheStyle.SetFont(fontNormalWhite);
                bodyEntradaNocheStyle.FillForegroundColorColor = nocheLight;
                bodyEntradaNocheStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaNocheStyle.BorderTop = thin;
                bodyEntradaNocheStyle.BorderBottom = thicc;
                bodyEntradaNocheStyle.BorderRight = thin;
                bodyEntradaNocheStyle.BorderLeft = thicc;
                bodyEntradaNocheStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaNocheStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaNocheStyle.SetFont(fontNormalWhite);
                bodySalidaNocheStyle.FillForegroundColorColor = nocheLight;
                bodySalidaNocheStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaNocheStyle.BorderTop = thin;
                bodySalidaNocheStyle.BorderBottom = thicc;
                bodySalidaNocheStyle.BorderRight = thicc;
                bodySalidaNocheStyle.BorderLeft = thin;
                bodySalidaNocheStyle.Alignment = HorizontalAlignment.Center;

                //Dia oscuro
                XSSFCellStyle bodyEntradaMananaDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaMananaDarkenedStyle.SetFont(fontNormal);
                bodyEntradaMananaDarkenedStyle.FillForegroundColorColor = manana;
                bodyEntradaMananaDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaMananaDarkenedStyle.BorderTop = thicc;
                bodyEntradaMananaDarkenedStyle.BorderBottom = thin;
                bodyEntradaMananaDarkenedStyle.BorderRight = thin;
                bodyEntradaMananaDarkenedStyle.BorderLeft = thicc;
                bodyEntradaMananaDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaMananaDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaMananaDarkenedStyle.SetFont(fontNormal);
                bodySalidaMananaDarkenedStyle.FillForegroundColorColor = manana;
                bodySalidaMananaDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaMananaDarkenedStyle.BorderTop = thicc;
                bodySalidaMananaDarkenedStyle.BorderBottom = thin;
                bodySalidaMananaDarkenedStyle.BorderRight = thicc;
                bodySalidaMananaDarkenedStyle.BorderLeft = thin;
                bodySalidaMananaDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaTardeDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaTardeDarkenedStyle.SetFont(fontNormal);
                bodyEntradaTardeDarkenedStyle.FillForegroundColorColor = tarde;
                bodyEntradaTardeDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaTardeDarkenedStyle.BorderTop = thin;
                bodyEntradaTardeDarkenedStyle.BorderBottom = thin;
                bodyEntradaTardeDarkenedStyle.BorderRight = thin;
                bodyEntradaTardeDarkenedStyle.BorderLeft = thicc;
                bodyEntradaTardeDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaTardeDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaTardeDarkenedStyle.SetFont(fontNormal);
                bodySalidaTardeDarkenedStyle.FillForegroundColorColor = tarde;
                bodySalidaTardeDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaTardeDarkenedStyle.BorderTop = thin;
                bodySalidaTardeDarkenedStyle.BorderBottom = thin;
                bodySalidaTardeDarkenedStyle.BorderRight = thicc;
                bodySalidaTardeDarkenedStyle.BorderLeft = thin;
                bodySalidaTardeDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaNocheDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaNocheDarkenedStyle.SetFont(fontNormalWhite);
                bodyEntradaNocheDarkenedStyle.FillForegroundColorColor = noche;
                bodyEntradaNocheDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaNocheDarkenedStyle.BorderTop = thin;
                bodyEntradaNocheDarkenedStyle.BorderBottom = thicc;
                bodyEntradaNocheDarkenedStyle.BorderRight = thin;
                bodyEntradaNocheDarkenedStyle.BorderLeft = thicc;
                bodyEntradaNocheDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaNocheDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaNocheDarkenedStyle.SetFont(fontNormalWhite);
                bodySalidaNocheDarkenedStyle.FillForegroundColorColor = noche;
                bodySalidaNocheDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaNocheDarkenedStyle.BorderTop = thin;
                bodySalidaNocheDarkenedStyle.BorderBottom = thicc;
                bodySalidaNocheDarkenedStyle.BorderRight = thicc;
                bodySalidaNocheDarkenedStyle.BorderLeft = thin;
                bodySalidaNocheDarkenedStyle.Alignment = HorizontalAlignment.Center;

                //Descansos
                XSSFCellStyle bodyEntradaEmptyStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaEmptyStyle.SetFont(fontNormalBluwy);
                bodyEntradaEmptyStyle.FillForegroundColorColor = descansoLight;
                bodyEntradaEmptyStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaEmptyStyle.BorderTop = thin;
                bodyEntradaEmptyStyle.BorderBottom = thin;
                bodyEntradaEmptyStyle.BorderRight = thin;
                bodyEntradaEmptyStyle.BorderLeft = thicc;
                bodyEntradaEmptyStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaEmptyStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaEmptyStyle.SetFont(fontNormalBluwy);
                bodySalidaEmptyStyle.FillForegroundColorColor = descansoLight;
                bodySalidaEmptyStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaEmptyStyle.BorderTop = thin;
                bodySalidaEmptyStyle.BorderBottom = thin;
                bodySalidaEmptyStyle.BorderRight = thicc;
                bodySalidaEmptyStyle.BorderLeft = thin;
                bodySalidaEmptyStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaEmptyDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaEmptyDarkenedStyle.SetFont(fontNormalBluwy);
                bodyEntradaEmptyDarkenedStyle.FillForegroundColorColor = descanso;
                bodyEntradaEmptyDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaEmptyDarkenedStyle.BorderTop = thin;
                bodyEntradaEmptyDarkenedStyle.BorderBottom = thin;
                bodyEntradaEmptyDarkenedStyle.BorderRight = thin;
                bodyEntradaEmptyDarkenedStyle.BorderLeft = thicc;
                bodyEntradaEmptyDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaEmptyDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaEmptyDarkenedStyle.SetFont(fontNormalBluwy);
                bodySalidaEmptyDarkenedStyle.FillForegroundColorColor = descanso;
                bodySalidaEmptyDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaEmptyDarkenedStyle.BorderTop = thin;
                bodySalidaEmptyDarkenedStyle.BorderBottom = thin;
                bodySalidaEmptyDarkenedStyle.BorderRight = thicc;
                bodySalidaEmptyDarkenedStyle.BorderLeft = thin;
                bodySalidaEmptyDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaEmptyFirstStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaEmptyFirstStyle.SetFont(fontNormalBluwy);
                bodyEntradaEmptyFirstStyle.FillForegroundColorColor = descansoLight;
                bodyEntradaEmptyFirstStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaEmptyFirstStyle.BorderTop = thicc;
                bodyEntradaEmptyFirstStyle.BorderBottom = thin;
                bodyEntradaEmptyFirstStyle.BorderRight = thin;
                bodyEntradaEmptyFirstStyle.BorderLeft = thicc;
                bodyEntradaEmptyFirstStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaEmptyFirstStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaEmptyFirstStyle.SetFont(fontNormalBluwy);
                bodySalidaEmptyFirstStyle.FillForegroundColorColor = descansoLight;
                bodySalidaEmptyFirstStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaEmptyFirstStyle.BorderTop = thicc;
                bodySalidaEmptyFirstStyle.BorderBottom = thin;
                bodySalidaEmptyFirstStyle.BorderRight = thicc;
                bodySalidaEmptyFirstStyle.BorderLeft = thin;
                bodySalidaEmptyFirstStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaEmptyFirstDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaEmptyFirstDarkenedStyle.SetFont(fontNormalBluwy);
                bodyEntradaEmptyFirstDarkenedStyle.FillForegroundColorColor = descanso;
                bodyEntradaEmptyFirstDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaEmptyFirstDarkenedStyle.BorderTop = thicc;
                bodyEntradaEmptyFirstDarkenedStyle.BorderBottom = thin;
                bodyEntradaEmptyFirstDarkenedStyle.BorderRight = thin;
                bodyEntradaEmptyFirstDarkenedStyle.BorderLeft = thicc;
                bodyEntradaEmptyFirstDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaEmptyFirstDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaEmptyFirstDarkenedStyle.SetFont(fontNormalBluwy);
                bodySalidaEmptyFirstDarkenedStyle.FillForegroundColorColor = descanso;
                bodySalidaEmptyFirstDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaEmptyFirstDarkenedStyle.BorderTop = thicc;
                bodySalidaEmptyFirstDarkenedStyle.BorderBottom = thin;
                bodySalidaEmptyFirstDarkenedStyle.BorderRight = thicc;
                bodySalidaEmptyFirstDarkenedStyle.BorderLeft = thin;
                bodySalidaEmptyFirstDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaEmptyLastStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaEmptyLastStyle.SetFont(fontNormalBluwy);
                bodyEntradaEmptyLastStyle.FillForegroundColorColor = descansoLight;
                bodyEntradaEmptyLastStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaEmptyLastStyle.BorderTop = thin;
                bodyEntradaEmptyLastStyle.BorderBottom = thicc;
                bodyEntradaEmptyLastStyle.BorderRight = thin;
                bodyEntradaEmptyLastStyle.BorderLeft = thicc;
                bodyEntradaEmptyLastStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaEmptyLastStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaEmptyLastStyle.SetFont(fontNormalBluwy);
                bodySalidaEmptyLastStyle.FillForegroundColorColor = descansoLight;
                bodySalidaEmptyLastStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaEmptyLastStyle.BorderTop = thin;
                bodySalidaEmptyLastStyle.BorderBottom = thicc;
                bodySalidaEmptyLastStyle.BorderRight = thicc;
                bodySalidaEmptyLastStyle.BorderLeft = thin;
                bodySalidaEmptyLastStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodyEntradaEmptyLastDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEntradaEmptyLastDarkenedStyle.SetFont(fontNormalBluwy);
                bodyEntradaEmptyLastDarkenedStyle.FillForegroundColorColor = descanso;
                bodyEntradaEmptyLastDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodyEntradaEmptyLastDarkenedStyle.BorderTop = thin;
                bodyEntradaEmptyLastDarkenedStyle.BorderBottom = thicc;
                bodyEntradaEmptyLastDarkenedStyle.BorderRight = thin;
                bodyEntradaEmptyLastDarkenedStyle.BorderLeft = thicc;
                bodyEntradaEmptyLastDarkenedStyle.Alignment = HorizontalAlignment.Center;

                XSSFCellStyle bodySalidaEmptyLastDarkenedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodySalidaEmptyLastDarkenedStyle.SetFont(fontNormalBluwy);
                bodySalidaEmptyLastDarkenedStyle.FillForegroundColorColor = descanso;
                bodySalidaEmptyLastDarkenedStyle.FillPattern = FillPattern.SolidForeground;
                bodySalidaEmptyLastDarkenedStyle.BorderTop = thin;
                bodySalidaEmptyLastDarkenedStyle.BorderBottom = thicc;
                bodySalidaEmptyLastDarkenedStyle.BorderRight = thicc;
                bodySalidaEmptyLastDarkenedStyle.BorderLeft = thin;
                bodySalidaEmptyLastDarkenedStyle.Alignment = HorizontalAlignment.Center;

                //Hojas
                ISheet sheet = workbook.CreateSheet("Horario");

                //Tamaños de filas y columnas
                ICell cell;
                IRow row, row1, row2, row3, rowE;
                sheet.SetColumnWidth(1, 14 * 256);
                sheet.SetColumnWidth(2, 45 * 256);
                sheet.SetColumnWidth(3, 11 * 256);
                for (int i = 0; i < 14; i++)
                    sheet.SetColumnWidth(4 + i, 10 * 256);

                //Escribir primer cabecera
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 3, 9));
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(1, 1, 10, 17));
                row = sheet.CreateRow(1);
                row.HeightInPoints = 30;
                cell = row.CreateCell(1);
                cell.CellStyle = headerHorarioStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Horario");
                cell = row.CreateCell(2);
                cell.CellStyle = headerDateStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue($"{FomatSpanishDate(start)} - {FomatSpanishDate(end)}");
                cell = row.CreateCell(3);
                cell.CellStyle = headerCompanyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(companyNombre);
                cell = row.CreateCell(10);
                cell.CellStyle = headerCentroStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(centroAlias);
                for (int i = 0; i < 6; i++)
                {
                    cell = row.CreateCell(4 + i);
                    cell.CellStyle = headerCompanyStyle;
                    cell.SetCellType(CellType.String);
                    cell = row.CreateCell(11 + i);
                    cell.CellStyle = headerCentroStyle;
                    cell.SetCellType(CellType.String);
                }
                cell = row.CreateCell(17);  //El centro tiene una celda más
                cell.CellStyle = headerCentroStyle;
                cell.SetCellType(CellType.String);

                //Escribir segunda cabecera
                row1 = sheet.CreateRow(2);
                row2 = sheet.CreateRow(3);
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(2, 2, 1, 2));   //Trabajador
                cell = row1.CreateCell(1);
                cell.CellStyle = headerTrabajadorStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Trabajador");
                cell = row1.CreateCell(2);
                cell.CellStyle = headerTrabajadorStyle;
                cell.SetCellType(CellType.String);
                cell = row2.CreateCell(1);
                cell.CellStyle = headerDniStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("DNI");
                cell = row2.CreateCell(2);
                cell.CellStyle = headerNombreStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Nombre");
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(2, 3, 3, 3));   //Turno
                cell = row1.CreateCell(3);
                cell.CellStyle = headerTurnoStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Turno");
                cell = row2.CreateCell(3);
                cell.CellStyle = headerTurnoStyle;
                cell.SetCellType(CellType.String);
                for (int i = 0; i < 7; i++)
                {
                    bool darken = i % 2 == 1;
                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(2, 2, 4 + (i * 2), 5 + (i * 2)));   //Dia de la semana
                    cell = row1.CreateCell(4 + (i * 2));
                    cell.CellStyle = darken ? headerDiaOscuroStyle : headerDiaClaroStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue(DIAS_SEMANA[i]);
                    cell = row1.CreateCell(5 + (i * 2));
                    cell.CellStyle = darken ? headerDiaOscuroStyle : headerDiaClaroStyle;
                    cell.SetCellType(CellType.String);
                    cell = row2.CreateCell(4 + (i * 2));
                    cell.CellStyle = darken ? headerEntradaOscuroStyle : headerEntradaClaroStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("Entrada");
                    cell = row2.CreateCell(5 + (i * 2));
                    cell.CellStyle = darken ? headerSalidaOscuroStyle : headerSalidaClaroStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("Salida");
                }

                //Escribir los candidatos
                int r = 4, l = lines.Count - 1;
                foreach (ExcelLine line in lines)
                {
                    row1 = sheet.CreateRow(r + 0);
                    row2 = sheet.CreateRow(r + 1);
                    row3 = sheet.CreateRow(r + 2);
                    rowE = sheet.CreateRow(r + 3);

                    rowE.Height = 5 * 20;

                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(r + 0, r + 2, 1, 1));   //DNI
                    cell = row1.CreateCell(1);
                    cell.CellStyle = bodyDniStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue(line.dni);
                    cell = row2.CreateCell(1);
                    cell.CellStyle = bodyDniStyle;
                    cell = row3.CreateCell(1);
                    cell.CellStyle = bodyDniStyle;

                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(r + 0, r + 2, 2, 2));   //Nombre
                    cell = row1.CreateCell(2);
                    cell.CellStyle = bodyNombreStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue(line.name);
                    cell = row2.CreateCell(2);
                    cell.CellStyle = bodyNombreStyle;
                    cell = row3.CreateCell(2);
                    cell.CellStyle = bodyNombreStyle;

                    cell = row1.CreateCell(3);  //Mañana
                    cell.CellStyle = bodyMananaStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("Mañana");
                    cell = row2.CreateCell(3);  //Tarde
                    cell.CellStyle = bodyTardeStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("Tarde");
                    cell = row3.CreateCell(3);  //Noche
                    cell.CellStyle = bodyNocheStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("Noche");

                    int c = 4, d = 0;
                    foreach (Dia dia in line.horario)
                    {
                        bool darkened = d % 2 == 1;

                        cell = row1.CreateCell(c);  //Mañana entrada
                        if (dia.manana != null) cell.CellStyle = darkened ? bodyEntradaMananaDarkenedStyle : bodyEntradaMananaStyle; else cell.CellStyle = darkened ? bodyEntradaEmptyFirstDarkenedStyle : bodyEntradaEmptyFirstStyle;
                        cell.SetCellType(CellType.String);
                        if (dia.manana != null) cell.SetCellValue(dia.manana.entrada.ToString()); else cell.SetCellValue("D");
                        cell = row1.CreateCell(c + 1); //Mañana salida
                        if (dia.manana != null) cell.CellStyle = darkened ? bodySalidaMananaDarkenedStyle : bodySalidaMananaStyle; else cell.CellStyle = darkened ? bodySalidaEmptyFirstDarkenedStyle : bodySalidaEmptyFirstStyle;
                        cell.SetCellType(CellType.String);
                        if (dia.manana != null) cell.SetCellValue(dia.manana.salida.ToString()); else cell.SetCellValue("D");

                        cell = row2.CreateCell(c);  //Tarde entrada
                        if (dia.tarde != null) cell.CellStyle = darkened ? bodyEntradaTardeDarkenedStyle : bodyEntradaTardeStyle; else cell.CellStyle = darkened ? bodyEntradaEmptyDarkenedStyle : bodyEntradaEmptyStyle;
                        cell.SetCellType(CellType.String);
                        if (dia.tarde != null) cell.SetCellValue(dia.tarde.entrada.ToString()); else cell.SetCellValue("D");
                        cell = row2.CreateCell(c + 1); //Tarde salida
                        if (dia.tarde != null) cell.CellStyle = darkened ? bodySalidaTardeDarkenedStyle : bodySalidaTardeStyle; else cell.CellStyle = darkened ? bodySalidaEmptyDarkenedStyle : bodySalidaEmptyStyle;
                        cell.SetCellType(CellType.String);
                        if (dia.tarde != null) cell.SetCellValue(dia.tarde.salida.ToString()); else cell.SetCellValue("D");

                        cell = row3.CreateCell(c);  //Noche entrada
                        if (dia.noche != null) cell.CellStyle = darkened ? bodyEntradaNocheDarkenedStyle : bodyEntradaNocheStyle; else cell.CellStyle = darkened ? bodyEntradaEmptyLastDarkenedStyle : bodyEntradaEmptyLastStyle;
                        cell.SetCellType(CellType.String);
                        if (dia.noche != null) cell.SetCellValue(dia.noche.entrada.ToString()); else cell.SetCellValue("D");
                        cell = row3.CreateCell(c + 1); //Noche salida
                        if (dia.noche != null) cell.CellStyle = darkened ? bodySalidaNocheDarkenedStyle : bodySalidaNocheStyle; else cell.CellStyle = darkened ? bodySalidaEmptyLastDarkenedStyle : bodySalidaEmptyLastStyle;
                        cell.SetCellType(CellType.String);
                        if (dia.noche != null) cell.SetCellValue(dia.noche.salida.ToString()); else cell.SetCellValue("D");

                        c += 2;
                        d++;
                    }

                    r += 4;
                    l--;
                }


                //Guardado
                sheet.ProtectSheet("1234");
                string fileName = "Horario.xlsx";
                string tmpFile = Path.Combine(tmpDir, fileName);
                FileStream file = new FileStream(tmpFile, FileMode.Create);
                workbook.Write(file);
                file.Close();

                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                HttpContext.Response.ContentType = contentType;
                FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(tmpFile), contentType)
                {
                    FileDownloadName = fileName
                };

                Directory.Delete(tmpDir, true);

                return response;
            }
            catch (Exception)
            {
                //Console.WriteLine(e.Message);
                //Console.WriteLine(e.StackTrace);
            }

            return new NoContentResult();
        }

        [HttpGet]
        [Route("without-horario-for-client/{centroId}/{year}/{month}/{day}/")]
        public IActionResult ListCandidatesWithoutHorarioForClient(string centroId, int? year, int? month, int? day)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (centroId == "null") centroId = null;
            DateTime semana = ((year == null || month == null || day == null) ? DateTime.Now : new DateTime(year.Value, month.Value, day.Value)).GetMonday();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn) == null)
                    {
                        return Ok(new { error = "Error 1002, permisos insuficientes" });
                    }

                    string userId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id FROM client_users WHERE token = @TOKEN";
                        command.Parameters.AddWithValue("@TOKEN", clientToken);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                userId = reader.GetString(reader.GetOrdinal("id"));
                    }
                    if (userId == null)
                        return Ok(new { error = "Error 4788, cliente no encontrado" });

                    result = new { error = false, centros = getCandidatesWithoutHorario(userId, centroId, semana, conn, null) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5789, no se ha podido obtener los trabajadores sin horario" };
                }
            }

            return Ok(result);
        }

        //Otros

        [HttpGet]
        [Route("notify-without-horario-for-client/")]
        public async Task<IActionResult> NotifyCandidatesWithoutHorarioForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_HORARIOS, conn, null, out ClientUserAccess access) == null || !access.isSuper)
                        return Ok(new { error = "Error 1002, permisos insuficientes" });

                    List<ClientUser> users = listUsersForClientByIntersection(clientToken, conn).Where(u => u.accessLevel == "Encargado").ToList();
                    await notifyClientsCandidatesWithoutHorario(users, conn);

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5789, no se ha podido obtener los trabajadores sin horario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("notify-without-horario/")]
        public async Task<IActionResult> NotifyCandidatesWithoutHorario()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("Horarios.NotifyCandidatesWithoutHorario", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<ClientUser> users = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT U.* " +
                            "FROM client_users U " +
                            "WHERE U.accessLevel = @LEVEL AND " +
                            "NOT EXISTS(SELECT * FROM client_user_dashboard_restrictions CUDR WHERE CUDR.userId = U.id AND CUDR.type = 'CandidatosSinHorario')";

                        command.Parameters.AddWithValue("@LEVEL", "Encargado");

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new ClientUser
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    username = reader.GetString(reader.GetOrdinal("username")),
                                    email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                    accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                                    activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1
                                });
                            }
                        }
                    }

                    await notifyClientsCandidatesWithoutHorario(users, conn);

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5790, no se han podido notificar a los encargados sobre trajadores sin horario para la semana que viene" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("notify-without-horario-final-warning/")]
        public async Task<IActionResult> NotifyCandidatesWithoutHorarioFinalWarning()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("Horarios.NotifyCandidatesWithoutHorarioFinalWarning", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<ClientUser> users = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT U.* " +
                            "FROM client_users U " +
                            "WHERE U.accessLevel = @LEVEL AND " +
                            "NOT EXISTS(SELECT * FROM client_user_dashboard_restrictions CUDR WHERE CUDR.userId = U.id AND CUDR.type = 'CandidatosSinHorario')";

                        command.Parameters.AddWithValue("@LEVEL", "Master");

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new ClientUser
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    username = reader.GetString(reader.GetOrdinal("username")),
                                    email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                    accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                                    activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1,
                                    clientToken = reader.GetString(reader.GetOrdinal("token"))
                                });
                            }
                        }
                    }

                    await notifyClientsCandidatesWithoutHorario(users, conn, null, true);

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5790, no se han podido notificar a los masters sobre trajadores sin horario para la semana que viene" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------

        public struct ExcelLine
        {
            public string id { get; set; }
            public string dni { get; set; }
            public string name { get; set; }
            public string puesto { get; set; }
            public Horario horario { get; set; }
        }
        public struct CandidatesWithoutHorarios
        {
            public string centroId { get; set; }
            public string centroAlias { get; set; }
            public List<MinimalCandidateData> candidates { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        private async Task setSemana(string centroId, DateTime monday, Semana semana, HashSet<string> afectados, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Borrar las asignaciones y horarios de este centro, esta semana
            deleteSemana(centroId, monday, conn, transaction);

            bool calculateAfectados = afectados == null;
            if (afectados == null)
                afectados = new();

            //Insertar los horarios para cada candidato
            foreach (KeyValuePair<string, Horario> candidate in semana.candidatos)
            {
                if (candidate.Value == null) continue;
                int horarioId;
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO horarios (centroId, semana, horario) output INSERTED.ID VALUES (@CENTRO, @SEMANA, @HORARIO)";
                    command.Parameters.AddWithValue("@CENTRO", centroId);
                    command.Parameters.AddWithValue("@SEMANA", monday);
                    command.Parameters.AddWithValue("@HORARIO", serializeSemana(candidate.Value));
                    horarioId = (int)command.ExecuteScalar();
                }
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO horarios_asignacion (horarioId, candidateId, seen) VALUES (@HORARIO, @CANDIDATO, @SEEN)";
                    command.Parameters.AddWithValue("@HORARIO", horarioId);
                    command.Parameters.AddWithValue("@CANDIDATO", candidate.Key);
                    command.Parameters.AddWithValue("@SEEN", calculateAfectados ? 0 : (afectados.Contains(candidate.Key) ? 0 : 1));
                    command.ExecuteNonQuery();
                }
                if (calculateAfectados)
                    afectados.Add(candidate.Key);
            }

            //Insertar los horarios para grupos
            foreach (Semana.Grupo grupo in semana.grupos)
            {
                if (grupo.horario == null) continue;
                int horarioId;
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO horarios (grupo, centroId, semana, horario) output INSERTED.ID VALUES (@GRUPO, @CENTRO, @SEMANA, @HORARIO)";
                    command.Parameters.AddWithValue("@GRUPO", grupo.nombre);
                    command.Parameters.AddWithValue("@CENTRO", centroId);
                    command.Parameters.AddWithValue("@SEMANA", monday);
                    command.Parameters.AddWithValue("@HORARIO", serializeSemana(grupo.horario));
                    horarioId = (int)command.ExecuteScalar();
                }
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO horarios_asignacion (horarioId, candidateId, seen) VALUES (@HORARIO, @CANDIDATO, @SEEN)";
                    command.Parameters.AddWithValue("@HORARIO", horarioId);
                    command.Parameters.Add("@CANDIDATO", SqlDbType.VarChar);
                    command.Parameters.Add("@SEEN", SqlDbType.Int);
                    foreach (string candidateId in grupo.miembros)
                    {
                        command.Parameters["@CANDIDATO"].Value = candidateId;
                        command.Parameters["@SEEN"].Value = calculateAfectados ? 0 : (afectados.Contains(candidateId) ? 0 : 1);
                        command.ExecuteNonQuery();
                        if (calculateAfectados)
                            afectados.Add(candidateId);
                    }
                }
            }

            await sendEmailsToAffected(afectados, monday, conn, transaction);
        }

        private void deleteSemana(string centroId, DateTime monday, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Borrar asignaciones
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE HA FROM horarios_asignacion HA INNER JOIN horarios H ON(HA.horarioId = H.id) WHERE H.centroId = @CENTRO AND H.semana = @SEMANA";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@SEMANA", monday);
                command.ExecuteNonQuery();
            }

            //Borrar horarios
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM horarios WHERE centroId = @CENTRO AND semana = @SEMANA";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@SEMANA", monday);
                command.ExecuteNonQuery();
            }
        }

        private Semana getSemana(string centroId, DateTime monday, SqlConnection conn, SqlTransaction transaction = null)
        {
            Semana semana = new Semana() { candidatos = new() };

            //Obtener los horarios para candidatos independientes
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT HA.candidateId, H.horario FROM horarios_asignacion HA INNER JOIN horarios H ON(HA.horarioId = H.id) WHERE H.centroId = @CENTRO AND H.semana = @SEMANA AND H.grupo IS NULL";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@SEMANA", monday);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        semana.candidatos[reader.GetString(reader.GetOrdinal("candidateId"))] = parseHorario(reader.GetString(reader.GetOrdinal("horario")), true);
                    }
                }
            }

            //Obtener los horario de grupos
            Dictionary<int, Semana.Grupo> grupos = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT H.id, H.grupo, H.horario FROM horarios H WHERE H.centroId = @CENTRO AND H.semana = @SEMANA AND H.grupo IS NOT NULL";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@SEMANA", monday);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        grupos[reader.GetInt32(reader.GetOrdinal("id"))] = new Semana.Grupo()
                        {
                            nombre = reader.GetString(reader.GetOrdinal("grupo")),
                            horario = parseHorario(reader.GetString(reader.GetOrdinal("horario")), true),
                            miembros = new()
                        };
                    }
                }
            }

            //Obtener los miembros de cada horario de grupo
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT HA.candidateId FROM horarios_asignacion HA WHERE HA.horarioId = @HORARIO";
                command.Parameters.Add("@HORARIO", System.Data.SqlDbType.Int);
                foreach (KeyValuePair<int, Semana.Grupo> grupo in grupos)
                {
                    command.Parameters["@HORARIO"].Value = grupo.Key;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            grupo.Value.miembros.Add(reader.GetString(reader.GetOrdinal("candidateId")));
                        }
                    }
                }
            }

            semana.grupos = grupos.Values.ToList();

            return semana;
        }
        private Dia getTemplate(string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            Dia template = null;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT plantillaHorario FROM centros WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", centroId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        template = reader.IsDBNull(reader.GetOrdinal("plantillaHorario")) ? null : parseDia(reader.GetString(reader.GetOrdinal("plantillaHorario")), true);
                    }
                }
            }

            return template;
        }

        private void setTemplate(string centroId, Dia template, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "UPDATE centros SET plantillaHorario = @TEMPLATE WHERE id = @ID";
                command.Parameters.AddWithValue("@TEMPLATE", serializeDia(template));
                command.Parameters.AddWithValue("@ID", centroId);
                command.ExecuteNonQuery();
            }
        }
        public static Horario getCandidateSemana(string candidateId, DateTime semana, out bool empty, SqlConnection conn, SqlTransaction transaction = null)
        {
            Horario horario = getEmptyHorario();

            DateTime firstDay = semana.Date;
            DateTime lastDay = semana.Date.AddDays(7);

            //Buscar el horario de esa semana
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT H.horario " +
                    "FROM horarios_asignacion HA " +
                    "INNER JOIN horarios H ON(HA.horarioId = H.id) " +
                    "WHERE HA.candidateId = @CANDIDATE AND H.semana =  @SEMANA " +
                    "ORDER BY H.grupo ASC";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                command.Parameters.AddWithValue("@SEMANA", semana);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        horario = parseHorario(reader.GetString(reader.GetOrdinal("horario")), true);
                        empty = false;
                    }
                    else
                    {
                        empty = true;
                    }
                }
            }

            //Buscar bajas
            List<Tuple<DateTime, DateTime>> bajas = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT date, bajaEnd, bajaRevision " +
                    "FROM incidencia_falta_asistencia " +
                    "WHERE candidateId = @CANDIDATE AND " +
                    "baja = 1 AND " +
                    "state = 'aceptada' AND " +
                    "date <= @END AND " +
                    "(bajaEnd >= @START OR bajaRevision >= @START)";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                command.Parameters.AddWithValue("@START", firstDay.Date);
                command.Parameters.AddWithValue("@END", lastDay.Date);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime start = reader.GetDateTime(reader.GetOrdinal("date"));
                        DateTime? end = reader.IsDBNull(reader.GetOrdinal("bajaEnd")) ? null : reader.GetDateTime(reader.GetOrdinal("bajaEnd"));
                        DateTime? revision = reader.IsDBNull(reader.GetOrdinal("bajaRevision")) ? null : reader.GetDateTime(reader.GetOrdinal("bajaRevision"));

                        if (end != null && revision == null)
                            bajas.Add(new(start, end.Value));
                        else if (end == null && revision != null)
                            bajas.Add(new(start, revision.Value));
                        else if (end != null && revision != null)
                            bajas.Add(new(start, end.Value > revision.Value ? end.Value : revision.Value));
                    }
                }
            }

            //Buscar vacaciones
            List<Tuple<DateTime, DateTime>> vacaciones = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT E.vacacionesInicio, E.vacacionesFin " +
                    "FROM incidencia_horas_extra_entrada E " +
                    "INNER JOIN candidatos C ON(C.dni = E.candidateDni) " +
                    "INNER JOIN incidencia_horas_extra I ON(E.incidenceId = I.id) " +
                    "WHERE C.id = @CANDIDATE AND " +
                    //"I.state = 'validada' AND " + //Se validan a final a de mes, asi que es mejor mostrarla aunque no este validada
                    "E.vacacionesInicio IS NOT NULL AND " +
                    "E.vacacionesFin IS NOT NULL AND " +
                    "E.vacacionesInicio <= @END AND " +
                    "E.vacacionesFin >= @START";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                command.Parameters.AddWithValue("@START", firstDay.Date);
                command.Parameters.AddWithValue("@END", lastDay.Date);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        vacaciones.Add(new(reader.GetDateTime(reader.GetOrdinal("vacacionesInicio")), reader.GetDateTime(reader.GetOrdinal("vacacionesFin"))));
            }

            //Aplicar las bajas y vacaciones
            DateTime cDay = firstDay.Date;
            foreach (Dia dia in horario)
            {
                dia.baja = bajas.Any(b => b.Item1 <= cDay && b.Item2 >= cDay);
                dia.vacaciones = vacaciones.Any(b => b.Item1 <= cDay && b.Item2 >= cDay);
                cDay = cDay.AddDays(1);
            }

            //Obtener vacaciones mediante el calendario de vacaciones
            bool[] vacacionesCalendario = CandidateVacacionesController.getCandidateSemana(firstDay.Date, candidateId, conn, transaction);
            if (horario.Count == vacacionesCalendario.Length)
            {
                for (int i = 0; i < horario.Count; i++)
                    if (vacacionesCalendario[i])
                        horario[i].vacaciones = true;
            }

            return horario;
        }

        public static Dia getCandidateDia(string candidateId, DateTime day, out bool empty, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Buscar el primer lunes y anotar el dia de la semana
            int index = 0;
            DateTime firstDay = day.Date;
            while (firstDay.DayOfWeek != DayOfWeek.Monday)
            {
                index++;
                firstDay = firstDay.AddDays(-1);
            }

            Horario semana = getCandidateSemana(candidateId, firstDay, out empty, conn, transaction);

            return semana[index % 7];
        }

        private static List<DateTime> getCandidateDefinedSemanas(string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<DateTime> semanas = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT DISTINCT H.semana " +
                    "FROM horarios H INNER JOIN horarios_asignacion HA ON(HA.horarioId = H.id) " +
                    "WHERE HA.candidateId = @CANDIDATE ";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        semanas.Add(reader.GetDateTime(reader.GetOrdinal("semana")));
                    }
                }
            }

            return semanas;
        }

        private static List<DateTime> getCandidateUnseenSemanas(string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<DateTime> semanas = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT H.semana " +
                    "FROM horarios H " +
                    "INNER JOIN horarios_asignacion HA ON(H.id = HA.horarioId) " +
                    "WHERE HA.candidateId = @CANDIDATE AND HA.seen = 0 ";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        semanas.Add(reader.GetDateTime(reader.GetOrdinal("semana")));
                    }
                }
            }

            return semanas;
        }

        private static bool tryGetCandidateEmailCentroAndSignlink(string candidateId, out string candidateEmail, out string candidateCentroId, out string candidateSignLink, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT centroId, email, lastSignLink FROM candidatos WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", candidateId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        candidateCentroId = reader.GetString(reader.GetOrdinal("centroId"));
                        candidateEmail = reader.GetString(reader.GetOrdinal("email"));
                        candidateSignLink = reader.GetString(reader.GetOrdinal("lastSignLink"));
                        return true;
                    }
                    else
                    {
                        candidateEmail = null;
                        candidateCentroId = null;
                        candidateSignLink = null;
                        return false;
                    }
                }
            }
        }
        public static DateTime getLockDate()
        {
            return DateTime.Now.GetMonday();
        }

        public static Semana parseSemana(JsonElement json)
        {
            Semana semana = new Semana { candidatos = new(), grupos = new() };

            if (json.TryGetProperty("candidatos", out JsonElement candidatos))
            {
                foreach (JsonProperty candidato in candidatos.EnumerateObject())
                {
                    semana.candidatos[candidato.Name] = parseHorario(candidato.Value);
                }
            }
            if (json.TryGetProperty("grupos", out JsonElement grupos))
            {
                foreach (JsonElement group in grupos.EnumerateArray())
                {
                    if (group.TryGetString("nombre", out string nombre)
                        && group.TryGetStringList("miembros", out List<string> miembros)
                        && group.TryGetProperty("horario", out JsonElement horario))
                    {
                        semana.grupos.Add(new Semana.Grupo()
                        {
                            nombre = nombre,
                            miembros = miembros,
                            horario = parseHorario(horario)
                        });
                    }
                }
            }

            return semana;
        }

        public static Horario parseHorario(JsonElement json, bool shortened = false)
        {
            Horario dias = new();

            //Devuelve null si el horario es null
            if (json.ValueKind == JsonValueKind.Null)
                return null;

            try
            {
                int dia = 0;
                //Un dia a null es trasparente
                foreach (JsonElement element in json.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Null)
                    {
                        dias.Add(null);
                    }
                    else
                    {
                        dias.Add(new Dia()
                        {
                            manana = parseTurno(element.GetProperty(shortened ? "m" : "manana"), shortened),
                            tarde = parseTurno(element.GetProperty(shortened ? "t" : "tarde"), shortened),
                            noche = parseTurno(element.GetProperty(shortened ? "n" : "noche"), shortened)
                        });
                    }
                    dia++;
                }
            }
            catch (Exception)
            {
            }

            return dias;
        }
        public static Dia parseDia(JsonElement json, bool shortened = false)
        {
            return new Dia()
            {
                manana = parseTurno(json.GetProperty(shortened ? "m" : "manana"), shortened),
                tarde = parseTurno(json.GetProperty(shortened ? "t" : "tarde"), shortened),
                noche = parseTurno(json.GetProperty(shortened ? "n" : "noche"), shortened)
            };
        }

        public static Turno parseTurno(JsonElement json, bool shortened = false)
        {
            Turno turno = null;

            try
            {
                if (json.ValueKind != JsonValueKind.Null)
                {
                    if (json.TryGetProperty(shortened ? "r" : "responsable", out JsonElement responsable) &&
                        json.TryGetProperty(shortened ? "e" : "entrada", out JsonElement entrada) &&
                        json.TryGetProperty(shortened ? "s" : "salida", out JsonElement salida))
                    {
                        if (entrada.TryGetProperty(shortened ? "h" : "hora", out JsonElement entradaHora) &&
                            entrada.TryGetProperty(shortened ? "m" : "minuto", out JsonElement entradaMinuto) &&
                            salida.TryGetProperty(shortened ? "h" : "hora", out JsonElement salidaHora) &&
                            salida.TryGetProperty(shortened ? "m" : "minuto", out JsonElement salidaMinuto))
                        {

                            turno = new Turno()
                            {
                                responsable = responsable.GetString(),
                                entrada = new Hora() { hora = entradaHora.GetInt32(), minuto = entradaMinuto.GetInt32() },
                                salida = new Hora() { hora = salidaHora.GetInt32(), minuto = salidaMinuto.GetInt32() }
                            };
                        }
                    }
                }
            }
            catch (Exception) { }

            return turno;
        }
        public static Semana parseSemana(string jsonText)
        {
            return parseSemana(JsonDocument.Parse(jsonText).RootElement);
        }

        public static Horario parseHorario(string jsonText, bool shortened = false)
        {
            return parseHorario(JsonDocument.Parse(jsonText).RootElement, shortened);
        }

        public static Dia parseDia(string jsonText, bool shortened = false)
        {
            return parseDia(JsonDocument.Parse(jsonText).RootElement, shortened);
        }
        public static Turno parseTurno(string jsonText, bool shortened = false)
        {
            return parseTurno(JsonDocument.Parse(jsonText).RootElement, shortened);
        }

        public static string serializeSemana(Horario semana)
        {
            object[] semanaJson = new object[semana.Count];
            for (int i = 0; i < semana.Count; i++)
            {
                Dia dia = semana[i];
                if (dia == null)
                {
                    semanaJson[i] = null;
                }
                else
                {
                    semanaJson[i] = new
                    {
                        m = serializeTurno(dia.manana),
                        t = serializeTurno(dia.tarde),
                        n = serializeTurno(dia.noche)
                    };
                }
            }
            return JsonSerializer.Serialize(semanaJson);
        }

        public static object serializeTurno(Turno turno)
        {
            if (turno == null) return null;
            return new
            {
                r = turno.responsable,
                e = new { h = turno.entrada.hora, m = turno.entrada.minuto },
                s = new { h = turno.salida.hora, m = turno.salida.minuto }
            };
        }

        public static string serializeDia(Dia dia)
        {
            return JsonSerializer.Serialize(new
            {
                m = serializeTurno(dia.manana),
                t = serializeTurno(dia.tarde),
                n = serializeTurno(dia.noche)
            });
        }
        public static async Task sendEmailsToAffected(HashSet<string> afectados, DateTime semana, SqlConnection conn, SqlTransaction transaction)
        {
            foreach (string afectado in afectados)
            {
                if (!tryGetCandidateEmailCentroAndSignlink(afectado, out string email, out _, out _, conn, transaction)) continue;
                sendEmailNew(semana, afectado, email, conn, transaction);
            }
            await PushNotificationController.sendNotifications(afectados.Select(candidateId => new PushNotificationController.UID() { type = "ca", id = candidateId }), new()
            {
                title = "Nuevo horario",
                body = $"Semana del {FormatTextualDate(semana)}",
                type = "candidate-horario"
            }, conn, transaction);
        }

        public static void sendEmailNew(DateTime semana, string candidateId, string candidateEmail, SqlConnection conn, SqlTransaction transaction)
        {
            EventMailer.SendEmail(new EventMailer.Email()
            {
                template = "candidateHorarioModified",
                inserts = new() { { "url", AccessController.getAutoLoginUrl("ca", candidateId, null, conn, transaction) }, { "change_text", $"Se te ha asignado un horario nuevo para la semana del {FormatTextualDate(semana)}." } },
                toEmail = candidateEmail,
                subject = "[Think&Job] Nuevo horario",
                priority = EventMailer.EmailPriority.SLOWLANE
            });
        }
        public static Horario getEmptyHorario()
        {
            Horario horario = new();
            for (int i = 0; i < 7; i++)
            {
                horario.Add(new Dia()
                {
                    manana = null,
                    tarde = null,
                    noche = null,
                    baja = false,
                    vacaciones = false
                });
            }
            return horario;
        }

        public static List<ExcelLine> getHorarioCentroSemana(string centroId, DateTime start, string workId, SqlConnection conn)
        {
            List<ExcelLine> lines = new();

            //Obtener los candidatos del centro
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.id, C.dni, TRIM(CONCAT(C.apellidos, ', ', C.nombre)) as name, CA.name as puesto " +
                    "FROM candidatos C " +
                    "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                    "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                    "WHERE C.centroId = @CENTRO AND " +
                    "(@WORK IS NULL OR @WORK = T.id) " +
                    "ORDER BY name, C.dni";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@WORK", (object)workId ?? DBNull.Value);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lines.Add(new ExcelLine
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            puesto = reader.GetString(reader.GetOrdinal("puesto"))
                        });
                    }
                }
            }

            //Obtener el horario de esos candidatos
            for (int i = 0; i < lines.Count; i++)
            {
                ExcelLine line = lines[i];
                line.horario = getCandidateSemana(lines[i].id, start, out _, conn);
                lines[i] = line;
            }

            return lines;
        }
        public static List<CandidatesWithoutHorarios> getCandidatesWithoutHorario(string clientUserId, string centroId, DateTime semana, SqlConnection conn, SqlTransaction transaction = null)
        {
            Dictionary<string, CandidatesWithoutHorarios> centros = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT C.id, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, C.dni, C.email, C.telefono, CE.id as centroId, CE.alias as centroAlias \n" +
                    "FROM candidatos C INNER JOIN centros CE ON(CE.id = C.centroId) \n" +
                    "WHERE (@CENTRO IS NULL OR CE.id = @CENTRO) AND \n" +
                    "(C.fechaFinTrabajo IS NULL OR C.fechaFinTrabajo >= @SEMANA) AND \n" + //No tener en cuenta a los candidatos que dejaron de trabajar antes de esta semana
                    "(@USER IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC WHERE CUC.clientUserId = @USER AND CUC.centroId = CE.id)) AND \n" +
                    "NOT EXISTS(SELECT * FROM horarios_asignacion HA INNER JOIN horarios H ON(HA.horarioId = H.id) WHERE HA.candidateId = C.id AND H.semana = @SEMANA) \n" +
                    "ORDER BY centroAlias, name";
                command.Parameters.AddWithValue("@USER", (object)clientUserId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);
                command.Parameters.AddWithValue("@SEMANA", semana);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string cId = reader.GetString(reader.GetOrdinal("centroId"));
                        if (!centros.ContainsKey(cId))
                        {
                            centros[cId] = new CandidatesWithoutHorarios()
                            {
                                centroId = cId,
                                centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                                candidates = new()
                            };
                        }
                        centros[cId].candidates.Add(new MinimalCandidateData()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            email = reader.GetString(reader.GetOrdinal("email")),
                            phone = reader.GetString(reader.GetOrdinal("telefono"))
                        });
                    }
                }
            }

            return centros.Values.ToList();
        }

        public static async Task notifyClientsCandidatesWithoutHorario(List<ClientUser> users, SqlConnection conn, SqlTransaction transaction = null, bool isFinalWarning = false)
        {
            List<string> usersNotified = new();
            foreach (ClientUser user in users)
            {
                List<CandidatesWithoutHorarios> withoutHorario = getCandidatesWithoutHorario(user.id, null, DateTime.Now.AddDays(7).GetMonday(), conn, transaction);

                if (withoutHorario.Count > 0)
                {
                    usersNotified.Add(user.id);
                    if (user.email == null) continue;

                    int nCandidatos = withoutHorario.Select(c => c.candidates.Count).Sum();
                    int nCentros = withoutHorario.Count;

                    string text = "";
                    if (nCandidatos == 1 && nCentros == 1)
                        text = "Un trabajador no tiene horario para la semana que viene";
                    else if (nCandidatos > 1 && nCentros == 1)
                        text = $"{nCandidatos} trabajadores no tienen horario para la semana que viene.";
                    else if (nCandidatos == 1 && nCentros > 1)
                        text = "Un trabajador, de varios centros, no tiene horario para la semana que viene.";
                    else
                        text = $"{nCandidatos} trabajadores, de varios centros, no tienen horario para la semana que viene.";

                    if (isFinalWarning)
                    {
                        List<ClientUser> subusers = listUsersForClientByIntersection(user.clientToken, conn).Where(u => u.accessLevel == "Encargado").ToList();
                        text += "<br/><br/>";
                        if (subusers.Count == 0)
                        {
                            text += "<b>No hay usuarios que puedan encargarse de estos horarios.</b>";
                        }
                        else
                        {
                            text += "<b>Usuarios que se pueden encargar de estos horarios:</b><ul style='text-align: left;'>";
                            foreach (ClientUser subuser in subusers)
                            {
                                text += $"<li>{subuser.username}</li>";
                            }
                            text += "</ul>";
                        }
                        text += "<br/>";
                    }

                    EventMailer.SendEmail(new EventMailer.Email()
                    {
                        template = "candidatesWithoutHorarioNotice",
                        inserts = new() { { "text", text }, { "url", AccessController.getAutoLoginUrl("ca", user.id, null, conn, null) } },
                        toEmail = user.email,
                        toName = user.username,
                        subject = "[Think&Job] Candidatos sin horario",
                        priority = EventMailer.EmailPriority.MODERATE
                    });
                }
            }
            await PushNotificationController.sendNotifications(usersNotified.Select(id => new PushNotificationController.UID() { type = "cl", id = id }), new()
            {
                title = "Trabajadores sin turnos",
                body = "Hay trabajadores sin turnos para la semana que viene",
                type = "client-candidatos-sin-horario"
            }, conn, transaction);
        }


        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
