using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Model.Client;
using ThinkAndJobSolution.Controllers.MainHome.Sysadmin;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.MainHome.Sysadmin.TiposContratosEmpresasController;
using static ThinkAndJobSolution.Controllers.MainHome.Sysadmin.UserCredentialsController;
using TemplateEngine.Docx;
using ThinkAndJobSolution.Utils;
using System.Text;
using ThinkAndJobSolution.Controllers.Commons;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    //[Route("api/[controller]")]
    //[ApiController]
    [Route("api/v1/company-contrato")]
    [ApiController]
    [Authorize]
    public class CompanyContratoController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        //Listado

        [HttpGet]
        [Route(template: "list/{companyId}/")]
        public IActionResult List(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                result = new
                {
                    error = false,
                    contratos = listContratos(conn, companyId)
                };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-client/{companyId}/")]
        public IActionResult ListForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_CONTRATOS) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                result = new
                {
                    error = false,
                    contratos = listContratos(conn, companyId)
                };
            }

            return Ok(result);
        }

        //Obtencion

        [HttpGet]
        [Route(template: "{contratoId}")]
        public IActionResult Get(string contratoId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            CompanyContrato? contrato = getContrato(contratoId);

            if (contrato == null)
            {
                result = new
                {
                    error = "Error 4802, contrato no encontrado"
                };
            }
            else
            {
                try
                {
                    List<Puesto> puestos = getCurrentCategoriesOfCompany(contrato.Value.companyId);
                    CompanyContrato tmpContrato = contrato.Value;
                    for (int i = 0; i < tmpContrato.puestos.Count; i++)
                    {
                        Puesto tmpPuesto = tmpContrato.puestos[i];
                        if (puestos.Any(p => p.categoryId == tmpPuesto.categoryId))
                            tmpPuesto.mandatory = true;
                        tmpContrato.puestos[i] = tmpPuesto;
                    }
                    contrato = tmpContrato;
                }
                catch (Exception) { }
                result = new
                {
                    error = false,
                    contrato = contrato.Value
                };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "current-categories/{companyId}/")]
        public IActionResult GetCurrentCategoriesOfCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.GetCurrentCategoriesOfCompany", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            try
            {
                result = new { error = false, categories = getCurrentCategoriesOfCompany(companyId) };
            }
            catch (Exception)
            {
                result = new { error = "Error 5803, No se han podido obtener las categorias mínimas del contrato." };
            }

            return Ok(result);
        }

        //Creacion

        [HttpPost]
        [Route(template: "create/{companyId}/")]
        public async Task<IActionResult> Create(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.Create", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("reuse", out JsonElement reuseJson))
            {
                bool reuse = GetJsonBool(reuseJson) ?? false;

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    bool failed = false;

                    //Obtener los puestos minimos que debe tener el contrato
                    string puestos = JsonSerializer.Serialize(getCurrentCategoriesOfCompany(companyId, conn));

                    //Eliminar el contrato abierto anterior
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM company_contratos WHERE companyId = @COMPANY AND closed = 0";
                        command.Parameters.AddWithValue("@COMPANY", companyId);
                        command.ExecuteNonQuery();
                    }

                    //Insertar el nuevo contrato
                    string id = ComputeStringHash(companyId + DateTime.Now.Millisecond);
                    if (reuse)
                    {
                        CompanyContrato? vigente = null;
                        string vigenteId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT TOP 1 id FROM company_contratos WHERE companyId = @COMPANY AND closed = 1 ORDER BY date DESC";
                            command.Parameters.AddWithValue("@COMPANY", companyId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    vigenteId = reader.GetString(reader.GetOrdinal("id"));
                                }
                            }
                        }

                        if (vigenteId != null)
                        {
                            vigente = getContrato(vigenteId, conn);
                        }

                        if (vigente == null)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 4490, esta empresa no tiene un contrato vigente"
                            };
                        }
                        else
                        {
                            string username = FindUsernameBySecurityToken(securityToken, conn);
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "INSERT INTO company_contratos " +
                                                                            "(id, companyId, representanteNombre, representanteApellido1, representanteApellido2, representanteDni, representanteTelefono, representanteEmail, duracion, puestos, tarifa, indemnizacion, clausulas, tipoId, provincia, localidad, createdBy, categoria) VALUES " +
                                                                            "(@ID, @COMPANY, @REP_NOMBRE, @REP_APELLIDO1, @REP_APELLIDO2, @REP_DNI, @REP_TELEFONO, @REP_EMAIL, @DURACION, @PUESTOS, @TARIFA, @INDEMNIZACION, @CLAUSULAS, @TIPO, @PROVINCIA, @LOCALIDAD, @CREATED_BY, @CATEGORIA)";

                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@COMPANY", companyId);
                                command.Parameters.AddWithValue("@REP_NOMBRE", (object)vigente.Value.representanteNombre ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REP_APELLIDO1", (object)vigente.Value.representanteApellido1 ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REP_APELLIDO2", (object)vigente.Value.representanteApellido2 ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REP_DNI", (object)vigente.Value.representanteDni ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REP_TELEFONO", (object)vigente.Value.representanteTelefono ?? DBNull.Value);
                                command.Parameters.AddWithValue("@REP_EMAIL", (object)vigente.Value.representanteEmail ?? DBNull.Value);
                                command.Parameters.AddWithValue("@DURACION", (object)vigente.Value.duracion ?? DBNull.Value);
                                command.Parameters.AddWithValue("@PUESTOS", puestos);
                                command.Parameters.AddWithValue("@TARIFA", (object)vigente.Value.tarifa ?? DBNull.Value);
                                command.Parameters.AddWithValue("@INDEMNIZACION", (object)vigente.Value.indemnizacion ?? DBNull.Value);
                                command.Parameters.AddWithValue("@CLAUSULAS", vigente.Value.clausulas == null ? DBNull.Value : JsonSerializer.Serialize(vigente.Value.clausulas));
                                command.Parameters.AddWithValue("@TIPO", (object)vigente.Value.tipoId ?? DBNull.Value);
                                command.Parameters.AddWithValue("@PROVINCIA", (object)vigente.Value.provincia ?? DBNull.Value);
                                command.Parameters.AddWithValue("@LOCALIDAD", (object)vigente.Value.localidad ?? DBNull.Value);
                                command.Parameters.AddWithValue("@CREATED_BY", username);
                                command.Parameters.AddWithValue("@CATEGORIA", (object)vigente.Value.categoria ?? DBNull.Value);

                                if (command.ExecuteNonQuery() == 0)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5491, no se ha podido crear el contrato"
                                    };
                                }
                            }
                        }
                    }
                    else
                    {
                        string username = FindUsernameBySecurityToken(securityToken, conn);
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO company_contratos " +
                                                                        "(id, companyId, puestos, createdBy) VALUES " +
                                                                        "(@ID, @COMPANY, @PUESTOS, @CREATED_BY)";

                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@COMPANY", companyId);
                            command.Parameters.AddWithValue("@PUESTOS", puestos);
                            command.Parameters.AddWithValue("@CREATED_BY", username);

                            if (command.ExecuteNonQuery() == 0)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5491, no se ha podido crear el contrato"
                                };
                            }
                        }
                    }


                    if (!failed)
                    {

                        result = new
                        {
                            error = false,
                            id
                        };
                    }
                }
            }

            return Ok(result);
        }

        //Eliminacion

        [HttpDelete]
        [Route(template: "delete/{contratoId}/")]
        public IActionResult Delete(string contratoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.Delete", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                //Obtener la empresa
                string companyId = null;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT companyId FROM company_contratos WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", contratoId);
                    using (SqlDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            companyId = reader.GetString(reader.GetOrdinal("companyId"));
                }

                if (companyId == null)
                {
                    result = new { error = "Error 4862, contrato de cliente no encontrado" };
                }
                else
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        try
                        {
                            command.CommandText = "DELETE FROM company_contratos WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", contratoId);
                            command.ExecuteNonQuery();
                            DeleteDir(new[] { "companies", companyId, "contrato", contratoId });
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            result = new
                            {
                                error = "Error 5730, no se ha podido eliminar el contrato de cliente"
                            };
                        }
                    }
                }
            }

            return Ok(result);
        }

        //Actualizacion

        [HttpPatch]
        [Route(template: "close/{companyId}/")]
        public async Task<IActionResult> Close(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.Close", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        bool failed = false;

                        //Obtener el contrato abierto actual y sus parámetros
                        string contratoId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT id FROM company_contratos WHERE companyId = @COMPANY AND closed = 0";
                            command.Parameters.AddWithValue("@COMPANY", companyId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    contratoId = reader.GetString(reader.GetOrdinal("id"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new { error = "Error 4002, empresa no encontrada o no tiene un contrato abierto" };
                                }
                            }
                        }

                        CompanyContrato contrato = getContrato(contratoId, conn, transaction).Value;
                        ExtendedCompanyData company = CompanyController.getCompany(conn, transaction, contrato.companyId);

                        string possibleError = checkContratoIsReady(contrato, conn, transaction);
                        if (possibleError != null)
                        {
                            failed = true;
                            result = new { error = possibleError };
                        }

                        //Comprobar los limites
                        if (!failed && !CheckUserIsSuper(conn, transaction, securityToken))
                        {
                            ContractConstants limits = getContractConstants(contrato.categoria, conn, transaction);

                            if (contrato.tarifa < limits.tarifaMin || contrato.tarifa > limits.tarifaMax)
                            {
                                failed = true;
                                result = new
                                {
                                    error = $"Error 4003, la tarifa {contrato.tarifa:0.##} está fuera del rango permitido ({limits.tarifaMin:0.##} - {limits.tarifaMax:0.##})"
                                };
                            }
                            if (contrato.indemnizacion != null && (contrato.indemnizacion.Value < limits.indemnizacionMin || contrato.indemnizacion.Value > limits.indemnizacionMax))
                            {
                                failed = true;
                                result = new
                                {
                                    error = $"Error 4003, la indemnización {contrato.indemnizacion:0.##} está fuera del rango permitido ({limits.indemnizacionMin:0.##} - {limits.indemnizacionMax:0.##})"
                                };
                            }
                        }

                        //Comprobar si se tendra que crear un nuevo usuario CL administrador
                        bool adminCLhasToBeCreated = true;
                        string adminCLId = null;
                        if (!failed)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT id FROM client_users WHERE email = @EMAIL";
                                command.Parameters.AddWithValue("@EMAIL", company.firmanteEmail);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        adminCLhasToBeCreated = false;
                                        adminCLId = reader.GetString(reader.GetOrdinal("id"));
                                    }
                                }
                            }
                        }

                        //Precomprobar si habra problemas creando el usuario CL administrador
                        if (!failed && adminCLhasToBeCreated)
                        {
                            try
                            {
                                string dniUsedBy = CheckDNINIECIFunique(company.firmanteDni, adminCLId, conn, transaction);
                                if (dniUsedBy != null && dniUsedBy != "un usuario de cliente")
                                {
                                    throw new Exception($"Error 4848, no se puede crear un usuario para el administrador, el dni {company.firmanteDni} ya está en uso por {dniUsedBy}.");
                                }

                                string emailUsedBy = CheckEMAILunique(company.firmanteEmail, adminCLId, conn, transaction);
                                if (emailUsedBy != null)
                                {
                                    throw new Exception($"Error 4848, no se puede crear un usuario para el administrador, el email {company.firmanteEmail} ya está en uso por {emailUsedBy}");
                                }

                                string phoneUsedBy = CheckPHONEunique(company.firmanteTelefono, adminCLId, conn, transaction);
                                if (phoneUsedBy != null && dniUsedBy != "un usuario de cliente")
                                {
                                    throw new Exception($"Error 4848, no se puede crear un usuario para el administrador, el teléfono {company.firmanteTelefono} ya está en uso por {phoneUsedBy}");
                                }
                            }
                            catch (Exception e)
                            {
                                failed = true;
                                result = new { error = e.Message };
                            }
                        }

                        //Generar el contrato
                        if (!failed)
                        {
                            try
                            {
                                // Si ya existe un contrato generado, borrarlo
                                DeleteDir(new[] { "companies", companyId, "contrato", contratoId });

                                generateContrato(contrato, conn, new(), transaction);

                                //Crear las categorias necesarias
                                contrato.puestos = createCategoriesIfNeeded(conn, transaction, contrato.puestos);

                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE company_contratos SET puestos = @PUESTOS WHERE id = @CONTRATO_ID";
                                    command.Parameters.AddWithValue("@CONTRATO_ID", contratoId);
                                    command.Parameters.AddWithValue("@PUESTOS", JsonSerializer.Serialize(contrato.puestos));
                                    command.ExecuteNonQuery();
                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5005, error al generar contrato" };
                            }
                        }

                        //Cerrar el contrato
                        // * El que cierra el contrato queda fijado como creador del mismo
                        if (!failed)
                        {
                            string username = FindUsernameBySecurityToken(securityToken, conn);
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE company_contratos SET closed = 1, createdBy = @CREATED_BY WHERE id = @ID";
                                command.Parameters.AddWithValue("@CREATED_BY", username);
                                command.Parameters.AddWithValue("@ID", contratoId);
                                command.ExecuteNonQuery();
                            }
                        }

                        //Crear trabajos en los centros de la empresa con las categorias seleccionadas
                        //TODO: Revertir esta movida de codigo cuando quieran que haya que cerrar un contrato para que las categorias tomen efecto
                        /*
						if (!failed)
						{
								CentroController.addWorksToCenterFromContrato(conn, transaction, contrato);
						}
						*/

                        //Crear usuario administrador si es neceasrio y enviar correo
                        if (!failed)
                        {
                            //Comprobar si ya existe el usuario
                            try
                            {
                                if (adminCLhasToBeCreated)
                                {
                                    adminCLId = ClientUserController.createAdminClientUserFromCompanyId(companyId, securityToken, conn, transaction);
                                }
                                else
                                {
                                    //No hay que crearlo, pero le ponemos los permisos
                                    ClientUserController.addAdminClientPermissions(adminCLId, companyId, conn, transaction);
                                }
                                EventMailer.SendEmail(new EventMailer.Email()
                                {
                                    template = "companyContratoPendingSign",
                                    toEmail = company.firmanteEmail,
                                    toName = company.firmanteNombre,
                                    subject = "[Think&Job] Contrato listo para firmar",
                                    priority = EventMailer.EmailPriority.MODERATE
                                });
                                await PushNotificationController.sendNotification(new() { type = "cl", id = adminCLId }, new()
                                {
                                    title = "Nuevo contrato disponible",
                                    body = "Tiene disponible un nuevo contrato para revisar y firmar",
                                    type = "client-contrato-pendiente-firmar",
                                    data = new() { { "id", contratoId }, { "companyId", companyId } }
                                }, conn, transaction);
                            }
                            catch (Exception e)
                            {
                                failed = true;
                                result = new { error = e.Message };
                            }
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            result = new { error = false };
                            transaction.Commit();
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5006, no se ha podido crear el contrato" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "regen/{companyId}/")]
        public IActionResult Regen(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.Regen", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        bool failed = false;

                        //Obtener el contrato abierto actual y sus parámetros
                        string contratoId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT id FROM company_contratos WHERE companyId = @COMPANY AND closed = 1";
                            command.Parameters.AddWithValue("@COMPANY", companyId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    contratoId = reader.GetString(reader.GetOrdinal("id"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new { error = "Error 4002, empresa no encontrada o no tiene un contrato" };
                                }
                            }
                        }
                        CompanyContrato contrato = getContrato(contratoId, conn, transaction).Value;
                        ExtendedCompanyData company = CompanyController.getCompany(conn, transaction, contrato.companyId);

                        try
                        {
                            generateContrato(contrato, conn, new(), transaction);
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new { error = "Error 5005, error al generar contrato" };
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            result = new { error = false };
                            transaction.Commit();
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5006, no se ha podido crear el contrato" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "update/{contratoId}/")]
        public async Task<IActionResult> Update(string contratoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.Update", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerJson.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("representanteNombre", out JsonElement representanteNombreJson) &&
                    json.TryGetProperty("representanteApellido1", out JsonElement representanteApellido1Json) &&
                    json.TryGetProperty("representanteApellido2", out JsonElement representanteApellido2Json) &&
                    json.TryGetProperty("representanteDni", out JsonElement representanteDniJson) &&
                    json.TryGetProperty("representanteTelefono", out JsonElement representanteTelefonoJson) &&
                    json.TryGetProperty("representanteEmail", out JsonElement representanteEmailJson) &&
                    json.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                    json.TryGetProperty("localidad", out JsonElement localidadJson) &&
                    json.TryGetProperty("duracion", out JsonElement duracionJson) &&
                    json.TryGetProperty("puestos", out JsonElement puestosJson) &&
                    json.TryGetProperty("tarifa", out JsonElement tarifaJson) &&
                    json.TryGetProperty("indemnizacion", out JsonElement indemnizacionJson) &&
                    json.TryGetProperty("tipoId", out JsonElement tipoIdJson) &&
                    json.TryGetProperty("clausulas", out JsonElement clausulasJson) &&
                    json.TryGetProperty("categoria", out JsonElement categoriaJson))
            {
                string representanteNombre = representanteNombreJson.GetString();
                string representanteApellido1 = representanteApellido1Json.GetString();
                string representanteApellido2 = representanteApellido2Json.GetString();
                string representanteDni = representanteDniJson.GetString();
                string representanteTelefono = representanteTelefonoJson.GetString();
                string representanteEmail = representanteEmailJson.GetString();
                string provincia = provinciaJson.GetString();
                string localidad = localidadJson.GetString();
                int? duracion = GetJsonInt(duracionJson);
                List<Puesto> puestos = parsePuestos(puestosJson);
                double? tarifa = GetJsonDouble(tarifaJson);
                double? indemnizacion = GetJsonDouble(indemnizacionJson);
                string tipoId = tipoIdJson.GetString();
                List<Clausula> clausulas = parseClausulas(clausulasJson);
                string categoria = categoriaJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    bool failed = false;
                    string username = FindUsernameBySecurityToken(securityToken, conn);

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {

                            //Comprobar que el contrado exista y que no este cerrado
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT closed FROM company_contratos WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", contratoId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        if (reader.GetInt32(reader.GetOrdinal("closed")) == 1)
                                        {
                                            failed = true;
                                            result = new { error = "Error 4559, el contrato esta cerrado, no se puede modificar" };
                                        }
                                    }
                                    else
                                    {
                                        failed = true;
                                        result = new { error = "Error 4558, contrato no encontrado" };
                                    }
                                }
                            }

                            //Actualizar el contrato
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE company_contratos SET " +
                                                                                "representanteNombre = @REP_NOMBRE, " +
                                                                                "representanteApellido1 = @REP_APELLIDO1, " +
                                                                                "representanteApellido2 = @REP_APELLIDO2, " +
                                                                                "representanteDni = @REP_DNI, " +
                                                                                "representanteTelefono = @REP_TELEFONO, " +
                                                                                "representanteEmail = @REP_EMAIL, " +
                                                                                "provincia = @PROVINCIA, " +
                                                                                "localidad = @LOCALIDAD, " +
                                                                                "duracion = @DURACION, " +
                                                                                "puestos = @PUESTOS, " +
                                                                                "tarifa = @TARIFA, " +
                                                                                "indemnizacion = @INDEMNIZACION, " +
                                                                                "clausulas = @CLAUSULAS, " +
                                                                                "tipoId = @TIPO, " +
                                                                                "categoria = @CATEGORIA, " +
                                                                                "createdBy = @CREATED_BY " +
                                                                                "WHERE id = @ID ";

                                    command.Parameters.AddWithValue("@ID", contratoId);
                                    command.Parameters.AddWithValue("@REP_NOMBRE", (object)representanteNombre ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@REP_APELLIDO1", (object)representanteApellido1 ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@REP_APELLIDO2", (object)representanteApellido2 ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@REP_DNI", (object)representanteDni ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@REP_TELEFONO", (object)representanteTelefono ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@REP_EMAIL", (object)representanteEmail ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@PROVINCIA", (object)provincia ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@LOCALIDAD", (object)localidad ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@DURACION", (object)duracion ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@PUESTOS", puestos == null ? DBNull.Value : JsonSerializer.Serialize(puestos));
                                    command.Parameters.AddWithValue("@TARIFA", (object)tarifa ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@INDEMNIZACION", (object)indemnizacion ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@CLAUSULAS", clausulas == null ? DBNull.Value : JsonSerializer.Serialize(clausulas));
                                    command.Parameters.AddWithValue("@TIPO", (object)tipoId ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@CREATED_BY", (object)username ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@CATEGORIA", (object)categoria ?? DBNull.Value);

                                    if (command.ExecuteNonQuery() == 0)
                                    {
                                        failed = true;
                                        result = new
                                        {
                                            error = "Error 5491, no se ha podido actualizar el contrato"
                                        };
                                    }
                                }
                            }

                            //Crear trabajos en los centros de la empresa con las categorias seleccionadas
                            //TODO: Revertir esta movida de codigo cuando quieran que haya que cerrar un contrato para que las categorias tomen efecto
                            if (!failed)
                            {
                                CompanyContrato? contratoTmp = getContrato(contratoId, conn, transaction);
                                if (contratoTmp != null)
                                {
                                    CompanyContrato contrato = contratoTmp.Value;
                                    puestos = createCategoriesIfNeeded(conn, transaction, contrato.puestos); //Crear las categorias necesarias
                                    CentroController.addWorksToCenterFromContrato(conn, transaction, contrato);
                                    //Guardar los puestos ahora con los Ids de las categorias
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText =
                                                "UPDATE company_contratos SET puestos = @PUESTOS WHERE id = @ID";

                                        command.Parameters.AddWithValue("@ID", contratoId);
                                        command.Parameters.AddWithValue("@PUESTOS", puestos == null ? DBNull.Value : JsonSerializer.Serialize(puestos));
                                        command.ExecuteNonQuery();
                                    }
                                }
                            }

                            if (!failed)
                            {
                                transaction.Commit();
                                result = new { error = false };
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5492, no se ha podido modificar el borrador" };
                        }
                    }
                }
            }

            return Ok(result);
        }

        //Acciones

        [HttpGet]
        [Route(template: "download/{contratoId}/{type}")]
        public IActionResult Download(string contratoId, string type)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            CompanyContrato? contrato = getContrato(contratoId);
            object result;

            if (contrato == null)
            {
                result = new { error = "Error 4002, contrato de cliente no encontrado" };
            }
            else
            {
                CompanyContrato cont = (CompanyContrato)contrato;
                if (!cont.closed)
                {
                    try
                    {
                        // Si ya existe un contrato generado, borrarlo
                        DeleteDir(new[] { "companies", contrato.Value.companyId, "contrato", contrato.Value.id, "contrato" });
                        DeleteDir(new[] { "companies", contrato.Value.companyId, "contrato", contrato.Value.id, "contratoWord" });

                        using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                        {
                            conn.Open();
                            using (SqlTransaction transaction = conn.BeginTransaction())
                            {
                                SystemUser user = new();
                                user.securityToken = securityToken;
                                generateContrato(cont, conn, user, transaction);

                                //Crear las categorias necesarias
                                cont.puestos = createCategoriesIfNeeded(conn, transaction, cont.puestos);

                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE company_contratos SET puestos = @PUESTOS WHERE id = @CONTRATO_ID";
                                    command.Parameters.AddWithValue("@CONTRATO_ID", contratoId);
                                    command.Parameters.AddWithValue("@PUESTOS", JsonSerializer.Serialize(cont.puestos));
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        if (type.Equals("pdf"))
                            result = ReadFile(new[] { "companies", contrato.Value.companyId, "contrato", contrato.Value.id, "contrato" });
                        else
                            result = ReadFile(new[] { "companies", contrato.Value.companyId, "contrato", contrato.Value.id, "contratoWord" });
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5005, error al descargar contrato" };
                    }
                }
                else
                {
                    if (type.Equals("pdf"))
                        result = ReadFile(new[] { "companies", contrato.Value.companyId, "contrato", contrato.Value.id, "contrato" });
                    else
                        result = ReadFile(new[] { "companies", contrato.Value.companyId, "contrato", contrato.Value.id, "contratoWord" });
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-signed/{contratoId}")]
        public IActionResult DownloadSigned(string contratoId)
        {
            CompanyContrato? contrato = getContrato(contratoId);
            object result;

            if (contrato == null)
            {
                result = new { error = "Error 4002, contrato de cliente no encontrado" };
            }
            else
            {
                result = ReadFile(new[] { "companies", contrato.Value.companyId, "contrato", contrato.Value.id, "firmado" });
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "sign/{contratoId}")]
        public async Task<IActionResult> Sign(string contratoId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerJson.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("sign", out JsonElement signJson))
            {
                string signBase64 = signJson.GetString();

                CompanyContrato? contrato = getContrato(contratoId);

                if (contrato == null)
                {
                    result = new { error = "Error 4002, contrato de cliente no encotnrado" };
                }
                else
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                        {
                            conn.Open();

                            signContrato(contrato.Value, signBase64);

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "UPDATE company_contratos SET signed = 1, signedDate = getdate() WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", contratoId);
                                command.ExecuteNonQuery();
                            }

                            string firmanteEmail = null, firmanteName = null, companyId = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                        "SELECT firmanteEmail, TRIM(CONCAT(firmanteNombre, ' ', firmanteApellido1, ' ', firmanteApellido2)) as firmanteName, companyId " +
                                        "FROM empresas E INNER JOIN company_contratos CC ON(CC.companyId = E.id) " +
                                        "WHERE CC.id = @ID";
                                command.Parameters.AddWithValue("@ID", contratoId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        firmanteEmail = reader.IsDBNull(reader.GetOrdinal("firmanteEmail")) ? null : reader.GetString(reader.GetOrdinal("firmanteEmail"));
                                        firmanteName = reader.IsDBNull(reader.GetOrdinal("firmanteName")) ? null : reader.GetString(reader.GetOrdinal("firmanteName"));
                                        companyId = reader.GetString(reader.GetOrdinal("companyId"));
                                    }
                                }
                            }

                            if (firmanteEmail != null && firmanteName != null)
                            {
                                EventMailer.SendEmail(new EventMailer.Email()
                                {
                                    template = "companyContratoSigned",
                                    toEmail = firmanteEmail,
                                    toName = firmanteName,
                                    subject = "[Think&Job] Contrato firmado",
                                    priority = EventMailer.EmailPriority.MODERATE
                                });

                                string firmanteId = null;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "SELECT id FROM client_users WHERE email = @EMAIL";
                                    command.Parameters.AddWithValue("@EMAIL", firmanteEmail);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            firmanteId = reader.GetString(reader.GetOrdinal("id"));
                                        }
                                    }
                                }
                                if (firmanteId != null)
                                {
                                    await PushNotificationController.sendNotification(new() { type = "cl", id = firmanteId }, new()
                                    {
                                        title = "Contrato firmado",
                                        type = "client-contrato-firmado",
                                        data = new() { { "id", contratoId }, { "companyId", companyId } }
                                    }, conn);
                                }
                            }

                            result = new { error = false };
                        }
                    }
                    catch
                    {
                        result = new { error = "Error 5003, no se ha podido firmar el contrato" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-pending-aproval/")]
        public IActionResult ListPendingAproval()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.ListPendingAproval", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                List<CompanyContratoForAproval> contratos = new();

                TiposContratosEmpresasController.ContractConstants limitsRJ = TiposContratosEmpresasController.getContractConstants("thinkandjob", conn);
                TiposContratosEmpresasController.ContractConstants limitsIM = TiposContratosEmpresasController.getContractConstants("imaginefreedom", conn);

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT CC.*, E.nombre as companyNombre " +
                                                                "FROM company_contratos CC " +
                                                                "INNER JOIN empresas E ON(CC.companyId = E.id) " +
                                                                "WHERE CC.closed = 0 " +
                                                                "ORDER BY date DESC";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            if (reader.IsDBNull(reader.GetOrdinal("categoria"))) continue;

                            TiposContratosEmpresasController.ContractConstants limits = reader.GetString(reader.GetOrdinal("categoria")).Equals("thinkandjob") ? limitsRJ : limitsIM;

                            double? tarifa = reader.IsDBNull(reader.GetOrdinal("tarifa")) ? null : reader.GetDouble(reader.GetOrdinal("tarifa"));
                            double? indemnizacion = reader.IsDBNull(reader.GetOrdinal("indemnizacion")) ? null : reader.GetDouble(reader.GetOrdinal("indemnizacion"));
                            bool tarifaOffLimits = tarifa != null && (tarifa < limits.tarifaMin || tarifa > limits.tarifaMax);
                            bool indemnizacionOffLimits = indemnizacion != null && (indemnizacion.Value < limits.indemnizacionMin || indemnizacion.Value > limits.indemnizacionMax);

                            if (!(tarifaOffLimits || indemnizacionOffLimits)) continue;

                            CompanyContratoForAproval contrato = new CompanyContratoForAproval()
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                companyId = reader.GetString(reader.GetOrdinal("companyId")),
                                companyNombre = reader.GetString(reader.GetOrdinal("companyNombre")),
                                duracion = reader.GetInt32(reader.GetOrdinal("duracion")),
                                tarifa = $"{tarifa}€ ({limits.tarifaMin} - {limits.tarifaMax})",
                                tarifaOffLimits = tarifaOffLimits,
                                indemnizacion = indemnizacion == null ? null : $"{indemnizacion}€ ({limits.indemnizacionMin} - {limits.indemnizacionMax})",
                                indemnizacionOffLimits = indemnizacionOffLimits,
                                date = reader.GetDateTime(reader.GetOrdinal("date"))
                            };
                            contratos.Add(contrato);
                        }
                    }
                }

                for (int i = 0; i < contratos.Count; i++)
                {
                    CompanyContratoForAproval contrato = contratos[i];
                    contrato.isReady = checkContratoIsReady(getContrato(contrato.id, conn).Value, conn) == null;
                    contratos[i] = contrato;
                }

                result = new
                {
                    error = false,
                    contratos
                };
            }

            return Ok(result);
        }

        // Clausulas

        [HttpGet]
        [Route(template: "clausulas/{category}")]
        public IActionResult GetClausulas(string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.GetClausulas", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            result = new
            {
                error = false,
                clausulas = parseClausulas(GetSysConfig(null, null, "clausulas-contratos", category) ?? "[]")
            };

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "clausulas/{category}")]
        public async Task<IActionResult> SetClausulas(string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("CompanyContrato.SetClausulas", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
            List<Clausula> clausulas = parseClausulas(await readerJson.ReadToEndAsync());

            SetSysConfig(null, null, "clausulas-contratos", JsonSerializer.Serialize(clausulas), category);

            result = new
            {
                error = false
            };

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        public struct CompanyContrato
        {
            public string id { get; set; }
            public string companyId { get; set; }
            public bool closed { get; set; }
            public bool signed { get; set; }
            public DateTime? signedDate { get; set; }
            public string representanteNombreCompleto { get; set; }
            public string representanteNombre { get; set; }
            public string representanteApellido1 { get; set; }
            public string representanteApellido2 { get; set; }
            public string representanteDni { get; set; }
            public string representanteTelefono { get; set; }
            public string representanteEmail { get; set; }
            public int? duracion { get; set; }
            public string localidad { get; set; }
            public string provincia { get; set; }
            public List<Puesto> puestos { get; set; }
            public double? tarifa { get; set; }
            public double? indemnizacion { get; set; }
            public string tipo { get; set; }
            public List<Clausula> clausulas { get; set; }
            public string tipoId { get; set; }
            public DateTime date { get; set; }
            public string createdBy { get; set; }
            public string categoria { get; set; }
        }

        public struct CompanyContratoForAproval
        {
            public string id { get; set; }
            public string companyId { get; set; }
            public string companyNombre { get; set; }
            public int duracion { get; set; }
            public string tarifa { get; set; }
            public bool tarifaOffLimits { get; set; }
            public string indemnizacion { get; set; }
            public bool indemnizacionOffLimits { get; set; }
            public DateTime date { get; set; }
            public bool isReady { get; set; }
        }

        public struct Clausula
        {
            public string name { get; set; }
            public string text { get; set; }
        }

        public struct Puesto
        {
            public string categoryId { get; set; }
            public string categoryName { get; set; }
            public string categoryDetails { get; set; }
            public bool mandatory { get; set; }
        }
        public struct InsertDescription
        {
            public string description { get; set; }
            public string type { get; set; }
            public InsertDescription(string type, string description)
            {
                this.type = type;
                this.description = description;
            }
        }
        public struct DocxAndPdf
        {
            public string docx { get; set; }
            public string pdf { get; set; }
        }

        public static Dictionary<string, InsertDescription> AVAILABLE_INSERTS = new() {
                        { "dia", new InsertDescription("text", "Día actual con dos dígitos. EJ: 01")},
                        { "mes", new InsertDescription("text", "Mes actual con dos dígitos, empieza en el 1. EJ: 01")},
                        { "mes-letras", new InsertDescription("text", "Nombre completo del mes actual. EJ: Enero")},
                        { "ano", new InsertDescription("text", "Año actual con cuatro dígitos. EJ: 2022")},
                        { "provincia", new InsertDescription("text", "Nombre de la provincia indicada en el contrato")},
                        { "localidad", new InsertDescription("text", "Nombre de la localidad indicada en el contrato")},
                        { "ett-nombre", new InsertDescription("text", "Nombre de la ETT en mayusculas. Depende de la sociedad. EJ: DEA ESTRATEGIAS LABORALES ETT SL")},
                        { "ett-cif", new InsertDescription("text", "CIF de la ETT. Depende de la sociedad. EJ: B00000000")},
                        { "ett-admin-nombre", new InsertDescription("text", "Nombre del administrador la ETT en mayusculas. Depende de la sociedad. EJ: SOFIA VERGARA")},
                        { "ett-admin-dni", new InsertDescription("text", "DNI del administrador la ETT. Depende de la sociedad. 00000000A") },
                        { "admin-nombre", new InsertDescription("text", "Nombre del administrador lal cliente (el firmante de los CPDs).")},
                        { "admin-dni", new InsertDescription("text", "DNI del administrador lal cliente (el firmante de los CPDs).")},
                        { "admin-telefono", new InsertDescription("text", "Teléfono del administrador lal cliente (el firmante de los CPDs).")},
                        { "admin-email", new InsertDescription("text", "Email del administrador lal cliente (el firmante de los CPDs).")},
                        { "cliente-nombre", new InsertDescription("text", "Nombre de la empresa cliente.")},
                        { "cliente-cif", new InsertDescription("text", "CIF de la empresa cliente.")},
                        { "cliente-logo", new InsertDescription("img", "Logo de la empresa cliente.")},
                        { "thinkandjob-logo", new InsertDescription("img", "Logo de ThinkAndJob.")},
                        { "jorge-firma", new InsertDescription("img", "Firma de jorge, disponible cuando se firme el contrato.")},
                        { "cliente-firma", new InsertDescription("img", "Firma del cliente, cuando firme el contrato.")},
                        { "puesto", new InsertDescription("list", "Listado de puestos que aparecen en el contrato.")},
                        { "duracion", new InsertDescription("text", "Duración del contrato en número de meses. EJ: 12")},
                        { "duracion-letras", new InsertDescription("text", "Duración del contrato en número de meses, escrito con letras. EJ: DOCE")},
                        { "tarifa", new InsertDescription("text", "Tarifa del contrato. EJ: 100")},
                        { "tarifa-letras", new InsertDescription("text", "Tarifa de lcontrato, escrita con letras. EJ: CIEN")},
                        { "clausula", new InsertDescription("list", "Listado de clausulas especificadas en el contrato.")},
                        { "comercial-nombre", new InsertDescription("text", "Nombre del usuario que creó el contrato.")},
                        { "comercial-telefono", new InsertDescription("text", "Teléfono del usuario que creó el contrato.")},
                        { "comercial-email", new InsertDescription("text", "Email del usuario que creó el contrato.")},
                        { "contacto-nombre", new InsertDescription("text", "Nombre del RRHH de contacto del cliente.")},
                        { "contacto-telefono", new InsertDescription("text", "Teléfono del RRHH de contacto del cliente.")},
                        { "contacto-email", new InsertDescription("text", "Email del RRHH de contacto del cliente.")}
                }; //Anadir columna tipo de dato y body de la tabla scroleable

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static CompanyContrato? getContrato(string contratoId, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            CompanyContrato? contrato = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CC.*, CTC.nombre as tipo " +
                                      "FROM company_contratos CC " +
                                      "LEFT OUTER JOIN company_tipo_contratos CTC ON(CC.tipoId = CTC.id) " +
                                      "WHERE CC.id = @ID";
                command.Parameters.AddWithValue("@ID", contratoId);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    if (reader.Read())
                    {
                        CompanyContrato tmpContrato = new CompanyContrato()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            tipo = reader.IsDBNull(reader.GetOrdinal("tipo")) ? null : reader.GetString(reader.GetOrdinal("tipo")),
                            tipoId = reader.IsDBNull(reader.GetOrdinal("tipoId")) ? null : reader.GetString(reader.GetOrdinal("tipoId")),
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            signed = reader.GetInt32(reader.GetOrdinal("signed")) == 1,
                            signedDate = reader.IsDBNull(reader.GetOrdinal("signedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("signedDate")),
                            representanteNombre = reader.IsDBNull(reader.GetOrdinal("representanteNombre")) ? null : reader.GetString(reader.GetOrdinal("representanteNombre")),
                            representanteApellido1 = reader.IsDBNull(reader.GetOrdinal("representanteApellido1")) ? null : reader.GetString(reader.GetOrdinal("representanteApellido1")),
                            representanteApellido2 = reader.IsDBNull(reader.GetOrdinal("representanteApellido2")) ? null : reader.GetString(reader.GetOrdinal("representanteApellido2")),
                            representanteDni = reader.IsDBNull(reader.GetOrdinal("representanteDni")) ? null : reader.GetString(reader.GetOrdinal("representanteDni")),
                            representanteTelefono = reader.IsDBNull(reader.GetOrdinal("representanteTelefono")) ? null : reader.GetString(reader.GetOrdinal("representanteTelefono")),
                            representanteEmail = reader.IsDBNull(reader.GetOrdinal("representanteEmail")) ? null : reader.GetString(reader.GetOrdinal("representanteEmail")),
                            duracion = reader.IsDBNull(reader.GetOrdinal("duracion")) ? null : reader.GetInt32(reader.GetOrdinal("duracion")),
                            localidad = reader.IsDBNull(reader.GetOrdinal("localidad")) ? null : reader.GetString(reader.GetOrdinal("localidad")),
                            provincia = reader.IsDBNull(reader.GetOrdinal("provincia")) ? null : reader.GetString(reader.GetOrdinal("provincia")),
                            puestos = reader.IsDBNull(reader.GetOrdinal("puestos")) ? new() : parsePuestos(reader.GetString(reader.GetOrdinal("puestos"))),
                            tarifa = reader.IsDBNull(reader.GetOrdinal("tarifa")) ? null : reader.GetDouble(reader.GetOrdinal("tarifa")),
                            indemnizacion = reader.IsDBNull(reader.GetOrdinal("indemnizacion")) ? null : reader.GetDouble(reader.GetOrdinal("indemnizacion")),
                            clausulas = reader.IsDBNull(reader.GetOrdinal("clausulas")) ? new() : parseClausulas(reader.GetString(reader.GetOrdinal("clausulas"))),
                            date = reader.GetDateTime(reader.GetOrdinal("date")),
                            createdBy = reader.IsDBNull(reader.GetOrdinal("createdBy")) ? null : reader.GetString(reader.GetOrdinal("createdBy")),
                            categoria = reader.IsDBNull(reader.GetOrdinal("categoria")) ? null : reader.GetString(reader.GetOrdinal("categoria"))
                        };
                        tmpContrato.representanteNombreCompleto = $"{tmpContrato.representanteNombre ?? ""} {tmpContrato.representanteApellido1 ?? ""} {tmpContrato.representanteApellido2 ?? ""}".Trim();
                        contrato = tmpContrato;
                    }
                }
            }

            if (lastConn == null) conn.Close();
            return contrato;
        }

        public static List<Puesto> getCurrentCategoriesOfCompany(string companyId, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            string contratoId = null;
            CompanyContrato? contrato = null;
            List<Puesto> puestos = new List<Puesto>();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                        "SELECT TOP 1 id FROM company_contratos " +
                        "WHERE companyId = @COMPANY " + //TODO: Agregar la condicion de que esté cerrado cuando las categorias se creen al cerra el contrato, en lugar de al guardarlo
                        "ORDER BY date DESC";
                command.Parameters.AddWithValue("@COMPANY", companyId);
                contratoId = (string)command.ExecuteScalar();
            }

            if (contratoId != null) contrato = getContrato(contratoId, conn, transaction);
            if (contrato != null) puestos = contrato.Value.puestos;

            if (lastConn == null) conn.Close();
            return puestos;
        }

        public static List<CompanyContrato> listContratos(SqlConnection conn, string companyId)
        {
            List<CompanyContrato> contratos = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText = "SELECT CC.*, CTC.nombre as tipo " +
                                                            "FROM company_contratos CC " +
                                                            "LEFT OUTER JOIN company_tipo_contratos CTC ON(CC.tipoId = CTC.id) " +
                                                            "WHERE CC.companyId = @ID " +
                                                            "ORDER BY date DESC";
                command.Parameters.AddWithValue("@ID", companyId);


                using (SqlDataReader reader = command.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        contratos.Add(new CompanyContrato()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            companyId = reader.GetString(reader.GetOrdinal("companyId")),
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            signed = reader.GetInt32(reader.GetOrdinal("signed")) == 1,
                            date = reader.GetDateTime(reader.GetOrdinal("date"))
                        });
                    }
                }
            }

            return contratos;
        }

        public static void generateContrato(CompanyContrato contrato, SqlConnection conn, SystemUser user, SqlTransaction transaction = null)
        {
            //Obtener la empresa
            ExtendedCompanyData company = new ExtendedCompanyData();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT E.*, G.nombre as companyGroupName FROM empresas E LEFT OUTER JOIN grupos_empresariales G ON(E.grupoId = G.id) WHERE E.id = @COMPANY_ID";
                command.Parameters.AddWithValue("@COMPANY_ID", contrato.companyId);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        company.Read(reader, true, false);
            }

            //Comprobar que la empresa tenga los datos minimos
            if (company.nombre == null) throw new Exception("La empresa no tiene nombre");
            if (company.cif == null) throw new Exception("La empresa no tiene CIF");

            if (company.companyGroupName != null && company.companyGroupName.ToUpper().Contains("PRIMOR"))
            {
                company.icon = null;
            }

            SystemUser creator = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                ContractConstants contractConstants = getContractConstants(contrato.categoria);
                string contactUserId = contractConstants.contactUserId;
                if (contactUserId != null)
                {
                    command.CommandText = "SELECT * FROM users WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", contactUserId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            creator.name = reader.GetString(reader.GetOrdinal("name")).ToUpper();
                            creator.surname = reader.GetString(reader.GetOrdinal("surname")).ToUpper();
                            creator.phone = reader.GetString(reader.GetOrdinal("phone"));
                            creator.email = reader.GetString(reader.GetOrdinal("email"));
                        }
                    }
                }
            }

            //Generar el documento y guardarlo
            DocxAndPdf res = applyContratoTemplate(contrato, company, creator);
            SaveFile(new[] { "companies", contrato.companyId, "contrato", contrato.id, "contrato" }, res.pdf);
            SaveFile(new[] { "companies", contrato.companyId, "contrato", contrato.id, "contratoWord" }, res.docx);
        }

        public static DocxAndPdf applyContratoTemplate(CompanyContrato contrato, ExtendedCompanyData company, UserCredentialsController.SystemUser creator)
        {
            string tmpDir = GetTemporaryDirectory();
            string tmpDoc = Path.Combine(tmpDir, "contrato.docx");
            string tmpPdf = Path.Combine(tmpDir, "contrato.pdf");

            string template = ReadFile(new[] { "contrato_empresa", contrato.tipoId, "template" });
            System.IO.File.WriteAllBytes(tmpDoc, Convert.FromBase64String(template.Split(",")[1]));

            List<Content> contenidos = new();
            //Fecha y lugar
            contenidos.Add(new Content(new FieldContent("dia", DateTime.Now.Day.ToString())));
            contenidos.Add(new Content(new FieldContent("mes", MESES[DateTime.Now.Month - 1])));
            contenidos.Add(new Content(new FieldContent("ano", DateTime.Now.Year.ToString())));
            contenidos.Add(new Content(new FieldContent("provincia", contrato.provincia ?? "")));

            //Datos de la ETT
            contenidos.Add(new Content(new FieldContent("ett_nombre", DEA_NOMBRE)));
            contenidos.Add(new Content(new FieldContent("ett_cif", DEA_CIF)));
            contenidos.Add(new Content(new FieldContent("ett_administrador_nombre", DEA_ADMINISTRADOR_NOMBRE)));
            contenidos.Add(new Content(new FieldContent("ett_administrador_dni", DEA_ADMINISTRADOR_DNI)));

            //Datos del representante
            contenidos.Add(new Content(new FieldContent("representante_nombre", ((company.firmanteNombre ?? "") + " " + (company.firmanteApellido1 ?? "") + " " + (company.firmanteApellido2 ?? "")).Trim())));
            contenidos.Add(new Content(new FieldContent("representante_dni", company.firmanteDni ?? "")));
            //contenidos.Add(new Content(new FieldContent("representante_telefono", contrato.representanteTelefono)));
            //contenidos.Add(new Content(new FieldContent("representante_email", contrato.representanteEmail)));

            //Datos del cliente
            contenidos.Add(new Content(new FieldContent("cliente_nombre", company.nombre ?? "")));
            contenidos.Add(new Content(new FieldContent("cliente_cif", company.cif ?? "")));

            //Puestos
            contenidos.Add(new Content(new FieldContent("puestos", String.Join("\n", contrato.puestos.Select(p => "-- " + p.categoryName).ToList()))));

            //Duracion
            contenidos.Add(new Content(new FieldContent("duracion", contrato.duracion != null ? contrato.duracion.Value.ToString() : "")));
            contenidos.Add(new Content(new FieldContent("duracion_letras", contrato.duracion != null ? DigitSpeller.NumeroALetras(contrato.duracion.Value) : "")));

            //Tarifa e indemnizacion
            contenidos.Add(new Content(new FieldContent("tarifa", $"{contrato.tarifa:0.##}")));
            contenidos.Add(new Content(new FieldContent("tarifa_letras", DigitSpeller.NumeroALetras(contrato.tarifa.Value))));
            if (contrato.indemnizacion != null)
            {
                //contenidos.Add(new Content(new FieldContent("indemnizacion", $"{contrato.indemnizacion.Value:0.##}")));
                //contenidos.Add(new Content(new FieldContent("indemnizacion_letras", DigitSpeller.NumeroALetras(contrato.indemnizacion.Value))));
            }

            //Clausulas
            contenidos.Add(new Content(new FieldContent("clausulas", (contrato.clausulas.Count > 0 ? "Se añaden y describen las siguientes cláusulas:\n\n" : "") + String.Join("\n\n", contrato.clausulas.Select(c => "-- " + c.text).ToList()))));

            //Logo
            if (company.icon != null)
                contenidos.Add(new Content(new ImageContent("logo", Convert.FromBase64String(company.icon != null ? company.icon.Split(",")[1] : ""))));
            else
                contenidos.Add(new Content(new ImageContent("logo", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAAC3npUWHRSYXcgcHJvZmlsZSB0eXBlIGV4aWYAAHja7ZdbkhshDEX/WUWWgCSExHKgaaqygyw/Fxrb43mkKo+PfLhxAy3TkrgHmHE4f3wf4RsuKsIhqXkuOUdcqaTCFR2P13W1FNOqbxft+ske7l1GK2jl+iKf+60Kuz5esLTt7dke7Lg67NvRLfJ2KDMyo9N3ktuR8GWn/RwKX52a30xn322/vCLHj8/JIEZX+INGfApJXDVfkWTeIhWtoiYhDERm6LMYahX/qF/4lYBf6RePbZeHHJej27TyO522nfRz/ZZKbzMivkfmtxmZ3EN80G+M7mOc1+xqygFy5T2p21RWDwMb5JT1WkYx3Iq+rVJQPNZ4gFrHVFuIDQ+FGIoPStSp0qBztQcdSDHxyYaW+WBZNhfjwscCkGahwRakSBcHjwPkBGa+50IrbpnxEMwRuRNGMsEZGD+X8N7wp+XJ0RhzmRNFv2uFvHiuL6Qxyc0aowCExtZUl74Uria+vyZYAUFdMjsmWGO7XDSlx9qSxVmiBgxN8dovZH07gESIrUgG6zpRzCRKmaIxGxF0dPCpyJwlcQMB0qDckSUnkQw4zjM23jFaY1n5MuN4AQiVjE3iAFQBKyVNGfvNsYRqUNGkqllNXYvWLDllzTlbnudUNbFkatnM3IpVF0+unt3cvXgtXATHmIaSixUvpdSKoDVV+KoYX2Fo3KSlpi03a95KqweWz5EOPfJhhx/lqJ27dBwBoedu3Xvp9aQTS+lMp575tNPPctaBtTZkpKEjDxs+yqh3apvqM7X35H5NjTY1XqDmOHtQg9ns5oLmcaKTGYhxIhC3SQALmiez6JQST3KTWSwsQUQZWeqE02kSA8F0EuugO7sHuS+5Baj7u9z4M3JhovsX5MJE94bcR26fUOt1HbeyAM1dCE1xQgq2HwZVdnzw5+TP2hD/0sHL0cvRy9HL0cvRy9HL0X/lSAb+eSj4OfUTtoeSHcsSawcAAAGFaUNDUElDQyBwcm9maWxlAAB4nH2RPUjDQBzFX1tLpVQcLCjqEKQ6WRAVcZQqFsFCaSu06mBy6Rc0aUhSXBwF14KDH4tVBxdnXR1cBUHwA8TVxUnRRUr8X1JoEePBcT/e3XvcvQO8jQpTjK4JQFFNPRWPCdncqhB4hR9B9GMIIyIztER6MQPX8XUPD1/vojzL/dyfo0fOGwzwCMRzTNNN4g3imU1T47xPHGYlUSY+Jx7X6YLEj1yXHH7jXLTZyzPDeiY1TxwmFoodLHUwK+kK8TRxRFZUyvdmHZY5b3FWKjXWuid/YSivrqS5TnMYcSwhgSQESKihjApMRGlVSTGQov2Yi3/Q9ifJJZGrDEaOBVShQLT94H/wu1ujMDXpJIVigP/Fsj5GgcAu0Kxb1vexZTVPAN8zcKW2/dUGMPtJer2tRY6A3m3g4rqtSXvA5Q4w8KSJumhLPpreQgF4P6NvygF9t0BwzemttY/TByBDXS3fAAeHwFiRstdd3t3d2du/Z1r9/QCJEnKwYYBuBQAAAAZiS0dEAEEAEQAR64QjMwAAAAlwSFlzAAAOxAAADsQBlSsOGwAAAAd0SU1FB+cFChcbO2GcecUAAAJbSURBVHja7dQxAQAACMMwwL/nYQAHJBJ6tJOkgJdGAjAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwAMADAAAADAAwADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAwAMAAAAMADAAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMADAAwAAAAwAMALgsHrAF/LXOFRoAAAAASUVORK5CYII="))));

            //Comercial que ha creado el contrato
            contenidos.Add(new Content(new FieldContent("comercial_nombre", $"{creator.name ?? ""} {creator.surname ?? ""}")));
            contenidos.Add(new Content(new FieldContent("comercial_telefono", creator.phone ?? "")));
            contenidos.Add(new Content(new FieldContent("comercial_email", creator.email ?? "")));

            //RRHH de contacto de la empresa
            contenidos.Add(new Content(new FieldContent("contacto_rrhh_nombre", ((company.nombreRRHH ?? "") + " " + (company.apellido1RRHH ?? "") + " " + ((company.apellido2RRHH ?? "") ?? "")).Trim())));
            contenidos.Add(new Content(new FieldContent("contacto_rrhh_telefono", company.telefonoRRHH ?? "")));
            contenidos.Add(new Content(new FieldContent("contacto_rrhh_email", company.emailRRHH ?? "")));

            contenidos.Add(new Content(new FieldContent("contacto_rrhh_emailaaaaa", company.emailRRHH ?? "")));

            try
            {
                using (TemplateProcessor outputDocument = new TemplateProcessor(tmpDoc).SetRemoveContentControls(true))
                {
                    outputDocument.SetNoticeAboutErrors(false);
                    foreach (Content contenido in contenidos)
                        outputDocument.FillContent(contenido);
                    outputDocument.SaveChanges();
                }
            }
            catch (Exception)
            {
                throw new Exception("No se ha podido convertir a pdf");
            }


            string docx = "data:@file/vnd.openxmlformats-officedocument.wordprocessingml.document;base64," + Convert.ToBase64String(System.IO.File.ReadAllBytes(tmpDoc));
            string pdf = null;

            try
            {
                Process p = new Process();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    p.StartInfo.FileName = InstallationConstants.WIN_PYTHON_OSFFICE;
                    p.StartInfo.Arguments = InstallationConstants.WIN_UNICONV + " -f pdf " + "\"" + tmpDoc + "\"";
                }
                else
                {
                    p.StartInfo.FileName = InstallationConstants.UNIX_UNICONV;
                    p.StartInfo.Arguments = " -f pdf " + tmpDoc;
                }

                p.Start();
                p.WaitForExit(60 * 1000);

                pdf = "data:@file/pdf;base64," + Convert.ToBase64String(System.IO.File.ReadAllBytes(tmpPdf));
            }
            catch (Exception)
            {
                throw new Exception("No se ha podido convertir a pdf");
            }
            finally
            {
                Directory.Delete(tmpDir, true);
            }


            return new DocxAndPdf()
            {
                docx = docx,
                pdf = pdf
            };
        }

        public static void signContrato(CompanyContrato contrato, string firmaBase64)
        {
            string pdfBase64 = ReadFile(new[] { "companies", contrato.companyId, "contrato", contrato.id, "contrato" });
            PdfReader reader = new PdfReader(Convert.FromBase64String(pdfBase64.Split(",")[1]));
            using (MemoryStream ms = new MemoryStream())
            {
                PdfStamper stamper = new PdfStamper(reader, ms, '\0', true);

                float x = 350;
                float y = 10;
                float scale = 15;
                Image img = Image.GetInstance(Convert.FromBase64String(firmaBase64.Split(",")[1]));
                img.ScaleToFit(16 * scale, 9 * scale);
                img.SetAbsolutePosition(x, y);
                for (int i = 1; i <= reader.NumberOfPages; i++)
                    stamper.GetOverContent(i).AddImage(img);

                x = 50;
                y = 10;
                scale = 15;
                img = Image.GetInstance(Convert.FromBase64String(JORGE_FIRMA));
                img.ScaleToFit(16 * scale, 9 * scale);
                img.SetAbsolutePosition(x, y);
                for (int i = 1; i <= reader.NumberOfPages; i++)
                    stamper.GetOverContent(i).AddImage(img);

                stamper.Close();

                byte[] signedPdf = ms.ToArray();
                string signedPdfBase64 = "data:@file/pdf;base64," + Convert.ToBase64String(signedPdf);
                SaveFile(new[] { "companies", contrato.companyId, "contrato", contrato.id, "firmado" }, signedPdfBase64);
            }
        }
        public static List<Puesto> parsePuestos(JsonElement puestosJson)
        {
            List<Puesto> puestos = new();

            try
            {
                foreach (JsonElement puestoJson in puestosJson.EnumerateArray())
                {
                    if (puestoJson.TryGetProperty("categoryId", out JsonElement categoryIdJson) &&
                            puestoJson.TryGetProperty("categoryName", out JsonElement categoryNameJson) &&
                            puestoJson.TryGetProperty("categoryDetails", out JsonElement categoryDetailsJson))
                    {
                        puestos.Add(new Puesto()
                        {
                            categoryId = categoryIdJson.GetString(),
                            categoryName = categoryNameJson.GetString(),
                            categoryDetails = categoryDetailsJson.GetString()
                        });
                    }
                }
            }
            catch (Exception) { }

            return puestos;
        }
        public static List<Clausula> parseClausulas(JsonElement clausulasJson)
        {
            List<Clausula> clausulas = new();

            try
            {
                foreach (JsonElement clausulaJson in clausulasJson.EnumerateArray())
                {
                    if (clausulaJson.TryGetProperty("name", out JsonElement nameJson) && clausulaJson.TryGetProperty("text", out JsonElement textJson))
                    {
                        clausulas.Add(new Clausula()
                        {
                            name = nameJson.GetString(),
                            text = textJson.GetString()
                        });
                    }
                }
            }
            catch (Exception) { }

            return clausulas;
        }
        public static List<Puesto> parsePuestos(string puestosJsonString)
        {
            return parsePuestos(JsonDocument.Parse(puestosJsonString).RootElement);
        }
        public static List<Clausula> parseClausulas(string clausulasJsonString)
        {
            return parseClausulas(JsonDocument.Parse(clausulasJsonString).RootElement);
        }
        public static string checkContratoIsReady(CompanyContrato contrato, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Comprobar que tenga todos los datos y la empresa tambien
            if (contrato.tarifa == null ||
                    contrato.clausulas == null ||
                    contrato.puestos == null ||
                    contrato.duracion == null ||
                    contrato.provincia == null ||
                    contrato.localidad == null ||
                    contrato.tipoId == null ||
                    contrato.categoria == null
                    // || 
                    //String.IsNullOrWhiteSpace(contrato.representanteDni) ||
                    //String.IsNullOrWhiteSpace(contrato.representanteEmail) ||
                    //String.IsNullOrWhiteSpace(contrato.representanteTelefono) ||
                    //String.IsNullOrWhiteSpace(contrato.representanteNombre) ||
                    //String.IsNullOrWhiteSpace(contrato.representanteApellido1)
                    )
            {
                return "Error 4004, faltan datos esenciales en el contrato";
            }

            //Comprobar que tiene creador
            if (contrato.createdBy == null)
            {
                return "Error 4004, el contrato no tiene un comercial asociado";
            }

            //Comprobar que tiene puestos
            if (contrato.puestos.Count == 0)
            {
                return "Error 4004, el contrato no tiene puestos de trabajo";
            }

            //TODO: Comprobar que los puestos tienen las condiciones rellenas

            //Comprobar que tenga todos los datos y la empresa tambien
            ExtendedCompanyData company = CompanyController.getCompany(conn, transaction, contrato.companyId);
            if (String.IsNullOrWhiteSpace(company.cif) ||
                    String.IsNullOrWhiteSpace(company.nombre) ||
                    String.IsNullOrWhiteSpace(company.direccion) ||
                    String.IsNullOrWhiteSpace(company.cp) ||
                    String.IsNullOrWhiteSpace(company.nombreRRHH) ||
                    String.IsNullOrWhiteSpace(company.apellido1RRHH) ||
                    String.IsNullOrWhiteSpace(company.telefonoRRHH) ||
                    String.IsNullOrWhiteSpace(company.emailRRHH) ||
                    company.diaCobro == null ||
                    String.IsNullOrWhiteSpace(company.formaDePago) ||
                    String.IsNullOrWhiteSpace(company.indemnizacion) ||
                    String.IsNullOrWhiteSpace(company.firmanteNombre) ||
                    String.IsNullOrWhiteSpace(company.firmanteApellido1) ||
                    String.IsNullOrWhiteSpace(company.firmanteDni) ||
                    String.IsNullOrWhiteSpace(company.firmanteCargo) ||
                    String.IsNullOrWhiteSpace(company.firmanteEmail) ||
                    String.IsNullOrWhiteSpace(company.firmanteTelefono)
            )
            {
                return "Error 4005, faltan datos esenciales en la empresa";
            }

            //Comprobar que tiene al menos un centro
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM centros WHERE companyId = @ID";
                command.Parameters.AddWithValue("@ID", contrato.companyId);
                if ((int)command.ExecuteScalar() == 0)
                {
                    return "Error 4006, la empresa no tiene centros de trabajo";
                }
            }

            return null;
        }
              
        public static List<Puesto> createCategoriesIfNeeded(SqlConnection conn, SqlTransaction transaction, List<Puesto> puestos)
        {
            for (int i = 0; i < puestos.Count; i++)
            {
                Puesto puesto = puestos[i];

                //Crear la categoria si esta no existe
                if (puesto.categoryId == null)
                {
                    string id = ComputeStringHash(puesto.categoryName + puesto.categoryDetails + DateTime.Now);
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText = "INSERT INTO categories (id, name, details, isNew) VALUES (@ID, @NAME, @DETAILS, 1)";
                        command.Parameters.AddWithValue("@ID", id);
                        command.Parameters.AddWithValue("@NAME", puesto.categoryName);
                        command.Parameters.AddWithValue("@DETAILS", puesto.categoryDetails ?? "");
                        command.ExecuteNonQuery();
                    }
                    puesto.categoryId = id;
                }

                puestos[i] = puesto;
            }

            return puestos;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
