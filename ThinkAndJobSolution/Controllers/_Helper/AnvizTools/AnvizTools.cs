using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers._Helper.AnvizTools
{
    public class AnvizTools
    {
        /// <summary>
        /// Get all employees from a device.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <returns> List of employees. </returns>
        /// <exception cref="Exception"></exception>
        public static async Task<List<DeviceUser>> GetAllEmployees(string deviceId)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { device_id = deviceId })), "data" },
                { new StringContent("getAllEmployees"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Status code not OK: " + message.StatusCode);
            return JsonSerializer.Deserialize<List<DeviceUser>>(await message.Content.ReadAsStringAsync());
        }

        /// <summary>
        /// Register new employees into a device.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <param name="employees"> Array of employees. Employee type: DeviceUser </param>
        /// <returns>Task</returns>
        /// <exception cref="Exception"></exception>
        public static async Task InsertEmployees(string deviceId, List<DeviceUser> employees)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { employees, device_id = deviceId })), "data" },
                { new StringContent("insertEmployees"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Status code not OK: " + message.StatusCode);
        }

        /// <summary>
        /// Delete a list of employees from a device.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <param name="employees"> Array of employees. Employee type: DeviceUser </param>
        /// <returns>Task</returns>
        /// <exception cref="Exception"></exception>
        public static async Task DeleteEmployees(string deviceId, List<string> employees)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { device_id = deviceId, idds = employees })), "data" },
                { new StringContent("deleteEmployees"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Status code not OK: " + message.StatusCode);
        }

        /// <summary>
        /// Return all devices registered in the server.
        /// </summary>
        /// <param name="type"> Type of the device (external 0, internal 1). </param>
        /// <returns>List of devices.</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<List<FaceDetectionDevice>> GetDevices(DeviceType type)
        {
            // Gets the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, type))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { })), "data" },
                { new StringContent("getDevices"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Status code not OK: " + message.StatusCode);
            return JsonSerializer.Deserialize<List<FaceDetectionDevice>>(await message.Content.ReadAsStringAsync());
        }

        /// <summary>
        /// Controls the workshift of an employee.<br></br>
        /// If null is returned, shift has to be deleted. Otherwise, use candidate corrections.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <param name="groupName"> Group name. </param>
        /// <param name="candidateName"> Employee name. </param>
        /// <param name="state"> Employee state. </param>
        /// <param name="start"> Start time. </param>
        /// <param name="end"> End time. </param>
        /// <param name="turnoStart"> Workshift start time. </param>
        /// <param name="turnoEnd"> Workshift end time. </param>
        /// <returns>Task</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<Tuple<DateTime?, DateTime?>> Workshift(string deviceId, string groupName, string candidateName, int state, DateTime start, DateTime? end, DateTime? turnoStart, DateTime? turnoEnd)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                {
                    new StringContent(JsonSerializer.Serialize(new
                    {
                        device_id = deviceId,
                        group_name = groupName,
                        candidate_name = candidateName,
                        state,
                        shift = new
                        {
                            start = start.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                            end = end.HasValue ? end.Value.ToString("yyyy-MM-dd'T'HH:mm:ss") : null
                        },
                        turno = new
                        {
                            start = turnoStart.HasValue ? turnoStart.Value.ToString("yyyy-MM-dd'T'HH:mm:ss") : null,
                            end = turnoEnd.HasValue ? turnoEnd.Value.ToString("yyyy-MM-dd'T'HH:mm:ss") : null
                        }
                    })),
                    "data"
                },
                { new StringContent("getWorkshift"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Status code not OK: " + message.StatusCode);
            string response = await message.Content.ReadAsStringAsync();
            JsonElement json = JsonDocument.Parse(response).RootElement;
            if (json.TryGetString("start", out string startTime) && json.TryGetString("end", out string endTime))
            {
                return new(
                    startTime == null ? null : DateTime.ParseExact(startTime, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                    endTime == null ? null : DateTime.ParseExact(endTime, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
                );
            }
            return null;
        }

        /// <summary>
        /// Ping a device, verifying if it is connected.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <returns>Task</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<bool> Ping(string deviceId)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { device_id = deviceId })), "data" },
                { new StringContent("ping"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            bool allOk = message.StatusCode == System.Net.HttpStatusCode.OK;
            if (allOk)
                UpdateLastPing(deviceId);
            return allOk;
        }

        /// <summary>
        /// Set the current date to a device.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <returns>Task</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<bool> SetCurrentDate(string deviceId)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { device_id = deviceId })), "data" },
                { new StringContent("setCurrentDate"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            return message.StatusCode == System.Net.HttpStatusCode.OK;
        }

        /// <summary>
        /// Remove all employees and faces.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <returns> Task </returns>
        public static async Task<bool> RemoveAllEmployees(string deviceId)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { device_id = deviceId })), "data" },
                { new StringContent("deleteAllEmployees"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            return message.StatusCode == System.Net.HttpStatusCode.OK;
        }




        // FUNCTIONS NOT USED //

        /// <summary>
        /// NOT USED - Change the face of an employee.
        /// </summary>
        /// <param name="deviceId"> Id of the target device. </param>
        /// <param name="idd"> Id of employee. </param>
        /// <param name="image"> Image of an employee. </param>
        /// <returns>Task</returns>
        /// <exception cref="Exception"></exception>
        public static async Task ChangeEmployeeFace(string deviceId, string idd, string image)
        {
            // Get the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out string apikey, GetAnvizType(deviceId)))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes($"{apikey}:")));

            using MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture))
            {
                { new StringContent(JsonSerializer.Serialize(new { device_id = deviceId, idd, image })), "data" },
                { new StringContent("changeEmployeeFace"), "action" }
            };

            using HttpResponseMessage message = await client.PostAsync(endpoint + "/sample/client/Index.php", content);
            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Status code not OK: " + message.StatusCode);
        }




        // INTERNAL FUNCTIONS //

        /// <summary>
        /// Inserts a worker registration task into the dispatcher database.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="user"> User to register </param>
        /// <returns></returns>
        public static int InsertRegisterTask(string deviceId, DeviceUser user) => InsertTask(new DeviceTask()
        {
            deviceId = deviceId,
            action = "register",
            user = user,
            device_type = GetAnvizType(deviceId)
        });

        /// <summary>
        /// Inserta una tarea de borrado de trabajador en la base de datos del despachador.
        /// Inserts a worker removal task into the dispatcher database.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="idd"> Idd of the user </param>
        /// <returns></returns>
        public static int InsertRemoveTask(string deviceId, string idd) => InsertTask(new DeviceTask()
        {
            // The idd is passed forcibly, because the transformation cannot be applied multiple times
            deviceId = deviceId,
            action = "remove",
            user = new DeviceUser() { idd = idd },
            device_type = GetAnvizType(deviceId)
        });

        /// <summary>
        /// Inserts a task into the dispatcher.
        /// </summary>
        /// <param name="task"> The task </param>
        /// <returns></returns>
        private static int InsertTask(DeviceTask task)
        {
            using SqlConnection conn = new(InstallationConstants.ANVIZ_CONNECTION_STRING);
            conn.Open();
            // Calculate the data and the estimate
            string data;
            int estimate = 5;
            switch (task.action)
            {
                case "register":
                    data = JsonSerializer.Serialize(task.user);
                    estimate += 10;
                    break;
                default:
                    data = null;
                    estimate += 5;
                    break;
            }

            // Compact queued tasks
            switch (task.action)
            {
                case "register":
                case "remove":
                    // Delete all tasks involving this user
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM tasks WHERE deviceId = @DEVICE AND idd = @IDD AND state = 0";
                        command.Parameters.AddWithValue("@DEVICE", task.deviceId);
                        command.Parameters.AddWithValue("@IDD", task.user.idd);
                        command.ExecuteNonQuery();
                    }
                    break;
            }

            // Insert the new task
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "INSERT INTO tasks (deviceId, action, idd, data, estimate, device_type) VALUES (@DEVICE, @ACTION, @IDD, @DATA, @ESTIMATE, @DEVICETYPE)";
                command.Parameters.AddWithValue("@DEVICE", task.deviceId);
                command.Parameters.AddWithValue("@ACTION", task.action);
                command.Parameters.AddWithValue("@IDD", task.user.idd);
                command.Parameters.AddWithValue("@DATA", (object)data ?? DBNull.Value);
                command.Parameters.AddWithValue("@ESTIMATE", estimate);
                command.Parameters.AddWithValue("@DEVICETYPE", task.device_type);
                command.ExecuteNonQuery();
            }

            // Calculate the time it will take to execute
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT COALESCE(SUM(estimate), 0) FROM tasks WHERE (state = 0 OR state = 1)";
                command.Parameters.AddWithValue("@DEVICE", task.deviceId);
                return (int)command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Obtains the action queue of the device.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="showFailed"> Show failed tasks </param>
        /// <returns></returns>
        public static List<DeviceTask> GetQueue(string deviceId, bool showFailed = false)
        {
            List<DeviceTask> tasks = new();
            using (SqlConnection conn = new(InstallationConstants.ANVIZ_CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT id, action, idd, state, estimate FROM tasks " +
                    "WHERE (@DEVICE IS NULL OR deviceId = @DEVICE) AND " +
                    "state " + (showFailed ? "=" : "<>") + " 2 " +
                    "ORDER BY id ASC";
                command.Parameters.AddWithValue("@DEVICE", (object)deviceId ?? DBNull.Value);
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    tasks.Add(new DeviceTask()
                    {
                        id = reader.GetInt32(reader.GetOrdinal("id")),
                        action = reader.GetString(reader.GetOrdinal("action")),
                        idd = reader.GetString(reader.GetOrdinal("idd")),
                        state = reader.GetInt32(reader.GetOrdinal("state")),
                        estimate = reader.GetInt32(reader.GetOrdinal("estimate"))
                    });
                }
            }
            return tasks;
        }

        /// <summary>
        /// Restart a failed task.
        /// </summary>
        /// <param name="taskId"> Task id </param>
        /// <returns></returns>
        public static int RestartTask(int taskId)
        {
            if (!TryGetTask(taskId, out DeviceTask task))
                return -1;

            using (SqlConnection conn = new(InstallationConstants.ANVIZ_CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "DELETE FROM tasks WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", task.id);
                command.ExecuteNonQuery();
            }

            using (SqlConnection conn = new(InstallationConstants.ANVIZ_CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "INSERT INTO tasks (deviceId, action, idd, data, estimate, device_type) VALUES (@DEVICE, @ACTION, @IDD, @DATA, @ESTIMATE, @DEVICETYPE)";
                command.Parameters.AddWithValue("@DEVICE", task.deviceId);
                command.Parameters.AddWithValue("@ACTION", task.action);
                command.Parameters.AddWithValue("@IDD", task.idd);
                command.Parameters.AddWithValue("@DATA", (object)task.data ?? DBNull.Value);
                command.Parameters.AddWithValue("@ESTIMATE", task.estimate);
                command.Parameters.AddWithValue("@DEVICETYPE", task.device_type);
                command.ExecuteNonQuery();
            }
            return GetEstimate(task.deviceId);
        }

        /// <summary>
        /// Delete a task from the database.
        /// </summary>
        /// <param name="taskId"> Task id </param>
        public static void DeleteTask(int taskId)
        {
            using SqlConnection conn = new(InstallationConstants.ANVIZ_CONNECTION_STRING);
            conn.Open();

            using SqlCommand command = conn.CreateCommand();
            command.CommandText = "DELETE FROM tasks WHERE id = @ID";
            command.Parameters.AddWithValue("@ID", taskId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Try to get a task from the database.
        /// </summary>
        /// <param name="taskId"> Task id </param>
        /// <param name="task"> The task </param>
        /// <returns></returns>
        public static bool TryGetTask(int taskId, out DeviceTask task)
        {
            using SqlConnection conn = new(InstallationConstants.ANVIZ_CONNECTION_STRING);
            conn.Open();

            using SqlCommand command = conn.CreateCommand();
            command.CommandText = "SELECT * FROM tasks WHERE id = @ID";
            command.Parameters.AddWithValue("@ID", taskId);
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                task = new DeviceTask()
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    deviceId = reader.GetString(reader.GetOrdinal("deviceId")),
                    action = reader.GetString(reader.GetOrdinal("action")),
                    idd = reader.GetString(reader.GetOrdinal("idd")),
                    data = reader.IsDBNull(reader.GetOrdinal("data")) ? null : reader.GetString(reader.GetOrdinal("data")),
                    state = reader.GetInt32(reader.GetOrdinal("state")),
                    estimate = reader.GetInt32(reader.GetOrdinal("estimate")),
                    device_type = (DeviceType)reader.GetInt32(reader.GetOrdinal("device_type"))
                };
                return true;
            }
            else
            {
                task = new DeviceTask();
                return false;
            }
        }

        /// <summary>
        /// Calls the Anviz endpoint to get the face image of a user.
        /// </summary>
        /// <param name="image"> Face image </param>
        /// <returns> Image of the detected face </returns>
        /// <exception cref="Exception"></exception>
        public static async Task<Face?> DetectFace(string image)
        {
            // Gets the enpoint and apikey
            if (!TryGetEndpointAndApikey(out string endpoint, out _, DeviceType.External))
                throw new Exception("Could not restore endpoint or api key");

            using HttpClient client = new();
            using StringContent content = new(EncryptString(image), Encoding.UTF8, "text/plain");
            using HttpResponseMessage message = await client.PostAsync(endpoint + ":5000/faceextractor", content);
            if (message.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return new Face()
                {
                    image = DecryptString(await message.Content.ReadAsStringAsync()), // Face detected
                    orientation = int.Parse(message.Headers.TryGetValues("x-orientation", out var orientationHeader) ? orientationHeader.First() : "0")
                };
            }

            if (message.StatusCode == System.Net.HttpStatusCode.NoContent)
                return null;    // No face detected

            // Other problem
            throw new Exception("Status code not OK: " + message.StatusCode);
        }

        /// <summary>
        /// Returns the estimated time of execution of the device's tasks.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <returns></returns>
        public static int GetEstimate(string deviceId)
        {
            using SqlConnection conn = new(InstallationConstants.ANVIZ_CONNECTION_STRING);
            conn.Open();

            using SqlCommand command = conn.CreateCommand();
            command.CommandText =
                "SELECT COALESCE(SUM(estimate), 0) FROM tasks " +
                "WHERE deviceId = @DEVICE AND state <> 2";
            command.Parameters.AddWithValue("@DEVICE", deviceId);
            return (int)command.ExecuteScalar();
        }

        /// <summary>
        /// Device task struct. 
        /// </summary>
        public struct DeviceTask
        {
            public int id { get; set; }
            public string deviceId { get; set; }
            public string action { get; set; }
            public string idd { get; set; }
            public DeviceUser user { get; set; }
            public string data { get; set; }
            public int state { get; set; }
            public int estimate { get; set; }
            //Extra
            public string deviceName { get; set; }
            public DeviceType device_type { get; set; }
        }
        /// <summary>
        /// Device user struct.
        /// </summary>
        public struct DeviceUser
        {
            public string rjId { get; set; }
            public string idd { get; set; }
            public string dni { get; set; }
            public string name { get; set; }
            public string pass { get; set; }
            public string image { get; set; }
            public string cardid { get; set; }
            public int identity_type { get; set; }
        }
        /// <summary>
        /// Face detection device struct.
        /// </summary>
        public struct FaceDetectionDevice
        {
            public string deviceId { get; set; }
            public string name { get; set; }
            public string serial { get; set; }
            public int type { get; set; }
            public int identity_type { get; set; }
            public DateTime? last_ping { get; set; }
        }
        /// <summary>
        /// Selectable face detection device struct.
        /// </summary>
        public struct SelectableFaceDetectionDevice
        {
            public string deviceId { get; set; }
            public string name { get; set; }
            public string serial { get; set; }
            public int identity_type { get; set; }
            public DeviceType type { get; set; }
            public DateTime? last_ping { get; set; }
            public bool selected { get; set; }
        }
        /// <summary>
        /// Face struct.
        /// </summary>
        public struct Face
        {
            public string image { get; set; }
            public int orientation { get; set; }
        }

        /// <summary>
        /// Device type. External 0, internal 1.
        /// </summary>
        public enum DeviceType
        {
            All = -1,
            External = 0,
            Internal = 1
        }


        /// <summary>
        /// Tries to get the endpoint and the api key from the database.
        /// </summary>
        /// <param name="endpoint"> The endpoint </param>
        /// <param name="apikey"> The api key </param>
        /// <param name="type"> The type of the device. External 0, internal 1. </param>
        /// <returns> True if the endpoint and the api key are restored, false otherwise. </returns>
        private static bool TryGetEndpointAndApikey(out string endpoint, out string apikey, in DeviceType type)
        {
            // If type equals 0, means external device, otherwise internal device
            endpoint = null;
            apikey = null;
            string endpointtext, apikeytext;
            if (type == DeviceType.External)
            {
                endpointtext = "anviz-endpoint"; apikeytext = "anviz-apikey";
            }
            else
            {
                endpointtext = "anvizint-endpoint"; apikeytext = "anvizint-apikey";
            }
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT [value] FROM sys_config WHERE [key] = '" + endpointtext + "'";
                    using SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                        endpoint = reader.GetString(reader.GetOrdinal("value"));
                    else if (Constants.DEFAULT_SYSCONFIG.ContainsKey(endpointtext))
                        endpoint = Constants.DEFAULT_SYSCONFIG[endpointtext]["thinkandjob"];
                }
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT [value] FROM sys_config WHERE [key] = '" + apikeytext + "'";
                    using SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                        apikey = reader.GetString(reader.GetOrdinal("value"));
                    else if (Constants.DEFAULT_SYSCONFIG.ContainsKey(apikeytext))
                        endpoint = Constants.DEFAULT_SYSCONFIG[apikeytext]["thinkandjob"];
                }
            }
            return endpoint != null && apikey != null;
        }
        /// <summary>
        /// Updates the last ping of a device.
        /// </summary>
        /// <param name="deviceId"> The id of the device </param>
        private static void UpdateLastPing(string deviceId)
        {
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "UPDATE rf_devices SET last_ping = GETDATE() WHERE deviceId = @DEVICEID";
                command.Parameters.AddWithValue("@DEVICEID", deviceId);
                command.ExecuteNonQuery();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Returns the type of the device by its id.
        /// </summary>
        /// <param name="deviceId"> The id of the device </param>
        /// <returns> The type of the device. External 0, internal 1. </returns>
        public static DeviceType GetAnvizType(string deviceId)
        {
            DeviceType type = DeviceType.External;
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT type FROM rf_devices WHERE deviceId = @DEVICEID";
                command.Parameters.AddWithValue("@DEVICEID", deviceId);
                type = (DeviceType)command.ExecuteScalar();
            }
            catch (Exception) { }
            return type;
        }
        /// <summary>
        /// Returns the identity type of the device by its id.
        /// </summary>
        /// <param name="deviceId"> The id of the device. </param>
        /// <returns> The identity type of the device. </returns>
        public static int GetAnvizIdentityType(string deviceId)
        {
            int identity_type = 0;
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT identity_type FROM rf_devices WHERE deviceId = @DEVICEID";
                command.Parameters.AddWithValue("@DEVICEID", deviceId);
                identity_type = (int)command.ExecuteScalar();
            }
            catch (Exception) { }
            return identity_type;
        }
        /// <summary>
        /// Returns all the device ids of the Anviz devices by its type.
        /// </summary>
        /// <param name="type"> The type of the device. External 0, internal 1. </param>
        /// <returns> The list of device ids. </returns>
        public static List<string> GetAnvizDeviceIds(DeviceType type = DeviceType.External)
        {
            List<string> deviceIds = new();
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT deviceId FROM rf_devices";
                if (type != DeviceType.All)
                {
                    command.CommandText += " WHERE type = @TYPE";
                    command.Parameters.AddWithValue("@TYPE", type);
                }
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                    deviceIds.Add(reader.GetString(reader.GetOrdinal("deviceId")));
            }
            catch (Exception) { }
            return deviceIds;
        }
        /// <summary>
        /// Returns the device info by its id.
        /// </summary>
        /// <param name="deviceId"> The id of the device. </param>
        /// <returns> The device info. </returns>
        public static FaceDetectionDevice GetDeviceInfo(string deviceId)
        {
            FaceDetectionDevice device = new();
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT * FROM rf_devices WHERE deviceId = @DEVICEID";
                command.Parameters.AddWithValue("@DEVICEID", deviceId);
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    device.deviceId = reader.GetString(reader.GetOrdinal("deviceId"));
                    device.name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name"));
                    device.serial = reader.GetString(reader.GetOrdinal("serial"));
                    device.type = reader.GetInt32(reader.GetOrdinal("type"));
                    device.identity_type = reader.GetInt32(reader.GetOrdinal("identity_type"));
                    device.last_ping = reader.IsDBNull(reader.GetOrdinal("last_ping")) ? null : reader.GetDateTime(reader.GetOrdinal("last_ping"));
                }
            }
            catch (Exception) { }
            return device;
        }

    }
}
