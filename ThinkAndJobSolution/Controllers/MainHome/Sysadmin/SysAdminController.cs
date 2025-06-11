using ByteSizeLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Helper.OfficeTools;
using ThinkAndJobSolution.Utils;
using System.IO.Compression;
using static System.Environment;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using System.Text.Json;
using ThinkAndJobSolution.Controllers.Commons;
using System.Text;
using ThinkAndJobSolution.Controllers._Helper.AnvizTools;
using ThinkAndJobSolution.Controllers.Candidate;
using static ThinkAndJobSolution.Controllers._Helper.AnvizTools.AnvizTools;
using SixLabors.ImageSharp.Formats.Jpeg;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.ClientUserController;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace ThinkAndJobSolution.Controllers.MainHome.Sysadmin
{
    [Route("api/v1/sysadmin")]
    [ApiController]
    [Authorize]
    public class SysAdminController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpGet]
        [Route(template: "rename/")]
        public IActionResult Rename()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            try
            {
                string[] companyIds = ListDirectories(new[] { "companies" });
                for (int i = 0; i < companyIds.Length; i++)
                {
                    string companyId = companyIds[i];
                    if (Directory.Exists(companyId + $"{Path.DirectorySeparatorChar}centro"))
                    {
                        string[] centroIds = Directory.GetDirectories(companyId + $"{Path.DirectorySeparatorChar}centro");

                        for (int j = 0; j < centroIds.Length; j++)
                        {
                            string centroId = centroIds[j];
                            if (Directory.Exists(centroId + $"{Path.DirectorySeparatorChar}work"))
                            {
                                string[] workIds = Directory.GetDirectories(centroId + $"{Path.DirectorySeparatorChar}work");
                                for (int x = 0; x < workIds.Length; x++)
                                {
                                    string workId = workIds[x];
                                    if (Directory.Exists(workId))
                                    {
                                        // Comprobamos si existe la carpeta temp, si existe la borramos y la creamos de nuevo
                                        if (Directory.Exists(workId + $"{Path.DirectorySeparatorChar}temp"))
                                        {
                                            Directory.Delete(workId + $"{Path.DirectorySeparatorChar}temp", true);
                                        }
                                        Directory.CreateDirectory(workId + $"{Path.DirectorySeparatorChar}temp");
                                        string[] evaldocs = Directory.GetFiles(workId);
                                        // Renombramos los archivos y los movemos a la carpeta temp
                                        for (int y = 0; y < evaldocs.Length; y++)
                                        {
                                            System.IO.File.Move(evaldocs[y], workId + $"{Path.DirectorySeparatorChar}temp{Path.DirectorySeparatorChar}evaldoc" + y);
                                        }

                                        // Movemos los archivos de la carpeta temp a la carpeta workId y borramos la carpeta temp
                                        string[] tempFiles = Directory.GetFiles(workId + $"{Path.DirectorySeparatorChar}temp");
                                        for (int y = 0; y < tempFiles.Length; y++)
                                        {
                                            System.IO.File.Move(tempFiles[y], workId + $"{Path.DirectorySeparatorChar}evaldoc" + y);
                                        }
                                        Directory.Delete(workId + $"{Path.DirectorySeparatorChar}temp", true);
                                    }
                                }
                            }
                        }
                    }
                }
                result = new { error = false };
            }
            catch (Exception ex)
            {
                result = new
                {
                    error = "Error de ejecución: " + ex.Message
                };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "reformat/")]
        public IActionResult Reformat()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.Reformat", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                bool failed = false;

                //Trimear, eliminar multiples espacios y aplicar pretty case de los candidatos
                if (!failed)
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        //Ejke
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidatos SET \n" +
                                    "nombre = dbo.CapitalizeFirstLetter(TRIM(replace(replace(replace(nombre, ' ', '<>'), '><', ''), '<>', ' '))), \n" +
                                    "apellidos = dbo.CapitalizeFirstLetter(TRIM(replace(replace(replace(apellidos, ' ', '<>'), '><', ''), '<>', ' '))), \n" +
                                    "dni = UPPER(REPLACE(dni, ' ', ''))";

                            try
                            {
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5510, no se ha podido trimear, eliminar multiples espacios y aplicar pretty case de los candidatos."
                                };
                            }
                        }
                    }
                }

                if (!failed)
                {
                    result = new
                    {
                        error = false
                    };

                    LogToDB(LogType.FORMAT, "Formateado de la BD realizado", FindUsernameBySecurityToken(securityToken));
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "all-caps/")]
        public IActionResult AllCaps()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.AllCaps", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                bool failed = false;

                //Poner los nombres de todos los candidatos en mayusculas
                if (!failed)
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidatos SET \n" +
                                    "nombre = UPPER(nombre), \n" +
                                    "apellidos = UPPER(apellidos)";

                            try
                            {
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5511, no se ha podido poner todos los nombres en mayusculas."
                                };
                            }
                        }
                    }
                }

                if (!failed)
                {
                    result = new
                    {
                        error = false
                    };

                    LogToDB(LogType.FORMAT, "Formateado de la BD realizado", FindUsernameBySecurityToken(securityToken));
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "backup/")]
        public IActionResult Backup()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Sysadmin.Backup", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            bool failed = false;

            //Intentar eliminar el backup en su ruta origunal
            if (!failed)
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "EXECUTE master.dbo.xp_delete_file 0, @FILE";

                        command.Parameters.AddWithValue("@FILE", InstallationConstants.BACKUP_DEFAULT_DIR + "DefaultBackup.bak");

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            //No tiene porque ser un fallo porque si no hay fichero no pasa nada
                            //failed = true;
                        }
                    }
                }
            }

            //Ejecutar el backup
            if (!failed)
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "BACKUP DATABASE @DB TO DefaultBackup";

                        command.Parameters.AddWithValue("@DB", conn.Database);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                        }
                    }
                }
            }

            string date = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            string outfile = InstallationConstants.BACKUP_TARGET_DIR + date + ".zip";
            //Copiar el .bak a la carpeta de blobs para hacer un zip facilmente
            if (!failed)
            {
                try
                {
                    System.IO.File.Copy(InstallationConstants.BACKUP_DEFAULT_DIR + "DefaultBackup.bak", Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "thinkandjob", "bd-" + date + ".bak"));
                    ZipFile.CreateFromDirectory(Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "thinkandjob"), outfile);
                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            //Borrar el .bak copiado
            if (!failed)
            {
                try
                {
                    System.IO.File.Delete(Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "thinkandjob", "bd-" + date + ".bak"));
                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            if (!failed)
            {
                LogToDB(LogType.BACKUP, "Copia de seguridad realizada", FindUsernameBySecurityToken(securityToken));
                return Ok(new { error = false, fileName = date, size = new FileInfo(outfile).Length });
            }

            return Ok(new { error = "Error 5981, No se ha podido generar el backup." });
        }

        [HttpGet]
        [Route(template: "download-backup/{date}/")]
        public IActionResult DownloadBackup(string date)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Sysadmin.DownloadBackup", securityToken).Acceso)
            {
                return new ForbidResult();
            }

            string fileName = InstallationConstants.BACKUP_TARGET_DIR + date + ".zip";
            string contentType = "application/zip";
            HttpContext.Response.ContentType = contentType;
            var response = new FileContentResult(System.IO.File.ReadAllBytes(fileName), contentType)
            {
                FileDownloadName = fileName
            };
            return response;
        }

        [HttpPost]
        [Route(template: "update-terms/")]
        public async Task<IActionResult> UpdateTermsAndConditions()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.UpdateTermsAndConditions", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                bool failed = false;
                string newValue = null;
                string key = null;
                if (json.TryGetProperty("text", out JsonElement textJson) && json.TryGetProperty("type", out JsonElement typeJson))
                {
                    newValue = textJson.GetString();
                    key = typeJson.GetString().ToLower();
                    if (!(key.Equals("terms") || key.Equals("cookies")))
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 4514, tipo no valido."
                        };
                    }
                }
                else
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 4513, no se ha proporcionado texto o tipo."
                    };
                }

                if (!failed)
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        SetSysConfig(conn, null, key, newValue);

                        if (!failed && key.Equals("terms"))
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE candidatos SET terminosAceptados = 0";

                                try
                                {
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5576, no se ha podido reiniciar la aceptacion de terminos y condiciones."
                                    };
                                }
                            }
                        }

                        if (!failed && key.Equals("terms"))
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE client_users SET terminosAceptados = 0";

                                try
                                {
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5576, no se ha podido reiniciar la aceptacion de terminos y condiciones."
                                    };
                                }
                            }
                        }

                        if (!failed && key.Equals("terms"))
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE users SET terminosAceptados = 0";

                                try
                                {
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5576, no se ha podido reiniciar la aceptacion de terminos y condiciones."
                                    };
                                }
                            }
                        }


                    }
                }


                if (!failed)
                {
                    result = new
                    {
                        error = false
                    };

                    LogToDB(LogType.TERMS_UPDATED, "Terminos y condiciones actualizados", FindUsernameBySecurityToken(securityToken));
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-terms/{type}")]
        public IActionResult GetTermsAndConditions(string type)
        {
            return Ok(HelperMethods.GetTermsAndConditions(type));
        }

        [HttpPost]
        [Route(template: "log-intrusion")]
        public async Task<IActionResult> LogIntrusion()
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("user", out JsonElement userJson) && json.TryGetProperty("section", out JsonElement sectionJson))
            {
                string user = userJson.GetString();
                string section = sectionJson.GetString();

                LogToDB(LogType.INTRUSION, "Intento de acceso a " + section + " sin permiso para ello.", user);

                result = new
                {
                    error = false
                };
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "log/{onlyNew}/{page?}/{type?}")]
        public IActionResult ListLogs(bool onlyNew, int? page, string? type)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            return Ok(listLogsFiltered(securityToken, page, type, true, onlyNew));
        }

        [HttpGet]
        [Route(template: "log/count-new/{type?}")]
        public IActionResult CountNewLogs(int? page, string? type)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            return Ok(countNewLogsFiltered(securityToken, page, type, true));
        }


        [HttpGet]
        [Route(template: "stop-long-workshifts/")]
        public async Task<IActionResult> StopLongWorkshifts()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.StopLongWorkshifts", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {

                List<string> candidates = new();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "UPDATE workshifts SET endTime = getdate() " +
                                "output inserted.[candidateId] " +
                                "WHERE DATEDIFF(second, startTime, getdate()) / 3600.0 > 12 AND endTime IS NULL";

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                                candidates.Add(candidateId);
                            }
                        }

                        result = new { error = false, candidates };
                    }
                    await PushNotificationController.sendNotifications(candidates.Select(id => new PushNotificationController.UID() { type = "ca", id = id }), new() { title = "Interrupción de jornada laboral", body = "Su fijachaje ha sido sido interrumpido debido a que ha superado el límite de horas permitidas. Le recordamos que es importante que tome en cuenta esta situación para futuras ocasiones, y que gestione su tiempo de trabajo de manera adecuada. ", type = "candidate-shift-start" });



                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "conflicto-incidencias-pendientes-antiguas/")]
        public IActionResult ConflictoIncidenciasPendientesAntiguas()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.ConflictoIncidenciasPendientesAntiguas", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "UPDATE incidencia_falta_asistencia SET state = 'conflicto' WHERE (state = 'pendiente-cliente' OR state = 'pendiente-candidato') AND creationTime < DATEADD(DAY, -3, getdate())";

                        command.ExecuteNonQuery();

                        result = new { error = false };
                    }
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "measure-size/")]
        public IActionResult MeasureSize()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.MeasureSize", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                string dbName = null, dbSize = null, dbPercent = null, dbMax = "10 GB";
                string diskName = null, diskSize = null, diskPercent = null, diskMax = "100 GB";

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "exec sp_spaceused";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dbName = reader.GetString(reader.GetOrdinal("database_name"));
                                dbSize = reader.GetString(reader.GetOrdinal("database_size"));
                                dbPercent = $"{(ByteSize.Parse(dbSize).Bytes / ByteSize.Parse(dbMax).Bytes) * 100:0.##}%";

                            }
                        }
                    }
                }
                diskName = DriveInfo.GetDrives()[0].Name;
                long bytes = getDirectorySize(DISK_DIR);
                ByteSize parsedBytes = ByteSize.FromBytes(bytes);
                diskSize = $"{parsedBytes.LargestWholeNumberBinaryValue:0.##} {parsedBytes.LargestWholeNumberBinarySymbol}";
                diskPercent = $"{(bytes / ByteSize.Parse(diskMax).Bytes) * 100:0.##}%";

                result = new { error = false, db = new { name = dbName, size = dbSize, percent = dbPercent, max = dbMax }, disk = new { name = diskName, size = diskSize, percent = diskPercent, max = diskMax } };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "purge-old-workshifts/")]
        public IActionResult PurgeOldWorkshifts()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.PurgeOldWorkshifts", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            DateTime now = DateTime.Now;
            DateTime month = new DateTime(now.Year, now.Month, 1);
            DateTime lastMonthStart = month.AddMonths(-1);
            DateTime lastMonthEnd = month.AddDays(-1);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT candidateId, localId = CAST(DATEDIFF(s, '1970-01-01 00:00:00', startTime) AS BIGINT) " +
                                                                "FROM workshifts " +
                                                                "WHERE startTime > @START AND startTime < @END";
                    command.Parameters.AddWithValue("@START", lastMonthStart);
                    command.Parameters.AddWithValue("@END", lastMonthEnd);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                            Int64 localId = reader.GetInt64(reader.GetOrdinal("localId"));

                            DeleteDir(new[] { "candidate", candidateId, "workshifts", localId.ToString() });
                        }
                    }
                }

                result = new { error = false };
            }


            return Ok(result);
        }

        [HttpGet]
        [Route(template: "measure-cartero-load/")]
        public IActionResult MeasureCarteroLoad()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.MeasureCarteroLoad", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            if (CARTERO_CONNECTION_STRING == null)
            {
                return Ok(new { error = "Error 5205, No se puede conectar con el cartero." });
            }

            Dictionary<int, int> avgWait30days = new(), avgWait7days = new();
            int maxPerDay = 0;
            Dictionary<DateTime, int> lastOverflows = new();
            double avgLoad7days = 0;

            using (SqlConnection conn = new SqlConnection(CARTERO_CONNECTION_STRING))
            {
                conn.Open();

                //Calcular capacidad maxima de emails por dia
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT SUM(refill_uses*(1440/refill_time)) FROM sources";
                    maxPerDay = Convert.ToInt32(command.ExecuteScalar());
                }

                //Obtener los overflows que han ocurrido en los ultimos 30 dias
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                            "SELECT DATEADD(dd, DATEDIFF(dd, 0, date_creation), 0) as day, COUNT(*) as number " +
                            "FROM logs " +
                            "WHERE date_creation > DATEADD(d, -30, DATEADD(dd, DATEDIFF(dd, 0, getdate()), 0)) " +
                            "GROUP BY DATEADD(dd, DATEDIFF(dd, 0, date_creation), 0) " +
                            "HAVING COUNT(*) > @LIMIT";
                    command.Parameters.AddWithValue("@LIMIT", maxPerDay);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lastOverflows[reader.GetDateTime(reader.GetOrdinal("day")).Date] = reader.GetInt32(reader.GetOrdinal("number"));
                        }
                    }
                }

                //Calcular tiempo de espera medio durante los ultimos 30 dias para cada prioridad
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                            "SELECT priority, AVG(DATEDIFF(mi, date_creation, date_send)) as average " +
                            "FROM logs " +
                            "WHERE date_creation > DATEADD(d, -30, DATEADD(dd, DATEDIFF(dd, 0, getdate()), 0)) GROUP BY priority";
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            avgWait30days[reader.GetInt32(reader.GetOrdinal("priority"))] = reader.GetInt32(reader.GetOrdinal("average"));
                        }
                    }
                }

                //Calcular tiempo de espera medio durante los ultimos 7 dias para cada prioridad
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                            "SELECT priority, AVG(DATEDIFF(mi, date_creation, date_send)) as average " +
                            "FROM logs " +
                            "WHERE date_creation > DATEADD(d, -7, DATEADD(dd, DATEDIFF(dd, 0, getdate()), 0)) GROUP BY priority";
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            avgWait7days[reader.GetInt32(reader.GetOrdinal("priority"))] = reader.GetInt32(reader.GetOrdinal("average"));
                        }
                    }
                }

                //Calcular la carga promedio de los ultimos 7 dias
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                            "SELECT (100.0*COUNT(*))/(@LIMIT*7.0) " +
                            "FROM logs " +
                            "WHERE date_creation > DATEADD(d, -7, DATEADD(dd, DATEDIFF(dd, 0, getdate()), 0))";
                    command.Parameters.AddWithValue("@LIMIT", maxPerDay);
                    avgLoad7days = Convert.ToDouble(command.ExecuteScalar());
                }

            }

            return Ok(new { maxPerDay, lastOverflows, avgWait30days, avgWait7days, avgLoad7days, error = false });
        }

        [HttpPost]
        [Route(template: "mass-upload-cpds/")]
        public async Task<IActionResult> MassUploadCPDs()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.MassUploadCPDs", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            List<string> notFound = new(), existing = new();
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    foreach (JsonElement element in json.EnumerateArray())
                    {
                        string fullName = element.GetProperty("name").GetString();
                        string base64 = element.GetProperty("base64").GetString();
                        if (!fullName.Contains("_CPD_"))
                        {
                            notFound.Add(fullName);
                            continue;
                        }

                        string cpdId = ComputeStringHash(fullName); //Usar el nombre del fichero para evitar que se suban duplicados
                        string name = fullName.Split("_CPD_")[0];

                        //Interntar obtener la ID de la empresa
                        string companyId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT id FROM empresas WHERE nombre LIKE @NAME";
                            command.Parameters.AddWithValue("@NAME", name);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    companyId = reader.GetString(reader.GetOrdinal("id"));
                        }
                        if (companyId == null)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT companyId FROM centros WHERE alias = @NAME OR alias = CONCAT(@NAME, ' Principal')";
                                command.Parameters.AddWithValue("@NAME", name);
                                using (SqlDataReader reader = command.ExecuteReader())
                                    if (reader.Read())
                                        companyId = reader.GetString(reader.GetOrdinal("companyId"));
                            }
                        }
                        if (companyId == null)
                        {
                            notFound.Add(name);
                            continue;
                        }

                        //Comprobar que no exista ya
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT id FROM cpd WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", cpdId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    existing.Add(fullName);
                                    continue;
                                }
                            }
                        }

                        //Insertar el CPD
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO cpd " +
                                                                        "(id, companyId, date) VALUES " +
                                                                        "(@ID, @COMPANY, @DATE)";

                            command.Parameters.AddWithValue("@ID", cpdId);
                            command.Parameters.AddWithValue("@COMPANY", companyId);
                            command.Parameters.AddWithValue("@DATE", DateTime.Now);
                            command.ExecuteNonQuery();
                        }

                        SaveFile(new[] { "companies", companyId, "cpd", cpdId, "cpd" }, base64);
                    }

                }
                result = new { error = false, notFound, existing };
            }
            catch (Exception e)
            {
                result = new { error = e.Message, notFound, existing };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-candidates-without-contrato/")]
        public IActionResult GetCandidatesWithoutContrato()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.GetCandidatesWithoutContrato", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> candidates = new();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT C.id, C.dni FROM candidatos C WHERE NOT EXISTS(SELECT * FROM contratos CO WHERE CO.candidateId = C.id)";
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            candidates.Add(new
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                dni = reader.GetString(reader.GetOrdinal("dni"))
                            });
                        }
                    }
                }

                result = new { error = false, candidates };
            }


            return Ok(result);
        }

        [HttpPost]
        [Route(template: "test-word-template/")]
        public async Task<IActionResult> TestWordTemplate()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.TestWordTemplate", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("template", out JsonElement templateJson) &&
                    json.TryGetProperty("inserts", out JsonElement insertsJson) &&
                    json.TryGetProperty("convertToPdf", out JsonElement convertToPdfJson))
            {
                string template = templateJson.GetString();
                List<Insert> inserts = new();
                bool convertToPdf = GetJsonBool(convertToPdfJson) ?? false;
                foreach (JsonElement insertJson in insertsJson.EnumerateArray())
                {
                    if (insertJson.TryGetProperty("id", out JsonElement idJson) &&
                            insertJson.TryGetProperty("type", out JsonElement typeJson) &&
                            insertJson.TryGetProperty("value", out JsonElement valueJson))
                    {
                        switch (typeJson.GetString())
                        {
                            case "text":
                                inserts.Add(new TextInsert(idJson.GetString(), valueJson.GetString()));
                                break;
                            case "img":
                                inserts.Add(new ImageInsert(idJson.GetString(), valueJson.GetString()));
                                break;
                            case "list":
                                inserts.Add(new ListInsert(idJson.GetString(), GetJsonStringList(valueJson)));
                                break;
                        }
                    }
                }

                try
                {
                    await OfficeTools.prepareCombinedImages(template, inserts);
                    result = new { error = false, file = await OfficeTools.applyTemplateProcessor(template, inserts, convertToPdf) };
                }
                catch (Exception e)
                {
                    result = new { error = "Error 5880, " + e.Message };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "test-control-horario/")] //TODO: Delete this
        public async Task<IActionResult> TestControlHorario()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.TestControlHorario", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            try
            {
                string deviceId = "d1";
                string groupName = "PRIMOR Almacenes";
                string candidateName = "Marcos Antonio";
                int state = 0;
                DateTime start = DateTime.Now;
                DateTime? end = DateTime.Now;
                //DateTime? turnoStart = new DateTime(2022, 11, 29, 17, 30, 0);
                //DateTime? turnoEnd = new DateTime(2022, 11, 29, 20, 0, 0);
                DateTime? turnoStart = null;
                DateTime? turnoEnd = null;

                var value = await AnvizTools.Workshift(deviceId, groupName, candidateName, state, start, end, turnoStart, turnoEnd);

                result = new { error = false, value };
            }
            catch (Exception e)
            {
                result = new { error = "Error 5880, " + e.Message };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "update-all-candidates/")] //TODO: Delete this
        public async Task<IActionResult> UpdateAllCandidates()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.UpdateAllCandidates", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        List<string> ids = new();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT id FROM candidatos";
                            using (SqlDataReader reader = command.ExecuteReader())
                                while (reader.Read())
                                    ids.Add(reader.GetString(reader.GetOrdinal("id")));
                        }

                        foreach (string id in ids)
                        {
                            CandidateController.updateCandidateData(conn, transaction, new _Model.Candidate.CandidateStats() { id = id });
                        }

                        transaction.Commit();
                        result = new { error = false, n = ids.Count };
                    }
                    catch (Exception e)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5880, " + e.Message };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "test-calendar-userevent/")] //TODO: Delete this
        public IActionResult TestCalendarUserEvent()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.TestCalendarUserEvent", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "INSERT INTO calendar_userevent (id, title, description, category, date, timeStart, timeEnd) " +
                                "VALUES (@ID, @TITLE, @DESCRIPTION, @CATEGORY, @DATE, @START, @END)";
                        command.Parameters.AddWithValue("@ID", ComputeStringHash("r"));
                        command.Parameters.AddWithValue("@TITLE", "Titulazo");
                        command.Parameters.AddWithValue("@DESCRIPTION", "Una descripcion");
                        command.Parameters.AddWithValue("@CATEGORY", "Informacion");
                        command.Parameters.AddWithValue("@DATE", new DateTime(2022, 12, 5));
                        command.Parameters.AddWithValue("@START", new TimeSpan(8, 0, 0));
                        command.Parameters.AddWithValue("@END", new TimeSpan(14, 0, 0));
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "SELECT * FROM calendar_userevent";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime date = reader.GetDateTime(reader.GetOrdinal("date"));
                                TimeSpan start = reader.GetTimeSpan(reader.GetOrdinal("timeStart"));
                                TimeSpan end = reader.GetTimeSpan(reader.GetOrdinal("timeEnd"));

                                DateTime dateStart = date.Add(start);
                                DateTime dateEnd = date.Add(end);

                                result = new { date, dateStart, dateEnd };
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result = new { error = "Error 5880, " + e.Message };
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "orient-candidate-photos/")]
        public async Task<IActionResult> OrientCandidatePhotos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.OrientCandidatePhotos", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<string> candidates = new();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT id FROM candidatos";
                    using (SqlDataReader reader = command.ExecuteReader())
                        while (reader.Read())
                            candidates.Add(reader.GetString(reader.GetOrdinal("id")));
                }
            }

            //candidates = new() { "1f17ae64f10cb9f4999830ce0fdf8a2ece71ab8f4bdbcc927c278cbf2eaf4f20" };
            /*
			candidates = new () {
					"489f664aff2ab954bf2298b4adcc14eb1bf43c036e4e86e291a0155182b86e00",
					"7f2570e103c593fd97113d16c1cdffc55405734a43cb3d5c970cc07290100677",
					"202a896201aa5df19da9ba30fa7e46b19635c014631dd291f88e5b37adbdbef6",
					"532b61d6ba01bf6b0b0ace81b9cb2fda5c4a7b276100b68e5733b6587f3c5cc1",
					"2398d43d94d531cf92a6bd74b18922f59c97e6e22c26d87e88a541ec15097891",
					"22af46269f208d289f29bc70cada161b04d0cd597892960291ce1f2cf45248f2",
					"02f3a09aefcfd5d8e5b333a3007cbfa85816be87ea713c14a4a705ca29d3ab71",
					"73caf6c8b72eac72e65ffcb29f829dd1092e826d0d13924296a026901b7cad10",
					"8a5285d2d88499e01edb246c1e9f36414b44caa25b0b62a7793de75d54474b39",
					"6a9f034a11f7faee22cb9cab747f1fd874a69f183d9a3f3ba8e1116af52312f5",
					"1e9c27ab871117979e92839cca4e59b46abb6b1d702a8e3330c289deb4005f77",
					"990f5a3bcf97aee23e7abceeafd70c31e56be0b6239dfbd29ff88b5d0b3a4479",
					"f4d15da057d7e7a900802082441535aa7897e0538627c88965aa1b1d3d08f3ae",
					"96924d050374fd4246bba9dd432d250bd198ea66331998e325d4c7cf59ca2062",
					"7f9df50043ac63e38690328973c72c6a39f84dec25ba9255614bd4f2bdad02e3",
					"90b17aba1484b0d785d02b707964d1cc83c3eb9fb2531429c1bcc19e3881c033",
					"122e550edf73c76d134dfb7acbcc51a24707a8eed3f2fd71516685756ef95f18",
					"4b6f9cf0899dafec97884bd567b061f495ce76cea7697e894b7beae085a40833",
					"06e4875c81af1783fd092f2c4f0c1a2a63102345e30c27f1c8b84b1c15e8ebd5",
					"2a3ed6213cf52c5d54388266a1d579b3ae90e7121c6a0c7e96fcb4905c84f810",
					"04834c673b307726e0febcfbda3a814b101e9d5d13f4863a028becf938468136",
					"75197f1f3e905059846b71aaa7bccf0bf057e195bf9317b4ec320a153f34c367",
					"2818e2f027a6d0de59d0f8d5440259fd98664c058da1e11a51539f24ab3ab3c2",
					"050cc63c60213fe2d01477d98d7e4aac74b5fe8e42cfe7d83bd27d68e20d371f",
					"fae5134170c16337b11207e0dc6cba356b900d66ff694922a2f34ce6a2dbc765",
					"9f3080ebc4355197443c818132c4147321cf092fa07b738a613fed91100b4692",
					"46e0b2fb2c8fba01c6057c68bd871e0aa1ebf5bd21387871a8c47bfe2f048049",
					"85bafa4b486b952fcd9ca2b76ea45636c95dc5396709fb13d0d885e1d6e57bf7",
					"70189146464b705ed9f5becc0f449dc185c52b58561ccc2b4ad1f38d26eeba84",
					"44793e73536ceabb11a283d27b0bb123346e9e3b5e71d9a8c773a5ace3546d29",
					"59684f43dce2064cbf9114ee6a149cfcad38472e05dcb13ea3076cdc25179221",
					"1cf21bd14d92aafd225bcec400d8ea726f67348fd10645c7afc901ae0262bd70",
					"35ff3ece7b39b718e6c25a15b7fbfee3b66214205956a2e3c6b978669809e662",
					"56ab0d206ee4e120d4c79aa731b586dba606d9c84f8042ff9080d6c2704852a5",
					"0fc116e3325d5f2f3029e0a5189850f761c76d7f505571055bce00a3e71847c0",
					"85f6a476dbde349a73d13ab414654f2a7d7b1dc56fee28b17183126e201f43b3",
					"076cbe0e594f117b4b470973dee66b34ff2f10fe02aa4a6b9e17f271d0ac5588",
					"5c732db2fe672fb12e376ecc9faa4f95800360ab4ff58d8c569fa070a73910f8",
					"0407dfbd5d63436d156d51966c2a9cfd6325f12faf00e1241512075b724f7a13",
					"757b0ee590a2ed04224af2ddc9cbce298ace018714e7558903db4716f90f75d8",
					"8d6f3d12052e239bd4108aa0d503b231cbfc066a195ddc8ffd46519ab6a10568",
					"31f12c550269eb671ef19e0a74db235343c2e03d03056e1826dbe55548b39a22",
					"5327bf334e5b2846daae183e4a8d05a2ef04601e633f2fa78fa86c0d9ea3a4de",
					"42c218dcc4a3e0df4fe68027a024aec51ad948fdbc8d00994cc65e3b9cb02405",
					"793a554fb705a419cf1c7b4cdac17d825e4b6ce9383d245cf052219e763b75a0",
					"d86bc0925ddaf784f322158f452be24b67e0fab695f2b96b84762c80a517a2a2",
					"35a7f2064ac72b1281bfd11799932fe1af663187d87c9594de83a808aeea9730"
			};
			*/
            int rotated = 0, detected = 0, total = 0;
            List<string> changed = new(), faceNotFound = new();
            foreach (string candidate in candidates)
            {
                string picture = ReadFile(new[] { "candidate", candidate, "photo" });
                if (picture == null) continue;
                total++;

                try
                {
                    picture = RotateImage(picture, null);
                    SaveFile(new[] { "candidate", candidate, "photo" }, picture);

                    Face? face = await DetectFace(picture);

                    if (face == null)
                    {
                        faceNotFound.Add(candidate);
                        continue;
                    }
                    detected++;

                    if (face.Value.orientation == 0) continue;
                    SaveFile(new[] { "candidate", candidate, "photo" }, RotateImage(picture, face.Value.orientation * 90));
                    changed.Add(candidate);
                    rotated++;
                }
                catch (Exception) { }
            }

            result = new { error = false, rotated, detected, total, changed, faceNotFound };


            return Ok(result);
        }

        [HttpPost]
        [Route(template: "test-image-date-stamping/")]
        public async Task<IActionResult> TestImageDateStamping()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Sysadmin.TestImageDateStamping", securityToken).Acceso)
                return Forbid();

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("image", out string image) && json.TryGetInt32("fontSize", out int fontSize))
            {
                try
                {
                    JpegEncoder encoder = new JpegEncoder() { Quality = 50 };
                    image = StampTimestamp(image, true, 256, encoder, fontSize);
                    if (image.Contains(","))
                        image = image.Split(",")[1];
                    return File(Convert.FromBase64String(image), "image/jpeg");
                }
                catch (Exception e)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, e);
                }
            }

            return BadRequest();
        }

        [HttpGet]
        [Route(template: "purge-old-client-logs/")]
        public IActionResult PurgeOldClientLogs()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.PurgeOldClientLogs", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            DateTime date = DateTime.Now.AddDays(-90).Date;

            List<string> users = new();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT DISTINCT id FROM client_users";
                    using (SqlDataReader reader = command.ExecuteReader())
                        while (reader.Read())
                            users.Add(reader.GetString(reader.GetOrdinal("id")));
                }
            }

            using (SqlConnection conn = new SqlConnection(InstallationConstants.LOGS_CL_CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "DELETE FROM logs WHERE date < @DATE";
                    command.Parameters.AddWithValue("@DATE", date);
                    command.ExecuteNonQuery();
                }

                List<string> logsUsers = new();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT DISTINCT userId FROM logs";
                    using (SqlDataReader reader = command.ExecuteReader())
                        while (reader.Read())
                            logsUsers.Add(reader.GetString(reader.GetOrdinal("userId")));
                }

                List<string> usersToDelete = logsUsers.Where(u => !users.Contains(u)).ToList();
                if (usersToDelete.Count > 0)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM logs WHERE userId = @USER";
                        command.Parameters.Add("@USER", System.Data.SqlDbType.VarChar);
                        foreach (string user in usersToDelete)
                        {
                            command.Parameters["@USER"].Value = user;
                            command.ExecuteNonQuery();
                        }
                    }
                }

                result = new { error = false };
            }


            return Ok(result);
        }

        [HttpGet]
        [Route(template: "send-daily-dashboard/")]
        public async Task<IActionResult> SendDailyDashboard()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.SendDailyDashboard", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            try
            {
                DateTime today = DateTime.Now;

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Obtener los usuarios cliente activos con email
                    List<ClientUser> users = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id, email, token FROM client_users WHERE activo = 1 AND visible = 1";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                    clientToken = reader.GetString(reader.GetOrdinal("token"))
                                });
                            }
                        }
                    }
                    List<string> usersNotified = new();
                    foreach (ClientUser user in users)
                    {
                        ClientUserAccess access = getClientUserAccess(user.clientToken, false, conn);
                        ClientDashboard dash = getClientDashboard(user.clientToken, access, conn);
                        if (dash.empty) continue;

                        usersNotified.Add(user.id);
                        if (user.email == null) continue;

                        List<string> lines = new();

                        if (dash.pendingCommsClientClient > 0)
                        {
                            if (dash.pendingCommsClientClient == 1)
                                lines.Add("Hay mensajes sin leer en una conversación.");
                            else
                                lines.Add($"Hay mensajes sin leer en {dash.pendingCommsClientClient} conversaciones.");
                        }
                        if (dash.pendingIncidencesNotAttend > 0)
                        {
                            if (dash.pendingIncidencesNotAttend == 1)
                                lines.Add("Una incidencia requiere atención.");
                            else
                                lines.Add($"{dash.pendingIncidencesNotAttend} incidencias requieren atención.");
                        }
                        if (dash.pendingCandidateIncidences > 0)
                        {
                            if (dash.pendingCandidateIncidences == 1)
                                lines.Add("Una incidencia de trabajador tiene cambios sin leer.");
                            else
                                lines.Add($"{dash.pendingCandidateIncidences} incidencias de trabajador tienen cambios sin leer.");
                        }
                        if (dash.candidatesWithoutHorario > 0)
                        {
                            if (dash.candidatesWithoutHorario == 1)
                                lines.Add("Un trabajador no tiene turnos para la semana que viene.");
                            else if (dash.candidatesWithoutHorario > 1 && dash.candidatesWithoutHorarioCentros == 1)
                                lines.Add($"{dash.candidatesWithoutHorario} trabajadores no tienen turnos para la semana que viene.");
                            else
                                lines.Add($"{dash.candidatesWithoutHorario} trabajadores, de varios centros, no tienen turnos para la semana que viene.");
                        }
                        if (dash.pendingChecks > 0)
                        {
                            if (dash.pendingChecks == 1)
                                lines.Add("Un centro no tiene la verificación completa.");
                            else
                                lines.Add($"{dash.pendingChecks} centros no tiene la verificación completa.");
                        }
                        if (dash.pendingUploadingExtraHours > 0)
                        {
                            if (dash.pendingUploadingExtraHours == 1)
                                lines.Add("Falta el reporte de extras de un centro.");
                            else
                                lines.Add($"Falta el reporte de extras de {dash.pendingUploadingExtraHours} centros.");
                        }
                        if (dash.pendingValidatingExtraHours > 0)
                        {
                            if (dash.pendingValidatingExtraHours == 1)
                                lines.Add("Un reporte de extras requiere revisión y validación.");
                            else
                                lines.Add($"{dash.pendingValidatingExtraHours} reportes de extras requieren revisión y validación.");
                        }
                        if (dash.pendingEvalDocs > 0)
                        {
                            if (dash.pendingEvalDocs == 1)
                                lines.Add("Un puesto de trabajo necesita un documento de evaluación.");
                            else
                                lines.Add($"{dash.pendingEvalDocs} puestos de trabajo necesitan documentos de evaluación.");
                        }
                        if (dash.pendingSignContratos > 0)
                        {
                            if (dash.pendingSignContratos == 1)
                                lines.Add("Un contrato está pendiente de ser firmado.");
                            else
                                lines.Add($"{dash.pendingSignContratos} contratos están pendientes de ser firmados.");
                        }
                        if (dash.pendingCPDSign > 0)
                        {
                            if (dash.pendingCPDSign == 1)
                                lines.Add("Un CPD está pendiente de ser firmado.");
                            else
                                lines.Add($"{dash.pendingCPDSign} CPDs están pendientes de ser firmados.");
                        }
                        /*
						if (dash.pendingCondicionesEconomicas > 0)
						{
								if (dash.pendingCondicionesEconomicas == 1)
										lines.Add("Un trabajo no tiene establecidas las condiciones económicas.");
								else
										lines.Add($"{dash.pendingCondicionesEconomicas} trabajos no tienen establecidas las condiciones económicas.");
						}
						*/

                        if (access.isSuper)
                            lines.Reverse();

                        StringBuilder sb = new();
                        bool darken = false;
                        foreach (string line in lines)
                        {
                            string bg = darken ? "#B3B3B3" : "#E6E6E6";
                            sb.Append("<tr style='background-color:");
                            sb.Append(bg);
                            sb.Append(";'><td style='text-align:left; width: 100%;'><div style='padding: 5px;'>");
                            sb.Append(line);
                            sb.Append("</div></td></tr>");
                            darken = !darken;
                        }

                        EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "clientDashboardDayly",
                            inserts = new() {
                                                                { "dashboard", sb.ToString() }
                                                        },
                            toEmail = user.email,
                            subject = $"[Think&Job] Novedades {today:dd/MM/yyyy}",
                            priority = EventMailer.EmailPriority.MODERATE
                        });
                    }
                    await PushNotificationController.sendNotifications(usersNotified.Select(id => new PushNotificationController.UID() { type = "cl", id = id }), new()
                    {
                        title = "Novedades en la app",
                        body = "Acceda a la aplicación para gestionar sus tareas",
                        type = "client-dashboard"
                    }, conn);
                }
                result = new { error = false };
            }
            catch (Exception)
            {
                result = new { error = "Error 5689, no se ha podido enviar el email diario a los clientes" };
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "revert-files-to-correct-format/")]
        public IActionResult RevertFilesToCorrectFormat()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Sysadmin.RevertFilesToCorrectFormat", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }
            try
            {
                revertFiles(ComposePath(System.Array.Empty<string>()), new[] { "stored_sign", "icon", "signBase64Image" });
                result = new { error = false };
            }
            catch (Exception e)
            {
                result = new { error = "Error 5700: " + e.Message };
            }
            return Ok(result);
        }



        [HttpPost]
        [Route(template: "set-festivos-locales/")]
        public async Task<IActionResult> SetFestivosLocales()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.SetFestivosLocales", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetInt32("ano", out int ano) &&
                    json.TryGetStringList("festivos", out List<string> nuevosFestivos) &&
                    json.TryGetString("localidad", out string localidad) &&
                    json.TryGetString("provincia", out string provincia))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        //Obtener la referencia de la localidad
                        int? localidadRef = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT ref FROM const_localidades WHERE nombre = @NOMBRE";
                            command.Parameters.AddWithValue("@NOMBRE", localidad);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    localidadRef = reader.GetInt32(reader.GetOrdinal("ref"));
                        }

                        if (localidadRef == null)
                            return Ok(new { error = "Error 4881, Localidad no encontrada" });

                        //Obtener los festivos de la provincia
                        Constants.FestivoType[] festivosProvincia = Constants.getFestivos(ano, provincia, null, conn);

                        //Sumarle los festivos nuevos
                        foreach (string nuevoFestivo in nuevosFestivos)
                        {
                            DateTime date = DateTime.Parse(nuevoFestivo);
                            if (festivosProvincia[date.DayOfYear - 1] == Constants.FestivoType.NADA)
                                festivosProvincia[date.DayOfYear - 1] = Constants.FestivoType.LOCAL;
                        }

                        //Borrar el calendario si ya tenia uno
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM const_festivos WHERE nivel = 3 AND localidadRef = @LOCALIDAD";
                            command.Parameters.AddWithValue("@LOCALIDAD", localidadRef.Value);
                            command.ExecuteNonQuery();
                        }

                        //Establecer los festivos de la localidad
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO const_festivos (localidadRef, ano, festivos, nivel) VALUES (@LOCALIDAD, @ANO, @FESTIVOS, 3)";
                            command.Parameters.AddWithValue("@LOCALIDAD", localidadRef.Value);
                            command.Parameters.AddWithValue("@ANO", ano);
                            command.Parameters.AddWithValue("@FESTIVOS", string.Join("", festivosProvincia.Select(f =>
                            {
                                switch (f)
                                {
                                    case Constants.FestivoType.LOCAL: return '3';
                                    case Constants.FestivoType.AUTONOMICA: return '2';
                                    case Constants.FestivoType.NACIONAL: return '1';
                                    default: return '0';
                                }
                            }).ToArray()));
                            command.ExecuteNonQuery();
                        }
                    }

                    result = new { error = false };
                }
                catch (Exception e)
                {
                    result = new { error = "Error 5880, " + e.Message };
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "remove-festivos-locales/")]
        public async Task<IActionResult> RemoveFestivosLocales()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.RemoveFestivosLocales", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetInt32("ano", out int ano) &&
                    json.TryGetString("localidad", out string localidad))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        //Obtener la referencia de la localidad
                        int? localidadRef = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT ref FROM const_localidades WHERE nombre = @NOMBRE";
                            command.Parameters.AddWithValue("@NOMBRE", localidad);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    localidadRef = reader.GetInt32(reader.GetOrdinal("ref"));
                        }

                        if (localidadRef == null)
                            return Ok(new { error = "Error 4881, Localidad no encontrada" });

                        //Borrar el calendario si ya tenia uno
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM const_festivos WHERE nivel = 3 AND localidadRef = @LOCALIDAD";
                            command.Parameters.AddWithValue("@LOCALIDAD", localidadRef.Value);
                            command.ExecuteNonQuery();
                        }
                    }

                    result = new { error = false };
                }
                catch (Exception e)
                {
                    result = new { error = "Error 5880, " + e.Message };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-localidades-without-festivos/{ano}/")]
        public IActionResult GetLocadadesWithoutFestivos(int ano)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.GetLocadadesWithoutFestivos", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Obtener la referencia de la localidad
                    Dictionary<string, List<string>> localidades = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                "SELECT DISTINCT P.nombre as provinciaNombre, L.nombre as localidadNombre " +
                                "FROM centros CE " +
                                "INNER JOIN const_localidades L ON(CE.poblacion = L.nombre) " +
                                "INNER JOIN const_provincias P ON(L.provinciaRef = P.ref) " +
                                "WHERE NOT EXISTS(SELECT * FROM const_festivos WHERE localidadRef = L.ref AND ano = @ANO) " +
                                "ORDER BY P.nombre, L.nombre ";
                        command.Parameters.AddWithValue("@ANO", ano);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string provincia = reader.GetString(reader.GetOrdinal("provinciaNombre"));
                                string localidad = reader.GetString(reader.GetOrdinal("localidadNombre"));
                                if (!localidades.ContainsKey(provincia))
                                    localidades[provincia] = new();
                                localidades[provincia].Add(localidad);
                            }
                        }
                    }

                    result = new { error = false, localidades };
                }
            }
            catch (Exception e)
            {
                result = new { error = "Error 5880, " + e.Message };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "update-candidate-cache/")]
        public async Task<IActionResult> UpdateCandidatesCache()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.UpdateCandidatesCache", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        List<string> ids = new();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT id FROM candidatos WHERE periodoGracia = @TODAY";
                            command.Parameters.AddWithValue("@TODAY", DateTime.Now.Date);
                            using (SqlDataReader reader = command.ExecuteReader())
                                while (reader.Read())
                                    ids.Add(reader.GetString(reader.GetOrdinal("id")));
                        }

                        foreach (string id in ids)
                        {
                            CandidateController.updateCandidateData(conn, transaction, new _Model.Candidate.CandidateStats() { id = id });
                        }

                        transaction.Commit();
                        result = new { error = false, n = ids.Count };
                    }
                    catch (Exception e)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5880, " + e.Message };
                    }
                }
            }

            return Ok(result);
        }


        [HttpPost]
        [Route(template: "set-infojobs-data/{apiKey}")]
        public async Task<IActionResult> SetInfoJobsData(string apiKey)
        {
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (apiKey != "secretApiKey")
                return Unauthorized();

            //return Problem();

            return Ok();
        }

        [HttpGet]
        [Route(template: "convert-control-horario-to-pdf/")]
        public IActionResult ConvertControlHorarioToPdf()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Sysadmin.ConvertControlHorarioToPdf", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    List<string> CandidatesIds = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT DISTINCT candidateId FROM ccontrol_horario";
                        using (SqlDataReader reader = command.ExecuteReader())
                            while (reader.Read())
                                CandidatesIds.Add(reader.GetString(reader.GetOrdinal("candidateId")));
                    }

                    foreach (string candidateid in CandidatesIds)
                    {
                        //acceder al sistema de ficheros candidate / idCandidate si tiene la carpeta control_horario
                        //y tiene dentro algun fichero hay que convertirlo a pdf
                        string path = ComposePath(new[] { "candidate", candidateid, "control_horario" });
                        //si existe el directorio control_horario y tiene ficheros convertir esos ficheros a pdf
                        if (Directory.Exists(path))
                        {
                            string[] files = Directory.GetFiles(path);
                            foreach (string file in files)
                            {
                                if (Path.GetExtension(file).ToLower() == ".pdf") continue;
                                string pdf = Path.ChangeExtension(file, ".pdf");

                                string[] innerFiles = Directory.GetFiles(path);
                                foreach (string innerFile in innerFiles)
                                {
                                    string extension = Path.GetExtension(innerFile).ToLower();
                                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                                    {
                                        ConvertImageToPdf(innerFile);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //devuelvo un error que no existe el directorio control_horario
                            return Ok(new { error = "No existe el directorio." });
                        }


                    }

                    result = new { error = false, n = CandidatesIds.Count };
                }
            }
            catch (Exception e)
            {
                result = new { error = "Error 5880, " + e.Message };
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static object listLogsFiltered(string securityToken, int? page, string type, bool sysadmin, bool onlyNew)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission(sysadmin ? "Sysadmin.ListLogs" : "Candidates.ListLogs", securityToken);
            if (!access.Acceso)
            {
                return new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }

            if (!sysadmin && !access.EsJefe)
            {
                type = "CANDIDATE_IBAN_CHANGE";
            }

            if (type != null) type = LogTypeFilter(type).ToString();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                List<object> logs = new List<object>();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                            "SELECT  id, [date], action, type, readed, [user] " +
                            "FROM [logs] " +
                            "WHERE (@TYPE IS NULL OR type = @TYPE) AND " +
                            "((@SYSADMIN = 1 AND LEFT(type, 10) <> 'CANDIDATE_') OR (@SYSADMIN = 0 AND LEFT(type, 10) = 'CANDIDATE_')) AND " +
                            "(@ONLY_NEW = 0 OR readed = 0) " +
                            "ORDER BY [date] DESC " +
                            "OFFSET @OFFSET ROWS FETCH NEXT 10 ROWS ONLY";

                    command.Parameters.AddWithValue("@TYPE", ((object)type) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@OFFSET", page == null ? 0 : (page * 10));
                    command.Parameters.AddWithValue("@SYSADMIN", sysadmin ? 1 : 0);
                    command.Parameters.AddWithValue("@ONLY_NEW", onlyNew ? 1 : 0);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            logs.Add(new
                            {
                                id = reader.GetInt32(reader.GetOrdinal("id")),
                                date = reader.GetDateTime(reader.GetOrdinal("date")),
                                action = reader.GetString(reader.GetOrdinal("action")),
                                type = reader.GetString(reader.GetOrdinal("type")),
                                isNew = reader.GetInt32(reader.GetOrdinal("readed")) == 0,
                                username = reader.IsDBNull(reader.GetOrdinal("user")) ? null : reader.GetString(reader.GetOrdinal("user"))
                            });
                        }
                    }
                }

                //Marcarlos como leidos
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                            "UPDATE [logs] SET readed = 1 " +
                            "WHERE id in (SELECT id FROM [logs] " +
                            "WHERE (@TYPE IS NULL OR type = @TYPE) AND " +
                            "((@SYSADMIN = 1 AND LEFT(type, 10) <> 'CANDIDATE_') OR (@SYSADMIN = 0 AND LEFT(type, 10) = 'CANDIDATE_')) AND " +
                            "(@ONLY_NEW = 0 OR readed = 0) " +
                            "ORDER BY [date] DESC " +
                            "OFFSET @OFFSET ROWS FETCH NEXT 10 ROWS ONLY)";

                    command.Parameters.AddWithValue("@TYPE", ((object)type) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@OFFSET", page == null ? 0 : (page * 10));
                    command.Parameters.AddWithValue("@SYSADMIN", sysadmin ? 1 : 0);
                    command.Parameters.AddWithValue("@ONLY_NEW", onlyNew ? 1 : 0);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            logs.Add(new
                            {
                                id = reader.GetInt32(reader.GetOrdinal("id")),
                                date = reader.GetDateTime(reader.GetOrdinal("date")),
                                action = reader.GetString(reader.GetOrdinal("action")),
                                type = reader.GetString(reader.GetOrdinal("type")),
                                isNew = reader.GetInt32(reader.GetOrdinal("readed")) == 0,
                                username = reader.IsDBNull(reader.GetOrdinal("user")) ? null : reader.GetString(reader.GetOrdinal("user"))
                            });
                        }
                    }
                }

                result = logs;
            }

            return result;
        }

        public static object countNewLogsFiltered(string securityToken, int? page, string type, bool sysadmin)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission(sysadmin ? "Sysadmin.CountNewLogs" : "Candidates.CountNewLogs", securityToken);
            if (!access.Acceso)
            {
                return new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }

            if (!sysadmin && !access.EsJefe)
            {
                type = "CANDIDATE_IBAN_CHANGE";
            }

            if (type != null) type = LogTypeFilter(type).ToString();

            using (SqlConnection conn = new(CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                        "SELECT  COUNT(*) " +
                        "FROM [logs] " +
                        "WHERE (@TYPE IS NULL OR type = @TYPE) AND " +
                        "((@SYSADMIN = 1 AND LEFT(type, 10) <> 'CANDIDATE_') OR (@SYSADMIN = 0 AND LEFT(type, 10) = 'CANDIDATE_')) AND " +
                        "readed = 0 ";

                command.Parameters.AddWithValue("@TYPE", ((object)type) ?? DBNull.Value);
                command.Parameters.AddWithValue("@SYSADMIN", sysadmin ? 1 : 0);

                result = command.ExecuteScalar();
            }

            return result;
        }

        private static long getDirectorySize(string folderPath)
        {
            DirectoryInfo di = new(folderPath);
            return di.EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }
        private void ConvertImageToPdf(string imagePath)
        {
            string outputFilePath = Path.ChangeExtension(imagePath, ".pdf");

            using (FileStream stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Document document = new Document(PageSize.A4);
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                using (FileStream imageStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var image = iTextSharp.text.Image.GetInstance(imageStream);
                    image.ScaleToFit(document.PageSize.Width - 25, document.PageSize.Height - 25);
                    image.Alignment = Element.ALIGN_CENTER;
                    document.Add(image);
                }

                document.Close();
                //borra el fichero original con system.io
                System.IO.File.Delete(imagePath);
            }
        }
        /// <summary>
        /// Convierte los archivos de base64 a su formato original.
        /// Además, los archivos png se convierten a jpeg.
        /// </summary>
        /// <param name="path"> Directorio donde se encuentran los archivos. </param>
        /// <param name="excludedFilesToPng"> Archivos que no se convertirán a jpeg. </param>

        private void revertFiles(string path, string[] excludedFilesToPng)
        {
            if (!Directory.Exists(path)) return;
            // Para todos los archivos que haya en el directorio que sean base64...
            // (los archivos base64 no tienen extensión)
            foreach (string file in Directory.GetFiles(path).Where(f => !f.Contains('.')).ToArray())
            {
                string file64 = ReadFile(System.Array.Empty<string>(), file);
                string[] extensions = getExtFromBase64(file64);
                if (extensions == null || extensions.Length == 0)
                {
                    System.IO.File.Delete(file);
                    continue;
                }
                System.IO.File.WriteAllBytes(file + '.' + extensions[0], Convert.FromBase64String(file64.Split(",")[1]));
                System.IO.File.Delete(file);
            }
            foreach (string file in Directory.GetFiles(path))
            {
                // filemane + extension
                string[] name_pack = file.Split(InstallationConstants.FOLDER_SPLITER).Last().Split(".");
                if (name_pack.Length > 1)
                {
                    if (name_pack[1] == "png" && !excludedFilesToPng.Contains(name_pack[0]))
                    {
                        System.Drawing.Image? img = null;                        
                        try
                        {
                            img = System.Drawing.Image.FromFile(file);
                            img.Save(name_pack[0] + ".jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                        catch (Exception) { }
                        finally { img?.Dispose(); }
                        System.IO.File.Delete(file);
                    }
                }
            }
            foreach (string dir in Directory.GetDirectories(path))
            {
                // Si el directorio está vacío, lo borramos
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir);
                    continue;
                }
                revertFiles(dir, excludedFilesToPng);
            }
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
