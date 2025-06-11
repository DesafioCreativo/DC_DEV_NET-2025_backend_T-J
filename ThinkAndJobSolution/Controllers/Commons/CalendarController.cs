using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers._Model.Calendar;
using ThinkAndJobSolution.Controllers.MainHome.Sysadmin;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.MainHome.RRHH.GuardiasController;

namespace ThinkAndJobSolution.Controllers.Commons
{
    [Route("api/v1/calendar")]
    [ApiController]
    [Authorize]
    public class CalendarController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route("api/v1/calendar/get-my-calendar/{year}/{month}/")]
        public IActionResult GetMyCalendar(int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.GetMyCalendar", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken);
                    DateTime start = new DateTime(year, month, 1);
                    DateTime end = start.AddMonths(1).AddSeconds(-1);

                    result = new
                    {
                        error = false,
                        events = getEvents(userId, start, end, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5301, no se ha podido obtener el canlendario" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("get-calendar/{userId}/{year}/{month}/")]
        public IActionResult GetCalendar(string userId, int year, int month)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.GetCalendar", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    DateTime start = new DateTime(year, month, 1);
                    DateTime end = start.AddMonths(1).AddSeconds(-1);
                    result = new
                    {
                        error = false,
                        events = getEvents(userId, start, end, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5302, no se ha podido obtener el calendario" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("get-my-calendar-day/{year}/{month}/{day}/")]
        public IActionResult GetMyCalendarDay(int year, int month, int day)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.GetMyCalendarDay", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken);
                    DateTime start = new DateTime(year, month, day);
                    DateTime end = start.AddDays(1).AddSeconds(-1);
                    result = new
                    {
                        error = false,
                        events = getEvents(userId, start, end, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5316, no se han podido obtener los eventos del día" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("get-my-calendar-day-events-number/{year}/{month}/{day}/")]
        public IActionResult GetMyCalendarDayNumber(int year, int month, int day)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.GetMyCalendarDayNumber", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken);
                    DateTime start = new DateTime(year, month, day);
                    DateTime end = start.AddDays(1).AddSeconds(-1);
                    List<CalendarEvent> events = getEvents(userId, start, end, conn);
                    result = new
                    {
                        error = false,
                        totalEvents = events.Count,
                        newEvents = events.Count(e => !e.seen),
                        anyImportantEvents = events.Any(e => e.important),
                        anyNewImportantEvents = events.Any(e => e.important && !e.seen)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5317, no se han podido contar los eventos del día" };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("create-my-event/")]
        public async Task<IActionResult> CreateMyEvent()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.CreateMyEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }

            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (!tryGetEvent(json, out UserEvent uevent))
                return Ok(result);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string userId = FindUserIdBySecurityToken(securityToken, conn, transaction);
                        string creator = FindUsernameById(userId, conn, transaction);
                        string id = createUserEvent(new() { userId }, new() { uevent.date }, uevent, creator, conn, transaction);
                        transaction.Commit();
                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5304, no se ha podido crear el evento" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("create-event/{userId}/")]
        public async Task<IActionResult> CreateEvent(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.CreateEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!tryGetEvent(json, out UserEvent uevent))
                return Ok(result);
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string creator = FindUsernameBySecurityToken(securityToken, conn, transaction);
                        string id = createUserEvent(new() { userId }, new() { uevent.date }, uevent, creator, conn, transaction);
                        transaction.Commit();
                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5305, no se ha podido crear el evento" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPut]
        [Route("edit-my-event/{eventId}/")]
        public async Task<IActionResult> EditMyEvent(string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.EditMyEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!tryGetEvent(json, out UserEvent uevent))
                return Ok(result);
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string username = FindUsernameBySecurityToken(securityToken);
                    UserEvent pastEvent = getUserEvent(eventId, conn);
                    if (pastEvent == null)
                        return Ok(new { error = "Error 4301, evento no encontrado" });
                    else if (pastEvent.createdBy != username)
                        return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

                    uevent.id = eventId;
                    updateUserEvent(uevent, conn);

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5307, no se ha podido editar el evento" };
                }
            }
            return Ok(result);
        }

        [HttpPut]
        [Route("edit-event/{eventId}/")]
        public async Task<IActionResult> EditEvent(string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.EditEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!tryGetEvent(json, out UserEvent uevent))
                return Ok(result);
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string username = FindUsernameBySecurityToken(securityToken, conn, transaction);
                        UserEvent pastEvent = getUserEvent(eventId, conn, transaction);
                        if (pastEvent == null)
                            return Ok(new { error = "Error 4301, evento no encontrado" });
                        uevent.id = eventId;
                        updateUserEvent(uevent, conn, transaction);
                        transaction.Commit();
                        result = new
                        {
                            error = false
                        };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5308, no se ha podido editar el evento" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route("delete-my-event/{eventId}/")]
        public IActionResult DeleteMyEvent(string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.DeleteMyEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string username = FindUsernameBySecurityToken(securityToken);
                    UserEvent pastEvent = getUserEvent(eventId, conn);
                    if (pastEvent == null)
                        return Ok(new { error = "Error 4301, evento no encontrado" });
                    else if (pastEvent.createdBy != username)
                        return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
                    deleteUserEvent(eventId, conn);
                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5310, no se ha podido eliminar el evento" };
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route("delete-event/{eventId}/")]
        public IActionResult DeleteEvent(string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.DeleteEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string username = FindUsernameBySecurityToken(securityToken);
                    UserEvent pastEvent = getUserEvent(eventId, conn);
                    if (pastEvent == null)
                        return Ok(new { error = "Error 4301, evento no encontrado" });
                    deleteUserEvent(eventId, conn);
                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5311, no se ha podido eliminar el evento" };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("create-group-event/")]
        public async Task<IActionResult> CreateGroupEvent()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.CreateEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!(json.TryGetProperty("event", out JsonElement eventJson) && tryGetEvent(eventJson, out UserEvent uevent) && json.TryGetStringList("users", out List<string> users)))
                return Ok(result);
            List<DateTime> days = new();
            if (json.TryGetProperty("days", out JsonElement daysJson) && daysJson.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement dayJson in daysJson.EnumerateArray())
                {
                    DateTime? day = GetJsonDate(dayJson);
                    if (day != null)
                        days.Add(day.Value);
                }
            }
            if (days.Count == 0)
                days.Add(uevent.date);
            if (users.Count == 0)
                return Ok(new { error = "Error 4304, no se ha especificado ningún usuario" });
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string creator = FindUsernameBySecurityToken(securityToken, conn, transaction);
                        string id = createUserEvent(users, days, uevent, creator, conn, transaction);
                        transaction.Commit();
                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5306, no se ha podido crear el evento multiple" };
                    }
                }
            }
            return Ok(result);
        }

        [HttpPut]
        [Route("edit-event-single-user/{userId}/{eventId}/")]
        public async Task<IActionResult> EditEventSingleUser(string userId, string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.EditEventSingleUser", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!tryGetEvent(json, out UserEvent uevent))
                return Ok(result);
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string creator = FindUsernameBySecurityToken(securityToken);
                    UserEvent pastEvent = getUserEvent(eventId, conn);
                    if (pastEvent == null)
                        return Ok(new { error = "Error 4301, evento no encontrado" });
                    else if (!pastEvent.isGroup)
                        return Ok(new { error = "Error 4302, el evento no es multiple" });
                    uevent.id = eventId;
                    result = new
                    {
                        error = false,
                        id = updateSingleUserEventInGroup(userId, uevent, creator, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5309, no se ha podido editar el evento" };
                }
            }
            return Ok(result);
        }

        [HttpPatch]
        [Route("set-users-group-event/{eventId}/")]
        public async Task<IActionResult> SetUsersGroupEvent(string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.SetUsersGroupEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!json.TryGetStringList("users", out List<string> users))
                return Ok(result);
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string creator = FindUsernameBySecurityToken(securityToken);
                    UserEvent pastEvent = getUserEvent(eventId, conn);
                    if (pastEvent == null)
                        return Ok(new { error = "Error 4301, evento no encontrado" });
                    else if (!pastEvent.isGroup)
                        return Ok(new { error = "Error 4302, el evento no es multiple" });
                    updateEventGroupUsers(eventId, users, conn);
                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5312, no se han podido modificar los participantes del evento" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("list-group-events/")]
        public IActionResult ListGroupEvents()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.ListGroupEvents", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new
                    {
                        error = false,
                        events = listGroupEvents(conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5313, no se ha podido obtener los eventos multiples" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("list-users-group-event/{eventId}/")]
        public IActionResult ListUsersGroupEvent(string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.ListUsersGroupEvent", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new
                    {
                        error = false,
                        users = listUsersGroupEvent(eventId, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5314, no se ha podido obtener los usuarios del evento" };
                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route("delete-event-single-user/{userId}/{eventId}/")]
        public IActionResult DeleteEventSingleUser(string userId, string eventId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.DeleteEventSingleUser", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    string username = FindUsernameBySecurityToken(securityToken);
                    UserEvent pastEvent = getUserEvent(eventId, conn);
                    if (pastEvent == null)
                        return Ok(new { error = "Error 4301, evento no encontrado" });
                    deleteSingleUserEventInGroup(userId, eventId, conn);
                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5315, no se ha podido eliminar el evento" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("api/v1/calendar/list-users/{securityToken}")]
        public IActionResult ListUsers()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.ListUsers", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT U.id, U.username, TRIM(CONCAT(U.name, ' ', U.surname)) as fullName, U.department " +
                            "FROM users U " +
                            "WHERE U.isExternal = 0 " +
                            "ORDER BY fullName ASC";

                        List<object> users = new List<object>();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    username = reader.GetString(reader.GetOrdinal("username")),
                                    name = reader.GetString(reader.GetOrdinal("fullName")),
                                    department = reader.GetString(reader.GetOrdinal("department"))
                                });
                            }
                        }

                        result = new
                        {
                            error = false,
                            users
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5303, no se ha podido listar los usuarios" };
                }
            }
            return Ok(result);
        }

        [HttpPatch]
        [Route("set-my-seen-events/")]
        public async Task<IActionResult> SetMySeenEvents()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.SetMySeenEvents", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!(json.TryGetProperty("userevents", out JsonElement usereventsJson) && json.TryGetProperty("guardias", out JsonElement guardiasJson)))
                return Ok(result);
            List<DateTime> guardias = new();
            foreach (JsonElement guardiaJson in guardiasJson.EnumerateArray())
            {
                DateTime? day = GetJsonDate(guardiaJson);
                if (day != null) guardias.Add(day.Value);
            }
            List<Tuple<string, DateTime>> userevents = new();
            foreach (JsonElement usereventJson in usereventsJson.EnumerateArray())
            {
                if (usereventJson.TryGetProperty("id", out JsonElement idJson) && usereventJson.TryGetProperty("day", out JsonElement dayJson))
                {
                    DateTime? date = GetJsonDate(dayJson);
                    if (date != null) userevents.Add(new(idJson.GetString(), date.Value));
                }
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);
                    setSeen(userId, userevents, guardias, conn);

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5319, no se han podido confirmar la lectura de los eventos" };
                }
            }
            return Ok(result);
        }


        [HttpGet]
        [Route("send-daily-email/")]
        public IActionResult SendDaylyEmail()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            ResultadoAcceso access = HasPermission("Calendar.SendDaylyEmail", securityToken);
            if (!access.Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    DateTime start = DateTime.Now.Date;
                    DateTime end = start.AddDays(1).AddSeconds(-1);
                    //Listado de usuarios internos
                    List<UserCredentialsController.SystemUser> users = UserCredentialsController.listInternalUsers(conn);
                    //Por cada usuario, obtener sus eventos diarios y enviarlos por correo
                    foreach (var user in users)
                    {
                        List<CalendarEvent> events = getEvents(user.id, start, end, conn);
                        if (events.Count == 0) continue;
                        StringBuilder sb = new();

                        //Reordenar en funcion de si son importantes o no.
                        List<CalendarEvent> importants = new(), notImportants = new();
                        foreach (CalendarEvent evt in events)
                            (evt.important ? importants : notImportants).Add(evt);
                        events.Clear();
                        events.AddRange(importants.SortByDate());
                        events.AddRange(notImportants.SortByDate());
                        bool darken = false;
                        foreach (CalendarEvent evt in events)
                        {
                            string duration;
                            if (evt.start != null && evt.end != null)
                                duration = $"{evt.start.Value:HH:mm} - {evt.end.Value:HH:mm}";
                            else if (evt.start != null & evt.end == null)
                                duration = $"Desde {evt.start.Value:HH:mm}";
                            else if (evt.start == null & evt.end != null)
                                duration = $"Hasta {evt.end.Value:HH:mm}";
                            else
                                duration = "Todo el día";

                            string bg = darken ? "#B3B3B3" : "#E6E6E6";
                            sb.Append("<tr style='background-color:");
                            sb.Append(bg);
                            sb.Append(";'><td><div style='padding: 5px; display: flex;'><img style='width: auto; height: 24px; margin: auto;' src='");
                            sb.Append(InstallationConstants.PUBLIC_URL);
                            sb.Append("/emailStatic/calendar_");
                            if (evt.type == "guardia")
                                sb.Append("guardia");
                            else
                            {
                                sb.Append("user_");
                                sb.Append(evt.category);
                            }
                            sb.Append(".png'/></div></td><td style='text-align:left; width: 100%;'><div style='padding: 5px;'>");
                            sb.Append(evt.title);
                            sb.Append("</div></td><td style='text-align:right;'><div style='padding: 5px;white-space: nowrap;'>");
                            sb.Append(duration);
                            sb.Append("</div></td></tr>");
                            darken = !darken;
                        }

                        EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "calendarEventsDayly",
                            inserts = new() {
                                { "name", $"{user.name} {user.surname}" },
                                { "word", events.Count==1?"el siguiente evento":"los siguientes eventos" },
                                { "events", sb.ToString() }
                            },
                            toEmail = user.email,
                            subject = $"[Think&Job] Eventos diarios {start:dd/MM/yyyy}",
                            priority = EventMailer.EmailPriority.MODERATE
                        });
                    }

                    result = new
                    {
                        error = false
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5318, no se han podido enviar los emails con eventos diarios" };
                }
            }
            return Ok(result);
        }





        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        private List<CalendarEvent> getEvents(string userId, DateTime start, DateTime end, SqlConnection conn, SqlTransaction transaction = null)
        {
            // Buscar las guardias
            List<GuardiaEvent> guardiaEvents = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT G.* " +
                                      "FROM guardias_rrhh G " +
                                      "WHERE @USER = G.userId AND " +
                                      "G.firstDay <= @END AND G.lastDay >= @START";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@START", start);
                command.Parameters.AddWithValue("@END", end);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Guardia guardia = new()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            userId = reader.GetString(reader.GetOrdinal("userId")),
                            startTime = reader.GetDateTime(reader.GetOrdinal("startTime")),
                            endTime = reader.GetDateTime(reader.GetOrdinal("endTime")),
                            firstDay = reader.GetDateTime(reader.GetOrdinal("firstDay")),
                            lastDay = reader.GetDateTime(reader.GetOrdinal("lastDay"))
                        };
                        guardiaEvents.AddRange(GuardiaEvent.fromGuardia(guardia));
                    }
                }
            }

            // Buscar las confirmaciones de lectura de las guardias
            List<DateTime> guardiasSeen = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT day FROM calendar_guardia_seen WHERE @USER = userId AND day <= @END AND day >= @START";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@START", start);
                command.Parameters.AddWithValue("@END", end);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        guardiasSeen.Add(reader.GetDateTime(reader.GetOrdinal("day")));
                    }
                }
            }
            foreach (GuardiaEvent guardiaEvent in guardiaEvents)
            {
                guardiaEvent.seen = guardiasSeen.Contains(guardiaEvent.date);
            }

            // Buscar los eventos de usuario
            List<UserEvent> userEvents = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CUE.*, CUED.day " +
                                      "FROM calendar_userevents CUE " +
                                      "INNER JOIN calendar_userevent_users CUEU ON(CUE.id = CUEU.eventId) " +
                                      "INNER JOIN calendar_userevent_dates CUED ON(CUE.id = CUED.eventId) " +
                                      "WHERE CUEU.userId = @USER AND " +
                                      "(CUED.day <= @END AND CUED.day >= @START)";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@START", start);
                command.Parameters.AddWithValue("@END", end);

                using (SqlDataReader reader = command.ExecuteReader())
                    userEvents.AddRange(UserEvent.fromReader(reader));
            }

            // Buscar las confirmaciones de lectura de los userevents
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT eventId, day FROM calendar_userevent_seen WHERE @USER = userId AND day <= @END AND day >= @START";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@START", start);
                command.Parameters.AddWithValue("@END", end);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string eventId = reader.GetString(reader.GetOrdinal("eventId"));
                        DateTime day = reader.GetDateTime(reader.GetOrdinal("day"));
                        userEvents.Where(e => e.id == eventId && e.date == day).All(e => e.seen = true);
                    }
                }
            }

            //Combinar las fuentes de los e ventos
            List<CalendarEvent> events = new();
            events.AddRange(guardiaEvents);
            events.AddRange(userEvents);

            //Filtrar por si se cuelan eventos de varios dias, como las guardias. Y ordenar por fecha, hora de comienzo y hora de fin
            return events.Where(e => e.date >= start && e.date <= end).SortByDate().ToList();
        }
        private bool tryGetEvent(JsonElement json, out UserEvent uevent)
        {
            if (
                json.TryGetString("title", out string title) &&
                json.TryGetString("description", out string description) &&
                json.TryGetString("category", out string category) &&
                json.TryGetDate("date", out DateTime date)
                )
            {
                uevent = new()
                {
                    title = title,
                    description = description,
                    category = category,
                    date = date,
                    start = json.TryGetTime("start", out TimeSpan start) ? date.Add(start) : null,
                    end = json.TryGetTime("end", out TimeSpan end) ? date.Add(end) : null
                };
                return true;
            }

            uevent = default;
            return false;

        }
        private string createUserEvent(List<string> users, List<DateTime> days, UserEvent uevent, string creator, SqlConnection conn, SqlTransaction transaction = null)
        {
            string id = ComputeStringHash(uevent.title + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

            //Crear el evento
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "INSERT INTO calendar_userevents (id, title, description, category, timeStart, timeEnd, createdBy, grupal) " +
                    "VALUES (@ID, @TITLE, @DESCRIPTION, @CATEGORY, @START, @END, @CREATOR, @GROUPAL)";
                command.Parameters.AddWithValue("@ID", id);
                command.Parameters.AddWithValue("@TITLE", uevent.title);
                command.Parameters.AddWithValue("@DESCRIPTION", uevent.description ?? "");
                command.Parameters.AddWithValue("@CATEGORY", uevent.category);
                command.Parameters.AddWithValue("@START", uevent.start == null ? DBNull.Value : uevent.start.Value.TimeOfDay);
                command.Parameters.AddWithValue("@END", uevent.end == null ? DBNull.Value : uevent.end.Value.TimeOfDay);
                command.Parameters.AddWithValue("@CREATOR", creator);
                command.Parameters.AddWithValue("@GROUPAL", users.Count > 1 ? 1 : 0);
                command.ExecuteNonQuery();
            }

            //Insertar a los participantes
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "INSERT INTO calendar_userevent_users (eventId, userId) VALUES (@EVENT, @USER)";
                command.Parameters.AddWithValue("@EVENT", id);
                command.Parameters.Add("@USER", System.Data.SqlDbType.VarChar);
                foreach (string userId in users)
                {
                    command.Parameters["@USER"].Value = userId;
                    command.ExecuteNonQuery();
                }
            }

            //Insertar las fechas
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "INSERT INTO calendar_userevent_dates (eventId, day) VALUES (@EVENT, @DAY)";
                command.Parameters.AddWithValue("@EVENT", id);
                command.Parameters.Add("@DAY", System.Data.SqlDbType.Date);
                foreach (DateTime day in days)
                {
                    command.Parameters["@DAY"].Value = day;
                    command.ExecuteNonQuery();
                }
            }

            //Insertar los ya vistos si el usuario es el creador y la fecha es hoy
            DateTime today = DateTime.Now.Date;
            string creatorId = FindUserIdByUsername(creator, conn, transaction);
            if (users.Contains(creatorId) && days.Contains(today))
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO calendar_userevent_seen (eventId, userId, day) VALUES (@EVENT, @USER, @DAY)";
                    command.Parameters.AddWithValue("@EVENT", id);
                    command.Parameters.AddWithValue("@USER", creatorId);
                    command.Parameters.AddWithValue("@DAY", today);
                    command.ExecuteNonQuery();
                }
            }

            return id;
        }
        private UserEvent getUserEvent(string eventId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<UserEvent> events = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                } //day = creationDate es por tener algo, ya que el front no va a necesitarlo, lo mismo con el seen
                command.CommandText = "SELECT *, day = creationDate, seen = 0 FROM calendar_userevents WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", eventId);
                using (SqlDataReader reader = command.ExecuteReader())
                    events.AddRange(UserEvent.fromReader(reader));
            }

            if (events.Count == 0) return null;
            return events[0];
        }
        private void updateUserEvent(UserEvent uevent, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "UPDATE calendar_userevents " +
                    "SET title = @TITLE, description = @DESCRIPTION, category = @CATEGORY, timeStart = @START, timeEnd = @END " +
                    "WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", uevent.id);
                command.Parameters.AddWithValue("@TITLE", uevent.title);
                command.Parameters.AddWithValue("@DESCRIPTION", uevent.description);
                command.Parameters.AddWithValue("@CATEGORY", uevent.category);
                command.Parameters.AddWithValue("@START", uevent.start == null ? DBNull.Value : uevent.start.Value.TimeOfDay);
                command.Parameters.AddWithValue("@END", uevent.end == null ? DBNull.Value : uevent.end.Value.TimeOfDay);
                command.ExecuteNonQuery();
            }

            //Si tiene una sola fecha, se le puede cambiar
            bool singleDate;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM calendar_userevent_dates WHERE eventId = @ID";
                command.Parameters.AddWithValue("@ID", uevent.id);
                singleDate = (int)command.ExecuteScalar() <= 1;
            }

            if (singleDate)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "UPDATE calendar_userevent_dates SET day = @DATE WHERE eventId = @ID";
                    command.Parameters.AddWithValue("@ID", uevent.id);
                    command.Parameters.AddWithValue("@DATE", uevent.date);
                    command.ExecuteNonQuery();
                }
            }
        }
        private void deleteUserEvent(string eventId, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Eliminar los participantes
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM calendar_userevent_users WHERE eventId = @EVENT";
                command.Parameters.AddWithValue("@EVENT", eventId);
                command.ExecuteNonQuery();
            }

            //Eliminar las fechas
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM calendar_userevent_dates WHERE eventId = @EVENT";
                command.Parameters.AddWithValue("@EVENT", eventId);
                command.ExecuteNonQuery();
            }

            //Eliminar los vistos
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM calendar_userevent_seen WHERE eventId = @EVENT";
                command.Parameters.AddWithValue("@EVENT", eventId);
                command.ExecuteNonQuery();
            }

            //Eliminar el evento
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM calendar_userevents WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", eventId);
                command.ExecuteNonQuery();
            }
        }
        private string updateSingleUserEventInGroup(string userId, UserEvent uevent, string creator, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Sacar los dias del evento
            List<DateTime> days = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT day FROM calendar_userevent_dates WHERE eventId = @EVENT";
                command.Parameters.AddWithValue("@EVENT", uevent.id);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        days.Add(reader.GetDateTime(reader.GetOrdinal("day")));
            }

            //Sacarlo del grupo
            deleteSingleUserEventInGroup(userId, uevent.id, conn, transaction);

            //Crear un nuevo evento para el solito
            string id = createUserEvent(new() { userId }, days, uevent, creator, conn, transaction);

            //Si el grupo queda vacío, eliminarlo
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM calendar_userevent_users WHERE eventId = @EVENT";
                command.Parameters.AddWithValue("@EVENT", uevent.id);
                if ((int)command.ExecuteScalar() == 0)
                {
                    command.CommandText = "DELETE FROM calendar_userevent_dates WHERE eventId = @EVENT";
                    command.ExecuteNonQuery();
                    command.CommandText = "DELETE FROM calendar_userevents WHERE id = @EVENT";
                    command.ExecuteNonQuery();
                }
            }

            return id;
        }
        private void deleteSingleUserEventInGroup(string userId, string eventId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM calendar_userevent_users WHERE eventId = @EVENT AND userId = @USER";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@EVENT", eventId);
                command.ExecuteNonQuery();
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM calendar_userevent_seen WHERE eventId = @EVENT AND userId = @USER";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@EVENT", eventId);
                command.ExecuteNonQuery();
            }
        }
        private void updateEventGroupUsers(string eventId, List<string> users, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Borrar a los participantes actuales
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM calendar_userevent_users WHERE eventId = @EVENT";
                command.Parameters.AddWithValue("@EVENT", eventId);
                command.ExecuteNonQuery();
            }

            //Agregar a los nuevos participantes
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "INSERT INTO calendar_userevent_users (eventId, userId) VALUES (@EVENT, @USER)";
                command.Parameters.AddWithValue("@EVENT", eventId);
                command.Parameters.Add("@USER", System.Data.SqlDbType.VarChar);
                foreach (string userId in users)
                {
                    command.Parameters["@USER"].Value = userId;
                    command.ExecuteNonQuery();
                }
            }

            //Borrar los vistos de los participantes que ya no esten
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "DELETE FROM calendar_userevent_seen WHERE eventId = @EVENT AND " +
                    "NOT EXISTS(SELECT * FROM calendar_userevent_users WHERE calendar_userevent_users.eventId = calendar_userevent_seen.eventId AND calendar_userevent_users.userId = calendar_userevent_seen.userId)";
                command.Parameters.AddWithValue("@EVENT", eventId);

                command.ExecuteNonQuery();
            }
        }
        private List<UserEvent> listGroupEvents(SqlConnection conn, SqlTransaction transaction = null)
        {
            List<UserEvent> events = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                } //day = creationDate es por tener algo, ya que el front no va a necesitarlo, lo mismo con el seen
                command.CommandText = "SELECT *, day = creationDate, seen = 0 FROM calendar_userevents WHERE grupal = 1 AND creationDate > @CUTOFF ORDER BY creationDate DESC";
                command.Parameters.AddWithValue("@CUTOFF", DateTime.Now.AddYears(-1));
                using (SqlDataReader reader = command.ExecuteReader())
                    events.AddRange(UserEvent.fromReader(reader));
            }

            return events;
        }
        private List<string> listUsersGroupEvent(string eventId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<string> users = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT DISTINCT userId FROM calendar_userevent_users WHERE eventId = @EVENT";
                command.Parameters.AddWithValue("@EVENT", eventId);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        users.Add(reader.GetString(reader.GetOrdinal("userId")));
            }

            return users;
        }
        private void setSeen(string userId, List<Tuple<string, DateTime>> events, List<DateTime> guardias, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<Tuple<string, DateTime>> newUserevents = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM calendar_userevent_seen WHERE userId = @USER AND eventId = @ID AND day = @DAY";
                command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@USER", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@DAY", System.Data.SqlDbType.DateTime);
                foreach (Tuple<string, DateTime> uevent in events)
                {
                    command.Parameters["@ID"].Value = uevent.Item1;
                    command.Parameters["@USER"].Value = userId;
                    command.Parameters["@DAY"].Value = uevent.Item2;
                    if ((int)command.ExecuteScalar() == 0)
                    {
                        newUserevents.Add(uevent);
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
                command.CommandText = "INSERT INTO calendar_userevent_seen (eventId, userId, day) VALUES (@ID, @USER, @DAY)";
                command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@USER", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@DAY", System.Data.SqlDbType.DateTime);
                foreach (Tuple<string, DateTime> uevent in newUserevents)
                {
                    command.Parameters["@ID"].Value = uevent.Item1;
                    command.Parameters["@USER"].Value = userId;
                    command.Parameters["@DAY"].Value = uevent.Item2;
                    command.ExecuteNonQuery();
                }
            }

            List<DateTime> newGuardias = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM calendar_guardia_seen WHERE userId = @USER AND day = @DAY";
                command.Parameters.Add("@USER", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@DAY", System.Data.SqlDbType.DateTime);
                foreach (DateTime day in guardias)
                {
                    command.Parameters["@USER"].Value = userId;
                    command.Parameters["@DAY"].Value = day;
                    if ((int)command.ExecuteScalar() == 0)
                    {
                        newGuardias.Add(day);
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
                command.CommandText = "INSERT INTO calendar_guardia_seen (userId, day) VALUES (@USER, @DAY)";
                command.Parameters.Add("@USER", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@DAY", System.Data.SqlDbType.DateTime);
                foreach (DateTime day in newGuardias)
                {
                    command.Parameters["@USER"].Value = userId;
                    command.Parameters["@DAY"].Value = day;
                    command.ExecuteNonQuery();
                }
            }
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
