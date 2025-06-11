using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Controllers._Model.Client;
using ThinkAndJobSolution.Controllers.MainHome.RRHH;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/company")]
    [ApiController]
    [Authorize]
    public class CompanyController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        //Listado

        [HttpGet]
        [Route(template: "list/{withIcon?}")]
        public IActionResult List(bool withIcon = false)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                var empresas = new List<CompanyData>();

                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT E.*, G.nombre as companyGroupName " +
                                          "FROM empresas E LEFT OUTER JOIN grupos_empresariales G ON(E.grupoId = G.id)" +
                                          "ORDER BY CAST(E.nombre as Varchar(50)) ASC";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            CompanyData company = new CompanyData();
                            company.Read(reader, withIcon, true);
                            empresas.Add(company);
                        }
                    }
                }

                foreach (CompanyData empresa in empresas)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id, alias FROM centros WHERE companyId = @COMPANY_ID";

                        command.Parameters.AddWithValue("@COMPANY_ID", empresa.id);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                empresa.ReadCentro(reader);
                            }
                        }
                    }

                    foreach (CentroData centro in empresa.centros)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT T.*, C.name, C.details, C.id as categoryId, companyId = @COMPANY_ID FROM trabajos T INNER JOIN categories C ON(T.categoryId = C.id) WHERE centroId = @CENTRO_ID";

                            command.Parameters.AddWithValue("@CENTRO_ID", centro.id);
                            command.Parameters.AddWithValue("@COMPANY_ID", empresa.id);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    centro.ReadWork(reader);
                                }
                            }
                        }
                    }
                }

                result = new { error = false, companies = empresas };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "fast-list/")]
        public IActionResult FastList()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            ResultadoAcceso access = HasPermission("Company.FastList", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                var empresas = new List<CompanyData>();

                conn.Open();

                string filterSecurityToken = GuardiasController.getSecurityTokenFilter(securityToken, access.EsJefe, conn);

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (filterSecurityToken == null)
                        command.CommandText = "SELECT * FROM empresas ORDER BY CAST(nombre as Varchar(50)) ASC";
                    else
                    { //Mostrar solo las empresas de las que es responsable, si no es jefe o esta de guardia
                        command.CommandText = "SELECT E.* " +
                                              "FROM empresas E " +
                                              "WHERE EXISTS(SELECT * FROM asociacion_usuario_empresa AUE INNER JOIN users U ON(AUE.userId = U.id) WHERE U.securityToken = @TOKEN AND AUE.companyId = E.id) " +
                                              "ORDER BY CAST(E.nombre as Varchar(50)) ASC";
                        command.Parameters.AddWithValue("@TOKEN", filterSecurityToken);
                    }

                    using (SqlDataReader reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            CompanyData company = new CompanyData();
                            company.Read(reader, false, false);
                            empresas.Add(company);
                        }
                    }
                }

                result = empresas;
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

            ResultadoAcceso access = HasPermission("Company.ListForComercial", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }


            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                var empresas = new List<CompanyData>();

                conn.Open();

                string dni = null, id = null;
                if (!access.EsJefe)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT DocID, id FROM users WHERE securityToken = @TOKEN";
                        command.Parameters.AddWithValue("@TOKEN", securityToken);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dni = reader.GetString(reader.GetOrdinal("DocID"));
                                id = reader.GetString(reader.GetOrdinal("id"));
                            }
                        }
                    }
                }

                id = GuardiasController.getUserIdFilter(id, access.EsJefe, conn);

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT E.*, G.nombre as companyGroupName, R.username as responsable, " +
                        "estadoContrato = COALESCE((SELECT TOP 1 CASE WHEN CC.signed = 1 AND CC.closed = 1 THEN 3 WHEN CC.closed = 1 AND CC.signed = 0 THEN 2 ELSE 1 END FROM company_contratos CC WHERE CC.companyId = E.id ORDER BY CC.date DESC), 0) " +
                        "FROM empresas E LEFT OUTER JOIN grupos_empresariales G ON(E.grupoId = G.id) " +
                        "LEFT OUTER JOIN users R ON(E.creador = R.DocID) " +
                        "WHERE (@DNI IS NULL OR E.creador = @DNI) OR " +
                        "(@ID IS NULL OR EXISTS(SELECT * FROM asociacion_usuario_empresa AUE WHERE AUE.companyId = E.id AND AUE.userId = @ID))" +
                        "ORDER BY CAST(E.nombre as Varchar(50)) ASC";
                    command.Parameters.AddWithValue("@DNI", (object)dni ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ID", (object)id ?? DBNull.Value);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            CompanyData company = new CompanyData();
                            company.Read(reader, true, true);
                            company.ReadEstadoContrato(reader);
                            company.comercialResponsable = (reader.IsDBNull(reader.GetOrdinal("responsable")) || !access.EsJefe) ? null : reader.GetString(reader.GetOrdinal("responsable"));
                            empresas.Add(company);
                        }
                    }
                }

                result = new { error = false, companies = empresas };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-for-centro-select/")]
        public IActionResult ListForCentroSelect()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.ListForCentroSelect", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                var empresas = new List<CompanyData>();

                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT E.id, E.nombre " +
                                          "FROM empresas E " +
                                          "ORDER BY CAST(E.nombre as Varchar(50)) ASC";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            empresas.Add(new CompanyData()
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                nombre = reader.GetString(reader.GetOrdinal("nombre"))
                            });
                        }
                    }
                }

                foreach (CompanyData empresa in empresas)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT CE.id, CE.alias FROM centros CE WHERE CE.companyId = @COMPANY_ID";

                        command.Parameters.AddWithValue("@COMPANY_ID", empresa.id);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                empresa.centros.Add(new CentroData()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    alias = reader.GetString(reader.GetOrdinal("alias"))
                                });
                            }
                        }
                    }
                }

                result = new { error = false, companies = empresas };
            }

            return Ok(result);
        }

        //Obtencion

        [HttpGet]
        [Route(template: "{companyId}")]
        public IActionResult GetCompany(string companyId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            bool failed = false;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();


                ExtendedCompanyData company = getCompany(conn, null, companyId);
                if (company == null)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 4802, cliente no encontrado"
                    };
                }

                if (!failed)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id, alias FROM centros WHERE companyId = @COMPANY_ID";

                        command.Parameters.AddWithValue("@COMPANY_ID", companyId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                company.ReadCentro(reader);
                            }
                        }
                    }

                    foreach (CentroData centro in company.centros)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT T.*, C.name, C.details, C.id as categoryId, companyId = @COMPANY_ID FROM trabajos T INNER JOIN categories C ON(T.categoryId = C.id) WHERE centroId = @CENTRO_ID";

                            command.Parameters.AddWithValue("@CENTRO_ID", centro.id);
                            command.Parameters.AddWithValue("@COMPANY_ID", companyId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    centro.ReadWork(reader);
                                }
                            }
                        }
                    }
                }

                if (!failed)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT C.id, C.alias  " +
                                              "FROM centros C LEFT OUTER JOIN servicios_prevencion S ON(C.servicioPrevencion = S.id) WHERE C.companyId = @COMPANY_ID";

                        command.Parameters.AddWithValue("@COMPANY_ID", companyId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                company.ReadCentro(reader);
                            }
                        }
                    }

                    company.CalculateAdminUserShouldBeCreated(conn);

                    result = new
                    {
                        error = false,
                        company
                    };
                }
            }

            return Ok(result);
        }

        //Creacion

        [HttpPost]
        [Route(template: "create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.Create", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await reader.ReadToEndAsync();

                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("nombre", out JsonElement companyNameObj) &&
                    json.TryGetProperty("cif", out JsonElement companyCifObj))
                {

                    CompanyData companyData = new CompanyData
                    {
                        id = ComputeStringHash(companyNameObj.GetString() + companyCifObj.GetString()),
                        nombre = companyNameObj.GetString(),
                        cif = companyCifObj.GetString()
                    };

                    if (companyData.nombre.Length < 3) return Ok(new { error = "Error 4803, El nombre debe tener al menos 3 caracteres" });
                    if (companyData.cif.Length < 3) return Ok(new { error = "Error 4803, El cif no es válido" });

                    string dniUsedBy = CheckDNINIECIFunique(companyData.cif, null);
                    if (dniUsedBy != null)
                    {
                        result = new
                        {
                            error = $"Error 4001, el cif ya esta siendo usado por {dniUsedBy}."
                        };
                    }
                    else
                    {
                        using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                        {
                            conn.Open();
                            bool failed = false;

                            //obtener el DNI del usuario que crea la empresa
                            string creador = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT DocID FROM users WHERE securityToken = @TOKEN";
                                command.Parameters.AddWithValue("@TOKEN", securityToken);
                                creador = (string)command.ExecuteScalar();
                            }

                            //Insertar la empresa
                            if (!failed)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "INSERT INTO empresas(id, nombre, cif, creador) VALUES (@ID, @NOMBRE, @CIF, @CREADOR)";

                                    command.Parameters.AddWithValue("@ID", companyData.id);
                                    command.Parameters.AddWithValue("@NOMBRE", companyData.nombre);
                                    command.Parameters.AddWithValue("@CIF", companyData.cif);
                                    command.Parameters.AddWithValue("@CREADOR", (object)creador ?? DBNull.Value);

                                    if (command.ExecuteNonQuery() == 0)
                                    {
                                        failed = true;
                                        result = new
                                        {
                                            error = "Error 5491, no se ha podido insertar la empresa"
                                        };
                                    }
                                }
                            }

                            //Crear su primer centro
                            /*
                            if (!failed)
                            {
                                string alias = companyData.nombre + " Principal";
                                string centroId = ComputeStringHash(alias + DateTime.Now);
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText =
                                        "INSERT INTO centros (id, companyId, sociedad, alias) VALUES (@ID, @COMPANY, @SOCIEDAD, @ALIAS)";

                                    command.Parameters.AddWithValue("@ID", centroId);
                                    command.Parameters.AddWithValue("@COMPANY", companyData.id);
                                    command.Parameters.AddWithValue("@SOCIEDAD", "DEA ESTRATEGIAS LABORALES ETT, S.L.");
                                    command.Parameters.AddWithValue("@ALIAS", alias);

                                    command.ExecuteNonQuery();
                                }
                            }
                            */

                            if (!failed)
                            {

                                result = new
                                {
                                    error = false,
                                    companyData.id
                                };

                                LogToDB(LogType.COMPANY_CREATED, $"Empresa {companyData.nombre} creada", FindUsernameBySecurityToken(securityToken, conn), conn);
                            }
                        }
                    }
                }
            }

            return Ok(result);
        }

        //Eliminacion

        [HttpPost]
        [Route(template: "delete/{companyId}/")]
        public IActionResult DeleteCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.DeleteCompany", securityToken).Acceso)
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
                    bool failed = false;

                    //Obtener su nombre para poder registrar el evento
                    string nombreEmpresa = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "SELECT nombre FROM empresas WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", companyId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            try
                            {
                                if (reader.Read())
                                {
                                    nombreEmpresa = reader.GetString(reader.GetOrdinal("nombre"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new { error = "4401, empresa no encontrada" };
                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "5401, no se ha podido comprobar si la empresa existe" };
                            }
                        }
                    }

                    //Eliminar sus notas
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM company_notes WHERE companyId = @ID";
                                command.Parameters.AddWithValue("@ID", companyId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5730, no se ha podido eliminar las notas del cliente"
                                };
                            }
                        }
                    }

                    //Eliminar sus contratos
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM company_contratos WHERE companyId = @ID";
                                command.Parameters.AddWithValue("@ID", companyId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5730, no se ha podido eliminar los contratos del cliente"
                                };
                            }
                        }
                    }

                    //Eliminar sus asignaciones con usuarios
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM asociacion_usuario_empresa WHERE companyId = @ID";
                                command.Parameters.AddWithValue("@ID", companyId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5730, no se ha podido eliminar las asociaciones con usuarios"
                                };
                            }
                        }
                    }

                    //Eliminar sus asignaciones con usuarios
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM user_avisos WHERE companyId = @ID";
                                command.Parameters.AddWithValue("@ID", companyId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5730, no se ha podido eliminar las asociaciones con avisos de usuarios"
                                };
                            }
                        }
                    }

                    //Eliminar sus avisos
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM avisos WHERE companyId = @ID";
                                command.Parameters.AddWithValue("@ID", companyId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5730, no se ha podido eliminar los avisos de la empresa"
                                };
                            }
                        }
                    }

                    //Eliminar centros de la empresa
                    List<string> centros = new();
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT id FROM centros WHERE companyId = @ID";
                                command.Parameters.AddWithValue("@ID", companyId);
                                using (SqlDataReader reader = command.ExecuteReader()) while (reader.Read()) centros.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5730, no se ha podido eliminar los centros"
                                };
                            }
                        }
                    }
                    foreach (string centroId in centros)
                    {
                        if (failed) continue;
                        UpdateResult updateResult = CentroController.deleteCentro(centroId, conn, transaction, securityToken);
                        failed = updateResult.failed;
                        result = updateResult.result;
                    }

                    //Eliminar la empresa
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM empresas WHERE id LIKE @ID";
                                command.Parameters.AddWithValue("@ID", companyId);
                                command.ExecuteNonQuery();
                                DeleteDir(new[] { "companies", companyId });
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5730, no se ha podido eliminar la empresa"
                                };
                            }
                        }
                    }

                    if (failed)
                    {
                        transaction.Rollback();
                    }
                    else
                    {
                        LogToDB(LogType.COMPANY_DELETED, $"Empresa {nombreEmpresa} eliminada", FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);

                        transaction.Commit();

                        result = new
                        {
                            error = false
                        };
                    }
                }
            }

            return Ok(result);
        }

        //Actualizacion

        [HttpPut]
        [Route(template: "update/{companyId}/")]
        public async Task<IActionResult> UpdateCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            ResultadoAcceso access = HasPermission("Company.UpdateCompany", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader jsonReader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await jsonReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("cif", out JsonElement cifJson) &&
                json.TryGetProperty("nombre", out JsonElement nombreJson) &&
                json.TryGetProperty("cp", out JsonElement cpJson) &&
                json.TryGetProperty("direccion", out JsonElement direccionJson) &&
                json.TryGetProperty("nombreRRHH", out JsonElement nombreRRHHJson) &&
                json.TryGetProperty("apellido1RRHH", out JsonElement apellido1RRHHJson) &&
                json.TryGetProperty("apellido2RRHH", out JsonElement apellido2RRHHJson) &&
                json.TryGetProperty("telefonoRRHH", out JsonElement telefonoRRHHJson) &&
                json.TryGetProperty("emailRRHH", out JsonElement emailRRHHJson) &&
                json.TryGetProperty("web", out JsonElement webJson) &&
                json.TryGetProperty("formaDePago", out JsonElement formaDePagoJson) &&
                json.TryGetProperty("diaCobro", out JsonElement diaCobroJson) &&
                json.TryGetProperty("cuentaBancaria", out JsonElement cuentaBancariaJson) &&
                json.TryGetProperty("cuentaContable", out JsonElement cuentaContableJson) &&
                json.TryGetProperty("tva", out JsonElement tvaJson) &&
                json.TryGetProperty("vies", out JsonElement viesJson) &&
                json.TryGetProperty("companyGroupId", out JsonElement companyGroupIdJson) &&
                json.TryGetProperty("icon", out JsonElement iconJson) &&
                json.TryGetProperty("fotoCif", out JsonElement fotoCifJson) &&
                json.TryGetProperty("fotoDniAdministradorAnverso", out JsonElement fotoDniAdministradorAnversoJson) &&
                json.TryGetProperty("fotoDniAdministradorReverso", out JsonElement fotoDniAdministradorReversoJson) &&
                json.TryGetProperty("indemnizacion", out JsonElement indemnizacionJson) &&
                json.TryGetProperty("creador", out JsonElement creadorJson) &&
                json.TryGetProperty("firmanteNombre", out JsonElement firmanteNombreJson) &&
                json.TryGetProperty("firmanteApellido1", out JsonElement firmanteApellido1Json) &&
                json.TryGetProperty("firmanteApellido2", out JsonElement firmanteApellido2Json) &&
                json.TryGetProperty("firmanteDni", out JsonElement firmanteDniJson) &&
                json.TryGetProperty("firmanteCargo", out JsonElement firmanteCargoJson) &&
                json.TryGetProperty("firmanteEmail", out JsonElement firmanteEmailJson) &&
                json.TryGetProperty("firmanteTelefono", out JsonElement firmanteTelefonoJson) &&
                json.TryGetProperty("test", out JsonElement testJson))
            {
                ExtendedCompanyData company = new ExtendedCompanyData()
                {
                    id = companyId,
                    cif = cifJson.GetString(),
                    cp = cpJson.GetString(),
                    direccion = direccionJson.GetString(),
                    nombre = nombreJson.GetString(),
                    nombreRRHH = nombreRRHHJson.GetString(),
                    apellido1RRHH = apellido1RRHHJson.GetString(),
                    apellido2RRHH = apellido2RRHHJson.GetString(),
                    telefonoRRHH = telefonoRRHHJson.GetString(),
                    emailRRHH = emailRRHHJson.GetString(),
                    web = webJson.GetString(),
                    formaDePago = formaDePagoJson.GetString(),
                    diaCobro = GetJsonInt(diaCobroJson),
                    cuentaBancaria = cuentaBancariaJson.GetString(),
                    cuentaContable = cuentaContableJson.GetString(),
                    tva = tvaJson.GetString(),
                    vies = GetJsonBool(viesJson),
                    companyGroupId = companyGroupIdJson.GetString(),
                    indemnizacion = indemnizacionJson.GetString(),
                    creador = creadorJson.GetString(),
                    icon = iconJson.GetString(),
                    fotoCif = fotoCifJson.GetString(),
                    fotoDniAdministradorAnverso = fotoDniAdministradorAnversoJson.GetString(),
                    fotoDniAdministradorReverso = fotoDniAdministradorReversoJson.GetString(),
                    firmanteNombre = firmanteNombreJson.GetString(),
                    firmanteApellido1 = firmanteApellido1Json.GetString(),
                    firmanteApellido2 = firmanteApellido2Json.GetString(),
                    firmanteDni = firmanteDniJson.GetString(),
                    firmanteCargo = firmanteCargoJson.GetString(),
                    firmanteEmail = firmanteEmailJson.GetString(),
                    firmanteTelefono = firmanteTelefonoJson.GetString(),
                    test = GetJsonBool(testJson)
                };

                if (company.nombre != null && company.nombre.Length < 3) return Ok(new { error = "Error 4803, El nombre debe tener al menos 3 caracteres" });
                if (company.cif != null && company.cif.Length < 3) return Ok(new { error = "Error 4803, El cif no es válido" });

                if (company.creador != null && !access.EsJefe)
                {
                    return Ok(new
                    {
                        error = "Error 1005, no puedes cambiar el responsable de una empresa."
                    });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        bool failed = false;

                        //Comprobar si existe
                        string nombreEmpresa = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;

                            command.CommandText = "SELECT nombre FROM empresas WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", companyId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    nombreEmpresa = reader.GetString(reader.GetOrdinal("nombre"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 4002, empresa no encontrada"
                                    };
                                }
                            }
                        }

                        if (!failed)
                        {
                            UpdateResult updateResult = updateCompany(conn, transaction, company);
                            failed = updateResult.failed;
                            result = updateResult.result;
                        }

                        //Comprobar si deberia crear un usuario CL administrador
                        bool adminUserShouldBeCreated = false;
                        if (!failed)
                        {
                            ExtendedCompanyData updatedCompany = getCompany(conn, transaction, companyId);
                            updatedCompany.CalculateAdminUserShouldBeCreated(conn, transaction);
                            adminUserShouldBeCreated = updatedCompany.adminUserShouldBeCreated;
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            LogToDB(LogType.COMPANY_UPDATED, $"Empresa {nombreEmpresa} actualizada", FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);

                            transaction.Commit();

                            if ("B2B".Equals(company.formaDePago))
                            {
                                //TODO: Enviar un correo a la empresa
                            }

                            result = new { error = false, adminUserShouldBeCreated };
                        }
                    }
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "update-for-client/{companyId}/")]
        public async Task<IActionResult> UpdateCompanyForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_EMPRESAS) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader jsonReader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await jsonReader.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("nombreRRHH", out JsonElement nombreRRHHJson) &&
                json.TryGetProperty("apellido1RRHH", out JsonElement apellido1RRHHJson) &&
                json.TryGetProperty("apellido2RRHH", out JsonElement apellido2RRHHJson) &&
                json.TryGetProperty("telefonoRRHH", out JsonElement telefonoRRHHJson) &&
                json.TryGetProperty("emailRRHH", out JsonElement emailRRHHJson) &&
                json.TryGetProperty("web", out JsonElement webJson) &&
                json.TryGetProperty("fotoCif", out JsonElement fotoCifJson) &&
                json.TryGetProperty("fotoDniAdministradorAnverso", out JsonElement fotoDniAdministradorAnversoJson) &&
                json.TryGetProperty("fotoDniAdministradorReverso", out JsonElement fotoDniAdministradorReversoJson))
            {
                ExtendedCompanyData company = new ExtendedCompanyData()
                {
                    id = companyId,
                    cif = null,
                    cp = null,
                    direccion = null,
                    nombre = null,
                    nombreRRHH = nombreRRHHJson.GetString(),
                    apellido1RRHH = apellido1RRHHJson.GetString(),
                    apellido2RRHH = apellido2RRHHJson.GetString(),
                    telefonoRRHH = telefonoRRHHJson.GetString(),
                    emailRRHH = emailRRHHJson.GetString(),
                    web = webJson.GetString(),
                    formaDePago = null,
                    diaCobro = null,
                    cuentaBancaria = null,
                    cuentaContable = null,
                    tva = null,
                    vies = null,
                    icon = null,
                    fotoCif = fotoCifJson.GetString(),
                    fotoDniAdministradorAnverso = fotoDniAdministradorAnversoJson.GetString(),
                    fotoDniAdministradorReverso = fotoDniAdministradorReversoJson.GetString()
                };

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        bool failed = false;

                        //Comprobar si existe
                        string nombreEmpresa = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;

                            command.CommandText = "SELECT nombre FROM empresas WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", companyId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    nombreEmpresa = reader.GetString(reader.GetOrdinal("nombre"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 4002, empresa no encontrada"
                                    };
                                }
                            }
                        }

                        if (!failed)
                        {
                            UpdateResult updateResult = updateCompany(conn, transaction, company);
                            failed = updateResult.failed;
                            result = updateResult.result;
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            LogToDB(LogType.COMPANY_UPDATED_BY_CLIENT, $"Empresa {nombreEmpresa} actualizada por cliente {FindUserClientEmailByClientToken(clientToken, conn, transaction)}", null, conn, transaction);

                            transaction.Commit();

                            result = new { error = false };
                        }
                    }
                }
            }

            return Ok(result);
        }

        //Acciones

        [HttpGet]
        [Route(template: "download-attachment/{companyId}")]
        public IActionResult DownloadAttachment(string companyId, string attachment)
        {
            attachment = Uri.UnescapeDataString(attachment);
            object result;

            switch (attachment)
            {
                case "icon":
                    result = ReadFile(new[] { "companies", companyId, "icon" });
                    break;
                case "foto-cif":
                    result = ReadFile(new[] { "companies", companyId, "foto_cif" });
                    break;
                case "foto-dni-administrador-anverso":
                    result = ReadFile(new[] { "companies", companyId, "foto_dni_administrador" });
                    break;
                case "foto-dni-administrador-reverso":
                    result = ReadFile(new[] { "companies", companyId, "foto_dni_administrador_reverso" });
                    break;
                default:
                    result = null;
                    break;
            }

            return Ok(result);
        }

        //Icono

        [HttpGet]
        [Route(template: "{companyId}/icon/")]
        public IActionResult GetIcon(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.GetIcon", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            result = new
            {
                data = ReadFile(new[] { "companies", companyId, "icon" })
            };

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "icon-by-siglink/{signLink}")]
        public IActionResult GetIconBySignlink(string signLink)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT E.id FROM empresas E INNER JOIN centros C ON(E.id = C.companyId) INNER JOIN trabajos T ON(C.id = T.centroId) WHERE T.signLink LIKE @SIGNLINK";
                    command.Parameters.AddWithValue("@SIGNLINK", signLink);

                    string id = (string)command.ExecuteScalar();
                    result = new
                    {
                        data = id == null ? null : ReadFile(new[] { "companies", id, "icon" })
                    };
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "{companyId}/icon/")]
        public async Task<IActionResult> SetIcon(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.SetIcon", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader bodyReader = new StreamReader(Request.Body);
            string base64 = await bodyReader.ReadToEndAsync();

            try
            {
                base64 = LimitSquareImage(base64);
                SaveFile(new[] { "companies", companyId, "icon" }, base64);
                result = new
                {
                    data = base64
                };
            }
            catch (Exception)
            {
                result = new
                {
                    error = "Error 5026, La imagne no pudo ser procesada."
                };
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{companyId}/icon/")]
        public async Task<IActionResult> RemoveIcon(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.RemoveIcon", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            DeleteFile(new[] { "companies", companyId, "icon" });
            result = new { error = false };

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "{companyId}/icon-for-client/")]
        public IActionResult GetIconForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, companyId, null, null) == null)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            result = new
            {
                data = ReadFile(new[] { "companies", companyId, "icon" })
            };

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "{companyId}/icon-for-client/")]
        public async Task<IActionResult> SetIconForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_EMPRESAS) == null)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader bodyReader = new StreamReader(Request.Body);
            string base64 = await bodyReader.ReadToEndAsync();

            try
            {
                base64 = LimitSquareImage(base64);
                SaveFile(new[] { "companies", companyId, "icon" }, base64);
                result = new
                {
                    data = base64
                };
            }
            catch (Exception)
            {
                result = new
                {
                    error = "Error 5026, La imagne no pudo ser procesada."
                };
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{companyId}/icon-for-client/")]
        public async Task<IActionResult> RemoveIconForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_EMPRESAS) == null)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            DeleteFile(new[] { "companies", companyId, "icon" });
            result = new { error = false };

            return Ok(result);
        }

        //Notas de cliente

        [HttpPost]
        [Route(template: "{companyId}/note/{centroId?}")]
        public async Task<IActionResult> CreateNote(string companyId, string centroId = null)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.CreateNote", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("text", out JsonElement textJson))
                {
                    string text = textJson.GetString().Trim();

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        try
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;

                                command.CommandText =
                                    "INSERT INTO company_notes (companyId, centroId, username, text) " +
                                    "VALUES (@COMPANY_ID, @CENTRO_ID, @USERNAME, @TEXT)";

                                command.Parameters.AddWithValue("@COMPANY_ID", companyId);
                                command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);
                                command.Parameters.AddWithValue("@USERNAME", FindUsernameBySecurityToken(securityToken, conn));
                                command.Parameters.AddWithValue("@TEXT", text);

                                command.ExecuteNonQuery();

                                result = new { error = false };
                            }
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5089, problema creando nota." };
                        }

                    }
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "{companyId}/note/{noteId}/{centroId?}")]
        public async Task<IActionResult> EditNote(string companyId, string noteId, string centroId = null)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            ResultadoAcceso access = HasPermission("Company.EditNote", securityToken);
            if (!access.Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("text", out JsonElement textJson))
                {
                    string text = textJson.GetString().Trim();

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        try
                        {
                            //Si no es jefe, obtener el autor de la nota y comprobar que sea él
                            if (!access.EsJefe)
                            {
                                string username = FindUsernameBySecurityToken(securityToken, conn);
                                string makerUsername = null;
                                using (SqlCommand command = conn.CreateCommand())
                                {

                                    command.CommandText = "SELECT username FROM company_notes WHERE companyId = @COMPANY_ID AND id = @ID";
                                    command.Parameters.AddWithValue("@COMPANY_ID", companyId);
                                    command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);
                                    command.Parameters.AddWithValue("@ID", noteId);
                                    makerUsername = command.ExecuteScalar() as string;
                                }
                                if (makerUsername != username)
                                    return Ok(new { error = "Error 4090, no puedes editar una nota que no es tuya." });
                            }

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "UPDATE company_notes SET text = @TEXT, editdate = getdate() WHERE companyId = @COMPANY_ID AND id = @ID";

                                command.Parameters.AddWithValue("@COMPANY_ID", companyId);
                                command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);
                                command.Parameters.AddWithValue("@ID", noteId);
                                command.Parameters.AddWithValue("@TEXT", text);

                                command.ExecuteNonQuery();

                                result = new { error = false };
                            }
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5093, problema editando nota." };
                        }

                    }
                }
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{companyId}/note/{noteId}/")]
        public IActionResult DeleteNote(string companyId, string noteId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("Company.DeleteNote", securityToken).Acceso)
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

                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;

                            command.CommandText =
                                "DELETE FROM company_notes WHERE id = @NOTE_ID AND companyId = @COMPANY_ID";

                            command.Parameters.AddWithValue("@NOTE_ID", noteId);
                            command.Parameters.AddWithValue("@COMPANY_ID", companyId);

                            command.ExecuteNonQuery();

                            result = new { error = false };
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5090, problema eliminando nota." };
                    }

                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "{companyId}/note/{centroId?}")]
        public IActionResult ListNotes(string companyId, string centroId = "none")
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            ResultadoAcceso acceso = HasPermission("Company.ListNotes", securityToken);
            if (!acceso.Acceso)
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

                    try
                    {
                        if (centroId != null && centroId.ToLower().Equals("null")) centroId = null;
                        string username = FindUsernameBySecurityToken(securityToken, conn);
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT N.id, N.username, N.text, N.date, N.editdate, C.alias FROM company_notes N LEFT JOIN centros C ON(N.centroId = C.id) WHERE N.companyId = @COMPANY_ID AND (@CENTRO_ID IS NULL OR centroId = @CENTRO_ID OR (@CENTRO_ID = 'none' AND centroId IS NULL)) ORDER BY [date] DESC";

                            command.Parameters.AddWithValue("@COMPANY_ID", companyId);
                            command.Parameters.AddWithValue("@CENTRO_ID", (object)centroId ?? DBNull.Value);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                List<ClientNote> notes = new List<ClientNote>();

                                while (reader.Read())
                                {
                                    notes.Add(new ClientNote
                                    {
                                        id = reader.GetInt32(reader.GetOrdinal("id")),
                                        username = reader.GetString(reader.GetOrdinal("username")),
                                        text = reader.GetString(reader.GetOrdinal("text")),
                                        date = reader.GetDateTime(reader.GetOrdinal("date")),
                                        editdate = reader.IsDBNull(reader.GetOrdinal("editdate")) ? null : reader.GetDateTime(reader.GetOrdinal("editdate")),
                                        centro = reader.IsDBNull(reader.GetOrdinal("alias")) ? null : reader.GetString(reader.GetOrdinal("alias")),
                                        canEdit = acceso.EsJefe || username == reader.GetString(reader.GetOrdinal("username"))
                                    });
                                }

                                result = new { error = false, notes };
                            }
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5090, problema obteniendo notas." };
                    }

                }
            }

            return Ok(result);
        }

        //Archivos

        [HttpGet]
        [Route(template: "{companyId}/archive/")]
        public IActionResult ListArchives(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            if (!HasPermission("Company.ListArchives", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                string[] fileNames = ListFiles(new[] { "companies", companyId, "archive" });
                List<CompanyFile> files = new();
                foreach (string fileName in fileNames)
                {
                    files.Add(new CompanyFile()
                    {
                        name = Path.GetFileName(fileName),
                        size = new FileInfo(fileName).Length,
                        date = System.IO.File.GetLastWriteTime(fileName)
                    });
                }
                files.Sort((p, q) => p.date > q.date ? -1 : 1);
                result = new { error = false, files };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "{companyId}/archive/{fileName}/")]
        public IActionResult DownloadArchive(string companyId, string fileName)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            if (!HasPermission("Company.DownloadArchive", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                fileName = Encoding.UTF8.GetString(Convert.FromBase64String(fileName));
                result = new { error = false, file = ReadFile(new[] { "companies", companyId, "archive", fileName }) };
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{companyId}/archive/{fileName}/")]
        public IActionResult DeleteArchive(string companyId, string fileName)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            if (!HasPermission("Company.DeleteArchive", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                fileName = Encoding.UTF8.GetString(Convert.FromBase64String(fileName));
                DeleteFile(new[] { "companies", companyId, "archive", fileName });
                result = new { error = false };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "{companyId}/archive/{fileName}/")]
        public async Task<IActionResult> UploadArchive(string companyId, string fileName)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result;
            if (!HasPermission("Company.UploadArchive", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                fileName = Encoding.UTF8.GetString(Convert.FromBase64String(fileName));
                SaveFile(new[] { "companies", companyId, "archive", fileName }, data);
                result = new { error = false };
            }

            return Ok(result);
        }



        //------------------------------WorkController Inicio------------------------------
        [HttpGet]
        [Route(template: "work/forms/list/{workId}/")]
        public IActionResult ListWorkLinkedForms(string workId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Company.ListWorkLinkedForms", securityToken).Acceso)
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
                                "SELECT F.id as formId, F.nombre as formName, F.tipo as testType, " +
                                "CASE WHEN EXISTS(" +
                                "SELECT VTF.id FROM vinculos_trabajos_formularios VTF WHERE VTF.formularioId = F.id AND VTF.trabajoId = @WORK" +
                                ") THEN 'TRUE' ELSE 'FALSE' END AS checked, " +
                                "CASE WHEN EXISTS(" +
                                "SELECT VCF.id FROM vinculos_categorias_formularios VCF " +
                                "INNER JOIN categories CA ON(VCF.categoryId = CA.id) " +
                                "INNER JOIN trabajos T ON(CA.id = T.categoryId) " +
                                "WHERE VCF.formularioId = F.id AND T.id = @WORK" +
                                ") THEN 'TRUE' ELSE 'FALSE' END AS byCategory " +
                                "FROM formularios as F ORDER BY byCategory DESC, checked DESC";
                        command.Parameters.AddWithValue("@WORK", workId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {

                            List<object> linkedForms = new List<object>();
                            while (reader.Read())
                            {
                                linkedForms.Add(new
                                {
                                    formId = reader.GetString(reader.GetOrdinal("formId")),
                                    formName = reader.GetString(reader.GetOrdinal("formName")),
                                    type = reader.GetString(reader.GetOrdinal("testType")),
                                    check = reader.GetString(reader.GetOrdinal("checked")).Equals("TRUE") || reader.GetString(reader.GetOrdinal("byCategory")).Equals("TRUE"),
                                    byCategory = reader.GetString(reader.GetOrdinal("byCategory")).Equals("TRUE"),
                                });
                            }
                            result = linkedForms;
                        }
                    }
                }
            }
            return Ok(result);
        }


        [HttpGet]
        [Route(template: "work/forms/create/{workId}/{formId}/")]
        public IActionResult CreateWorkLinkedForm(string workId, string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Company.CreateWorkLinkedForm", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO vinculos_trabajos_formularios(id, trabajoId, formularioId) VALUES (@ID, @WORKID, @FORMID)";
                            command.Parameters.AddWithValue("@ID", ComputeStringHash(workId + formId));
                            command.Parameters.AddWithValue("@WORKID", workId);
                            command.Parameters.AddWithValue("@FORMID", formId);
                            command.ExecuteNonQuery();
                            result = new { error = false };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new
                    {
                        error = "Error 5421, no se ha podido asignar el formulario. Inténtelo más tarde."
                    };
                }
            }
            return Ok(result);
        }


        [HttpDelete]
        [Route(template: "work/forms/delete/{workId}/{formId}/")]
        public IActionResult DeleteWorkLinkedForm(string workId, string formId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Company.DeleteWorkLinkedForm", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM vinculos_trabajos_formularios WHERE trabajoId LIKE @WORKID AND formularioId LIKE @FORMID";
                            command.Parameters.AddWithValue("@WORKID", workId);
                            command.Parameters.AddWithValue("@FORMID", formId);
                            command.ExecuteNonQuery();
                            result = new { error = false };
                        }
                    }
                }
                catch (Exception)
                {
                    result = new
                    {
                        error = "Error 5421, no se ha podido desasignar el formulario. Inténtelo más tarde."
                    };
                }
            }
            return Ok(result);
        }
        //------------------------------WorkController Fin---------------------------------


        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "CLASES"

        //Ayuda
        public struct ClientNote
        {
            public int id { get; set; }
            public string username { get; set; }
            public string text { get; set; }
            public DateTime date { get; set; }
            public DateTime? editdate { get; set; }
            public string centro { get; set; }
            public bool canEdit { get; set; }
        }

        public struct CompanyFile
        {
            public string name { get; set; }
            public long size { get; set; }
            public DateTime date { get; set; }
        }



        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------


        public static UpdateResult updateCompany(SqlConnection conn, SqlTransaction transaction, ExtendedCompanyData data)
        {
            UpdateResult result = new UpdateResult
            {
                failed = false,
                result = new { error = false }
            };

            try
            {
                //Actualizar cif
                try
                {
                    if (data.cif != null)
                    {
                        string dniUsedBy = CheckDNINIECIFunique(data.cif, data.id, conn, transaction);
                        if (dniUsedBy == null)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE empresas SET cif = @VALUE WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", data.id);
                                command.Parameters.AddWithValue("@VALUE", data.cif);
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            result.failed = true;
                            result.result = new { error = $"Error 4910, el CIF {data.cif} está en uso por {dniUsedBy}." };
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el CIF." };
                }

                //Actualizar cp
                try
                {
                    if (data.cp != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET cp = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.cp);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el CP." };
                }

                //Actualizar direccion
                try
                {
                    if (data.direccion != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET direccion = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.direccion);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la dirección." };
                }

                //Actualizar nombre
                try
                {
                    if (data.nombre != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET nombre = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.nombre);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el nombre." };
                }

                //Actualizar nombreRRHH
                try
                {
                    if (data.nombreRRHH != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET nombreRRHH = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.nombreRRHH);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el nombre del RRHH." };
                }

                //Actualizar apellido1RRHH
                try
                {
                    if (data.apellido1RRHH != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET apellido1RRHH = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.apellido1RRHH);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el primer apellido del RRHH." };
                }

                //Actualizar apellido2RRHH
                try
                {
                    if (data.apellido2RRHH != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET apellido2RRHH = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.apellido2RRHH);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el segundo appelido del RRHH." };
                }

                //Actualizar telefonoRRHH
                try
                {
                    if (data.telefonoRRHH != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET telefonoRRHH = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.telefonoRRHH);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el telefono del RRHH." };
                }

                //Actualizar emailRRHH
                try
                {
                    if (data.emailRRHH != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET emailRRHH = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.emailRRHH);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el email del RRHH." };
                }

                //Actualizar web
                try
                {
                    if (data.web != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET web = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.web);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la web." };
                }

                //Actualizar forma de pago
                try
                {
                    if (data.formaDePago != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET formaDePago = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.formaDePago);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la forma de pago." };
                }

                //Actualizar dia de cobro
                try
                {
                    if (data.diaCobro != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET diaCobro = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.diaCobro);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el dia de cobro." };
                }

                //Actualizar cuenta bancaria
                try
                {
                    if (data.cuentaBancaria != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET cuentaBancaria = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.cuentaBancaria);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la cuenta bancaria." };
                }

                //Actualizar cuenta contable
                try
                {
                    if (data.cuentaContable != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET cuentaContable = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.cuentaContable);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la cuenta contable." };
                }

                //Actualizar tva
                try
                {
                    if (data.tva != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET tva = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.tva);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el TVA." };
                }

                //Actualizar vies
                try
                {
                    if (data.vies != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET vies = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.vies.Value ? 1 : 0);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el VIES." };
                }

                //Actualizar grupo empresarial
                try
                {
                    if (data.companyGroupId != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET grupoId = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.companyGroupId.Length == 7 ? DBNull.Value : data.companyGroupId);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el grupo empresarial." };
                }

                //Actualizar indemnización
                try
                {
                    if (data.indemnizacion != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET indemnizacion = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.indemnizacion);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la indemnizacion." };
                }

                //Actualizar creador
                try
                {
                    if (data.creador != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET creador = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.creador);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el creador." };
                }

                //Actualizar nombre del firmate
                try
                {
                    if (data.firmanteNombre != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET firmanteNombre = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.firmanteNombre);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el nombre de administrador." };
                }

                //Actualizar apellido 1 del firmate
                try
                {
                    if (data.firmanteApellido1 != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET firmanteApellido1 = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.firmanteApellido1);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el primer apellido del administrador." };
                }

                //Actualizar apellido 2 de; firmate
                try
                {
                    if (data.firmanteApellido2 != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET firmanteApellido2 = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.firmanteApellido2);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el segundo apellido del administrador." };
                }

                //Actualizar dni del firmate
                try
                {
                    if (data.firmanteDni != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET firmanteDni = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.firmanteDni);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el DNI del administrador." };
                }

                //Actualizar cargo del firmate
                try
                {
                    if (data.firmanteCargo != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET firmanteCargo = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.firmanteCargo);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el cargo del administrador." };
                }

                //Actualizar email del firmate
                try
                {
                    if (data.firmanteEmail != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET firmanteEmail = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.firmanteEmail);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el email del administrador." };
                }

                //Actualizar telefono del firmate
                try
                {
                    if (data.firmanteTelefono != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET firmanteTelefono = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.firmanteTelefono);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el telefono del administrador." };
                }

                //Actualizar test
                try
                {
                    if (data.test != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE empresas SET test = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.test.Value ? 1 : 0);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el marcador de pruebas." };
                }

                //Actualizar icon
                try
                {
                    if (data.icon != null)
                    {
                        data.icon = LimitSquareImage(data.icon);
                        DeleteFile(new[] { "companies", data.id, "icon" });
                        if (data.icon.Length != 7) SaveFile(new[] { "companies", data.id, "icon" }, data.icon);
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el icono." };
                }

                //Actualizar fotoCif
                try
                {
                    if (data.fotoCif != null)
                    {
                        //data.fotoCif = limitImageSize(data.fotoCif);
                        DeleteFile(new[] { "companies", data.id, "foto_cif" });
                        if (data.fotoCif.Length != 7) SaveFile(new[] { "companies", data.id, "foto_cif" }, data.fotoCif);
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la foto del cif." };
                }

                //Actualizar fotoDniAdministrador Anverso
                try
                {
                    if (data.fotoDniAdministradorAnverso != null)
                    {
                        //data.fotoDniAdministrador = limitImageSize(data.fotoDniAdministradorAnverso);
                        DeleteFile(new[] { "companies", data.id, "foto_dni_administrador" });
                        if (data.fotoDniAdministradorAnverso.Length != 7) SaveFile(new[] { "companies", data.id, "foto_dni_administrador" }, data.fotoDniAdministradorAnverso);
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la foto del dni del administrador." };
                }

                //Actualizar fotoDniAdministrador Reverso
                try
                {
                    if (data.fotoDniAdministradorReverso != null)
                    {
                        //data.fotoDniAdministrador = limitImageSize(data.fotoDniAdministradorReverso);
                        DeleteFile(new[] { "companies", data.id, "foto_dni_administrador_reverso" });
                        if (data.fotoDniAdministradorReverso.Length != 7) SaveFile(new[] { "companies", data.id, "foto_dni_administrador_reverso" }, data.fotoDniAdministradorReverso);
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la foto del dni del administrador." };
                }

            }
            catch (Exception)
            {
                result.failed = true;
                result.result = new { error = "Error 5804, no se han podido actualizar los datos" };
            }

            return result;
        }
        public static ExtendedCompanyData getCompany(SqlConnection conn, SqlTransaction transaction, string companyId)
        {
            ExtendedCompanyData company;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT E.*, G.nombre as companyGroupName " +
                                      "FROM empresas E LEFT OUTER JOIN grupos_empresariales G ON(E.grupoId = G.id) WHERE E.id = @COMPANY_ID";
                command.Parameters.AddWithValue("@COMPANY_ID", companyId);

                using (SqlDataReader reader = command.ExecuteReader())
                {

                    if (reader.Read())
                    {
                        company = new ExtendedCompanyData();
                        company.Read(reader);
                    }
                    else
                    {
                        company = null;
                    }
                }
            }
            return company;
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
