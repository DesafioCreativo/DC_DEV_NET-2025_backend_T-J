using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using ThinkAndJobSolution.Controllers._Helper.AnvizTools;
using ThinkAndJobSolution.Controllers._Model.Client;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.AnvizTools.AnvizTools;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.Client
{
    [Route("api/v1/anviz")]
    [ApiController]
    [Authorize]
    public class AnvizController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        #region > LISTINGS
        /// <summary>
        /// List the biometric devices of a center.
        /// </summary>
        /// <param name="workCenterId"> Center id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> List of devices </returns>
        [HttpGet]
        [Route(template: "get-devices-by-workcenter/{workCenterId}/")]
        public IActionResult GetDevicesByWorkCenter(string workCenterId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("Anviz.GetDeviceByWorkCenter", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (!TryGetDevicesByworkCenterId(workCenterId, out List<FaceDetectionDevice> devices, conn))
                        return Ok(new { error = "Error 4570, este centro no usa el sistema biométrico" });

                    result = new { error = false, devices };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5570, No se han podido obtener el dispositivo del centro" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// List the companies that use biometric devices.
        /// </summary>
        /// <param name="securityToken"> Security token </param>
        /// <returns> List of companies </returns>
        [HttpGet]
        [Route(template: "list-companies/")]
        public IActionResult ListCompanies()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.ListCompanies", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    List<CompanyData> companies = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT E.id as companyId, E.nombre, CE.id as workCenterId, CE.alias " +
                            "FROM empresas E " +
                            "INNER JOIN centros CE ON(CE.companyId = E.id) " +
                            "WHERE EXISTS(SELECT * FROM rf_devices_centros RDC WHERE RDC.centroId = CE.id)";
                        using SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            string companyId = reader.GetString(reader.GetOrdinal("companyId"));
                            string companyName = reader.GetString(reader.GetOrdinal("nombre"));
                            string workCenterId = reader.GetString(reader.GetOrdinal("workCenterId"));
                            string centroAlias = reader.GetString(reader.GetOrdinal("alias"));

                            CompanyData company = companies.FirstOrDefault(c => c.id == companyId);
                            if (company == null || company.id == null)
                            {
                                company = new CompanyData()
                                {
                                    id = companyId,
                                    nombre = companyName,
                                    centros = new()
                                };
                                companies.Add(company);
                            }

                            company.centros.Add(new CentroData()
                            {
                                id = workCenterId,
                                alias = centroAlias
                            });
                        }
                    }
                    result = new { error = false, companies };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5572, No se han podido obtener los centros que usan dispositivo biométricos" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// List the facial biometric devices.
        /// </summary>
        /// <param name="showAll"> Show all devices or only those with a name </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> List of devices </returns>
        [HttpGet]
        [Route(template: "list-devices/{showAll}/")]
        public IActionResult ListDevices(bool showAll)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.ListDevices", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new { error = false, devices = ListDevices(showAll, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5573, No se han podido listar los dispositivos de reconocimiento facial" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// List the anviz devices of a center.
        /// </summary>
        /// <param name="workCenterId"> Center id </param>
        /// <param name="showAll"> Show all devices or only those with a name </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> List of devices </returns>
        [HttpGet]
        [Route(template: "list-devices-by-workcenter/{workCenterId}/{showAll}/")]
        public IActionResult ListDevicesByWorkCenter(string workCenterId, bool showAll)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.ListDevicesByWorkCenter", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new { error = false, devices = ListDevicesByWorkCenter(workCenterId, showAll, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5579, No se han podido listar los dispositivos de reconocimiento facial por centro" };
                }
            }
            return Ok(result);
        }
        #endregion

        #region > DEVICE OPERATIONS
        /// <summary>
        /// Obtains the information of a device by its id.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Device information </returns>
        [HttpGet]
        [Route(template: "get-device-info/{deviceId}/")]
        public IActionResult GetDeviceInfo(string deviceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Anviz.GetDeviceByWorkCenter", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            object result;
            try
            {
                result = new { error = false, device = AnvizTools.GetDeviceInfo(deviceId) };
            }
            catch (Exception)
            {
                result = new { error = "Error 5570, No se han podido obtener la información del dispositivo" };
            }
            return Ok(result);
        }

        /// <summary>
        /// Asign the biometric devices to a center.
        /// </summary>
        /// <param name="workCenterId"> Center id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Result of the operation </returns>
        [HttpPost]
        [Route(template: "assign-workcenter/{workCenterId}/")]
        public async Task<IActionResult> AssignWorkCenter(string workCenterId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("Anviz.AssignWorkCenter", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });

            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            JsonElement json = JsonDocument.Parse(await readerBody.ReadToEndAsync()).RootElement;

            if (json.TryGetStringList("devices", out List<string> devices))
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM rf_devices_centros WHERE centroId = @CENTRO";
                        command.Parameters.AddWithValue("@CENTRO", workCenterId);
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "INSERT INTO rf_devices_centros (centroId, deviceId) VALUES (@CENTRO, @DEVICE)";
                        command.Parameters.Add("@CENTRO", System.Data.SqlDbType.VarChar);
                        command.Parameters.Add("@DEVICE", System.Data.SqlDbType.Char);
                        foreach (string device in devices)
                        {
                            command.Parameters["@CENTRO"].Value = workCenterId;
                            command.Parameters["@DEVICE"].Value = device;
                            command.ExecuteNonQuery();
                        }
                    }
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5582, No se han podido asociar los dispositivos al centro" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// Refresh the devices of the system.
        /// </summary>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Result of the operation </returns>
        [HttpGet]
        [Route(template: "fetch-devices/")]
        public async Task<IActionResult> FetchDevices()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.FetchDevices", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    List<FaceDetectionDevice> remoteDevices = await GetDevices(DeviceType.External);
                    remoteDevices.AddRange(await GetDevices(DeviceType.Internal));
                    List<FaceDetectionDevice> localDevices = ListDevices(true, conn);

                    // Calculating which devices to remove or keep
                    List<FaceDetectionDevice> devicesToRemove = localDevices.Where(ld => !remoteDevices.Any(rd => ld.deviceId == rd.deviceId && ld.type == rd.type)).ToList();
                    List<FaceDetectionDevice> devicesToInsert = remoteDevices.Where(rd => !localDevices.Any(ld => ld.deviceId == rd.deviceId && ld.type == rd.type)).ToList();

                    // Remove devices that no longer exist
                    foreach (FaceDetectionDevice device in devicesToRemove)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM rf_devices_centros WHERE deviceId = @ID";
                            command.Parameters.AddWithValue("@ID", device.deviceId);
                            command.ExecuteNonQuery();
                        }
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM rf_devices WHERE deviceId = @ID";
                            command.Parameters.AddWithValue("@ID", device.deviceId);
                            command.ExecuteNonQuery();
                        }
                    }

                    // Introduction of new devices
                    foreach (FaceDetectionDevice device in devicesToInsert)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "INSERT INTO rf_devices (deviceId, serial, name, type, identity_type) VALUES (@ID, @SERIAL, @NAME, @TYPE, 0)";
                        command.Parameters.AddWithValue("@ID", device.deviceId);
                        command.Parameters.AddWithValue("@SERIAL", device.serial);
                        command.Parameters.AddWithValue("@TYPE", device.type);
                        command.Parameters.AddWithValue("@NAME", "");
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5580, No se han podido refrescar " };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// Update the data of a device.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Result of the operation </returns>
        [HttpPut]
        [Route(template: "update-device/{deviceId}/")]
        public async Task<IActionResult> UpdateDevice(string deviceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.UpdateDevice", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });

            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            JsonElement json = JsonDocument.Parse(await readerBody.ReadToEndAsync()).RootElement;

            if (!json.TryGetProperty("name", out JsonElement nameJson))
                return Ok(new { error = "Error 2071, no se ha recibido el nombre" });
            if (!json.TryGetProperty("identity_type", out JsonElement identity_typeJson))
                return Ok(new { error = "Error 2072, no se ha recibido el tipo de identificación" });

            string name = nameJson.GetString();
            int identity_type = identity_typeJson.GetInt32();

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "UPDATE rf_devices SET name = @NAME, identity_type = @IDENTITYTYPE WHERE deviceId = @ID";
                        command.Parameters.AddWithValue("@ID", deviceId);
                        command.Parameters.AddWithValue("@NAME", name);
                        command.Parameters.AddWithValue("@IDENTITYTYPE", identity_type);
                        command.ExecuteNonQuery();
                    }
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5582, No se ha podido cambiar el nombre al dispositivo" };
                }
            }
            return Ok(result);
        }
        #endregion



        #region > MANUAL OPERATIONS
        /// <summary>
        /// Ping a device to check if it is connected.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Operation estimate </returns>
        [HttpGet]
        [Route(template: "ping-device/{deviceId}/")]
        public async Task<IActionResult> PingDevice(string deviceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.PingDevice", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new { error = false, ping = await Ping(deviceId) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5583, No se han podido hacer ping al dispositivo" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// Ping all devices to check if they are connected.
        /// </summary>
        /// <returns> Operation estimate </returns>
        [HttpGet]
        [Route(template: "ping-all-devices")]
        public async Task<IActionResult> PingAllDevice()
        {
            try
            {
                List<string> devices = GetAnvizDeviceIds(DeviceType.All);
                foreach (string device in devices)
                {
                    await Ping(device);
                }
            }
            catch (Exception) { }
            return Ok(new { error = false });
        }

        /// <summary>
        /// Set the date and time of a device.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Operation estimate </returns>
        [HttpGet]
        [Route(template: "set-date-time-device/{deviceId}/")]
        public async Task<IActionResult> SetDateTimeDevice(string deviceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.SetDateTimeDevice", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new { error = false, set = await SetCurrentDate(deviceId) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5584, No se han podido hacer establecer la fecha y hora en el dispositivo" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// Deletes all employees from a device, with their faces and fingerprints.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Operation estimate </returns>
        [HttpGet]
        [Route(template: "remove-all-employees/{deviceId}/")]
        public async Task<IActionResult> RemoveAllEmployees(string deviceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Anviz.SyncDevice", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            object result;
            try
            {
                try
                {
                    if (!await Ping(deviceId))
                        throw new Exception();
                }
                catch (Exception)
                {
                    return Ok(new { error = "Error 5574, El dispositivo no responde. Posiblemente esté apagado, fuera de línea o mal configurado." });
                }
                await AnvizTools.RemoveAllEmployees(deviceId);
                result = new { error = false, estimate = GetEstimate(deviceId) };
            }
            catch (Exception)
            {
                result = new { error = "Error 5574, No se han podido sincronizar el dispositivo" };
            }
            return Ok(result);
        }

        /// <summary>
        /// Detects if there is a face in the image.
        /// </summary>
        /// <param name="token"> Security token </param>
        /// <returns> Face detection result </returns>
        [HttpPost]
        [Route(template: "extract-face/{token}")]
        public async Task<IActionResult> ExtractFace(string token)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string image = await readerBody.ReadToEndAsync();
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    // Lookup the token in the timeouts table
                    string tokenType = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE id = @TOKEN";
                        command.Parameters.AddWithValue("@TOKEN", token);
                        if ((int)command.ExecuteScalar() > 0)
                            tokenType = "ca";
                    }
                    if (tokenType == null)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "SELECT COUNT(*) FROM users WHERE securityToken = @TOKEN AND [disabled] = 0";
                        command.Parameters.AddWithValue("@TOKEN", token);
                        if ((int)command.ExecuteScalar() > 0)
                            tokenType = "rj";
                    }
                    if (tokenType == null)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "SELECT COUNT(*) FROM client_users WHERE token = @TOKEN AND activo = 1";
                        command.Parameters.AddWithValue("@TOKEN", token);
                        if ((int)command.ExecuteScalar() > 0)
                            tokenType = "cl";
                    }
                    if (tokenType == null)
                    {
                        return Ok(new { error = "Error 1003, permisos insuficientes" });
                    }
                    token = $"{tokenType}-{token}";

                    // Lookup the token in the timeouts table
                    DateTime? cooldown = GetCoolDown(conn, null, token, "face-extractor");
                    if (cooldown.HasValue)
                    {
                        return Ok(new { error = "Error 4586, límite de ejecuciones sobrepasado", timeout = Math.Round((cooldown.Value - DateTime.Now).TotalSeconds) });
                    }
                    Face? face = await DetectFace(image);
                    if (face == null)
                        result = new { error = "Error 4572, No se ha encontrado ningún rostro en la imagen" };
                    else
                        result = new { error = false, face = face.Value.image };
                    int waitSeconds = Int32.Parse(GetSysConfig(conn, null, "face-extractor-cd"));
                    SetCoolDown(conn, null, token, "face-extractor", DateTime.Now.AddSeconds(waitSeconds));
                }
                catch (Exception)
                {
                    result = new { error = "Error 5571, No se han podido recortar el rostro" };
                }
            }
            return Ok(result);
        }
        #endregion


        #region > DEVICE SYNCHRONIZATION
        /// <summary>
        /// Sync all biometric devices.
        /// </summary>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Operation estimate </returns>
        [HttpGet]
        [Route(template: "sync-all-devices/")]
        public async Task<IActionResult> SyncAllDevices()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("Anviz.Sync", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    await SyncAllDevices(conn);
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5575, No se han podido sincronizar el dispositivo" };
                }
            }
            return Ok(result);
        }

        /// <summary>
        /// Sync a biometric device.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Operation estimate </returns>
        [HttpGet]
        [Route(template: "sync-device/{deviceId}/")]
        public async Task<IActionResult> SyncDevice(string deviceId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.SyncDevice", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    try
                    {
                        if (!await Ping(deviceId))
                            throw new Exception();
                    }
                    catch (Exception)
                    {
                        return Ok(new { error = "Error 5574, El dispositivo no responde. Posiblemente esté apagado, fuera de línea o mal configurado." });
                    }
                    await SyncDevice(deviceId, conn);
                    result = new { error = false, estimate = GetEstimate(deviceId) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5574, No se han podido sincronizar el dispositivo" };
                }
            }
            return Ok(result);
        }
        #endregion


        #region > TASK QUEUE
        /// <summary>
        /// Get the task queue of a device.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="showFailed"> Show failed tasks </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Task queue </returns>
        [HttpGet]
        [Route(template: "get-queue/{deviceId}/{showFailed}/")]
        public IActionResult GetQueue(string deviceId, bool showFailed)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.GetQueue", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            if (deviceId == "null")
                deviceId = null;
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new { error = false, queue = _Helper.AnvizTools.AnvizTools.GetQueue(deviceId, showFailed) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5575, No se han podido obtener la cola de tareas" };
                }
            }
            return Ok(result);
        }


        /// <summary>
        /// Obtains the information of a task by its id.
        /// </summary>
        /// <param name="taskId"> Task id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Task information </returns>
        [HttpGet]
        [Route(template: "get-task/{taskId}/")]
        public IActionResult GetTask(int taskId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.GetTask", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    if (!TryGetTask(taskId, out DeviceTask task))
                        return Ok(new { error = "Error 4572, tarea no encontrada" });
                    if (task.data != null)
                        task.user = JsonSerializer.Deserialize<DeviceUser>(task.data);
                    task.deviceName = GetDeviceName(task.deviceId, conn);

                    result = new { error = false, task };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5578, No se han podido pbtener la tarea" };
                }
            }
            return Ok(result);
        }


        /// <summary>
        /// Tries a failed task again.
        /// </summary>
        /// <param name="taskId"> Task id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Operation estimate </returns>
        [HttpPatch]
        [Route(template: "restart-task/{taskId}/")]
        public IActionResult RestartTask(int taskId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("Anviz.RestartTask", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    int estimate = _Helper.AnvizTools.AnvizTools.RestartTask(taskId);
                    if (estimate == -1)
                        throw new Exception();
                    result = new { error = false, estimate };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5576, No se han podido reiniciar la tarea" };
                }
            }
            return Ok(result);
        }


        /// <summary>
        /// Deletes a task from the task queue.
        /// </summary>
        /// <param name="taskId"> Task id </param>
        /// <param name="securityToken"> Security token </param>
        /// <returns> Operation estimate </returns>
        [HttpDelete]
        [Route(template: "delete-task/{taskId}/")]
        public IActionResult DeleteTask(int taskId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Anviz.DeleteTask", securityToken).Acceso)
                return Ok(new { error = "Error 1001, permisos insuficientes" });
            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    AnvizTools.DeleteTask(taskId);
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5575, No se han podido eliminar la tarea" };
                }
            }
            return Ok(result);
        }
        #endregion



        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        #region OTHER FUNCTIONS
        /// <summary>
        /// Try to get the devices of a center.
        /// </summary>
        /// <param name="workCenterId"> Center id </param>
        /// <param name="devices"> Devices </param>
        /// <param name="conn"> Database connection </param>
        /// <param name="transaction"> Transaction </param>
        /// <returns> True if the devices were found </returns>
        private static bool TryGetDevicesByworkCenterId(string workCenterId, out List<FaceDetectionDevice> devices, SqlConnection conn, SqlTransaction transaction = null)
        {
            devices = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT FDD.* " +
                    "FROM rf_devices_centros RDC " +
                    "INNER JOIN rf_devices FDD ON(FDD.deviceId = RDC.deviceId) " +
                    "WHERE RDC.centroId = @ID";
                command.Parameters.AddWithValue("@ID", workCenterId);
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    devices.Add(new FaceDetectionDevice()
                    {
                        deviceId = reader.GetString(reader.GetOrdinal("deviceId")),
                        serial = reader.GetString(reader.GetOrdinal("serial")),
                        name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
                        type = reader.GetInt32(reader.GetOrdinal("type")),
                        identity_type = reader.GetInt32(reader.GetOrdinal("identity_type")),
                        last_ping = reader.IsDBNull(reader.GetOrdinal("last_ping")) ? null : reader.GetDateTime(reader.GetOrdinal("last_ping"))
                    });
                }
            }
            return devices.Count > 0;
        }

        /// <summary>
        /// List the biometric devices.
        /// </summary>
        /// <param name="showAll"> Show all devices or only those with a name </param>
        /// <param name="conn"> Database connection </param>
        /// <param name="transaction"> Transaction </param>
        /// <returns> List of devices </returns>
        private static List<FaceDetectionDevice> ListDevices(bool showAll, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<FaceDetectionDevice> devices = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                if (showAll)
                    command.CommandText = "SELECT * FROM rf_devices";
                else
                    command.CommandText = "SELECT * FROM rf_devices WHERE name IS NOT NULL";
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    devices.Add(new FaceDetectionDevice()
                    {
                        deviceId = reader.GetString(reader.GetOrdinal("deviceId")),
                        serial = reader.GetString(reader.GetOrdinal("serial")),
                        name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
                        type = reader.GetInt32(reader.GetOrdinal("type")),
                        identity_type = reader.GetInt32(reader.GetOrdinal("identity_type")),
                        last_ping = reader.IsDBNull(reader.GetOrdinal("last_ping")) ? null : reader.GetDateTime(reader.GetOrdinal("last_ping"))
                    });
                }
            }
            return devices;
        }

        /// <summary>
        /// List the biometric devices of a center.
        /// </summary>
        /// <param name="workCenterId"> Center id </param>
        /// <param name="showAll"> Show all devices or only those with a name </param>
        /// <param name="conn"> Database connection </param>
        /// <param name="transaction"> Transaction </param>
        /// <returns> List of devices </returns>
        private static List<SelectableFaceDetectionDevice> ListDevicesByWorkCenter(string workCenterId, bool showAll, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<SelectableFaceDetectionDevice> devices = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT RFD.*, " +
                    "selected = CASE WHEN EXISTS(SELECT * FROM rf_devices_centros RDC WHERE RDC.centroId = @CENTRO AND RDC.deviceId = RFD.deviceId) THEN 1 ELSE 0 END " +
                    "FROM rf_devices RFD " + (showAll ? "" : "WHERE name IS NOT NULL");
                command.Parameters.AddWithValue("@CENTRO", workCenterId);
                SqlDataReader sqlDataReader = command.ExecuteReader();
                using SqlDataReader reader = sqlDataReader;
                while (reader.Read())
                {
                    devices.Add(new SelectableFaceDetectionDevice()
                    {
                        deviceId = reader.GetString(reader.GetOrdinal("deviceId")),
                        serial = reader.GetString(reader.GetOrdinal("serial")),
                        name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
                        type = (DeviceType)reader.GetInt32(reader.GetOrdinal("type")),
                        identity_type = reader.GetInt32(reader.GetOrdinal("identity_type")),
                        last_ping = reader.IsDBNull(reader.GetOrdinal("last_ping")) ? null : reader.GetDateTime(reader.GetOrdinal("last_ping")),
                        selected = reader.GetInt32(reader.GetOrdinal("selected")) == 1
                    });
                }
            }
            return devices;
        }

        public static string GetDeviceName(string deviceId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using SqlCommand command = conn.CreateCommand();
            if (transaction != null)
            {
                command.Connection = conn;
                command.Transaction = transaction;
            }
            command.CommandText = "SELECT COALESCE(name, serial) as name FROM rf_devices WHERE deviceId = @ID";
            command.Parameters.AddWithValue("@ID", deviceId);
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetString(reader.GetOrdinal("name"));
            }
            return "No registrado";
        }

        /// <summary>
        /// Sync all biometric devices.
        /// </summary>
        /// <param name="conn"> Database connection </param>
        /// <param name="transaction"> Transaction </param>
        /// <returns> Operation estimate </returns>
        private static async Task SyncAllDevices(SqlConnection conn, SqlTransaction transaction = null)
        {
            List<string> devices = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT deviceId FROM rf_devices";
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    devices.Add(reader.GetString(reader.GetOrdinal("deviceId")));
                }
            }
            foreach (string deviceId in devices)
            {
                try
                {
                    await SyncDevice(deviceId, conn, transaction);
                }
                catch (Exception) { }
            }
        }
        /// <summary>
        /// Sync a biometric device.
        /// </summary>
        /// <param name="deviceId"> Device id </param>
        /// <param name="conn"> Database connection </param>
        /// <param name="transaction"> Transaction </param>
        /// <returns> Operation estimate </returns>
        private static async Task SyncDevice(string deviceId, SqlConnection conn, SqlTransaction transaction = null)
        {
            // Obtain the employees of the centers that have this device
            // First, the type of the device is obtained. 0 = external, 1 = internal
            List<DeviceUser> users = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                bool isExternal = GetAnvizType(deviceId) == 0;

                if (isExternal)
                {
                    command.CommandText =
                        "SELECT C.id, C.dni, C.cardId " +
                        "FROM candidatos C " +
                        "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                        "INNER JOIN rf_devices_centros RDC ON(RDC.centroId = CE.id) " +
                        "WHERE RDC.deviceId = @ID";
                    command.Parameters.AddWithValue("@ID", deviceId);
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        users.Add(new DeviceUser
                        {
                            rjId = reader.GetString(reader.GetOrdinal("id")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            idd = Dni2idd(reader.GetString(reader.GetOrdinal("dni"))),
                            name = reader.GetString(reader.GetOrdinal("dni")),
                            cardid = reader.IsDBNull(reader.GetOrdinal("cardId")) ? null : reader.GetInt32(reader.GetOrdinal("cardId")).ToString(),
                            pass = reader.IsDBNull(reader.GetOrdinal("cardId")) ? null : reader.GetInt32(reader.GetOrdinal("cardId")).ToString()[^4..],
                            identity_type = GetAnvizIdentityType(deviceId)
                        });
                    }
                }
                else
                {
                    command.CommandText = "SELECT U.id, U.DocID, U.cardId, U.username FROM users U WHERE U.hasToShift = 1";
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        users.Add(new DeviceUser
                        {
                            rjId = reader.GetString(reader.GetOrdinal("id")),
                            dni = reader.GetString(reader.GetOrdinal("DocID")),
                            idd = Dni2idd(reader.GetString(reader.GetOrdinal("DocID"))),
                            name = reader.GetString(reader.GetOrdinal("username")),
                            cardid = reader.IsDBNull(reader.GetOrdinal("cardId")) ? null : reader.GetInt32(reader.GetOrdinal("cardId")).ToString(),
                            pass = reader.IsDBNull(reader.GetOrdinal("cardId")) ? null : reader.GetInt32(reader.GetOrdinal("cardId")).ToString()[^4..],
                            identity_type = GetAnvizIdentityType(deviceId)
                        });
                    }
                }

            }

            // Obtaining the users that are in the device
            List<DeviceUser> deviceUsers = await GetAllEmployees(deviceId);

            // Calculate the missing and the surplus
            List<DeviceUser> usersToInsert = users.Where(u => !deviceUsers.Any(du => u.idd == du.idd)).ToList();
            List<DeviceUser> usersToDelete = deviceUsers.Where(du => !users.Any(u => u.idd == du.idd)).ToList();

            // Insert the tasks
            foreach (DeviceUser user in usersToInsert)
            {
                InsertRegisterTask(deviceId, user);
            }
            foreach (DeviceUser user in usersToDelete)
            {
                InsertRemoveTask(deviceId, user.idd);
            }
        }
        #endregion
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
