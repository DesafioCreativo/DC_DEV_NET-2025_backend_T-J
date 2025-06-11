using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.Constants;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.ClientUserController;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    [Route("api/v1/candidate-vacaciones")]
    [ApiController]
    [Authorize]
    public class CandidateVacacionesController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpGet]
        [Route(template: "get-mes-candidate/{ano}/{mes}/{candidateId}/")]
        public IActionResult GetMesCandidate(int ano, int mes, string candidateId)
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
                    string centroId = getCentroIdByCandidateId(candidateId, conn);
                    if (centroId == null || ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_VACACIONES) == null)
                        return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                    result = new { error = false, empty = !tryGetMes(ano, mes, candidateId, centroId, out VacacionesType[] vacaciones, conn), vacaciones };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5381, No se han podido obtener las vacaciones del trabajador" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-mes-centro/{ano}/{mes}/{centroId}/")]
        public IActionResult GetMesCentro(int ano, int mes, string centroId)
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
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_VACACIONES) == null)
                        return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                    result = new { error = false, empty = !tryGetMes(ano, mes, null, centroId, out VacacionesType[] vacaciones, conn), vacaciones };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5382, No se han podido obtener las vacaciones del centro" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-centro-for-set/{ano}/{mes}/{centroId}/")]
        public IActionResult GetCentroForSet(int ano, int mes, string centroId)
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

                    if (centroId == null || ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_VACACIONES) == null)
                        return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });

                    //Obtener todos los candidatos del centro
                    List<string> candidates = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id FROM candidatos WHERE centroId = @CENTRO";
                        command.Parameters.AddWithValue("@CENTRO", centroId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                candidates.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }
                    }

                    //Obtener provincia y localidad del centro
                    Dictionary<int, FestivoType[]> festivos = new();
                    string provincia = null, localidad = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT C.poblacion, C.provincia FROM centros C WHERE C.id = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                provincia = reader.GetString(reader.GetOrdinal("provincia"));
                                localidad = reader.GetString(reader.GetOrdinal("poblacion"));
                            }
                        }
                    }

                    //Cargar las vacaciones del año actual + el ultimo mes del año anterior + el primer mes del año siguiente
                    List<DateTime> months = new List<DateTime>();

                    for (int i = 1; i <= 12; i++)
                        months.Add(new DateTime(ano, i, 1));
                    months.Add(new DateTime(ano - 1, 12, 1));
                    months.Add(new DateTime(ano + 1, 1, 1));

                    Dictionary<int, Dictionary<int, Dictionary<string, object>>> definition = new();
                    foreach (DateTime date in months)
                    {
                        int year = date.Year;
                        int month = date.Month;
                        if (!definition.ContainsKey(year))
                            definition[year] = new();
                        if (definition[year].ContainsKey(month))
                            continue; //No deberia ocurrir porque la combinacion ano, mes es unica

                        definition[year][month] = new() { { "candidato", new Dictionary<string, VacacionesType[]>() } };

                        foreach (string candidate in candidates)
                        {
                            tryGetMes(year, month, candidate, centroId, out VacacionesType[] mesCandidate, conn);
                            ((Dictionary<string, VacacionesType[]>)definition[year][month]["candidato"])[candidate] = mesCandidate;
                        }

                        tryGetMes(year, month, null, centroId, out VacacionesType[] mesCenter, conn);
                        definition[year][month]["centro"] = mesCenter;

                        if (!festivos.ContainsKey(year))
                        {
                            festivos[year] = getFestivos(year, provincia, localidad, conn);
                        }

                        int first = new DateTime(year, month, 1).DayOfYear;
                        int last = new DateTime(year, month, 1).AddMonths(1).AddSeconds(-1).DayOfYear;
                        definition[year][month]["festivos"] = festivos[year].Skip(first - 1).Take(last - first + 1);
                    }

                    result = new { error = false, definition };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5387, No se han podido obtener las vacaciones del centro" };
            }

            return Ok(result);
        }

        // Establecimiento
        [HttpPost]
        [Route(template: "set-meses/")]
        public async Task<IActionResult> SetMeses()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            Dictionary<string, string> centroOfCandidate = new();
            HashSet<string> needsAccessCentros = new();
            List<VacacionesDefinitionLine> lines = new();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        //Parsear los candidatos y el centro, y buscar los centros a los que pertenecen los candidatos
                        if (json.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement itemJson in json.EnumerateArray())
                            {
                                if (itemJson.TryGetInt32("ano", out int ano) &&
                                    itemJson.TryGetInt32("mes", out int mes) &&
                                    itemJson.TryGetProperty("vacaciones", out JsonElement vacacionesJson))
                                {
                                    VacacionesType[] vacaciones = parseVacaciones(vacacionesJson);
                                    if (vacaciones.Length != DateTime.DaysInMonth(ano, mes)) continue;
                                    if (!itemJson.TryGetString("candidateId", out string candidateId))
                                        candidateId = null;
                                    if (!itemJson.TryGetString("centroId", out string centroId))
                                        centroId = null;
                                    if (centroId == null) continue;
                                    if (centroId != null)
                                    {
                                        needsAccessCentros.Add(centroId);
                                    }
                                    if (candidateId != null)
                                    {
                                        if (!centroOfCandidate.ContainsKey(candidateId))
                                        {
                                            centroId = getCentroIdByCandidateId(candidateId, conn, transaction);
                                            if (centroId != null)
                                                centroOfCandidate[candidateId] = centroId;
                                        }
                                        if (!centroOfCandidate.ContainsKey(candidateId)) continue;
                                        needsAccessCentros.Add(centroOfCandidate[candidateId]);
                                    }
                                    if (centroId == null)
                                        continue;
                                    lines.Add(new()
                                    {
                                        candidateId = candidateId,
                                        centroId = centroId,
                                        ano = ano,
                                        mes = mes,
                                        vacaciones = vacaciones
                                    });
                                }
                            }
                        }
                        //Comprobar los permisos del usuario
                        if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_VACACIONES, conn, transaction, out ClientUserAccess access) == null)
                            return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });

                        if (needsAccessCentros.Any(cId => !access.empresas.Any(e => e.centros.Any(c => c.id == cId))))
                            return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });

                        //Establecer todas las lineas recibidas
                        foreach (VacacionesDefinitionLine line in lines)
                        {
                            setMes(line.ano, line.mes, line.candidateId, line.centroId, line.vacaciones, conn, transaction);
                        }

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5383, No se han podido establecer las vacaciones" };
                    }
                }
            }

            return Ok(result);
        }

        // Obtencion de vacaciones semanalmente
        [HttpGet]
        [Route(template: "get-semana-candidate/{ano}/{mes}/{dia}/{candidateId}/")]
        public IActionResult GetSemanaCandidate(int ano, int mes, int dia, string candidateId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            try
            {
                DateTime monday = new DateTime(ano, mes, dia).GetMonday();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string centroId = getCentroIdByCandidateId(candidateId, conn);
                    if (centroId == null || ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_VACACIONES) == null)
                        return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                    result = new { error = false, vacaciones = getCandidateSemana(monday, candidateId, conn) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5384, No se han podido obtener las vacaciones semanales del trabajador" };
            }
            return Ok(result);
        }



        [HttpGet]
        [Route(template: "api/v1/candidate-vacaciones/get-semana-centro/{ano}/{mes}/{dia}/{centroId}/")]
        public IActionResult GetSemanaCentro(int ano, int mes, int dia, string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            try
            {
                DateTime monday = new DateTime(ano, mes, dia).GetMonday();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_VACACIONES) == null)
                        return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });

                    result = new { error = false, vacaciones = getCentroSemana(monday, centroId, conn) };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5385, No se han podido obtener las vacaciones semanales del centro" };
            }
            return Ok(result);
        }




        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        // Ayuda
        public struct VacacionesDefinitionLine
        {
            public string candidateId { get; set; }
            public string centroId { get; set; }
            public int ano { get; set; }
            public int mes { get; set; }
            public VacacionesType[] vacaciones { get; set; }
        }
        public enum VacacionesType
        {
            No,
            No_Transparent,
            Yes
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static bool tryGetMes(int ano, int mes, string candidateId, string centroId, out VacacionesType[] vacaciones, SqlConnection conn, SqlTransaction transaction = null)
        {
            bool empty;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT vacaciones " +
                    "FROM candidate_vacaciones WHERE " +
                    "ano = @ANO AND mes = @MES AND centroId = @CENTRO AND " +
                    "((@CANDIDATE IS NULL AND candidateId IS NULL) OR candidateId = @CANDIDATE)";
                command.Parameters.AddWithValue("@ANO", ano);
                command.Parameters.AddWithValue("@MES", mes);
                command.Parameters.AddWithValue("@CANDIDATE", (object)candidateId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CENTRO", centroId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        vacaciones = decodeVacaciones(reader.GetString(reader.GetOrdinal("vacaciones")));
                        empty = false;
                    }
                    else
                    {
                        vacaciones = candidateId == null ? newEmptyMonth(DateTime.DaysInMonth(ano, mes)) : newTransparentMonth(DateTime.DaysInMonth(ano, mes));
                        empty = true;
                    }
                }
            }

            return !empty;
        }
        public static void setMes(int ano, int mes, string candidateId, string centroId, VacacionesType[] vacaciones, SqlConnection conn, SqlTransaction transaction = null)
        {
            //SI falta el centro, asumir que se refieren al actual del candiato
            if (centroId == null && candidateId != null)
                centroId = getCentroIdByCandidateId(candidateId, conn, transaction);

            //Borrar el mes anterior, si existe
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "DELETE FROM candidate_vacaciones WHERE ano = @ANO AND mes = @MES AND centroId = @CENTRO AND ((@CANDIDATE IS NULL AND candidateId IS NULL) OR candidateId = @CANDIDATE)";
                command.Parameters.AddWithValue("@ANO", ano);
                command.Parameters.AddWithValue("@MES", mes);
                command.Parameters.AddWithValue("@CANDIDATE", (object)candidateId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.ExecuteNonQuery();
            }

            //Establecer el nuevo mes
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "INSERT INTO candidate_vacaciones (ano, mes, candidateId, centroId, vacaciones) " +
                    "VALUES (@ANO, @MES, @CANDIDATE, @CENTRO, @VACACIONES)";
                command.Parameters.AddWithValue("@ANO", ano);
                command.Parameters.AddWithValue("@MES", mes);
                command.Parameters.AddWithValue("@CANDIDATE", (object)candidateId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@VACACIONES", encodeVacaciones(vacaciones));
                command.ExecuteNonQuery();
            }
        }
        public static string encodeVacaciones(VacacionesType[] arr)
        {
            return string.Join("", arr.Select(v => v == VacacionesType.Yes ? "2" : (v == VacacionesType.No_Transparent ? "1" : "0")));
        }

        public static VacacionesType[] decodeVacaciones(string str)
        {
            return str.Select(c => c == '2' ? VacacionesType.Yes : (c == '1' ? VacacionesType.No_Transparent : VacacionesType.No)).ToArray();
        }
        public static string encodeAceptadas(bool[] arr)
        {
            return string.Join("", arr.Select(v => v ? "1" : "0"));
        }
        public static bool[] decodeAceptadas(string str)
        {
            return str.Select(c => c == '1').ToArray();
        }
        public static bool[] mixVacaciones(VacacionesType[] arr1, VacacionesType[] arr2)
        {
            if (arr1.Length != arr2.Length)
                throw new Exception("No se pueden mezclar vacaciones de meses distintos.");

            bool[] result = new bool[arr1.Length];

            for (int i = 0; i < arr1.Length; i++)
                result[i] = arr1[i] == VacacionesType.Yes || (arr1[i] == VacacionesType.No_Transparent && arr2[i] == VacacionesType.Yes);

            return result;
        }
        public static bool[] mixVacaciones(VacacionesType[] arr)
        {
            return arr.Select(c => c == VacacionesType.Yes).ToArray();
        }


        public static VacacionesType[] parseVacaciones(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                VacacionesType[] vacaciones = new VacacionesType[json.GetArrayLength()];
                int i = 0;
                foreach (JsonElement dayJson in json.EnumerateArray())
                {
                    int value = dayJson.GetInt32();
                    vacaciones[i++] = value == 2 ? VacacionesType.Yes : (value == 1 ? VacacionesType.No_Transparent : VacacionesType.No);
                }
                return vacaciones;
            }
            throw new Exception("Se esperaba un array");
        }        
        public static VacacionesType[] newEmptyMonth(int nDays)
        {
            VacacionesType[] month = new VacacionesType[nDays];
            Array.Fill(month, VacacionesType.No);
            return month;
        }
        public static VacacionesType[] newTransparentMonth(int nDays)
        {
            VacacionesType[] month = new VacacionesType[nDays];
            Array.Fill(month, VacacionesType.No_Transparent);
            return month;
        }
                

        public static bool[] getCandidateMes(int ano, int mes, string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Obtener el centro del candidato
            string centroId = getCentroIdByCandidateId(candidateId, conn, transaction);

            //Intentar obtener las vacaciones expresas
            tryGetMes(ano, mes, candidateId, centroId, out VacacionesType[] vacacionesCandidato, conn, transaction);

            //Obtener las vacaciones de su centro
            tryGetMes(ano, mes, null, centroId, out VacacionesType[] vacacionesCentro, conn, transaction);

            //Mixear las vacaciones
            return mixVacaciones(vacacionesCandidato, vacacionesCentro);
        }
        public static bool[] getCandidateSemana(DateTime semana, string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            bool[] semanaVacaciones = new bool[7];

            DateTime dia = semana.GetMonday();
            Dictionary<int, Dictionary<int, bool[]>> meses = new();
            for (int diaIndex = 0; diaIndex < 7; diaIndex++)
            {
                if (!meses.ContainsKey(dia.Year))
                {
                    meses[dia.Year] = new();
                }
                if (!meses[dia.Year].ContainsKey(dia.Month))
                {
                    meses[dia.Year][dia.Month] = getCandidateMes(dia.Year, dia.Month, candidateId, conn, transaction);
                }
                semanaVacaciones[diaIndex] = meses[dia.Year][dia.Month][dia.Day - 1];
                dia = dia.AddDays(1);
            }

            return semanaVacaciones;
        }
        public static bool[] getCentroSemana(DateTime semana, string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            bool[] semanaVacaciones = new bool[7];

            DateTime dia = semana.GetMonday();
            Dictionary<int, Dictionary<int, bool[]>> meses = new();
            for (int diaIndex = 0; diaIndex < 7; diaIndex++)
            {
                if (!meses.ContainsKey(dia.Year))
                {
                    meses[dia.Year] = new();
                }
                if (!meses[dia.Year].ContainsKey(dia.Month))
                {
                    tryGetMes(dia.Year, dia.Month, null, centroId, out VacacionesType[] mes, conn, transaction);
                    meses[dia.Year][dia.Month] = mixVacaciones(mes);
                }
                semanaVacaciones[diaIndex] = meses[dia.Year][dia.Month][dia.Day - 1];
                dia = dia.AddDays(1);
            }

            return semanaVacaciones;
        }

        public static List<Tuple<DateTime, DateTime>> getVacacionesNotAccepted(string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            DateTime firstMonth = DateTime.Now.AddMonths(-6);
            DateTime firstDate = new DateTime(firstMonth.Year, firstMonth.Month, 1);
            DateTime date = firstDate.Date;
            List<bool> notRevised = new();
            for (int i = 0; i < 12; i++)
            {
                bool[] vacaciones = getCandidateMes(date.Year, date.Month, candidateId, conn, transaction);

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "SELECT aceptadas " +
                        "FROM candidate_vacaciones_aceptadas WHERE " +
                        "ano = @ANO AND mes = @MES AND " +
                        "candidateId = @CANDIDATE";
                    command.Parameters.AddWithValue("@ANO", date.Year);
                    command.Parameters.AddWithValue("@MES", date.Month);
                    command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            bool[] aceptadas = decodeAceptadas(reader.GetString(reader.GetOrdinal("aceptadas")));
                            notRevised.AddRange(vacaciones.Select((v, i) => v && !aceptadas[i]));
                        }
                        else
                        {
                            notRevised.AddRange(vacaciones);
                        }
                    }
                }

                date = date.AddMonths(1);
            }

            return monthsToIntervals(notRevised, firstDate);
        }
        public static void setVacacionesAccepted(string candidateId, List<Tuple<DateTime, DateTime>> intervals, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Analizar intervalos, generar los meses con trues en el intervalo
            Dictionary<int, Dictionary<int, bool[]>> meses = intervalsToMonths(intervals);

            //Buscar esos meses y fusionarlos si ya existe. Reemplazar si ya existe
            foreach (KeyValuePair<int, Dictionary<int, bool[]>> kvYear in meses)
            {
                int year = kvYear.Key;
                foreach (KeyValuePair<int, bool[]> kvMonth in kvYear.Value)
                {
                    int month = kvMonth.Key;
                    bool[] aceptadas = kvMonth.Value;

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText =
                            "SELECT aceptadas " +
                            "FROM candidate_vacaciones_aceptadas WHERE " +
                            "ano = @ANO AND mes = @MES AND " +
                            "candidateId = @CANDIDATE";
                        command.Parameters.AddWithValue("@ANO", year);
                        command.Parameters.AddWithValue("@MES", month);
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                bool[] yaAceptadas = decodeAceptadas(reader.GetString(reader.GetOrdinal("aceptadas")));
                                aceptadas = aceptadas.Select((v, i) => v || yaAceptadas[i]).ToArray();
                            }
                        }
                    }

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText =
                            "DELETE FROM candidate_vacaciones_aceptadas WHERE ano = @ANO AND mes = @MES AND candidateId = @CANDIDATE";
                        command.Parameters.AddWithValue("@ANO", year);
                        command.Parameters.AddWithValue("@MES", month);
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText =
                            "INSERT INTO candidate_vacaciones_aceptadas (candidateId, ano, mes, aceptadas) VALUES (@CANDIDATE, @ANO, @MES, @ACEPTADAS)";
                        command.Parameters.AddWithValue("@ANO", year);
                        command.Parameters.AddWithValue("@MES", month);
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        command.Parameters.AddWithValue("@ACEPTADAS", encodeAceptadas(aceptadas));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
        public static void unsetVacacionesReyected(string candidateId, List<Tuple<DateTime, DateTime>> intervals, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Obtener el centro del candidato
            string centroId = getCentroIdByCandidateId(candidateId, conn, transaction);

            //Analizar intervalos, generar los meses con trues en el intervalo
            Dictionary<int, Dictionary<int, bool[]>> mesesToErase = intervalsToMonths(intervals);

            //Buscar esos meses y fusionarlos si ya existe. Reemplazar si ya existe
            foreach (KeyValuePair<int, Dictionary<int, bool[]>> kvYear in mesesToErase)
            {
                int year = kvYear.Key;
                foreach (KeyValuePair<int, bool[]> kvMonth in kvYear.Value)
                {
                    int month = kvMonth.Key;
                    bool[] rechazadas = kvMonth.Value;

                    //Borrar las vacaciones aceptadas del calendario de vacaciones del candidato.
                    tryGetMes(year, month, candidateId, centroId, out VacacionesType[] vacaciones, conn, transaction);
                    vacaciones = vacaciones.Select((v, i) => rechazadas[i] ? VacacionesType.No : v).ToArray();
                    setMes(year, month, candidateId, null, vacaciones, conn, transaction);

                    //Borrar las vacaciones rechazadas de las aceptadas
                    bool[] aceptadas = null;

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText =
                            "SELECT aceptadas " +
                            "FROM candidate_vacaciones_aceptadas WHERE " +
                            "ano = @ANO AND mes = @MES AND " +
                            "candidateId = @CANDIDATE";
                        command.Parameters.AddWithValue("@ANO", year);
                        command.Parameters.AddWithValue("@MES", month);
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                aceptadas = decodeAceptadas(reader.GetString(reader.GetOrdinal("aceptadas")));
                                aceptadas = aceptadas.Select((v, i) => v && !rechazadas[i]).ToArray();
                            }
                        }
                    }

                    if (aceptadas == null) continue;

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText =
                            "DELETE FROM candidate_vacaciones_aceptadas WHERE ano = @ANO AND mes = @MES AND candidateId = @CANDIDATE";
                        command.Parameters.AddWithValue("@ANO", year);
                        command.Parameters.AddWithValue("@MES", month);
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText =
                            "INSERT INTO candidate_vacaciones_aceptadas (candidateId, ano, mes, aceptadas) VALUES (@CANDIDATE, @ANO, @MES, @ACEPTADAS)";
                        command.Parameters.AddWithValue("@ANO", year);
                        command.Parameters.AddWithValue("@MES", month);
                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                        command.Parameters.AddWithValue("@ACEPTADAS", encodeAceptadas(aceptadas));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private static Dictionary<int, Dictionary<int, bool[]>> intervalsToMonths(List<Tuple<DateTime, DateTime>> intervals)
        {
            Dictionary<int, Dictionary<int, bool[]>> meses = new();
            foreach (Tuple<DateTime, DateTime> interval in intervals)
            {
                if (interval.Item2 < interval.Item1) continue; //Ignorar si el intervalo es infinito
                DateTime date = interval.Item1.Date;
                while (date <= interval.Item2)
                {
                    if (!meses.ContainsKey(date.Year))
                        meses[date.Year] = new();
                    if (!meses[date.Year].ContainsKey(date.Month))
                        meses[date.Year][date.Month] = new bool[DateTime.DaysInMonth(date.Year, date.Month)];
                    meses[date.Year][date.Month][date.Day - 1] = true;
                    date = date.AddDays(1);
                }
            }
            return meses;
        }
        private static List<Tuple<DateTime, DateTime>> monthsToIntervals(List<bool> notRevised, DateTime firstDate)
        {
            List<Tuple<DateTime, DateTime>> intervals = new();

            DateTime date = firstDate.Date;
            bool openInterval = false;
            DateTime intervalStart = date.Date;
            DateTime intervalEnd = date.Date;
            foreach (bool day in notRevised)
            {
                if (day)
                {
                    //El dia es festivo sin revisar
                    if (openInterval)
                    {
                        //Hay un intervalo habierto -> Actualizar el cierre
                        intervalEnd = date.Date;
                    }
                    else
                    {
                        //No hay intervalo habierto -> Abrir intervalo
                        intervalStart = date.Date;
                        intervalEnd = date.Date;
                        openInterval = true;
                    }
                }
                else
                {
                    //El dia no es festivo sin revisar
                    if (openInterval)
                    {
                        //Hay un intervalo habierto -> Agregar el actual a la lista y cerrarlo
                        intervals.Add(new(intervalStart, intervalEnd));
                        openInterval = false;
                    }
                }
                date = date.AddDays(1);
            }

            if (openInterval)
            {
                intervals.Add(new(intervalStart, intervalEnd));
            }

            return intervals;
        }

        public static string getCentroIdByCandidateId(string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            string centroId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT centroId FROM candidatos WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", candidateId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        centroId = reader.GetString(reader.GetOrdinal("centroId"));
                    }
                }
            }
            return centroId;
        }


        //------------------------------------------FUNCIONES FIN---------------------------------------------

    }
}
