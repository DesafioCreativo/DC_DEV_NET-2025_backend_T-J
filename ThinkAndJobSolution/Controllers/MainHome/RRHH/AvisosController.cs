using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers._Model.Horario;
using ThinkAndJobSolution.Controllers.Candidate;
using ThinkAndJobSolution.Controllers.MainHome.Sysadmin;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.RRHH
{
    [Route("api/v1/avisos")]
    [ApiController]
    [Authorize]
    public class AvisosController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        #region Asignaciones y tipos

        [HttpGet]
        [Route(template: "get-types/")]
        public IActionResult GetTypes()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Avisos.GetTypes", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            return Ok(new { error = false, avisos = AVISOS });
        }

        [HttpGet]
        [Route(template: "get-names/")]
        public IActionResult GetNames()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Avisos.GetNames", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            return Ok(new { error = false, names = extractNames(AVISOS) });
        }

        [HttpPost]
        [Route(template: "get-types-filtered/")]
        public async Task<IActionResult> GetTypesFiltered()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5930, no se han podido obtener los avisos del usuario." };

            if (!HasPermission("Avisos.GetTypesFiltered", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            List<string> companies = GetJsonStringList(json);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);

                    Dictionary<string, List<TipoAviso>> avisos = getAssigned(userId, companies, conn);
                    Dictionary<string, List<TipoAviso>> avisosFiltered = new();
                    foreach (string company in companies)
                    {
                        if (company == "any")
                            avisosFiltered[company] = mergeAssignTypes(avisos);
                        else
                            avisosFiltered[company] = mergeAssignTypes(new() { { "any", new List<TipoAviso>(avisos["any"]) }, { company, new List<TipoAviso>(avisos[company]) } });
                        removeNotAssignTypes(avisosFiltered[company]);
                    }
                    result = new { error = false, avisos = avisosFiltered };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "get-assigned/{userId}/")]
        public async Task<IActionResult> GetAssigned(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5930, no se han podido obtener los avisos del usuario." };

            if (!HasPermission("Avisos.GetAssigned", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            List<string> companies = GetJsonStringList(json);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new { error = false, avisos = getAssigned(userId, companies, conn) };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "set-assigned/{userId}/")]
        public async Task<IActionResult> SetAssigned(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5931, no se han podido establecer los avisos del usuario." };

            if (!HasPermission("Avisos.SetAssigned", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        Dictionary<string, List<TipoAviso>> avisos = parseAssigned(json);
                        setAssigned(userId, avisos, conn, transaction);

                        result = new { error = false };
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                    }
                }
            }

            return Ok(result);
        }

        #endregion

        #region Avisos

        [HttpPost]
        [Route(template: "list/")]
        public async Task<IActionResult> ListAvisos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5934, no se han podido listar los avisos." };

            ResultadoAcceso access = HasPermission("Avisos.ListAvisos", securityToken);
            if (!access.Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            bool? hidden = null;
            List<string> types = null;
            string companyId = null;

            if (json.TryGetProperty("hidden", out JsonElement hiddenJson))
                hidden = GetJsonBool(hiddenJson);
            if (json.TryGetProperty("types", out JsonElement typesJson))
                if (typesJson.ValueKind == JsonValueKind.Array)
                    types = GetJsonStringList(typesJson);
            if (json.TryGetProperty("companyId", out JsonElement companyIdJson))
                companyId = companyIdJson.GetString();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);
                    string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);
                    //Si se usa la excepción de las guardias
                    result = new { error = false, avisos = listAvisos(userId, filterSecurityToken != null, types, hidden, companyId, conn) };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "count/")]
        public IActionResult CountAvisos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5934, no se han podido listar los avisos." };

            ResultadoAcceso access = HasPermission("Avisos.CountAvisos", securityToken);
            if (!access.Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);
                    string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);
                    //Si se usa la excepción de las guardias
                    result = new { error = false, avisos = countAvisos(userId, filterSecurityToken != null, conn) };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get/{avisoId}/")]
        public IActionResult GetAviso(string avisoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5935, no se han podido obtener el aviso." };

            if (!HasPermission("Avisos.GetAviso", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string username = FindUsernameBySecurityToken(securityToken, conn);
                    Aviso? aviso = getAviso(avisoId, conn, null, username);

                    if (aviso == null)
                        result = new { error = "Error 4932, No se ha encontrado el aviso." };
                    else
                        result = new { error = false, aviso = aviso.Value };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "dashboard/")]
        public IActionResult GetDashboard()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5936, no se han podido obtener un resumen de los avisos." };

            ResultadoAcceso access = HasPermission("Avisos.ListAvisos", securityToken);
            if (!access.Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);
                    string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);
                    //Si se usa la excepción de las guardias
                    result = new { error = false, dashboard = getDashboard(userId, filterSecurityToken != null, conn) };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "change-avisos-hidden/")]
        public async Task<IActionResult> ChangeAvisosHidden()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5937, no se han podido cambiar el estado." };

            if (!HasPermission("Avisos.ChangeAvisosHidden", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            bool hidden = false;
            List<string> ids = null;

            if (json.TryGetProperty("hidden", out JsonElement hiddenJson))
                hidden = GetJsonBool(hiddenJson) ?? false;
            if (json.TryGetProperty("ids", out JsonElement typesJson))
                if (typesJson.ValueKind == JsonValueKind.Array)
                    ids = GetJsonStringList(typesJson);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE avisos SET hidden = @HIDDEN WHERE id = @ID";
                        command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                        command.Parameters.AddWithValue("@HIDDEN", hidden ? 1 : 0);

                        foreach (string id in ids)
                        {
                            command.Parameters["@ID"].Value = id;
                            command.ExecuteNonQuery();
                        }
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        #endregion

        [HttpDelete]
        [Route(template: "delete/{avisoId}/")]
        public IActionResult DeleteAviso(string avisoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5938, no se han podido eliminar el aviso." };

            if (!HasPermission("Avisos.DeleteAviso", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM avisos WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", avisoId);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "change-state/{avisoId}/")]
        public async Task<IActionResult> ChangeState(string avisoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5939, no se ha podido cambiar el estado del aviso." };

            if (!HasPermission("Avisos.ChangeState", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            string state = null;

            if (json.TryGetProperty("state", out JsonElement stateJson))
                state = stateJson.GetString();

            if (state == null)
                return Ok(new { error = "Error 4939, Estado incorrecto o nulo." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE avisos SET state = @STATE WHERE id = @ID";
                        command.Parameters.AddWithValue("@STATE", state);
                        command.Parameters.AddWithValue("@ID", avisoId);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                }
            }

            return Ok(result);
        }

        #region Generacion de avisos

        [HttpGet]
        [Route(template: "generate/")]
        public IActionResult GenerarAvisos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 5935, no se han podido generar los avisos." };

            if (!HasPermission("Avisos.GenerarAvisos", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    DateTime day = DateTime.Now.Date;
                    List<Aviso> avisos = new();

                    //Detectar avisos
                    detectarAvisosBajasYSustitucion(avisos, day, conn);
                    detectarAvisosCuentaBancaria(avisos, day, conn);
                    detectarAvisosPermisoDeTrabajo(avisos, day, conn);
                    detectarAvisosAbsentismo(avisos, day, conn);
                    detectarAvisosIncidenciasAbiertas(avisos, day, conn);
                    detectarAvisosContratosFirmados(avisos, day, conn);
                    detectarAvisosModelo145(avisos, day, conn);

                    //Insertar avisos
                    foreach (Aviso aviso in avisos)
                        insertAviso(aviso, conn);

                    //Purgar avisos ocultos de hace un año
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM avisos WHERE hidden = 1 AND date < @DATE";
                        command.Parameters.AddWithValue("@DATE", day.AddYears(-1));
                        command.ExecuteNonQuery();
                    }

                    //Contar las notificaciones de todos los RRHH que tengan permiso de notificaciones y al menos una notificacion activada. Enviarle el numero por correo
                    List<UserCredentialsController.SystemUser> users = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT U.id, CONCAT(U.name, ' ', U.surname) as fullName, U.email, U.securityToken " +
                            "FROM users U " +
                            "WHERE EXISTS(SELECT * FROM user_avisos UA WHERE UA.userId = U.id)";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new UserCredentialsController.SystemUser()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    name = reader.GetString(reader.GetOrdinal("fullName")),
                                    email = reader.GetString(reader.GetOrdinal("email")),
                                    securityToken = reader.GetString(reader.GetOrdinal("securityToken"))
                                });
                            }
                        }
                    }

                    foreach (var user in users)
                    {
                        ResultadoAcceso access = HasPermission("Avisos.CountAvisos", user.securityToken);
                        if (!access.Acceso) continue;

                        string filterSecurityToken = GuardiasController.getSecurityTokenFilter(user.securityToken, access.EsJefe, conn);
                        //Si se usa la excepción de las guardias

                        int n = countAvisos(user.id, filterSecurityToken != null, conn);
                        if (n == 0) continue;

                        EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "avisosRRHH",
                            inserts = new() { { "avisos-number", n.ToString() } },
                            toEmail = user.email,
                            toName = user.name,
                            subject = $"[Think&Job] ({n}) Avisos sin revisar",
                            priority = EventMailer.EmailPriority.MODERATE
                        });
                    }

                    result = new { error = false };
                }
                catch (Exception e)
                {
                    result = new { error = "Error 5935, no se han podido generar los avisos: " + e.Message };
                }
            }

            return Ok(result);
        }

        #endregion

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region Estructuras
        public struct TipoAviso
        {
            public string id { get; set; }
            public string name { get; set; }
            public bool assign { get; set; }
            public List<TipoAviso> childs { get; set; }
        }

        private static readonly List<TipoAviso> AVISOS = new()
        {
            new TipoAviso(){id = "bajas", name = "Bajas y sustituciones", childs = null},
            new TipoAviso(){id = "cuentabancaria", name = "Trabajadores sin cuenta bancaria", childs = null},
            new TipoAviso(){id = "permisodetrabajo", name = "Caducidad del permiso de trabajo", childs = null},
            new TipoAviso(){id = "modelo145", name = "Cambios en el modelo145", childs = null},
            new TipoAviso(){id = "absentismo", name = "Absentismo laboral", childs = null},
            new TipoAviso(){id = "firmacontrato", name = "Contrato firmado por empresa", childs = null},
            new TipoAviso(){id = "notattend", name = "Incidencias de faltas de asistencias", childs = new(){
                new TipoAviso(){id = "Baja médica", name = "Incidencia: Baja médica", childs = null},
                new TipoAviso(){id = "Reposo", name = "Incidencia: Reposo", childs = null},
                new TipoAviso(){id = "Accidente", name = "Incidencia: Accidente", childs = null},
                new TipoAviso(){id = "Fallecimiento familiar", name = "Incidencia: Fallecimiento familiar", childs = null},
                new TipoAviso(){id = "Ingreso familiar", name = "Incidencia: Ingreso familiar", childs = null},
                new TipoAviso(){id = "Enfermedad/Cuidado familiar", name = "Incidencia: Enfermedad/Cuidado familiar", childs = null},
                new TipoAviso(){id = "Llegar tarde", name = "Incidencia: Llegar tarde", childs = null},
                new TipoAviso(){id = "Estar indispuesto", name = "Incidencia: Estar indispuesto", childs = null},
                new TipoAviso(){id = "Gestiones legales", name = "Incidencia: Gestiones legales", childs = null},
                new TipoAviso(){id = "Permiso matrimonio", name = "Incidencia: Permiso matrimonio", childs = null},
                new TipoAviso(){id = "Maternidad/Paternidad", name = "Incidencia: Maternidad/Paternidad", childs = null},
                new TipoAviso(){id = "Otros", name = "Incidencia: Otros", childs = null}
            } }
        };

        public struct Aviso
        {
            public string id { get; set; }
            public string companyId { get; set; }
            public string companyName { get; set; }
            public string type { get; set; }
            public string state { get; set; }
            public string username { get; set; }
            public DateTime date { get; set; }
            public string info { get; set; }
            public AvisoExtras extra { get; set; }
            public bool hidden { get; set; }
        }

        public struct AvisoExtras
        {
            //Candidato 1
            public string candidateId { get; set; }
            public string candidateName { get; set; }
            public string candidateDni { get; set; }
            public DateTime? candidateWorkStartDate { get; set; }
            public DateTime? candidatePermisoTrabajoCaducidad { get; set; }
            public DateTime? candidateAbsentismoDia { get; set; }
            //Candidato 2
            public string candidate2Id { get; set; }
            public string candidate2Name { get; set; }
            public string candidate2Dni { get; set; }
            //Incidencia notAttend
            public string incidenceNotAttendId { get; set; }
            public int? incidenceNotAttendNumber { get; set; }
            public DateTime? incidenceNotAttendBajaEnd { get; set; }
            public DateTime? incidenceNotAttendBajaRevision { get; set; }
            public string incidenceNotAttendState { get; set; }
        }

        public struct Dashboard
        {
            public int empresas { get; set; }
            public List<DashInfo> avisos { get; set; }
        }
        public struct DashInfo
        {
            public string type { get; set; }
            public int counter { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        #region Ayuda Asignaciones
        public static Dictionary<string, List<TipoAviso>> getAssigned(string userId, List<string> companies, SqlConnection conn, SqlTransaction transaction = null)
        {
            Dictionary<string, List<TipoAviso>> avisos = new();
            Dictionary<string, List<string>> flats = new();

            foreach (string company in companies)
            {
                avisos[company] = new();
                flats[company] = new();
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT avisoId, companyId FROM user_avisos WHERE userId = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string avisoId = reader.GetString(reader.GetOrdinal("avisoId"));
                        string companyId = reader.GetString(reader.GetOrdinal("companyId"));
                        if (flats.ContainsKey(companyId))
                            flats[companyId].Add(avisoId);
                    }
                }
            }

            foreach (string companyId in companies)
                stablishAvisosFromFlatList(avisos[companyId], AVISOS, flats[companyId]);

            return avisos;
        }
        private static void stablishAvisosFromFlatList(List<TipoAviso> avisos, List<TipoAviso> references, List<string> flat)
        {
            foreach (TipoAviso reference in references)
            {
                TipoAviso aviso = new TipoAviso()
                {
                    id = reference.id,
                    name = reference.name
                };
                if (reference.childs == null)
                {
                    aviso.assign = flat.Contains(reference.id);
                    aviso.childs = null;
                }
                else
                {
                    aviso.childs = new();
                    stablishAvisosFromFlatList(aviso.childs, reference.childs, flat);
                    aviso.assign = !aviso.childs.Any(a => !a.assign);
                }
                avisos.Add(aviso);
            }
        }

        public static void setAssigned(string userId, Dictionary<string, List<TipoAviso>> avisos, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM user_avisos WHERE userId = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                command.ExecuteNonQuery();
            }

            foreach (KeyValuePair<string, List<TipoAviso>> avisosCompany in avisos)
            {
                List<string> flat = new();
                extractFlatListFromAvisos(avisosCompany.Value, flat);

                foreach (string avisoId in flat)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText = "INSERT INTO user_avisos (userId, avisoId, companyId) VALUES (@USER, @AVISO, @COMPANY)";
                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@AVISO", avisoId);
                        command.Parameters.AddWithValue("@COMPANY", avisosCompany.Key);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
        private static void extractFlatListFromAvisos(List<TipoAviso> avisos, List<string> flat)
        {
            foreach (TipoAviso aviso in avisos)
            {
                if (aviso.childs == null)
                {
                    if (aviso.assign)
                        flat.Add(aviso.id);
                }
                else
                {
                    extractFlatListFromAvisos(aviso.childs, flat);
                }
            }
        }

        private static void removeNotAssignTypes(List<TipoAviso> avisos)
        {
            foreach (TipoAviso aviso in avisos)
            {
                if (aviso.childs != null)
                    removeNotAssignTypes(aviso.childs);
            }
            avisos.RemoveAll(aviso => !assignTypeShouldStay(aviso));
        }
        private static bool assignTypeShouldStay(TipoAviso aviso)
        {
            if (aviso.childs == null)
                return aviso.assign;
            else
                return aviso.childs.Any(c => assignTypeShouldStay(c));
        }
        private static List<TipoAviso> mergeAssignTypes(Dictionary<string, List<TipoAviso>> avisos)
        {
            List<TipoAviso> merged = new();
            mergeAssignTypes(merged, avisos.Values.ToList());
            return merged;
        }
        private static void mergeAssignTypes(List<TipoAviso> merged, List<List<TipoAviso>> sources)
        {
            foreach (TipoAviso cbase in sources[0])
            {
                TipoAviso aviso = new TipoAviso()
                {
                    id = cbase.id,
                    name = cbase.name,
                    assign = sources.Any(s => s.Any(c => c.id == cbase.id && c.assign)),
                    childs = null
                };
                if (cbase.childs != null)
                {
                    aviso.childs = new();
                    mergeAssignTypes(aviso.childs, sources.Select(s => s.Where(c => c.id == cbase.id).First().childs).ToList());
                }
                merged.Add(aviso);
            }
        }
        private static Dictionary<string, string> extractNames(List<TipoAviso> avisos)
        {
            Dictionary<string, string> list = new();
            foreach (TipoAviso aviso in avisos)
                extractNames(aviso, list);
            return list;
        }
        private static void extractNames(TipoAviso aviso, Dictionary<string, string> list)
        {
            list[aviso.id] = aviso.name;
            if (aviso.childs != null)
                foreach (TipoAviso child in aviso.childs)
                    extractNames(child, list);
        }

        private static Dictionary<string, List<TipoAviso>> parseAssigned(JsonElement json)
        {
            Dictionary<string, List<TipoAviso>> avisos = new();
            foreach (JsonProperty company in json.EnumerateObject())
            {
                avisos[company.Name] = parseTipos(company.Value);
            }
            return avisos;
        }
        private static List<TipoAviso> parseTipos(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Null)
                return null;
            List<TipoAviso> avisos = new();
            foreach (JsonElement avisoJson in json.EnumerateArray())
            {
                if (avisoJson.TryGetProperty("id", out JsonElement idJson) &&
                    avisoJson.TryGetProperty("name", out JsonElement nameJson) &&
                    avisoJson.TryGetProperty("assign", out JsonElement assignJson) &&
                    avisoJson.TryGetProperty("childs", out JsonElement childsJson))
                {
                    avisos.Add(new TipoAviso()
                    {
                        id = idJson.GetString(),
                        name = nameJson.GetString(),
                        assign = assignJson.GetBoolean(),
                        childs = parseTipos(childsJson)
                    });
                }
            }
            return avisos;
        }

        public static bool checkRRHHshouldBeNotified(string userId, string companyId, string avisoId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM user_avisos WHERE userId = @USER AND avisoId = @AVISO AND (companyId = 'any' OR companyId = @COMPANY)";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@AVISO", avisoId);
                command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);
                return (int)command.ExecuteScalar() > 0;
            }
        }

        #endregion

        #region Ayuda Avisos

        public static List<Aviso> listAvisos(string userId, bool filterCompanies, List<string> tipos, bool? hidden, string companyId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<Aviso> avisos = new();

            if (tipos != null && tipos.Count == 0) return avisos;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT A.*, E.nombre as companyName \n" +
                    "FROM avisos A \n" +
                    "LEFT OUTER JOIN empresas E ON(A.companyId = E.id) \n" +
                    "WHERE  (@HIDDEN IS NULL OR A.hidden = @HIDDEN) AND \n" +
                    "(@COMPANY IS NULL OR A.companyId = @COMPANY) AND \n" +
                    "EXISTS(SELECT * FROM user_avisos UA WHERE UA.userId = @USER AND UA.avisoId = A.type AND (A.companyId IS NULL OR A.companyId = UA.companyId OR UA.companyId = 'any')) AND \n" +
                    "(@UFILTERED IS NULL OR A.companyId IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE WHERE AUE.userId = @UFILTERED AND AUE.companyId = A.companyId)) \n" +
                    (tipos == null ? "" : "AND A.type in ({types}) \n") +
                    "ORDER BY date DESC \n" +
                    "OFFSET 0 ROWS FETCH NEXT 10000 ROWS ONLY";

                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@UFILTERED", filterCompanies ? userId : DBNull.Value);
                command.Parameters.AddWithValue("@HIDDEN", hidden == null ? DBNull.Value : (hidden.Value ? 1 : 0));
                command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);
                if (tipos != null)
                    AddArrayParameters(command, "types", tipos.ToArray());

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        avisos.Add(new Aviso()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            companyId = reader.IsDBNull(reader.GetOrdinal("companyId")) ? null : reader.GetString(reader.GetOrdinal("companyId")),
                            companyName = reader.IsDBNull(reader.GetOrdinal("companyName")) ? null : reader.GetString(reader.GetOrdinal("companyName")),
                            type = reader.GetString(reader.GetOrdinal("type")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            username = reader.IsDBNull(reader.GetOrdinal("username")) ? null : reader.GetString(reader.GetOrdinal("username")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            hidden = reader.GetInt32(reader.GetOrdinal("hidden")) == 1,
                        });
                    }
                }
            }

            return avisos;
        }
        public static int countAvisos(string userId, bool filterCompanies, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT COUNT(*) FROM avisos A \n" +
                    "WHERE  A.hidden = 0 AND \n" +
                    "EXISTS(SELECT * FROM user_avisos UA WHERE UA.userId = @USER AND UA.avisoId = A.type AND (A.companyId IS NULL OR A.companyId = UA.companyId OR UA.companyId = 'any')) AND \n" +
                    "(@UFILTERED IS NULL OR A.companyId IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE WHERE AUE.userId = @UFILTERED AND AUE.companyId = A.companyId))";

                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@UFILTERED", filterCompanies ? userId : DBNull.Value);

                return (int)command.ExecuteScalar();
            }
        }
        public static Dashboard getDashboard(string userId, bool filterCompanies, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<Aviso> avisos = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT A.companyId, A.type FROM avisos A \n" +
                    "WHERE  A.hidden = 0 AND \n" +
                    "EXISTS(SELECT * FROM user_avisos UA WHERE UA.userId = @USER AND UA.avisoId = A.type AND (A.companyId IS NULL OR A.companyId = UA.companyId OR UA.companyId = 'any')) AND \n" +
                    "(@UFILTERED IS NULL OR A.companyId IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE WHERE AUE.userId = @UFILTERED AND AUE.companyId = A.companyId))";

                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@UFILTERED", filterCompanies ? userId : DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        avisos.Add(new Aviso()
                        {
                            companyId = reader.IsDBNull(reader.GetOrdinal("companyId")) ? null : reader.GetString(reader.GetOrdinal("companyId")),
                            type = reader.GetString(reader.GetOrdinal("type"))
                        });
                    }
                }
            }

            Dictionary<string, int> count = new();
            Dictionary<string, string> rootNames = new();
            foreach (TipoAviso tipo in AVISOS)
            {
                rootNames[tipo.id] = tipo.name;
                if (tipo.childs != null)
                    foreach (string id in extractNames(tipo.childs).Keys)
                        rootNames[id] = tipo.name;
            }

            foreach (Aviso aviso in avisos)
            {
                string name = rootNames[aviso.type];
                if (count.ContainsKey(name))
                    count[name]++;
                else
                    count[name] = 1;
            }

            return new Dashboard()
            {
                avisos = count.Select(kv => new DashInfo()
                {
                    type = kv.Key,
                    counter = kv.Value
                }).ToList(),
                empresas = avisos.Select(a => a.companyId).Where(c => c != null).GroupBy(c => c).Count()
            };
        }
        public static Aviso? getAviso(string avisoId, SqlConnection conn, SqlTransaction transaction = null, string usernameReaded = null)
        {
            Aviso aviso;
            string extrasJson;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT A.*, E.nombre as companyName FROM avisos A LEFT OUTER JOIN empresas E ON(A.companyId = E.id) WHERE A.id = @ID";
                command.Parameters.AddWithValue("@ID", avisoId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        aviso = new Aviso()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            companyId = reader.IsDBNull(reader.GetOrdinal("companyId")) ? null : reader.GetString(reader.GetOrdinal("companyId")),
                            companyName = reader.IsDBNull(reader.GetOrdinal("companyName")) ? null : reader.GetString(reader.GetOrdinal("companyName")),
                            type = reader.GetString(reader.GetOrdinal("type")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            username = reader.IsDBNull(reader.GetOrdinal("username")) ? null : reader.GetString(reader.GetOrdinal("username")),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            info = reader.IsDBNull(reader.GetOrdinal("info")) ? null : reader.GetString(reader.GetOrdinal("info")),
                            hidden = reader.GetInt32(reader.GetOrdinal("hidden")) == 1,
                        };
                        extrasJson = reader.GetString(reader.GetOrdinal("extra"));
                    }
                    else
                        return null;
                }
            }

            aviso.extra = parseExtras(extrasJson, conn, transaction);

            if (!aviso.hidden && usernameReaded != null)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "UPDATE avisos SET hidden = 1, username = @USERNAME WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", avisoId);
                    command.Parameters.AddWithValue("@USERNAME", usernameReaded);
                    command.ExecuteNonQuery();
                }

                aviso.hidden = true;
                aviso.username = usernameReaded;
            }

            return aviso;
        }

        public static AvisoExtras parseExtras(string extrasString, SqlConnection conn, SqlTransaction transaction = null)
        {
            AvisoExtras extras = new AvisoExtras();
            JsonElement json = JsonDocument.Parse(extrasString).RootElement;

            if (json.TryGetProperty("candidateId", out JsonElement candidateIdJson))
                extras.candidateId = candidateIdJson.GetString();
            if (json.TryGetProperty("candidateName", out JsonElement candidateNameJson))
                extras.candidateName = candidateNameJson.GetString();
            if (json.TryGetProperty("candidateDni", out JsonElement candidateDniJson))
                extras.candidateDni = candidateDniJson.GetString();
            if (json.TryGetProperty("candidateWorkStartDate", out JsonElement candidateWorkStartDateJson))
                extras.candidateWorkStartDate = candidateWorkStartDateJson.GetDateTime();
            if (json.TryGetProperty("candidatePermisoTrabajoCaducidad", out JsonElement candidatePermisoTrabajoCaducidadJson))
                extras.candidatePermisoTrabajoCaducidad = candidatePermisoTrabajoCaducidadJson.GetDateTime();
            if (json.TryGetProperty("candidateAbsentismoDia", out JsonElement candidateAbsentismoDiaJson))
                extras.candidateAbsentismoDia = candidateAbsentismoDiaJson.GetDateTime();
            if (json.TryGetProperty("candidate2Id", out JsonElement candidate2IdJson))
                extras.candidate2Id = candidate2IdJson.GetString();
            if (json.TryGetProperty("candidate2Name", out JsonElement candidate2NameJson))
                extras.candidate2Name = candidate2NameJson.GetString();
            if (json.TryGetProperty("candidate2Dni", out JsonElement candidate2DniJson))
                extras.candidate2Dni = candidate2DniJson.GetString();
            if (json.TryGetProperty("incidenceNotAttendId", out JsonElement incidenceNotAttendIdJson))
                extras.incidenceNotAttendId = incidenceNotAttendIdJson.GetString();
            if (json.TryGetProperty("incidenceNotAttendNumber", out JsonElement incidenceNotAttendNumberJson))
                extras.incidenceNotAttendNumber = incidenceNotAttendNumberJson.GetInt32();
            if (json.TryGetProperty("incidenceNotAttendBajaEnd", out JsonElement incidenceNotAttendBajaEndJson))
                extras.incidenceNotAttendBajaEnd = incidenceNotAttendBajaEndJson.GetDateTime();
            if (json.TryGetProperty("incidenceNotAttendBajaRevision", out JsonElement incidenceNotAttendBajaRevisionJson))
                extras.incidenceNotAttendBajaRevision = incidenceNotAttendBajaRevisionJson.GetDateTime();
            if (json.TryGetProperty("incidenceNotAttendState", out JsonElement incidenceNotAttendStateJson))
                extras.incidenceNotAttendState = incidenceNotAttendStateJson.GetString();

            if (extras.candidateId == null && extras.candidateDni != null)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT id FROM candidatos WHERE dni = @DNI";
                    command.Parameters.AddWithValue("@DNI", extras.candidateDni);
                    using (SqlDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            extras.candidateId = reader.GetString(reader.GetOrdinal("id"));
                }
            }

            if (extras.candidate2Id == null && extras.candidate2Dni != null)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT id FROM candidatos WHERE dni = @DNI";
                    command.Parameters.AddWithValue("@DNI", extras.candidate2Dni);
                    using (SqlDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            extras.candidate2Id = reader.GetString(reader.GetOrdinal("id"));
                }
            }

            if (extras.incidenceNotAttendId == null && extras.incidenceNotAttendNumber != null)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT id FROM incidencia_falta_asistencia WHERE number = @NUMBER";
                    command.Parameters.AddWithValue("@NUMBER", extras.incidenceNotAttendNumber);
                    using (SqlDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            extras.incidenceNotAttendId = reader.GetString(reader.GetOrdinal("id"));
                }
            }

            return extras;
        }
        public static Dictionary<string, object> convertExtras(AvisoExtras extras)
        {
            Dictionary<string, object> converted = new();

            if (extras.candidateId != null) converted["candidateId"] = extras.candidateId;
            if (extras.candidateName != null) converted["candidateName"] = extras.candidateName;
            if (extras.candidateDni != null) converted["candidateDni"] = extras.candidateDni;
            if (extras.candidateWorkStartDate != null) converted["candidateWorkStartDate"] = extras.candidateWorkStartDate.Value;
            if (extras.candidatePermisoTrabajoCaducidad != null) converted["candidatePermisoTrabajoCaducidad"] = extras.candidatePermisoTrabajoCaducidad.Value;
            if (extras.candidateAbsentismoDia != null) converted["candidateAbsentismoDia"] = extras.candidateAbsentismoDia.Value;
            if (extras.candidate2Id != null) converted["candidate2Id"] = extras.candidate2Id;
            if (extras.candidate2Name != null) converted["candidate2Name"] = extras.candidate2Name;
            if (extras.candidate2Dni != null) converted["candidate2Dni"] = extras.candidate2Dni;
            if (extras.incidenceNotAttendId != null) converted["incidenceNotAttendId"] = extras.incidenceNotAttendId;
            if (extras.incidenceNotAttendNumber != null) converted["incidenceNotAttendNumber"] = extras.incidenceNotAttendNumber.Value;
            if (extras.incidenceNotAttendBajaEnd != null) converted["incidenceNotAttendBajaEnd"] = extras.incidenceNotAttendBajaEnd.Value;
            if (extras.incidenceNotAttendBajaRevision != null) converted["incidenceNotAttendBajaRevision"] = extras.incidenceNotAttendBajaRevision.Value;
            if (extras.incidenceNotAttendState != null) converted["incidenceNotAttendState"] = extras.incidenceNotAttendState;

            return converted;
        }

        public static void insertAviso(Aviso aviso, SqlConnection conn, SqlTransaction transaction = null)
        {
            Dictionary<string, object> extras = convertExtras(aviso.extra);
            string extrasString = JsonSerializer.Serialize(extras);

            if (aviso.id == null)
            {
                //Generarle una ID automaticamente e insertar siempre
                aviso.id = ComputeStringHash(aviso.type + aviso.companyId + aviso.info + extrasString + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            else
            {
                //Si ya tiene una ID, comprobar que no exista antes de insertar
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT COUNT(*) FROM avisos WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", aviso.id);
                    if ((int)command.ExecuteScalar() > 0)
                        return;
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
                    "INSERT INTO avisos " +
                    "(id, companyId, type, state, info, extra) VALUES " +
                    "(@ID, @COMPANY, @TYPE, @STATE, @INFO, @EXTRA)";

                command.Parameters.AddWithValue("@ID", aviso.id);
                command.Parameters.AddWithValue("@COMPANY", (object)aviso.companyId ?? DBNull.Value);
                command.Parameters.AddWithValue("@TYPE", aviso.type);
                command.Parameters.AddWithValue("@STATE", aviso.state);
                command.Parameters.AddWithValue("@INFO", aviso.info);
                command.Parameters.AddWithValue("@EXTRA", extrasString);

                command.ExecuteNonQuery();
            }
        }

        #endregion

        #region Acciones
        private void detectarAvisosBajasYSustitucion(List<Aviso> avisos, DateTime day, SqlConnection conn)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.dni, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, E.id as companyId, I.number, I.state, I.bajaEnd, I.bajaRevision, CS.dni as cDni, cName = CASE WHEN CS.nombre IS NULL THEN NULL ELSE TRIM(CONCAT(CS.nombre, ' ', CS.apellidos)) END  \n" +
                    "FROM incidencia_falta_asistencia I \n" +
                    "INNER JOIN candidatos C ON(I.candidateId = C.id) \n" +
                    "LEFT OUTER JOIN candidatos CS ON(I.dniSustitucion = CS.dni) \n" +
                    "INNER JOIN centros CE ON(CE.id = I.centroId) \n" +
                    "INNER JOIN empresas E ON(E.id = CE.companyId) \n" +
                    "WHERE I.closed = 0 AND I.baja = 1 AND C.test = 0 AND \n" +
                    "(I.bajaEnd = @DAY OR I.bajaRevision = @DAY)";

                command.Parameters.AddWithValue("@DAY", day);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Aviso aviso = new Aviso()
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            type = "bajas",
                            state = "nuevo",
                            extra = new AvisoExtras()
                            {
                                candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                                candidateName = reader.GetString(reader.GetOrdinal("name")),
                                incidenceNotAttendNumber = reader.GetInt32(reader.GetOrdinal("number")),
                                incidenceNotAttendState = reader.GetString(reader.GetOrdinal("state")),
                                incidenceNotAttendBajaEnd = reader.IsDBNull(reader.GetOrdinal("bajaEnd")) ? null : reader.GetDateTime(reader.GetOrdinal("bajaEnd")).Date,
                                incidenceNotAttendBajaRevision = reader.IsDBNull(reader.GetOrdinal("bajaRevision")) ? null : reader.GetDateTime(reader.GetOrdinal("bajaRevision")).Date,
                                candidate2Dni = reader.IsDBNull(reader.GetOrdinal("cDni")) ? null : reader.GetString(reader.GetOrdinal("cDni")),
                                candidate2Name = reader.IsDBNull(reader.GetOrdinal("cName")) ? null : reader.GetString(reader.GetOrdinal("cName"))
                            }
                        };

                        bool isEnd = aviso.extra.incidenceNotAttendBajaEnd == day, isRevision = aviso.extra.incidenceNotAttendBajaRevision == day;

                        aviso.info = (isEnd && isRevision) ? "La baja termina y debe ser revisada hoy." : (isEnd ? "La baja termina hoy." : "La baja debe ser revisada hoy.");
                        if (aviso.extra.candidate2Dni != null)
                            aviso.info += " Hay un candidato de sustitución asociado.";

                        aviso.id = ComputeStringHash(aviso.extra.incidenceNotAttendNumber + (aviso.extra.incidenceNotAttendBajaEnd == null ? "" : aviso.extra.incidenceNotAttendBajaEnd.Value.ToString("yyyy-MM-dd")) + aviso.type);

                        avisos.Add(aviso);
                    }
                }
            }
        }

        private void detectarAvisosCuentaBancaria(List<Aviso> avisos, DateTime day, SqlConnection conn)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.dni, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, C.fechaComienzoTrabajo, E.id as companyId \n" +
                    "FROM candidatos C \n" +
                    "INNER JOIN centros CE ON(CE.id = C.centroId) \n" +
                    "INNER JOIN empresas E ON(E.id = CE.companyId) \n" +
                    "WHERE (C.cuenta_bancaria IS NULL OR LEN(TRIM(C.cuenta_bancaria)) = 0) AND \n" +
                    "(C.periodoGracia IS NULL OR C.periodoGracia <= @DAY) AND C.test = 0 AND C.active = 1 AND C.fechaComienzoTrabajo IS NOT NULL ";

                command.Parameters.AddWithValue("@DAY", day.Date);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Aviso aviso = new Aviso()
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            type = "cuentabancaria",
                            state = "nuevo",
                            extra = new AvisoExtras()
                            {
                                candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                                candidateName = reader.GetString(reader.GetOrdinal("name")),
                                candidateWorkStartDate = reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo")).Date
                            }
                        };

                        aviso.info = "Ha pasado el periodo de gracia del trabajadory no ha proporcionado una cuenta bancaria.";

                        aviso.id = ComputeStringHash(aviso.extra.candidateDni + aviso.extra.candidateWorkStartDate.Value.ToString("yyyy-MM-dd") + aviso.type);

                        avisos.Add(aviso);
                    }
                }
            }
        }

        private void detectarAvisosPermisoDeTrabajo(List<Aviso> avisos, DateTime day, SqlConnection conn)
        {
            DateTime dentrounmes = day.AddDays(30);

            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.dni, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, C.permiso_trabajo_caducidad, E.id as companyId, C.nacionalidad \n" +
                    "FROM candidatos C \n" +
                    "INNER JOIN centros CE ON(CE.id = C.centroId) \n" +
                    "INNER JOIN empresas E ON(E.id = CE.companyId) \n" +
                    "WHERE C.lastSignLink IS NOT NULL AND C.test = 0 AND C.active = 1 AND \n" +
                    "C.permiso_trabajo_caducidad IS NOT NULL AND \n" +
                    "C.permiso_trabajo_caducidad < @DAY \n";

                command.Parameters.AddWithValue("@DAY", dentrounmes);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        //No procesarlo si es schengen
                        string nacionalidad = reader.IsDBNull(reader.GetOrdinal("nacionalidad")) ? null : reader.GetString(reader.GetOrdinal("nacionalidad"));

                        Constants.Pais? pais = Constants.getPaisByIso3(nacionalidad);
                        if (pais == null || pais.Value.schengen) continue;

                        Aviso aviso = new Aviso()
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            type = "permisodetrabajo",
                            state = "nuevo",
                            extra = new AvisoExtras()
                            {
                                candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                                candidateName = reader.GetString(reader.GetOrdinal("name")),
                                candidatePermisoTrabajoCaducidad = reader.GetDateTime(reader.GetOrdinal("permiso_trabajo_caducidad")).Date
                            }
                        };

                        bool caducado = aviso.extra.candidatePermisoTrabajoCaducidad < day;

                        if (caducado)
                            aviso.info = "El permiso de trabajo de este trabajador ha caducado.";
                        else
                            aviso.info = "El permiso de trabajo de este trabajador caduca en menos de 30 días.";
                        aviso.info += $" Al estar nacionalizado en el pais '{pais.Value.nombre}', requiere un permiso de trabajo.";

                        aviso.id = ComputeStringHash(aviso.extra.candidateDni + aviso.extra.candidatePermisoTrabajoCaducidad.Value.ToString("yyyy-MM-dd") + (caducado ? "caducado" : "caducara") + aviso.type);

                        avisos.Add(aviso);
                    }
                }
            }
        }

        private void detectarAvisosAbsentismo(List<Aviso> avisos, DateTime day, SqlConnection conn)
        {
            DateTime yesterday = day.AddDays(-1).Date;
            DateTime monday = yesterday.GetMonday();
            string yesterdayString = $"{yesterday.Day} del {MESES[yesterday.Month - 1]} del {yesterday.Year}";
            List<Aviso> tmpAvisos = new();

            //Obtener todos los candidatos que tienen un horario definido para ayer y no tienen workshift que comenzase ese día
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.id, C.dni, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, E.id as companyId \n" +
                    "FROM candidatos C \n" +
                    "INNER JOIN centros CE ON(CE.id = C.centroId) \n" +
                    "INNER JOIN empresas E ON(E.id = CE.companyId) \n" +
                    "WHERE C.test = 0 AND " +
                    "EXISTS(SELECT * FROM horarios_asignacion HA INNER JOIN horarios H ON(HA.horarioId = H.id) WHERE HA.candidateId = C.id AND H.semana = @MONDAY) AND \n" +
                    "NOT EXISTS(SELECT * FROM workshifts W WHERE W.candidateId = C.id AND CAST(W.startTime as Date) = CAST(@DAY as Date)) ";

                command.Parameters.AddWithValue("@DAY", yesterday);
                command.Parameters.AddWithValue("@MONDAY", monday);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Aviso aviso = new Aviso()
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            type = "absentismo",
                            state = "nuevo",
                            extra = new AvisoExtras()
                            {
                                candidateId = reader.GetString(reader.GetOrdinal("id")),
                                candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                                candidateName = reader.GetString(reader.GetOrdinal("name")),
                                candidateAbsentismoDia = yesterday
                            }
                        };

                        aviso.id = ComputeStringHash(aviso.extra.candidateDni + aviso.extra.candidateAbsentismoDia.Value.ToString("yyyy-MM-dd") + aviso.type);

                        tmpAvisos.Add(aviso);
                    }
                }
            }

            //Cache de horarios por día para cada candidato
            Dictionary<string, Dia> horario = new();

            //Eliminar los avisos de los que esten de baja, vacaciones o no tengan ningun turno ese dia
            tmpAvisos = tmpAvisos.FindAll(aviso =>
            {
                Dia dia = HorariosController.getCandidateDia(aviso.extra.candidateId, yesterday, out bool empty, conn);
                if (empty || dia == null || dia.baja || dia.vacaciones || (dia.manana == null && dia.tarde == null && dia.noche == null)) return false;

                horario[aviso.extra.candidateId] = dia;
                return true;
            });

            //Agregar la info dependiendo de los turnos y eliminar la ID del candidato
            tmpAvisos = tmpAvisos.Select(aviso =>
            {
                List<string> turnos = new();
                Dia dia = horario[aviso.extra.candidateId];
                if (dia.manana != null) turnos.Add("Mañana " + dia.manana.ToString());
                if (dia.tarde != null) turnos.Add("Tarde " + dia.tarde.ToString());
                if (dia.noche != null) turnos.Add("Noche " + dia.noche.ToString());
                aviso.info = $"El trabajador no ha fichero en el control horario el {yesterdayString}. Cuando tenía asignados el siguiente horario para ese día: {String.Join(", ", turnos)}";

                aviso.extra = new AvisoExtras()
                {
                    candidateDni = aviso.extra.candidateDni,
                    candidateName = aviso.extra.candidateName,
                    candidateAbsentismoDia = aviso.extra.candidateAbsentismoDia
                };
                return aviso;
            }).ToList();

            avisos.AddRange(tmpAvisos);
        }

        private void detectarAvisosIncidenciasAbiertas(List<Aviso> avisos, DateTime day, SqlConnection conn)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.dni, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, E.id as companyId, I.number, I.altCategory, I.state \n" +
                    "FROM incidencia_falta_asistencia I \n" +
                    "INNER JOIN candidatos C ON(I.candidateId = C.id) \n" +
                    "INNER JOIN centros CE ON(CE.id = I.centroId) \n" +
                    "INNER JOIN empresas E ON(E.id = CE.companyId) \n" +
                    "WHERE C.test = 0 AND I.closed = 0 AND I.state in ({states}) ";

                command.Parameters.AddWithValue("@DAY", day);
                AddArrayParameters(command, "states", new string[] { "espera-pendiente-candidato", "espera-pendiente-cliente", "conflicto" });

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Aviso aviso = new Aviso()
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            type = reader.GetString(reader.GetOrdinal("altCategory")),
                            state = "nuevo",
                            extra = new AvisoExtras()
                            {
                                candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                                candidateName = reader.GetString(reader.GetOrdinal("name")),
                                incidenceNotAttendNumber = reader.GetInt32(reader.GetOrdinal("number")),
                                incidenceNotAttendState = reader.GetString(reader.GetOrdinal("state"))
                            }
                        };

                        aviso.info = "Esta incidencia requiere atención para ser completada.";

                        aviso.id = ComputeStringHash(aviso.extra.incidenceNotAttendNumber + aviso.extra.incidenceNotAttendState + aviso.type);

                        avisos.Add(aviso);
                    }
                }
            }
        }

        private void detectarAvisosContratosFirmados(List<Aviso> avisos, DateTime day, SqlConnection conn)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT E.id as companyId, E.cif, E.nombre, CC.id, CC.signedDate \n" +
                    "FROM company_contratos CC \n" +
                    "INNER JOIN empresas E ON(CC.companyId = E.id) \n" +
                    "WHERE signed = 1 AND signedDate >= @DAY ";

                command.Parameters.AddWithValue("@DAY", day.AddDays(-2));
                AddArrayParameters(command, "states", new string[] { "espera-pendiente-candidato", "espera-pendiente-cliente", "conflicto" });

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Aviso aviso = new Aviso()
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            type = "firmacontrato",
                            state = "nuevo",
                            extra = new AvisoExtras()
                            {
                            }
                        };
                        string id = reader.GetString(reader.GetOrdinal("id"));
                        string cif = reader.GetString(reader.GetOrdinal("cif"));
                        string name = reader.GetString(reader.GetOrdinal("nombre"));
                        DateTime signedDate = reader.GetDateTime(reader.GetOrdinal("signedDate"));

                        aviso.info = $"La empresa '{name}' con CIF '{cif}' ha firmado un contrato nuevo el día {FormatTextualDateWithDayOfWeek(signedDate)}.";

                        aviso.id = ComputeStringHash(id + aviso.type);

                        avisos.Add(aviso);
                    }
                }
            }
        }

        private void detectarAvisosModelo145(List<Aviso> avisos, DateTime day, SqlConnection conn)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT CMC.id, C.dni, TRIM(CONCAT(C.nombre, ' ', C.apellidos)) as name, CE.companyId \n" +
                    "FROM candidate_modelo145_changes CMC \n" +
                    "INNER JOIN candidatos C ON(CMC.candidateId = C.id) \n" +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) \n" +
                    "WHERE C.test = 0 AND CMC.date > @DAY ";

                command.Parameters.AddWithValue("@DAY", day.AddDays(-2));

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int changeId = reader.GetInt32(reader.GetOrdinal("id"));
                        Aviso aviso = new Aviso()
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            type = "modelo145",
                            state = "nuevo",
                            extra = new AvisoExtras()
                            {
                                candidateDni = reader.GetString(reader.GetOrdinal("dni")),
                                candidateName = reader.GetString(reader.GetOrdinal("name"))
                            }
                        };

                        aviso.info = "El trabajador ha cambiado su modelo 145.";

                        aviso.id = ComputeStringHash(changeId + aviso.type);

                        avisos.Add(aviso);
                    }
                }
            }
        }
        #endregion

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
