using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;


namespace ThinkAndJobSolution.Controllers.MainHome.Sysadmin
{
    [Route("api/v1/permissions")]
    [ApiController]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpPost]
        [Route(template: "alter/{userId}/")]
        public async Task<IActionResult> AlterUserPermissions(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Permissions.AlterUserPermissions", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            else
            {
                if (HelperMethods.CheckUserIsSuper(null, null, null, userId))
                {
                    return Ok(new{ error = "Error 1002, El super usuario no puede ser modificado."});
                }
                using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("modulos", out JsonElement modulosArray))
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            bool failed = false;
                            //Comprobar si existe el usuario
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM users WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", userId);
                                try
                                {
                                    if ((Int32)command.ExecuteScalar() == 0)
                                    {
                                        failed = true;
                                        result = new { error = "Error 4300, no existe el usuario" };
                                    }
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new { error = "Error 5300, no se ha podido determinar si el usuario existe" };
                                }
                            }
                            //Eliminar permisos previos de componentes
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM permisos_componente WHERE userId = @ID";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5301, no se ha podido eliminar los permisos de componentes previos" };
                                    }
                                }
                            }
                            //Eliminar permisos previos de modulos
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM permisos_modulo WHERE userId = @ID";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5301, no se ha podido eliminar los permisos de componentes previos" };
                                    }
                                }
                            }
                            //Insertar los nuevos permisos
                            if (!failed)
                            {
                                foreach (var moduloJson in modulosArray.EnumerateArray())
                                {
                                    if (moduloJson.TryGetProperty("modulo", out JsonElement nombreModuloJson) &&
                                        moduloJson.TryGetProperty("jefe", out JsonElement jefeJson) &&
                                        moduloJson.TryGetProperty("componentes", out JsonElement componentesArray))
                                    {
                                        string nombreModulo = nombreModuloJson.GetString();
                                        bool jefe = jefeJson.GetBoolean();
                                        HelperMethods.Modulo? modulo = null;
                                        try
                                        {
                                            modulo = HelperMethods.modulos[nombreModulo];
                                        }
                                        catch (Exception)
                                        {
                                            failed = true;
                                            result = new { error = "Error 4302, nombre de modulo incorrecto" };
                                        }
                                        if (!failed)
                                        {
                                            using (SqlCommand command = conn.CreateCommand())
                                            {
                                                command.Connection = conn;
                                                command.Transaction = transaction;
                                                command.CommandText = "INSERT INTO permisos_modulo (userId, modulo, jefe) VALUES (@ID, @MODULO, @JEFE)";
                                                command.Parameters.AddWithValue("@ID", userId);
                                                command.Parameters.AddWithValue("@MODULO", nombreModulo);
                                                command.Parameters.AddWithValue("@JEFE", jefe ? 1 : 0);
                                                try
                                                {
                                                    command.ExecuteNonQuery();
                                                }
                                                catch (Exception)
                                                {
                                                    failed = true;
                                                    result = new { error = "Error 5302, no se ha podido insertar un nuevo permiso de modulo" };
                                                }
                                            }
                                        }
                                        if (!failed)
                                        {
                                            foreach (var componenteJson in componentesArray.EnumerateArray())
                                            {
                                                string nombreComponente = componenteJson.GetString();
                                                HelperMethods.Componente? componente = null;
                                                try
                                                {
                                                    componente = modulo.Value.Componentes[nombreComponente];
                                                }
                                                catch (Exception)
                                                {
                                                    failed = true;
                                                    result = new { error = "Error 4302, nombre de componente incorrecto" };
                                                }

                                                if (!failed)
                                                {
                                                    using (SqlCommand command = conn.CreateCommand())
                                                    {
                                                        command.Connection = conn;
                                                        command.Transaction = transaction;
                                                        command.CommandText = "INSERT INTO permisos_componente (userId, modulo, componente) VALUES (@ID, @MODULO, @COMPONENTE)";
                                                        command.Parameters.AddWithValue("@ID", userId);
                                                        command.Parameters.AddWithValue("@MODULO", nombreModulo);
                                                        command.Parameters.AddWithValue("@COMPONENTE", nombreComponente);
                                                        try
                                                        {
                                                            command.ExecuteNonQuery();
                                                        }
                                                        catch (Exception)
                                                        {
                                                            failed = true;
                                                            result = new { error = "Error 5302, no se ha podido insertar un nuevo permiso de modulo" };
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        failed = true;
                                        result = new { error = "Error 4302, formato de modulo incorrecto" };
                                    }
                                }
                            }

                            if (!failed)
                            {
                                transaction.Commit();
                                result = new
                                {
                                    error = false
                                };
                                HelperMethods.LogToDB(HelperMethods.LogType.USER_PERMISSIONS_MODIFIED, "Permisos de usuario " + HelperMethods.FindUsernameById(userId, conn) + " modificado", HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "alter-single-module/{userId}/")]
        public async Task<IActionResult> AlterSingleModule(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Permissions.AlterSingleModule", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });                
            }
            else
            {
                if (HelperMethods.CheckUserIsSuper(null, null, null, userId))
                {
                    return Ok(new{error = "Error 1002, El super usuario no puede ser modificado."});
                }
                using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("modulo", out JsonElement moduloJson) && json.TryGetProperty("permiso", out JsonElement permisoJson))
                {
                    string modulo = moduloJson.GetString();
                    bool permiso = permisoJson.GetBoolean();
                    if (!HelperMethods.modulos.ContainsKey(modulo))
                    {
                        return Ok(new{error = "Error 4303, el nombre del modulo no es correcto."});
                    }
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            bool failed = false;
                            //Comprobar si existe el usuario
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM users WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", userId);
                                try
                                {
                                    if ((Int32)command.ExecuteScalar() == 0)
                                    {
                                        failed = true;
                                        result = new { error = "Error 4300, no existe el usuario" };
                                    }
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new { error = "Error 5300, no se ha podido determinar si el usuario existe" };
                                }
                            }
                            //Eliminar permisos previos de componentes
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM permisos_componente WHERE userId = @ID AND modulo = @MODULO";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    command.Parameters.AddWithValue("@MODULO", modulo);
                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5301, no se ha podido eliminar los permisos de componentes previos" };
                                    }
                                }
                            }
                            //Eliminar el permiso de modulo
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM permisos_modulo WHERE userId = @ID AND modulo = @MODULO";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    command.Parameters.AddWithValue("@MODULO", modulo);
                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5301, no se ha podido eliminar los permisos de componentes previos" };
                                    }
                                }
                            }
                            //Insertar el permiso de modulo nuevo
                            if (!failed && permiso)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "INSERT INTO permisos_modulo (userId, modulo, jefe) VALUES (@ID, @MODULO, @JEFE)";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    command.Parameters.AddWithValue("@MODULO", modulo);
                                    command.Parameters.AddWithValue("@JEFE", 0);
                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5302, no se ha podido insertar un nuevo permiso de modulo" };
                                    }
                                }
                            }
                            if (!failed)
                            {
                                transaction.Commit();
                                result = new
                                {
                                    error = false
                                };

                                HelperMethods.LogToDB(HelperMethods.LogType.USER_PERMISSIONS_MODIFIED, "Permisos de usuario " + HelperMethods.FindUsernameById(userId, conn) + " modificado", HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }


        [HttpPost]
        [Route(template: "alter-single-module-opt/{userId}/")]
        public async Task<IActionResult> AlterSingleModuleOpt(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Permissions.AlterSingleModuleOpt", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                if (HelperMethods.CheckUserIsSuper(null, null, null, userId))
                {
                    return Ok(new{error = "Error 1002, El super usuario no puede ser modificado."});
                }
                using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("modulo", out JsonElement moduloJson) && json.TryGetProperty("nombre", out JsonElement nombreJson) &&
                    json.TryGetProperty("valor", out JsonElement valorJson))
                {
                    string modulo = moduloJson.GetString();
                    string nombre = nombreJson.GetString();
                    bool valor = valorJson.GetBoolean();
                    if (!HelperMethods.modulos.ContainsKey(modulo))
                    {
                        return Ok(new{error = "Error 4303, el nombre del modulo no es correcto."});
                    }
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            bool failed = false;
                            //Comprobar si el usuario tiene permiso para ese modulo
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM permisos_modulo WHERE userId = @ID AND modulo = @MODULO";
                                command.Parameters.AddWithValue("@ID", userId);
                                command.Parameters.AddWithValue("@MODULO", modulo);
                                try
                                {
                                    if ((Int32)command.ExecuteScalar() == 0)
                                    {
                                        failed = true;
                                        result = new { error = "Error 4300, el usuario no tiene permiso para el modulo" };
                                    }
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new { error = "Error 5300, no se ha podido determinar si el usuario tiene permisos para este modulo" };
                                }
                            }
                            //Actualizar el valor dependiendo de cual sea
                            if (!failed)
                            {
                                switch (nombre)
                                {
                                    case "jefe":
                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            command.Connection = conn;
                                            command.Transaction = transaction;
                                            command.CommandText = "UPDATE permisos_modulo SET jefe = @JEFE WHERE userId = @ID AND modulo = @MODULO";
                                            command.Parameters.AddWithValue("@ID", userId);
                                            command.Parameters.AddWithValue("@MODULO", modulo);
                                            command.Parameters.AddWithValue("@JEFE", valor ? 1 : 0);
                                            try
                                            {
                                                command.ExecuteNonQuery();
                                            }
                                            catch (Exception)
                                            {
                                                failed = true;
                                                result = new { error = "Error 5301, no se ha podido eliminar los permisos de componentes previos" };
                                            }
                                        }
                                        break;
                                    default:
                                        failed = true;
                                        result = new { error = "Error 4305, nombre de opt no reconocido" };
                                        break;
                                }
                            }
                            if (!failed)
                            {
                                transaction.Commit();
                                result = new
                                {
                                    error = false
                                };

                                HelperMethods.LogToDB(HelperMethods.LogType.USER_PERMISSIONS_MODIFIED, "Permisos de usuario " + HelperMethods.FindUsernameById(userId, conn) + " modificado", HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }


        [HttpPost]
        [Route(template: "alter-single-component/{userId}/")]
        public async Task<IActionResult> AlterSingleComponent(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Permissions.AlterSingleComponent", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                if (HelperMethods.CheckUserIsSuper(null, null, null, userId))
                {
                    return Ok(new{error = "Error 1002, El super usuario no puede ser modificado."});
                }
                using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await bodyReader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("modulo", out JsonElement moduloJson) && json.TryGetProperty("componente", out JsonElement componenteJson) &&
                    json.TryGetProperty("permiso", out JsonElement permisoJson))
                {
                    string modulo = moduloJson.GetString();
                    string componente = componenteJson.GetString();
                    bool permiso = permisoJson.GetBoolean();
                    if (!HelperMethods.modulos.ContainsKey(modulo))
                    {
                        return Ok(new{error = "Error 4303, el nombre del modulo no es correcto."});
                    }
                    if (!HelperMethods.modulos[modulo].Componentes.ContainsKey(componente))
                    {
                        return Ok(new{error = "Error 4303, el nombre del componente no es correcto."});
                    }
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            bool failed = false;
                            //Comprobar si el usuario tiene permiso para ese modulo
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM permisos_modulo WHERE userId = @ID AND modulo = @MODULO";
                                command.Parameters.AddWithValue("@ID", userId);
                                command.Parameters.AddWithValue("@MODULO", modulo);
                                try
                                {
                                    if ((Int32)command.ExecuteScalar() == 0)
                                    {
                                        failed = true;
                                        result = new { error = "Error 4300, el usuario no tiene permiso para el modulo" };
                                    }
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new { error = "Error 5300, no se ha podido determinar si el usuario tiene permisos para este modulo" };
                                }
                            }
                            //Eliminar el permiso del componente
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM permisos_componente WHERE userId = @ID AND modulo = @MODULO AND componente = @COMPONENTE";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    command.Parameters.AddWithValue("@MODULO", modulo);
                                    command.Parameters.AddWithValue("@COMPONENTE", componente);
                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5301, no se ha podido eliminar los permisos de componentes previos" };
                                    }
                                }
                            }
                            //Insertar el permiso del componente
                            if (!failed && permiso)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "INSERT INTO permisos_componente (userId, modulo, componente) VALUES (@ID, @MODULO, @COMPONENTE)";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    command.Parameters.AddWithValue("@MODULO", modulo);
                                    command.Parameters.AddWithValue("@COMPONENTE", componente);
                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5302, no se ha podido insertar un nuevo permiso de modulo" };
                                    }
                                }
                            }
                            if (!failed)
                            {
                                transaction.Commit();
                                result = new
                                {
                                    error = false
                                };

                                HelperMethods.LogToDB(HelperMethods.LogType.USER_PERMISSIONS_MODIFIED, "Permisos de usuario " + HelperMethods.FindUsernameById(userId, conn) + " modificado", HelperMethods.FindUsernameBySecurityToken(securityToken, conn), conn);
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                    }
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "list/{userId}/")]
        public IActionResult List(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Permissions.List", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                List<PermisoModulo> permisos = new List<PermisoModulo>();
                bool failed = false;
                if (HelperMethods.CheckUserIsSuper(null, null, null, userId))
                {
                    foreach (var dModulo in HelperMethods.modulos)
                    {
                        PermisoModulo modulo = new PermisoModulo
                        {
                            modulo = dModulo.Key,
                            jefe = true,
                            componentes = new List<string>()
                        };
                        foreach (var dCategoria in dModulo.Value.Componentes)
                        {
                            modulo.componentes.Add(dCategoria.Key);
                        }
                        permisos.Add(modulo);
                    }
                }
                else
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT modulo, jefe FROM permisos_modulo WHERE userId = @ID";
                            command.Parameters.AddWithValue("@ID", userId);
                            try
                            {
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        permisos.Add(new PermisoModulo
                                        {
                                            modulo = reader.GetString(reader.GetOrdinal("modulo")),
                                            jefe = reader.GetInt32(reader.GetOrdinal("jefe")) == 1,
                                            componentes = new List<string>()
                                        });
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5302, no se han podido obtener los permisos de modulo" };
                            }
                        }
                        foreach (PermisoModulo modulo in permisos)
                        {
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {

                                    command.CommandText = "SELECT componente FROM permisos_componente WHERE userId = @ID AND modulo = @MODULO";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    command.Parameters.AddWithValue("@MODULO", modulo.modulo);
                                    try
                                    {
                                        using (SqlDataReader reader = command.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                modulo.componentes.Add(reader.GetString(reader.GetOrdinal("componente")));
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5302, no se han podido obtener los permisos de componente" };
                                    }
                                }
                            }
                        }
                    }
                }
                if (!failed)
                {
                    result = new { error = false, permisos };
                }
            }

            return Ok(result);
        }

        //-----
        [HttpGet]
        [Route(template: "list-all/{userId}/")]
        public IActionResult ListAll(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Permissions.ListAll", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                List<ModuloParaPermisos> permisos = new List<ModuloParaPermisos>();
                bool failed = false;
                if (HelperMethods.CheckUserIsSuper(null, null, null, userId))
                {
                    foreach (var dModulo in HelperMethods.modulos)
                    {
                        ModuloParaPermisos modulo = new ModuloParaPermisos
                        {
                            id = dModulo.Key,
                            nombre = dModulo.Value.Nombre,
                            unitario = dModulo.Value.Unitario,
                            esJefe = true,
                            seleccionado = true,
                            componentes = new List<ComponenteParaPermisos>()
                        };
                        foreach (var dComponente in dModulo.Value.Componentes)
                        {
                            modulo.componentes.Add(new ComponenteParaPermisos
                            {
                                id = dComponente.Key,
                                nombre = dComponente.Value.Nombre,
                                seleccionado = true
                            });
                        }
                        permisos.Add(modulo);
                    }
                }
                else
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        foreach (var dModulo in HelperMethods.modulos)
                        {
                            if (!failed)
                            {
                                ModuloParaPermisos modulo = new ModuloParaPermisos
                                {
                                    id = dModulo.Key,
                                    nombre = dModulo.Value.Nombre,
                                    unitario = dModulo.Value.Unitario,
                                    seleccionado = false,
                                    esJefe = false,
                                    componentes = new List<ComponenteParaPermisos>()
                                };
                                //Comprobar si tiene permisos para el modulo
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "SELECT jefe FROM permisos_modulo WHERE userId = @ID AND modulo = @MODULO";
                                    command.Parameters.AddWithValue("@ID", userId);
                                    command.Parameters.AddWithValue("@MODULO", dModulo.Key);
                                    try
                                    {
                                        using (SqlDataReader reader = command.ExecuteReader())
                                        {
                                            if (reader.Read())
                                            {
                                                modulo.seleccionado = true;
                                                modulo.esJefe = reader.GetInt32(reader.GetOrdinal("jefe")) == 1;
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5302, no se han podido obtener los permisos de modulo" };
                                    }
                                }

                                foreach (var dComponente in dModulo.Value.Componentes)
                                {
                                    if (!failed)
                                    {
                                        ComponenteParaPermisos componente = new ComponenteParaPermisos
                                        {
                                            id = dComponente.Key,
                                            nombre = dComponente.Value.Nombre,
                                            seleccionado = false
                                        };

                                        //Comprobar si tiene el permiso de componente
                                        using (SqlCommand command = conn.CreateCommand())
                                        {

                                            command.CommandText = "SELECT * FROM permisos_componente WHERE userId = @ID AND modulo = @MODULO AND componente = @COMPONENTE";
                                            command.Parameters.AddWithValue("@ID", userId);
                                            command.Parameters.AddWithValue("@MODULO", dModulo.Key);
                                            command.Parameters.AddWithValue("@COMPONENTE", dComponente.Key);
                                            try
                                            {
                                                using (SqlDataReader reader = command.ExecuteReader())
                                                {
                                                    if (reader.Read())
                                                    {
                                                        componente.seleccionado = true;
                                                    }
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                failed = true;
                                                result = new { error = "Error 5302, no se han podido obtener los permisos de componente" };
                                            }
                                        }

                                        modulo.componentes.Add(componente);
                                    }
                                }

                                permisos.Add(modulo);
                            }
                        }
                    }
                }

                if (!failed)
                {
                    result = new { error = false, permisos };
                }
            }

            return Ok(result);
        }


        [HttpPost]
        [Route(template: "filter-componentes/")]
        public async Task<IActionResult> FilterComponentes()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            bool failed = false;
            if (json.TryGetProperty("modulo", out JsonElement moduloJson) && json.TryGetProperty("componentes", out JsonElement componentesArray))
            {
                string modulo = moduloJson.GetString();
                List<string> componentesPermitidos = new List<string>();
                if (HelperMethods.CheckUserIsSuper(null, null, securityToken))
                {
                    foreach (var componenteJson in componentesArray.EnumerateArray())
                    {
                        componentesPermitidos.Add(componenteJson.GetString());
                    }
                }
                else
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT COUNT(*) FROM permisos_modulo PM INNER JOIN users U ON PM.userId = U.id WHERE U.securityToken = @TOKEN AND PM.modulo = @MODULO";
                            command.Parameters.AddWithValue("@TOKEN", securityToken);
                            command.Parameters.AddWithValue("@MODULO", modulo);
                            try
                            {
                                if ((Int32)command.ExecuteScalar() == 0)
                                {
                                    failed = true;
                                    result = new { error = "Error 4302, acceso denegado al modulo" };
                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5302, no se han podido determinar si el usuario tiene acceso al modulo" };
                            }
                        }

                        foreach (var componenteJson in componentesArray.EnumerateArray())
                        {
                            string componente = componenteJson.GetString();

                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "SELECT COUNT(*) FROM permisos_componente PM INNER JOIN users U ON PM.userId = U.id WHERE U.securityToken = @TOKEN AND PM.modulo = @MODULO AND PM.componente = @COMPONENTE";
                                    command.Parameters.AddWithValue("@TOKEN", securityToken);
                                    command.Parameters.AddWithValue("@MODULO", modulo);
                                    command.Parameters.AddWithValue("@COMPONENTE", componente);
                                    try
                                    {
                                        if ((Int32)command.ExecuteScalar() != 0)
                                        {
                                            componentesPermitidos.Add(componente);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new { error = "Error 5302, no se han podido determinar si el usuario tiene acceso al modulo" };
                                    }
                                }
                            }
                        }
                    }
                }
                if (!failed)
                {
                    result = new { error = false, componentes = componentesPermitidos };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "filter-modulos/")]
        public async Task<IActionResult> FilterModulos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            List<string> modulosPermitidos = new List<string>();
            bool failed = false;
            if (HelperMethods.CheckUserIsSuper(null, null, securityToken))
            {
                foreach (var moduloJson in json.EnumerateArray())
                {
                    modulosPermitidos.Add(moduloJson.GetString());
                }
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    foreach (var moduloJson in json.EnumerateArray())
                    {
                        if (!failed)
                        {
                            string modulo = moduloJson.GetString();
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT COUNT(*) FROM permisos_modulo PM INNER JOIN users U ON PM.userId = U.id WHERE U.securityToken = @TOKEN AND PM.modulo = @MODULO";
                                command.Parameters.AddWithValue("@TOKEN", securityToken);
                                command.Parameters.AddWithValue("@MODULO", modulo);
                                try
                                {
                                    if ((Int32)command.ExecuteScalar() != 0)
                                    {
                                        modulosPermitidos.Add(modulo);
                                    }
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new { error = "Error 5302, no se han podido determinar si el usuario tiene acceso al modulo" };
                                }
                            }
                        }
                    }
                }
            }
            if (!failed)
            {
                result = new { error = false, modulos = modulosPermitidos };
            }
            return Ok(result);
        }



        [HttpPost]
        [Route(template: "available-componentes/")]
        public async Task<IActionResult> AvailableComponentes()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using System.IO.StreamReader bodyReader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await bodyReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            List<ModuloParaFront> modulosPermitidos = new List<ModuloParaFront>();
            bool failed = false, algunComponente = false;
            if (json.TryGetProperty("modulos", out JsonElement modulosArray))
            {
                if (HelperMethods.CheckUserIsSuper(null, null, securityToken))
                {
                    algunComponente = true;
                    foreach (var moduloJson in modulosArray.EnumerateArray())
                    {
                        if (!failed)
                        {
                            if (moduloJson.TryGetProperty("id", out JsonElement idJson))
                            {
                                if (HelperMethods.modulos.ContainsKey(idJson.GetString()))
                                {
                                    ModuloParaFront modulo = new ModuloParaFront
                                    {
                                        id = idJson.GetString(),
                                        esJefe = true,
                                        componentes = new List<string>()
                                    };
                                    foreach (var dComponente in HelperMethods.modulos[modulo.id].Componentes)
                                    {
                                        modulo.componentes.Add(dComponente.Key);
                                    }
                                    modulosPermitidos.Add(modulo);
                                }
                                else
                                {
                                    failed = true;
                                    result = new { error = "Error 4303, nombre de modulo no encontrado" };
                                }
                            }
                            else
                            {
                                failed = true;
                                result = new { error = "Error 4303, formato incorrecto en la petición" };
                            }
                        }
                    }
                }
                else
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        foreach (var moduloJson in modulosArray.EnumerateArray())
                        {
                            if (!failed)
                            {
                                if (moduloJson.TryGetProperty("id", out JsonElement idJson))
                                {
                                    if (HelperMethods.modulos.ContainsKey(idJson.GetString()))
                                    {
                                        ModuloParaFront modulo = new ModuloParaFront
                                        {
                                            id = idJson.GetString(),
                                            esJefe = false,
                                            componentes = new List<string>()
                                        };
                                        bool accesoAlModulo = false;
                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            command.CommandText = "SELECT jefe FROM permisos_modulo PM INNER JOIN users U ON PM.userId = U.id WHERE U.securityToken = @TOKEN AND PM.modulo = @MODULO";
                                            command.Parameters.AddWithValue("@TOKEN", securityToken);
                                            command.Parameters.AddWithValue("@MODULO", modulo.id);
                                            try
                                            {
                                                using (SqlDataReader reader = command.ExecuteReader())
                                                {
                                                    if (reader.Read())
                                                    {
                                                        modulo.esJefe = reader.GetInt32(reader.GetOrdinal("jefe")) == 1;
                                                        accesoAlModulo = true;
                                                    }
                                                    else
                                                    {
                                                        accesoAlModulo = false;
                                                    }
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                failed = true;
                                                result = new { error = "Error 5302, no se han podido determinar si el usuario tiene acceso al modulo" };
                                            }
                                        }
                                        if (!failed && accesoAlModulo)
                                        {
                                            using (SqlCommand command = conn.CreateCommand())
                                            {
                                                command.CommandText = "SELECT componente FROM permisos_componente PM INNER JOIN users U ON PM.userId = U.id WHERE U.securityToken = @TOKEN AND PM.modulo = @MODULO";
                                                command.Parameters.AddWithValue("@TOKEN", securityToken);
                                                command.Parameters.AddWithValue("@MODULO", modulo.id);
                                                try
                                                {
                                                    using (SqlDataReader reader = command.ExecuteReader())
                                                    {
                                                        while (reader.Read())
                                                        {
                                                            modulo.componentes.Add(reader.GetString(reader.GetOrdinal("componente")));
                                                            algunComponente = true;
                                                        }
                                                    }
                                                }
                                                catch (Exception)
                                                {
                                                    failed = true;
                                                    result = new { error = "Error 5302, no se han podido determinar si el usuario tiene acceso al componente" };
                                                }
                                            }
                                        }
                                        if (accesoAlModulo)
                                        {
                                            modulosPermitidos.Add(modulo);
                                        }
                                    }
                                    else
                                    {
                                        failed = true;
                                        result = new { error = "Error 4303, nombre de modulo no encontrado" };
                                    }
                                }
                                else
                                {
                                    failed = true;
                                    result = new { error = "Error 4303, formato incorrecto en la petición" };
                                }
                            }
                        }
                    }
                }
                if (!failed)
                {
                    if (algunComponente)
                    {
                        result = modulosPermitidos;
                    }
                    else
                    {
                        result = new { error = true };
                    }
                }
                else
                {
                    result = new { error = true };
                }
            }
            return Ok(result);
        }


        [HttpGet]
        [Route(template: "available-modulos/")]
        public IActionResult AvailableModulos()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            Dictionary<string, bool> modulosPermitidos = new Dictionary<string, bool>();
            bool failed = false;
            if (HelperMethods.CheckUserIsSuper(null, null, securityToken))
            {
                foreach (var modulo in HelperMethods.modulos)
                {
                    modulosPermitidos[modulo.Key] = true;
                }
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    foreach (var modulo in HelperMethods.modulos)
                    {
                        if (!failed)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT COUNT(*) FROM permisos_modulo PM INNER JOIN users U ON PM.userId = U.id WHERE U.securityToken = @TOKEN AND PM.modulo = @MODULO";
                                command.Parameters.AddWithValue("@TOKEN", securityToken);
                                command.Parameters.AddWithValue("@MODULO", modulo.Key);
                                try
                                {
                                    modulosPermitidos[modulo.Key] = (Int32)command.ExecuteScalar() != 0;
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new { error = "Error 5302, no se han podido determinar si el usuario tiene acceso al modulo" };
                                }
                            }
                        }
                    }
                }
            }
            if (!failed)
            {
                result = new { error = false, modulos = modulosPermitidos };
            }
            return Ok(result);
        }


        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        public struct PermisoModulo
        {
            public string modulo { get; set; }
            public bool jefe { get; set; }
            public List<string> componentes { get; set; }
        }
        public struct ModuloParaPermisos
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public bool unitario { get; set; }
            public bool esJefe { get; set; }
            public bool seleccionado { get; set; }
            public List<ComponenteParaPermisos> componentes { get; set; }
        }

        public struct ComponenteParaPermisos
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public bool seleccionado { get; set; }
        }
        public struct ModuloParaFront
        {
            public string id { get; set; }
            public bool esJefe { get; set; }
            public List<string> componentes { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
