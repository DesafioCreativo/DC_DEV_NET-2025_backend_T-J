using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.AnvizTools;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;

namespace ThinkAndJobSolution.Controllers.MainHome.Sysadmin
{
    [Route("api/v1/user")]
    [ApiController]
    [Authorize]
    public class UserCredentialsController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        #region > GESTION DE USUARIOS
        [HttpPost]
        //[Route(template: "/")]
        public async Task<IActionResult> CreateUser()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.CreateUser", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using System.IO.StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("username", out JsonElement usernameJson) &&
                json.TryGetProperty("docID", out JsonElement docIdJson) &&
                json.TryGetProperty("docType", out JsonElement docTypeJson) &&
                json.TryGetProperty("name", out JsonElement nameJson) &&
                json.TryGetProperty("surname", out JsonElement surnameJson) &&
                json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("pwd", out JsonElement passJson) &&
                json.TryGetProperty("photo", out JsonElement photoJson) &&
                json.TryGetProperty("department", out JsonElement departmentJson) &&
                json.TryGetProperty("birth", out JsonElement birthJson) &&
                json.TryGetProperty("external", out JsonElement externalJson) &&
                json.TryGetProperty("phone", out JsonElement phoneJson) &&
                json.TryGetProperty("hideSocieties", out JsonElement hideSocietiesJson) &&
                json.TryGetProperty("hasToShift", out JsonElement hasToShiftJson) &&
                json.TryGetProperty("cardId", out JsonElement cardIdJson))
            {
                string username = usernameJson.GetString()?.Trim();
                string docId = docIdJson.GetString()?.Trim();
                string docType = docTypeJson.GetString()?.Trim();
                string name = nameJson.GetString()?.Trim();
                string surname = surnameJson.GetString()?.Trim();
                string email = emailJson.GetString()?.Trim();
                string phone = phoneJson.GetString()?.Trim();
                string predefPass = passJson.GetString();
                string photo = photoJson.GetString();
                string department = departmentJson.GetString();
                DateTime birth = HelperMethods.GetJsonDate(birthJson) ?? DateTime.Now;
                bool external = externalJson.GetBoolean();
                bool hideSocieties = hideSocietiesJson.GetBoolean();
                bool hasToShift = hasToShiftJson.GetBoolean();
                string cardId = cardIdJson.GetString()?.Trim();

                if (username.StartsWith("admin."))
                {
                    return Ok(new
                    { error = "Error 3100, Un nombre de usuario del sistema no puede empezar por admin." });
                }

                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();
                bool failed = false;

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT count(*) FROM users WHERE username = @USERNAME ";

                    command.Parameters.AddWithValue("@USERNAME", username);

                    if ((int)(command.ExecuteScalar()) > 0)
                    {
                        failed = true;
                        result = new
                        {
                            error = $"Error 4251, el nombre de usuario {username} ya está en uso."
                        };
                    }
                }

                string dniUsedBy = HelperMethods.CheckDNINIECIFunique(docId, null, conn);
                if (!failed && dniUsedBy != null)
                {
                    failed = true;
                    result = new
                    {
                        error = $"Error 4252, el dni {docId} ya está en uso por {dniUsedBy}."
                    };
                }

                string emailUsedBy = HelperMethods.CheckEMAILunique(email, null, conn);
                if (!failed && emailUsedBy != null)
                {
                    failed = true;
                    result = new
                    {
                        error = $"Error 4252, el email {email} ya está en uso por {emailUsedBy}."
                    };
                }

                string phoneUsedBy = HelperMethods.CheckPHONEunique(phone, null, conn);
                if (!failed && phoneUsedBy != null)
                {
                    failed = true;
                    result = new
                    {
                        error = $"Error 4252, el teléfono {phone} ya está en uso por {phoneUsedBy}."
                    };
                }

                if (!failed)
                {

                    try
                    {
                        photo = HelperMethods.LimitSquareImage(photo, true);
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5550, no se ha podido procesar la imagen"
                        };
                    }

                    if (!failed)
                    {
                        string id = HelperMethods.ComputeStringHash(
                            username + docId + docType + email +
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
                        );

                        string pwd;
                        if (predefPass == null)
                        {
                            pwd = HelperMethods.ComputeStringHash(
                                id +
                                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            )[..8].ToUpper();
                        }
                        else
                        {
                            pwd = predefPass;
                        }

                        string pwdEncipt = HelperMethods.EncryptString(pwd);

                        string token = HelperMethods.ComputeStringHash(
                            pwd +
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        );

                        using SqlTransaction transaction = conn.BeginTransaction();

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;

                            command.CommandText =
                                "INSERT INTO users (id, username, pwd, DocId, DocType, name, surname, email, securityToken, department, birth, isExternal, phone, hideSocieties, hasToShift, cardId) " +
                                "VALUES (@ID, @USERNAME, @PWD, @DOC_ID, @DOC_TYPE, @NAME, @SURNAME, @EMAIL, @TOKEN, @DEPARTMENT, @BIRTH, @EXTERNAL, @PHONE, @HIDE_SOCIETIES, @HAS_TO_SHIFT, @CARD_ID)";

                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@USERNAME", username);
                            command.Parameters.AddWithValue("@PWD", pwdEncipt);
                            command.Parameters.AddWithValue("@DOC_ID", docId);
                            command.Parameters.AddWithValue("@DOC_TYPE", docType);
                            command.Parameters.AddWithValue("@NAME", name);
                            command.Parameters.AddWithValue("@SURNAME", surname);
                            command.Parameters.AddWithValue("@EMAIL", email);
                            command.Parameters.AddWithValue("@TOKEN", token);
                            command.Parameters.AddWithValue("@DEPARTMENT", department);
                            command.Parameters.AddWithValue("@BIRTH", birth);
                            command.Parameters.AddWithValue("@EXTERNAL", external ? 1 : 0);
                            command.Parameters.AddWithValue("@PHONE", phone);
                            command.Parameters.AddWithValue("@HIDE_SOCIETIES", hideSocieties ? 1 : 0);
                            command.Parameters.AddWithValue("@HAS_TO_SHIFT", hasToShift ? 1 : 0);
                            command.Parameters.AddWithValue("@CARD_ID", cardId != null && cardId.Length > 0 ? int.Parse(cardId) : DBNull.Value);

                            try
                            {
                                command.ExecuteNonQuery();
                                HelperMethods.SaveFile(new[] { "users", id, "photo" }, photo);
                                string idd = HelperMethods.Dni2idd(docId);
                                // Delete the user (if exists) from all internal devices
                                List<string> deviceIds = AnvizTools.GetAnvizDeviceIds(AnvizTools.DeviceType.Internal);
                                foreach (string deviceId in deviceIds)
                                {
                                    AnvizTools.InsertRemoveTask(deviceId, idd);
                                    AnvizTools.InsertRegisterTask(deviceId, new()
                                    {
                                        rjId = id,
                                        dni = docId,
                                        idd = idd,
                                        name = docId,
                                        cardid = cardId ?? null,
                                        pass = cardId?.ToString()[4..],
                                        identity_type = AnvizTools.GetAnvizIdentityType(deviceId)
                                    });
                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5520, no se ha podido insertar el usuario"
                                };
                            }
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            transaction.Commit();
                            result = new
                            {
                                error = false,
                                pwd
                            };
                            HelperMethods.LogToDB(HelperMethods.LogType.USER_CREATED, "Se ha creado el usuario " + username, HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                        }
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get/{userId}/")]
        public IActionResult GetUser(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.GetUser", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT * FROM users WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", userId);

                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    string id = reader.GetString(reader.GetOrdinal("id"));
                    result = new
                    {
                        error = false,
                        id,
                        username = reader.GetString(reader.GetOrdinal("username")),
                        DocID = reader.GetString(reader.GetOrdinal("DocID")),
                        DocType = reader.GetString(reader.GetOrdinal("DocType")),
                        name = reader.GetString(reader.GetOrdinal("name")),
                        surname = reader.GetString(reader.GetOrdinal("surname")),
                        email = reader.GetString(reader.GetOrdinal("email")),
                        securityToken = reader.GetString(reader.GetOrdinal("securityToken")),
                        department = reader.GetString(reader.GetOrdinal("department")),
                        birth = reader.GetDateTime(reader.GetOrdinal("birth")),
                        external = reader.GetInt32(reader.GetOrdinal("isExternal")) == 1,
                        disabled = reader.GetInt32(reader.GetOrdinal("disabled")) == 1,
                        hideSocieties = reader.GetInt32(reader.GetOrdinal("hideSocieties")) == 1,
                        hasToShift = reader.GetInt32(reader.GetOrdinal("hasToShift")) == 1,
                        requiresKeepAlive = reader.GetInt32(reader.GetOrdinal("requiresKeepAlive")) == 1,
                        photo = HelperMethods.ReadFile(new[] { "users", id, "photo" }),
                        phone = reader.GetString(reader.GetOrdinal("phone")),
                        hasFace = reader.GetInt32(reader.GetOrdinal("hasFace")) == 1,
                        cardId = reader.IsDBNull(reader.GetOrdinal("cardId")) ? "" : reader.GetInt32(reader.GetOrdinal("cardId")).ToString(),
                    };
                }
                else
                {
                    result = new
                    {
                        error = "Error 2930, no existe el usuario.",
                        authorized = false
                    };
                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get/{userId}/pwd")]
        public IActionResult GetUserPwd(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.GetUserPwd", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT pwd FROM users WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", userId);

                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    string id = reader.GetString(reader.GetOrdinal("pwd"));
                    result = new
                    {
                        error = false,
                        pwd = HelperMethods.DecryptString(reader.GetString(reader.GetOrdinal("pwd")))
                    };
                }
                else
                {
                    result = new
                    {
                        error = "Error 2930, no existe el usuario.",
                        authorized = false
                    };
                }

            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-photo/")]
        public IActionResult GetUserPhoto()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT id FROM users WHERE securityToken = @TOKEN";
                command.Parameters.AddWithValue("@TOKEN", securityToken);

                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    string id = reader.GetString(reader.GetOrdinal("id"));
                    result = new
                    {
                        error = false,
                        photo = HelperMethods.ReadFile(new[] { "users", id, "photo" })
                    };
                }
                else
                {
                    result = new
                    {
                        error = "Error 2930, no existe el usuario.",
                        authorized = false
                    };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-photo-by-id/{userId}/")]
        public IActionResult GetUserPhotoById(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            _ = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HelperMethods.HasPermission("UserCredentials.GetUserPhotoById", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            object result = new
            {
                error = false,
                photo = HelperMethods.ReadFile(new[] { "users", userId, "photo" })
            };

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{userId}/")]
        public IActionResult DeleteUser(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.DeleteUser", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            if (HelperMethods.CheckUserIsSuper(null, null, null, userId))
            {
                return Ok(new
                {
                    error = "Error 1002, Los super usuarios no pueden ser eliminados."
                });
            }

            using (SqlConnection conn = new(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                string username = null;

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT username FROM users WHERE id = @ID";

                    command.Parameters.AddWithValue("@ID", userId);

                    username = (string)(command.ExecuteScalar());
                    if (username == null)
                    {
                        result = new
                        {
                            error = "Error 2252, no existe el usuario."
                        };
                    }
                }

                if (username != null)
                {
                    using SqlTransaction transaction = conn.BeginTransaction();
                    bool failed = false;

                    // Eliminar los permisos de componentes
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM permisos_componente WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar los permisos de componente"
                            };
                        }
                    }

                    // Eliminar los permisos de modulos
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM permisos_modulo WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar los permisos de modulo"
                            };
                        }
                    }

                    // Eliminar la recuperacion de contraseña
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM recovery_codes WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar los codigos de recuperacion de contraseña"
                            };
                        }
                    }

                    // Eliminar sus asignaciones de avisos
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM user_avisos WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar las asignaciones de avisos"
                            };
                        }
                    }

                    // Eliminar sus asignaciones de empresas
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM asociacion_usuario_empresa WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar las asignaciones de empresas"
                            };
                        }
                    }

                    // Eliminar sus guardias
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM guardias_rrhh WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar las guardias"
                            };
                        }
                    }

                    // Eliminar su fichaje interno
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM internal_fichaje WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar el fichaje interno"
                            };
                        }
                    }

                    // Eliminar su registro de macroacciones en teletrabajo
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM telework_register WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar el registro de macroacciones"
                            };
                        }
                    }

                    // Eliminar sus participacion en eventos multiples del calendarios
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM calendar_userevent_users WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar su participación en los eventos"
                            };
                        }
                    }

                    // Eliminar su confirmacion de lectura de eventos de usuario
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM calendar_userevent_seen WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar la confirmación de lectura en eventos de usuario"
                            };
                        }
                    }

                    // Eliminar los eventos que se hayan quedado sin participantes, por su salida
                    if (!failed)
                    {
                        try
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM calendar_userevent_dates WHERE NOT EXISTS(SELECT * FROM calendar_userevent_users WHERE calendar_userevent_users.eventId = calendar_userevent_dates.eventId)";
                                command.ExecuteNonQuery();
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM calendar_userevents WHERE NOT EXISTS(SELECT * FROM calendar_userevent_users WHERE eventId = id)";
                                command.ExecuteNonQuery();
                            }
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido limpiar los eventos sin participantes"
                            };
                        }
                    }

                    // Eliminar su confirmacion de lectura de eventos de guardias
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM calendar_guardia_seen WHERE userId = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5522, no se ha podido eliminar la confirmación de lectura en eventos de guardia"
                            };
                        }
                    }

                    //Eliminar al usuario
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.Connection = conn;
                        command.Transaction = transaction;

                        command.CommandText =
                            "DELETE FROM users WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        try
                        {
                            command.ExecuteNonQuery();
                            HelperMethods.DeleteDir(new[] { "users", userId });
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5524, no se ha podido eliminar al usuario"
                            };
                        }
                    }

                    if (failed)
                    {
                        transaction.Rollback();
                    }
                    else
                    {
                        transaction.Commit();
                        result = new
                        {
                            error = false
                        };
                        HelperMethods.LogToDB(HelperMethods.LogType.USER_DELETED, "Se ha eliminado al usuario " + username, HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        //[Route(template: "/")]
        public IActionResult List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> users = new();

            try
            {
                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT * FROM users ORDER BY username ASC";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            username = reader.GetString(reader.GetOrdinal("username")),
                            docID = reader.GetString(reader.GetOrdinal("DocID")),
                            docType = reader.GetString(reader.GetOrdinal("DocType")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            surname = reader.GetString(reader.GetOrdinal("surname")),
                            email = reader.GetString(reader.GetOrdinal("email")),
                            department = reader.GetString(reader.GetOrdinal("department")),
                            birth = reader.GetDateTime(reader.GetOrdinal("birth")),
                            external = reader.GetInt32(reader.GetOrdinal("isExternal")) == 1
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
                result = new
                {
                    error = "Error 5530, no se han podido obtener los usuarios."
                };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "new-pwd/{userId}/")]
        public IActionResult RegeneratePassword(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.RegeneratePassword", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            if (!HelperMethods.CheckUserIsSuper(null, null, securityToken) && HelperMethods.CheckUserIsSuper(null, null, null, userId))
            {
                return Ok(new
                {
                    error = "Error 1002, Los super usuarios no pueden ser modificados."
                });
            }

            string pwd = HelperMethods.ComputeStringHash(
                userId + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())[..8].ToUpper();

            string pwdHash = HelperMethods.ComputeStringHash(pwd);

            using (SqlConnection conn = new(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "UPDATE users SET pwd = @HASH WHERE id = @ID";

                command.Parameters.AddWithValue("@HASH", pwdHash);
                command.Parameters.AddWithValue("@ID", userId);

                try
                {
                    int rows = command.ExecuteNonQuery();
                    if (rows > 0)
                    {
                        result = new
                        {
                            error = false,
                            pwd
                        };
                    }
                    else
                    {
                        result = new
                        {
                            error = "Error 5525, no se ha podido cambiar la contraseña"
                        };
                    }
                }
                catch (Exception)
                {
                    result = new
                    {
                        error = "Error 5525, excepción al cambiar la contraseña"
                    };
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "{userId}/")]
        public async Task<IActionResult> UpdateUser(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.UpdateUser", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            if (!HelperMethods.CheckUserIsSuper(null, null, securityToken) && HelperMethods.CheckUserIsSuper(null, null, null, userId))
            {
                return Ok(new
                {
                    error = "Error 1002, Los super usuarios no pueden ser modificados."
                });
            }

            using System.IO.StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("username", out JsonElement usernameJson) &&
                json.TryGetProperty("docID", out JsonElement docIdJson) &&
                json.TryGetProperty("docType", out JsonElement docTypeJson) &&
                json.TryGetProperty("name", out JsonElement nameJson) &&
                json.TryGetProperty("surname", out JsonElement surnameJson) &&
                json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("pwd", out JsonElement pwdJson) &&
                json.TryGetProperty("department", out JsonElement departmentJson) &&
                json.TryGetProperty("birth", out JsonElement birthJson) &&
                json.TryGetProperty("photo", out JsonElement photoJson) &&
                json.TryGetProperty("external", out JsonElement externalJson) &&
                json.TryGetProperty("disabled", out JsonElement disabledJson) &&
                json.TryGetProperty("phone", out JsonElement phoneJson) &&
                json.TryGetProperty("hideSocieties", out JsonElement hideSocietiesJson) &&
                json.TryGetProperty("hasToShift", out JsonElement hasToShiftJson) &&
                json.TryGetProperty("requiresKeepAlive", out JsonElement requiresKeepAliveJson) &&
                json.TryGetProperty("cardId", out JsonElement cardIdJson))
            {

                string username = usernameJson.GetString().Trim();
                string docId = docIdJson.GetString().Trim();
                string docType = docTypeJson.GetString().Trim();
                string name = nameJson.GetString().Trim();
                string surname = surnameJson.GetString().Trim();
                string email = emailJson.GetString().Trim();
                string pwd = pwdJson.GetString();
                string department = departmentJson.GetString().Trim();
                DateTime birth = HelperMethods.GetJsonDate(birthJson) ?? DateTime.Now;
                string photo = photoJson.GetString();
                bool external = externalJson.GetBoolean();
                bool disabled = disabledJson.GetBoolean();
                bool hideSocieties = hideSocietiesJson.GetBoolean();
                bool hasToShift = hasToShiftJson.GetBoolean();
                bool requiresKeepAlive = requiresKeepAliveJson.GetBoolean();
                string phone = phoneJson.GetString();
                string cardId = cardIdJson.GetString()?.Trim();

                if (username.StartsWith("admin."))
                {
                    return Ok(new
                    { error = "Error 3100, Un nombre de usuario del sistema no puede empezar por admin." });
                }

                if (pwd != null) pwd = HelperMethods.EncryptString(pwd);


                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();
                bool failed = false, newUsername = false, newDocId = false, wasDisabled = false;

                //Obtener username y dni para ver si cambian
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT username, DocID, disabled FROM users WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", userId);
                    using SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        newUsername = !reader.GetString(reader.GetOrdinal("username")).Equals(username);
                        newDocId = !reader.GetString(reader.GetOrdinal("DocID")).Equals(docId);
                        wasDisabled = reader.GetInt32(reader.GetOrdinal("disabled")) == 1;
                    }
                    else
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 4252, usuario no encontrado."
                        };
                    }
                }


                if (newUsername)
                {
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText = "SELECT count(*) FROM users WHERE username = @USERNAME ";

                    command.Parameters.AddWithValue("@USERNAME", username);

                    if ((int)(command.ExecuteScalar()) > 0)
                    {
                        failed = true;
                        result = new
                        {
                            error = $"Error 4251, el nombre de usuario {username} ya está en uso."
                        };
                    }
                }

                string dniUsedBy = HelperMethods.CheckDNINIECIFunique(docId, userId, conn);
                if (!failed && newDocId && dniUsedBy != null)
                {
                    failed = true;
                    result = new
                    {
                        error = $"Error 4252, el dni {docId} ya está en uso por {dniUsedBy}."
                    };
                }

                string emailUsedBy = HelperMethods.CheckEMAILunique(email, userId, conn);
                if (!failed && emailUsedBy != null)
                {
                    failed = true;
                    result = new
                    {
                        error = $"Error 4252, el email {email} ya está en uso por {emailUsedBy}."
                    };
                }

                string phoneUsedBy = HelperMethods.CheckPHONEunique(phone, userId, conn);
                if (!failed && phoneUsedBy != null)
                {
                    failed = true;
                    result = new
                    {
                        error = $"Error 4252, el teléfono {phone} ya está en uso por {phoneUsedBy}."
                    };
                }

                if (!failed)
                {
                    try
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText =
                            "UPDATE users SET username = @USERNAME, DocId = @DOC_ID, " +
                            "DocType = @DOC_TYPE, name = @NAME, surname = @SURNAME, " +
                            "email = @EMAIL, department = @DEPARTMENT, birth = @BIRTH, " +
                            "isExternal = @EXTERNAL, [disabled] = @DISABLED, phone = @PHONE, " +
                            "hideSocieties = @HIDE_SOCIETIES, hasToShift = @HAS_TO_SHIFT, " +
                            "requiresKeepAlive = @REQUIRES_KEEPALIVE, cardId = @CARD_ID " +
                            "WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", userId);
                        command.Parameters.AddWithValue("@USERNAME", username);
                        command.Parameters.AddWithValue("@DOC_ID", docId);
                        command.Parameters.AddWithValue("@DOC_TYPE", docType);
                        command.Parameters.AddWithValue("@NAME", name);
                        command.Parameters.AddWithValue("@SURNAME", surname);
                        command.Parameters.AddWithValue("@EMAIL", email);
                        command.Parameters.AddWithValue("@DEPARTMENT", department);
                        command.Parameters.AddWithValue("@BIRTH", birth);
                        command.Parameters.AddWithValue("@EXTERNAL", external ? 1 : 0);
                        command.Parameters.AddWithValue("@DISABLED", disabled ? 1 : 0);
                        command.Parameters.AddWithValue("@HIDE_SOCIETIES", hideSocieties ? 1 : 0);
                        command.Parameters.AddWithValue("@HAS_TO_SHIFT", hasToShift ? 1 : 0);
                        command.Parameters.AddWithValue("@REQUIRES_KEEPALIVE", requiresKeepAlive ? 1 : 0);
                        command.Parameters.AddWithValue("@PHONE", phone);
                        command.Parameters.AddWithValue("@CARD_ID", cardId != null && cardId.Length > 0 ? int.Parse(cardId) : DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5526, excepción al cambiar los datos del usuario"
                        };
                    }
                }

                if (!failed && pwd != null)
                {
                    try
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText =
                            "UPDATE users SET pwd = @PWD WHERE id = @ID";

                        command.Parameters.AddWithValue("@PWD", pwd);
                        command.Parameters.AddWithValue("@ID", userId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5525, excepción al cambiar la contraseña"
                        };
                    }
                }

                if (!failed && photo != null)
                {
                    try
                    {
                        photo = HelperMethods.LimitSquareImage(photo);
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5550, error al procesar imagen"
                        };
                    }

                    if (!failed)
                    {
                        try
                        {
                            HelperMethods.SaveFile(new[] { "users", userId, "photo" }, photo);
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5525, excepción al cambiar la foto"
                            };
                        }
                    }
                }

                if (!failed)
                {
                    result = new
                    {
                        error = false
                    };
                    if (wasDisabled && !disabled)
                        HelperMethods.LogToDB(HelperMethods.LogType.USER_ENABLED, "Se ha desbloqueado al usuario " + username, HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                    else if (!wasDisabled && disabled)
                        HelperMethods.LogToDB(HelperMethods.LogType.USER_DISABLED, "Se ha bloqueado al usuario " + username, HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                    else
                        HelperMethods.LogToDB(HelperMethods.LogType.USER_MODIFIED, "Se ha actualizado el usuario " + username, HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-comercial/")]
        public IActionResult ListForComercial()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.ListForComercial", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> users = new();

            try
            {
                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT DocID, name, surname, username FROM users WHERE department = 'COMERCIAL' OR department = 'CEO'";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new
                        {
                            docID = reader.GetString(reader.GetOrdinal("DocID")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            surname = reader.GetString(reader.GetOrdinal("surname")),
                            username = reader.GetString(reader.GetOrdinal("username"))
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
                result = new
                {
                    error = "Error 5530, no se han podido obtener los usuarios."
                };
            }

            return Ok(result);
        }

        #endregion
        
        #region > GESTIÓN DE CUENTA

        [HttpGet]
        [Route(template: "get-my-user/")]
        public IActionResult GetMyUser()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText = "SELECT * FROM users WHERE securityToken = @TOKEN";
                command.Parameters.AddWithValue("@TOKEN", securityToken);

                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    string id = reader.GetString(reader.GetOrdinal("id"));
                    result = new
                    {
                        error = false,
                        user = new SystemUser
                        {
                            id = id,
                            username = reader.GetString(reader.GetOrdinal("username")),
                            DocID = reader.GetString(reader.GetOrdinal("DocID")),
                            DocType = reader.GetString(reader.GetOrdinal("DocType")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            surname = reader.GetString(reader.GetOrdinal("surname")),
                            email = reader.GetString(reader.GetOrdinal("email")),
                            department = reader.GetString(reader.GetOrdinal("department")),
                            birth = reader.GetDateTime(reader.GetOrdinal("birth")),
                            photo = HelperMethods.ReadFile(new[] { "users", id, "photo" }),
                            phone = reader.GetString(reader.GetOrdinal("phone")),
                            hasFace = reader.GetInt32(reader.GetOrdinal("hasFace")) == 1
                        }
                    };
                }
                else
                {
                    result = new
                    {
                        error = "Error 2930, no existe el usuario.",
                        authorized = false
                    };
                }

            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "update-my-user/")]
        public async Task<IActionResult> UpdateMyUser()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using System.IO.StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("email", out JsonElement emailJson) && json.TryGetProperty("phone", out JsonElement phoneJson) &&
                json.TryGetProperty("photo", out JsonElement photoJson) && json.TryGetProperty("oldPwd", out JsonElement oldPwdJson) &&
                json.TryGetProperty("newPwd", out JsonElement newPwdJson))
            {

                string email = emailJson.GetString();
                string phone = phoneJson.GetString();
                string photo = photoJson.GetString();
                string oldPwd = oldPwdJson.GetString() ?? "";
                string newPwd = newPwdJson.GetString();

                try
                {
                    using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                    conn.Open();

                    //Comprobar que el usuario existe y que tiene esa oldPwd
                    string userId = null, username;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id, pwd, username FROM users WHERE securityToken = @TOKEN";
                        command.Parameters.AddWithValue("@TOKEN", securityToken);
                        using SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            userId = reader.GetString(reader.GetOrdinal("id"));
                            username = reader.GetString(reader.GetOrdinal("username"));
                            string currentPwd = reader.GetString(reader.GetOrdinal("pwd"));
                            if (!HelperMethods.DecryptString(currentPwd).Equals(oldPwd))
                                return Ok(new { error = "Error 4253, contraseña actual incorrecta." });
                        }
                        else
                        {
                            return Ok(new { error = "Error 4252, usuario no encontrado." });
                        }
                    }

                    if (email != null)
                    {
                        string alreadyEmailType = HelperMethods.CheckEMAILunique(email, userId, conn);
                        if (alreadyEmailType != null)
                            return Ok(new { error = $"Error 4254, el email ya está en uso por {alreadyEmailType}." });
                    }

                    if (phone != null)
                    {
                        string alreadyPhoneType = HelperMethods.CheckPHONEunique(phone, userId, conn);
                        if (alreadyPhoneType != null)
                            return Ok(new { error = $"Error 4254, el teléfono ya está en uso por {alreadyPhoneType}." });
                    }

                    //Actualizar email
                    if (email != null)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "UPDATE users SET email = @EMAIL WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        command.Parameters.AddWithValue("@EMAIL", email);
                        command.ExecuteNonQuery();
                    }

                    //Actualizar telefono
                    if (phone != null)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "UPDATE users SET phone = @PHONE WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        command.Parameters.AddWithValue("@PHONE", phone);
                        command.ExecuteNonQuery();
                    }

                    //Actualizar pwd
                    if (newPwd != null)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "UPDATE users SET pwd = @PWD WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        command.Parameters.AddWithValue("@PWD", HelperMethods.EncryptString(newPwd));
                        command.ExecuteNonQuery();
                    }

                    //Actualizar foto
                    if (photo != null)
                    {
                        photo = HelperMethods.LimitSquareImage(photo);
                        HelperMethods.SaveFile(new[] { "users", userId, "photo" }, photo);
                    }

                    result = new
                    {
                        error = false
                    };
                    HelperMethods.LogToDB(HelperMethods.LogType.USER_MODIFIED, "Usuario actualizado por sí mismo " + username, HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                }
                catch (Exception)
                {
                    result = new { error = "Error 5255, no se ha podido actualizar el usuario" };
                }
            }

            return Ok(result);
        }

        #endregion

        #region > ASOCIACION CON EMPRESAS

        [HttpGet]
        [Route(template: "list-for-rrhh/")]
        public IActionResult ListForRRHH()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.ListForRRHH", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> users = new();

            try
            {
                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT U.id, U.DocID, U.name, U.surname, U.surname, " +
                    "jefe = CASE WHEN EXISTS(SELECT * FROM permisos_modulo PM WHERE PM.userId = U.id AND PM.modulo = 'RRHH' AND PM.jefe = 1) THEN 1 ELSE 0 END " +
                    "FROM users U WHERE EXISTS(SELECT * FROM permisos_modulo PM WHERE PM.userId = U.id AND PM.modulo = 'RRHH')";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string username = reader.GetString(reader.GetOrdinal("surname"));
                        users.Add(new
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            docID = reader.GetString(reader.GetOrdinal("DocID")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            surname = reader.GetString(reader.GetOrdinal("surname")),
                            username,
                            jefe = reader.GetInt32(reader.GetOrdinal("jefe")) == 1 || HelperMethods.CheckUserIsSuper(username)
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
                result = new
                {
                    error = "Error 5530, no se han podido obtener los usuarios."
                };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "{userId}/assignations/")]
        public IActionResult GetRRHHUserAssignations(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.GetRRHHUserAssignations", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> companies = new();

            try
            {
                //Comprobar si es jefe
                bool jefe;

                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();

                jefe = HelperMethods.CheckUserIsSuper(conn, null, null, userId);

                if (!jefe)
                {
                    using SqlCommand command = conn.CreateCommand();
                    command.CommandText =
                        "SELECT jefe = CASE WHEN EXISTS(SELECT * FROM permisos_modulo PM WHERE PM.userId = @ID AND PM.modulo = 'RRHH' AND PM.jefe = 1) THEN 1 ELSE 0 END";
                    command.Parameters.AddWithValue("@ID", userId);
                    jefe = (int)command.ExecuteScalar() == 1;
                }

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        jefe ?
                        "SELECT E.id, E.nombre FROM empresas E ORDER BY E.nombre"
                        :
                        "SELECT E.id, E.nombre FROM asociacion_usuario_empresa AUE INNER JOIN empresas E ON(AUE.companyId = E.id) WHERE AUE.userId = @ID ORDER BY E.nombre";
                    command.Parameters.AddWithValue("@ID", userId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            companies.Add(new
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("nombre"))
                            });
                        }
                    }

                    result = new
                    {
                        error = false,
                        companies
                    };
                }
            }
            catch (Exception)
            {
                result = new
                {
                    error = "Error 5530, no se han podido obtener las asignaciones."
                };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "assignations-by-company/{companyId}/")]
        public IActionResult GetRRHHUserAssignationsByCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.GetRRHHUserAssignationsByCompany", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> users = new();

            try
            {
                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT U.id, U.DocID, U.name, U.surname, UN.jefe FROM (" +
                    "SELECT PM.userId, jefe = 1 FROM permisos_modulo PM WHERE PM.modulo = 'RRHH' AND PM.jefe = 1 AND NOT EXISTS(SELECT * FROM asociacion_usuario_empresa AUE WHERE AUE.companyId = @ID AND AUE.userId = PM.userId) " +
                    "UNION " +
                    "SELECT AUE.userId, jefe = 0 FROM asociacion_usuario_empresa AUE WHERE AUE.companyId = @ID " +
                    ") UN INNER JOIN Users U ON(UN.userId = U.id)";
                command.Parameters.AddWithValue("@ID", companyId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            docID = reader.GetString(reader.GetOrdinal("DocID")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            surname = reader.GetString(reader.GetOrdinal("surname")),
                            jefe = reader.GetInt32(reader.GetOrdinal("jefe")) == 1
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
                result = new
                {
                    error = "Error 5530, no se han podido obtener las asignaciones."
                };
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "{userId}/assignations/")]
        public async Task<IActionResult> SetRRHHUserAssignations(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.SetRRHHUserAssignations", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using System.IO.StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("companies", out JsonElement companiesJson))
            {
                List<string> companies = HelperMethods.GetJsonStringList(companiesJson);

                try
                {
                    using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                    conn.Open();

                    // Borrar las asignaciones previas del usuario
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM asociacion_usuario_empresa WHERE userId = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        command.ExecuteNonQuery();
                    }

                    foreach (string companyId in companies)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "INSERT INTO asociacion_usuario_empresa (userId, companyId) VALUES (@USER, @COMPANY)";
                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@COMPANY", companyId);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new
                    {
                        error = "Error 5530, no se han podido establecer las asignaciones."
                    };
                }
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "assignations-by-company/{companyId}/")]
        public async Task<IActionResult> SetRRHHUserAssignationsByCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.SetRRHHUserAssignationsByCompany", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using System.IO.StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("users", out JsonElement usersJson))
            {
                List<string> users = HelperMethods.GetJsonStringList(usersJson);

                try
                {
                    using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                    conn.Open();

                    //Borrar las asignaciones previas del usuario
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM asociacion_usuario_empresa WHERE companyId = @ID";
                        command.Parameters.AddWithValue("@ID", companyId);
                        command.ExecuteNonQuery();
                    }

                    foreach (string userId in users)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        command.CommandText = "INSERT INTO asociacion_usuario_empresa (userId, companyId) VALUES (@USER, @COMPANY)";
                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@COMPANY", companyId);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new
                    {
                        error = "Error 5530, no se han podido establecer las asignaciones."
                    };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-companies-for-rrhh-assignation/")]
        public IActionResult ListCompaniesForRRHHAssignation()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.ListCompaniesForRRHHAssignation", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> companies = new();

            try
            {
                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT E.id, E.cif, E.nombre, " +
                    "empty = CASE WHEN EXISTS(SELECT * FROM asociacion_usuario_empresa AUE WHERE AUE.companyId = E.id) THEN 0 ELSE 1 END " +
                    "FROM empresas E " +
                    "ORDER BY empty DESC, CAST(nombre as varchar(250)) ASC";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        companies.Add(new
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            cif = reader.GetString(reader.GetOrdinal("cif")),
                            name = reader.GetString(reader.GetOrdinal("nombre")),
                            empty = reader.GetInt32(reader.GetOrdinal("empty")) == 1
                        });
                    }
                }

                result = new
                {
                    error = false,
                    companies
                };
            }
            catch (Exception)
            {
                result = new
                {
                    error = "Error 5530, no se han podido obtener las empresas."
                };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-rrhh-with-images/")]
        public IActionResult ListForRRHHWithImages()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("UserCredentials.ListForRRHHWithImages", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            List<object> users = new();

            try
            {
                using SqlConnection conn = new(HelperMethods.CONNECTION_STRING);
                conn.Open();

                using SqlCommand command = conn.CreateCommand();
                command.CommandText =
                    "SELECT U.id, U.DocID, U.name, U.surname " +
                    "FROM users U WHERE EXISTS(SELECT * FROM permisos_modulo PM WHERE PM.userId = U.id AND PM.modulo = 'RRHH')";

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string id = reader.GetString(reader.GetOrdinal("id"));
                        users.Add(new
                        {
                            id,
                            docID = reader.GetString(reader.GetOrdinal("DocID")),
                            name = reader.GetString(reader.GetOrdinal("name")) + " " + reader.GetString(reader.GetOrdinal("surname")),
                            photo = HelperMethods.ReadFile(new[] { "users", id, "photo" })
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
                result = new
                {
                    error = "Error 5530, no se han podido obtener los usuarios."
                };
            }

            return Ok(result);
        }

        #endregion


        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        public struct SystemUser
        {
            public string id { get; set; }
            public string username { get; set; }
            public string pwd { get; set; }
            public string DocID { get; set; }
            public string DocType { get; set; }
            public string name { get; set; }
            public string surname { get; set; }
            public string email { get; set; }
            public string phone { get; set; }
            public string securityToken { get; set; }
            public string department { get; set; }
            public DateTime birth { get; set; }
            public bool isExternal { get; set; }
            public bool disabled { get; set; }
            public bool hideSocieties { get; set; }
            public bool hasToShift { get; set; }
            public bool requiresKeepAlive { get; set; }
            public string photo { get; set; }
            public bool hasFace { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static List<SystemUser> listInternalUsers(SqlConnection conn, SqlTransaction transaction = null)
        {
            List<SystemUser> users = new();
            using SqlCommand command = conn.CreateCommand();
            if (transaction != null)
            {
                command.Connection = conn;
                command.Transaction = transaction;
            }
            command.CommandText =
                "SELECT U.* " +
                "FROM users U " +
                "WHERE U.isExternal = 0 " +
                "ORDER BY username ASC";

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    users.Add(new SystemUser()
                    {
                        id = reader.GetString(reader.GetOrdinal("id")),
                        username = reader.GetString(reader.GetOrdinal("username")),
                        name = reader.GetString(reader.GetOrdinal("name")),
                        surname = reader.GetString(reader.GetOrdinal("surname")),
                        department = reader.GetString(reader.GetOrdinal("department")),
                        email = reader.GetString(reader.GetOrdinal("email"))
                    });
                }
            }

            return users;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
