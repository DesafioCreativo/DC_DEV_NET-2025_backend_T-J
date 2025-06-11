using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;
using ThinkAndJobSolution.Controllers._Model.Client;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.CompanyContratoController;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Utils;
using ThinkAndJobSolution.Controllers.Candidate;
using static ThinkAndJobSolution.Controllers._Helper.Constants;
using ThinkAndJobSolution.Controllers._Helper;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/centro")]
    [ApiController]
    [Authorize]
    public class CentroController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        //------------------------------securityToken------------------------------

        [HttpPost]
        [Route(template: "create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Centros.Create", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("companyId", out JsonElement companyIdJson) &&
                json.TryGetProperty("alias", out JsonElement aliasJson) &&
                json.TryGetProperty("requiresPhoto", out JsonElement requiresPhotoJson) &&
                json.TryGetProperty("requiresLocation", out JsonElement requiresLocationJson) &&
                json.TryGetProperty("copyFromCompany", out JsonElement copyFromCompanyJson) &&
                json.TryGetProperty("poblacion", out JsonElement poblacionJson) &&
                json.TryGetProperty("provincia", out JsonElement provinciaJson))
            {

                string companyId = companyIdJson.GetString();
                string alias = aliasJson.GetString();
                bool requiresPhoto = GetJsonBool(requiresPhotoJson) ?? false;
                bool requiresLocation = GetJsonBool(requiresLocationJson) ?? false;
                bool copyFromCompany = GetJsonBool(copyFromCompanyJson) ?? false;
                string poblacion = poblacionJson.GetString();
                string provincia = provinciaJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        //Comprobar que el alias no exista
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT id FROM centros WHERE alias = @ALIAS";
                            command.Parameters.AddWithValue("@ALIAS", alias);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    return Ok(new { error = "Error 4892, ya existe un centro con el mismo alias" });
                                }
                            }
                        }


                        string centroId = ComputeStringHash(alias + DateTime.Now);
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "INSERT INTO centros (id, companyId, sociedad, alias, workshiftRequiereFoto, workshiftRequiereUbicacion) VALUES (@ID, @COMPANY, @SOCIEDAD, @ALIAS, @REQUIRES_PHOTO, @REQUIRES_LOCATION)";

                            command.Parameters.AddWithValue("@ID", centroId);
                            command.Parameters.AddWithValue("@COMPANY", companyId);
                            command.Parameters.AddWithValue("@SOCIEDAD", "DEA ESTRATEGIAS LABORALES ETT, S.L.");
                            command.Parameters.AddWithValue("@ALIAS", alias);
                            command.Parameters.AddWithValue("@REQUIRES_PHOTO", requiresPhoto ? 1 : 0);
                            command.Parameters.AddWithValue("@REQUIRES_LOCATION", requiresLocation ? 1 : 0);

                            command.ExecuteNonQuery();
                        }

                        if (copyFromCompany)
                        {
                            try
                            {
                                ExtendedCompanyData company = CompanyController.getCompany(conn, null, companyId);
                                updateCentro(conn, null, new ExtendedCentroData()
                                {
                                    id = centroId,
                                    poblacion = poblacion,
                                    provincia = provincia,
                                    cp = company.cp,
                                    domicilio = company.direccion,
                                    contactoNombre = company.nombreRRHH,
                                    contactoApellido1 = company.apellido1RRHH,
                                    contactoApellido2 = company.apellido2RRHH,
                                    telefono = company.telefonoRRHH,
                                    email = company.emailRRHH,
                                    ccc = company.cuentaContable
                                });
                            }
                            catch (Exception) { }
                        }

                        //Darle persmisos sobre este centro al usuario CL administrador de la empresa, si exsite
                        //Agregarle los trabajos de cada categoria del contrato vigente de la empresa
                        try
                        {
                            addPermissionToAdminCLWhenCentroIsCreated(conn, null, companyId, centroId);
                            addWorksToCenterFromLastClosedContrato(conn, null, companyId, centroId);
                        }
                        catch (Exception) { }

                        result = new
                        {
                            error = false
                        };

                        LogToDB(LogType.CENTRO_CREATED, $"Centro {FindCentroFullName(centroId)} creado", FindUsernameBySecurityToken(securityToken, conn), conn);
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5892, no se ha podido crear el centro" };
                    }

                }
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{centroId}/")]
        public IActionResult Delete(string centroId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Centros.Delete", securityToken).Acceso)
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
                        result = deleteCentro(centroId, conn, transaction, securityToken).result;
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5894, no se ha podido eliminar el centro" };
                    }
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "fast-list/{companyId}/")]
        public IActionResult FastList(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Centros.FastList", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<CentroData> centros = new();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT C.id, C.alias " +
                                              "FROM centros C " +
                                              "WHERE @COMPANY = C.companyId " +
                                              "ORDER BY C.alias ASC";
                        command.Parameters.AddWithValue("@COMPANY", companyId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                centros.Add(new CentroData()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    alias = reader.GetString(reader.GetOrdinal("alias"))
                                });
                            }
                        }
                    }

                    result = centros;
                }
                catch (Exception)
                {
                    result = new { error = "Error 5791, no han podido listar los centros" };
                }

            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "{centroId}")]
        public IActionResult Get(string centroId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT C.id, C.sociedad, C.alias, C.regimen, C.domicilio, C.cp, C.poblacion, C.provincia, C.contactoNombre, C.contactoApellido1, C.contactoApellido2, C.telefono, C.email, C.fechaAlta, C.servicioPrevencion, S.nombre as servicioPrevencionNombre, C.convenio, C.ccc, C.cnae, C.workshiftRequiereFoto, C.workshiftRequiereUbicacion, C.referenciaExterna " +
                                              "FROM centros C LEFT OUTER JOIN servicios_prevencion S ON(C.servicioPrevencion = S.id) " +
                                              "WHERE C.id = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ExtendedCentroData extendedCentro = new();
                                extendedCentro.Read(reader);
                                result = new { error = false, centro = extendedCentro };
                            }
                            else
                            {
                                result = new { error = "Error 4891, centro no encontrado" };
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener el centro" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-festivos/{ano}/{centroId}")]
        public IActionResult GetFestivos(int ano, string centroId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    bool found = false;
                    string provincia = null, localidad = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT C.poblacion, C.provincia " +
                                              "FROM centros C " +
                                              "WHERE C.id = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                provincia = reader.GetString(reader.GetOrdinal("provincia"));
                                localidad = reader.GetString(reader.GetOrdinal("poblacion"));
                                found = true;
                            }
                            else
                            {
                                result = new { error = "Error 4891, centro no encontrado" };
                            }
                        }
                    }

                    if (found)
                    {
                        FestivoType[] festivos = getFestivos(ano, provincia, localidad, conn);
                        FestivoType[][] meses = new FestivoType[12][];

                        for (int i = 0; i < 12; i++)
                        {
                            int first = new DateTime(ano, i + 1, 1).DayOfYear;
                            int last = new DateTime(ano, i + 1, 1).AddMonths(1).AddSeconds(-1).DayOfYear;
                            meses[i] = festivos.Skip(first - 1).Take(last - first + 1).ToArray();
                        }

                        result = new { error = false, festivos = meses };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5709, no han podido obtener los festivos del centro" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list/{companyId?}")]
        public IActionResult List(string companyId = null)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Centros.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new
                    {
                        error = false,
                        centros = listCentros(conn, null, companyId)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5791, no han podido listar los centros" };
                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-by-candidate/{candidateId}/")]
        public IActionResult ListByCandidate(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Centros.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string companyId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT CE.companyId FROM candidatos C INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) INNER JOIN centros CE ON(T.centroId = CE.id) WHERE C.id = @ID";
                        command.Parameters.AddWithValue("@ID", candidateId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                companyId = reader.GetString(reader.GetOrdinal("companyId"));
                    }

                    if (companyId == null)
                    {
                        result = new
                        {
                            error = "Error 4105, el candidato no se puede asociar con ninguna empresa"
                        };
                    }
                    else
                    {
                        result = new
                        {
                            error = false,
                            centros = listCentros(conn, null, companyId)
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5791, no han podido listar los centros" };
                }

            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "api/v1/centro/update/{centroId}/")]
        public async Task<IActionResult> Update(string centroId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            ResultadoAcceso access = HasPermission("Centros.Update", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await reader.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("sociedad", out JsonElement sociedadJson) &&
                json.TryGetProperty("alias", out JsonElement aliasJson) &&
                json.TryGetProperty("regimen", out JsonElement regimenJson) &&
                json.TryGetProperty("domicilio", out JsonElement domicilioJson) &&
                json.TryGetProperty("cp", out JsonElement cpJson) &&
                json.TryGetProperty("poblacion", out JsonElement poblacionJson) &&
                json.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                json.TryGetProperty("contactoNombre", out JsonElement contactoNombreJson) &&
                json.TryGetProperty("contactoApellido1", out JsonElement contactoApellido1Json) &&
                json.TryGetProperty("contactoApellido2", out JsonElement contactoApellido2Json) &&
                json.TryGetProperty("telefono", out JsonElement telefonoJson) &&
                json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("servicioPrevencion", out JsonElement servicioPrevencionJson) &&
                json.TryGetProperty("convenio", out JsonElement convenioJson) &&
                json.TryGetProperty("ccc", out JsonElement cccJson) &&
                json.TryGetProperty("cnae", out JsonElement cnaeJson) &&
                json.TryGetProperty("workshiftRequiereFoto", out JsonElement workshiftRequiereFotoJson) &&
                json.TryGetProperty("workshiftRequiereUbicacion", out JsonElement workshiftRequiereUbicacionJson) &&
                json.TryGetProperty("referenciaExterna", out JsonElement referenciaExternaJson))
            {
                ExtendedCentroData extendedCentro = new()
                {
                    id = centroId,
                    sociedad = sociedadJson.GetString(),
                    alias = aliasJson.GetString(),
                    regimen = regimenJson.GetString(),
                    domicilio = domicilioJson.GetString(),
                    cp = cpJson.GetString(),
                    poblacion = poblacionJson.GetString(),
                    provincia = provinciaJson.GetString(),
                    contactoNombre = contactoNombreJson.GetString(),
                    contactoApellido1 = contactoApellido1Json.GetString(),
                    contactoApellido2 = contactoApellido2Json.GetString(),
                    telefono = telefonoJson.GetString(),
                    email = emailJson.GetString(),
                    servicioPrevencion = GetJsonInt(servicioPrevencionJson),
                    convenio = convenioJson.GetString(),
                    ccc = cccJson.GetString(),
                    cnae = cnaeJson.GetString(),
                    workshiftRequiereFoto = GetJsonBool(workshiftRequiereFotoJson),
                    workshiftRequiereUbicacion = GetJsonBool(workshiftRequiereUbicacionJson),
                    referenciaExterna = referenciaExternaJson.GetString()
                };

                if (extendedCentro.alias != null && !access.EsJefe)
                {
                    return Ok(new
                    {
                        error = "Error 4001, No puede actualizar el alias del centro."
                    });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        bool failed = false;

                        //Comprobar si existe
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;

                            command.CommandText = "SELECT COUNT(*) FROM centros WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", centroId);

                            if ((int)command.ExecuteScalar() == 0)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 4002, centro no encontrado"
                                };
                            }
                        }

                        if (!failed)
                        {
                            UpdateResult updateResult = updateCentro(conn, transaction, extendedCentro);
                            failed = updateResult.failed;
                            result = updateResult.result;
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            LogToDB(LogType.CENTRO_UPDATED, $"Centro {FindCentroFullName(centroId, conn, transaction)} actualizado", FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);

                            transaction.Commit();
                        }
                    }
                }
            }

            return Ok(result);
        }


        [HttpPut]
        [Route(template: "update-for-client/{centroId}/")]
        public async Task<IActionResult> UpdateForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_CENTROS) == null)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await reader.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("contactoNombre", out JsonElement contactoNombreJson) &&
                json.TryGetProperty("contactoApellido1", out JsonElement contactoApellido1Json) &&
                json.TryGetProperty("contactoApellido2", out JsonElement contactoApellido2Json) &&
                json.TryGetProperty("telefono", out JsonElement telefonoJson) &&
                json.TryGetProperty("email", out JsonElement emailJson))
            {
                ExtendedCentroData extendedCentro = new()
                {
                    id = centroId,
                    sociedad = null,
                    alias = null,
                    regimen = null,
                    domicilio = null,
                    cp = null,
                    poblacion = null,
                    provincia = null,
                    contactoNombre = contactoNombreJson.GetString(),
                    contactoApellido1 = contactoApellido1Json.GetString(),
                    contactoApellido2 = contactoApellido2Json.GetString(),
                    telefono = telefonoJson.GetString(),
                    email = emailJson.GetString(),
                    servicioPrevencion = null,
                    convenio = null,
                    ccc = null,
                    cnae = null
                };

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        bool failed = false;

                        //Comprobar si existe
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;

                            command.CommandText = "SELECT COUNT(*) FROM centros WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", centroId);

                            if ((int)command.ExecuteScalar() == 0)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 4002, centro no encontrado"
                                };
                            }
                        }

                        if (!failed)
                        {
                            UpdateResult updateResult = updateCentro(conn, transaction, extendedCentro);
                            failed = updateResult.failed;
                            result = updateResult.result;
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            LogToDB(LogType.CENTRO_UPDATED_BY_CLIENT, $"Centro {FindCentroFullName(centroId, conn, transaction)} eliminado por {FindUserClientEmailByClientToken(clientToken, conn, transaction)}", null, conn, transaction);

                            transaction.Commit();
                        }
                    }
                }
            }

            return Ok(result);
        }

      

        //------------------------------WorkController Inicio------------------------------
        [HttpGet]
        [Route(template: "get/work/{workId}/")]
        public IActionResult GetWork(string workId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Centros.GetWork", securityToken).Acceso)
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
                        command.CommandText = "SELECT T.*, CA.name, CA.details, CA.id as categoryId, C.companyId FROM trabajos T  INNER JOIN categories CA ON(T.categoryId = CA.id) INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @WORKID";
                        command.Parameters.AddWithValue("@WORKID", workId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            reader.Read();
                            WorkData workData = new WorkData();
                            workData.Read(reader);
                            int numDoc = HelperMethods.CountFiles(new[] { "companies", workData.companyId, "centro", workData.centroId, "work", workData.id });
                            if (numDoc < 0) numDoc = 0;
                            workData.Read(reader, numDoc);
                            result = new { error = false, work = workData };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "work/list/{centroId}/")]
        public IActionResult ListWorksByCentro(string centroId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Centros.ListWorksByCompany", securityToken).Acceso)
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
                        command.CommandText = "SELECT T.*, CA.name, CA.details, CA.id as categoryId, C.companyId FROM trabajos T INNER JOIN categories CA ON(T.categoryId = CA.id) INNER JOIN centros C ON(T.centroId = C.id) WHERE C.id = @CENTRO_ID";
                        command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            List<WorkData> trabajos = new List<WorkData>();
                            while (reader.Read())
                            {
                                WorkData workData = new WorkData();
                                workData.Read(reader);
                                trabajos.Add(workData);
                            }
                            result = new { error = false, works = trabajos };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "work/list-for-client/{centroId}/")]
        public IActionResult ListWorksByCentroForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (ClientHasPermission(clientToken, null, centroId, null) == null)
            {
                result = new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT T.*, CA.name, CA.details, CA.id as categoryId, C.companyId FROM trabajos T INNER JOIN categories CA ON(T.categoryId = CA.id) INNER JOIN centros C ON(T.centroId = C.id) WHERE C.id = @CENTRO_ID";

                        command.Parameters.AddWithValue("@CENTRO_ID", centroId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {

                            List<WorkData> trabajos = new List<WorkData>();


                            while (reader.Read())
                            {
                                WorkData workData = new WorkData();
                                workData.Read(reader);
                                int numDoc = HelperMethods.CountFiles(new[] { "companies", workData.companyId, "centro", workData.centroId, "work", workData.id });
                                if (numDoc < 0) numDoc = 0;
                                workData.Read(reader, numDoc);
                                trabajos.Add(workData);
                            }

                            result = new { error = false, works = trabajos };
                        }
                    }
                }
            }

            return Ok(result);
        }

        // Creacion
        [HttpPost]
        [Route(template: "work/add/")]
        public async Task<IActionResult> AddWork()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Centros.AddWork", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await readerJson.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("categoryId", out JsonElement categoryIdJson) &&
                        json.TryGetProperty("centroId", out JsonElement centroIdJson))
                {
                    string categoryId = categoryIdJson.GetString();
                    string centroId = centroIdJson.GetString();
                    string id = ComputeStringHash(categoryId + centroId + DateTime.Now);
                    string signLink = id.Substring(0, 10);

                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        //Comprobar que el centro no tenga ya un trabajo de esa categoria
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT COUNT(*) FROM trabajos WHERE categoryId = @CATEGORY_ID AND centroId = @CENTRO_ID";
                            command.Parameters.AddWithValue("@CATEGORY_ID", categoryId);
                            command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                            try
                            {
                                if ((int)command.ExecuteScalar() > 0)
                                {
                                    return Ok(new { error = "Error 4492, el centro ya tiene un puesto con esa categoria" });
                                }
                            }
                            catch (Exception)
                            {
                                return Ok(new { error = "Error 5492, no se ha podido determinar si el centro ya tiene un puesto con esa categoria" });
                            }
                        }
                        //Crear el trabajo
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO trabajos (id, categoryId, centroId, signLink) VALUES " +
                            "(@ID, @CATEGORY_ID, @CENTRO_ID, @SIGN_LINK)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@CATEGORY_ID", categoryId);
                            command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                            command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                            try
                            {
                                if (command.ExecuteNonQuery() > 0)
                                {
                                    result = new
                                    {
                                        error = false
                                    };
                                }
                                else
                                {
                                    result = new
                                    {
                                        error = "Error 5491, no se ha podido actualizar la información"
                                    };
                                }
                            }
                            catch (Exception)
                            {
                                result = new
                                {
                                    error = "Error 5491, no se ha podido agregar el puesto"
                                };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        // Eliminacion
        [HttpPost]
        [Route(template: "work/delete/{workId}/")]
        public IActionResult DeleteWork(string workId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Centros.DeleteWork", securityToken).Acceso)
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

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        bool failed = deleteWorkInTransaction(conn, transaction, workId);
                        if (failed)
                        {
                            transaction.Rollback();
                            result = new
                            {
                                error = "Error 5433, No se ha podido eliminar el trabajo"
                            };
                        }
                        else
                        {
                            transaction.Commit();

                            result = new
                            {
                                error = false
                            };

                            LogToDB(LogType.DELETION, "Trabajo " + FindWorkNameById(workId, conn) + " eliminado", FindUsernameBySecurityToken(securityToken, conn), conn);
                        }
                    }
                }
            }
            return Ok(result);
        }




        //------------------------------WorkController Fin---------------------------------

        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "CLASES"
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static UpdateResult updateCentro(SqlConnection conn, SqlTransaction transaction, ExtendedCentroData data)
        {
            UpdateResult result = new UpdateResult
            {
                failed = false,
                result = new { error = false }
            };

            try
            {
                //Actualizar sociedad
                try
                {
                    if (data.sociedad != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET sociedad = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.sociedad);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la sociedad." };
                }

                //Actualizar alias
                try
                {
                    if (data.alias != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET alias = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.alias);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el alias." };
                }

                //Actualizar regimen
                try
                {
                    if (data.regimen != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET regimen = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.regimen);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el regimen." };
                }

                //Actualizar domicilio
                try
                {
                    if (data.domicilio != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET domicilio = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.domicilio);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el domicilio." };
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
                            command.CommandText = "UPDATE centros SET cp = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.cp);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el cp." };
                }

                //Actualizar poblacion
                try
                {
                    if (data.poblacion != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET poblacion = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.poblacion);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la poblacion." };
                }

                //Actualizar provincia
                try
                {
                    if (data.provincia != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET provincia = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.provincia);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la provincia." };
                }

                //Actualizar contacto nombre
                try
                {
                    if (data.contactoNombre != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET contactoNombre = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.contactoNombre);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el nombre de la persona de contacto." };
                }

                //Actualizar contacto apellido 1
                try
                {
                    if (data.contactoApellido1 != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET contactoApellido1 = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.contactoApellido1);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el primer apellido de la persona de contacto." };
                }

                //Actualizar contacto apellido 2
                try
                {
                    if (data.contactoApellido2 != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET contactoApellido2 = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.contactoApellido2);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el segundo apellido de la persona de contacto." };
                }

                //Actualizar telefono
                try
                {
                    if (data.telefono != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET telefono = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.telefono);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el telefono." };
                }

                //Actualizar email
                try
                {
                    if (data.email != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET email = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.email);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el email." };
                }

                //Actualizar servicio de prevencion
                try
                {
                    if (data.servicioPrevencion != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET servicioPrevencion = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.servicioPrevencion);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el servicio de prevencion." };
                }

                //Actualizar convenio
                try
                {
                    if (data.convenio != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET convenio = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.convenio);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el convenio." };
                }

                //Actualizar ccc
                try
                {
                    if (data.ccc != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET ccc = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.ccc);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el centro." };
                }

                //Actualizar cnae
                try
                {
                    if (data.cnae != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET cnae = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.cnae);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar el CNAE." };
                }

                //Actualizar fecha de alta
                try
                {
                    if (data.fechaAlta != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET fechaAlta = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.fechaAlta.Value.Date);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la fecha de alta." };
                }

                //Actualizar Workshift requiere foto
                try
                {
                    if (data.workshiftRequiereFoto != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET workshiftRequiereFoto = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.workshiftRequiereFoto.Value ? 1 : 0);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar si el centro requeire foto" };
                }

                //Actualizar Workshift requiere ubicacion
                try
                {
                    if (data.workshiftRequiereUbicacion != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET workshiftRequiereUbicacion = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.workshiftRequiereUbicacion.Value ? 1 : 0);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar si el centro requiere ubicacion." };
                }

                //Actualizar Referencia externa
                try
                {
                    if (data.referenciaExterna != null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE centros SET referenciaExterna = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.referenciaExterna);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5805, no se ha podido actualizar la referencia externa." };
                }

            }
            catch (Exception)
            {
                result.failed = true;
                result.result = new { error = "Error 5804, no se han podido actualizar los datos" };
            }

            return result;
        }

        public static List<ExtendedCentroData> listCentros(SqlConnection conn, SqlTransaction transaction, string companyId = null)
        {
            List<ExtendedCentroData> centros = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT C.id, C.sociedad, C.alias, C.regimen, C.domicilio, C.cp, C.poblacion, C.provincia, C.contactoNombre, C.contactoApellido1, C.contactoApellido2, C.telefono, C.email, C.fechaAlta, C.servicioPrevencion, S.nombre as servicioPrevencionNombre, C.convenio, C.ccc, C.cnae, C.workshiftRequiereFoto, C.workshiftRequiereUbicacion, C.referenciaExterna " +
                                      "FROM centros C LEFT OUTER JOIN servicios_prevencion S ON(C.servicioPrevencion = S.id) " +
                                      "WHERE (@COMPANY IS NULL OR @COMPANY = C.companyId) " +
                                      "ORDER BY fechaAlta DESC";
                command.Parameters.AddWithValue("@COMPANY", (object)companyId ?? DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ExtendedCentroData extendedCentro = new();
                        extendedCentro.Read(reader);
                        centros.Add(extendedCentro);
                    }
                }
            }

            return centros;
        }


        public static void addPermissionToAdminCLWhenCentroIsCreated(SqlConnection conn, SqlTransaction transaction, string companyId, string centroId)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "INSERT INTO client_user_centros (centroId, clientUserId) " +
                    "SELECT centroId = @CENTRO, CU.id FROM client_users CU INNER JOIN empresas E ON(CU.email = E.firmanteEmail) WHERE E.id = @COMPANY";

                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@COMPANY", companyId);
                command.ExecuteNonQuery();
            }
        }


        public static void addWorksToCenterFromLastClosedContrato(SqlConnection conn, SqlTransaction transaction, string companyId, string centroId)
        {
            string contratoVigenteId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                //TODO: Volver a poner este closed = 1 cuando haya que cerrar los contratos para que las categorias apliquen.
                command.CommandText = "SELECT TOP 1 id FROM company_contratos WHERE companyId = @COMPANY ORDER BY date DESC"; // AND closed = 1
                command.Parameters.AddWithValue("@COMPANY", companyId);
                using (SqlDataReader reader = command.ExecuteReader())
                    if (reader.Read())
                        contratoVigenteId = reader.GetString(reader.GetOrdinal("id"));
            }

            if (contratoVigenteId == null) return;

            CompanyContrato? contrato = getContrato(contratoVigenteId, conn, transaction);
            if (contrato == null) return;

            addWorksToCenterFromContrato(conn, transaction, contrato.Value);
        }

        public static void addWorksToCenterFromContrato(SqlConnection conn, SqlTransaction transaction, CompanyContrato contrato)
        {
            foreach (Puesto puesto in contrato.puestos)
            {
                List<ExtendedCentroData> centros = CentroController.listCentros(conn, transaction, contrato.companyId);
                foreach (ExtendedCentroData centro in centros)
                {
                    //Comprobar si ya tiene un trabajo con la categoria
                    bool exists = false;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }
                        command.CommandText = "SELECT COUNT(*) FROM trabajos WHERE categoryId = @CATEGORY_ID AND centroId = @CENTRO_ID";
                        command.Parameters.AddWithValue("@CATEGORY_ID", puesto.categoryId);
                        command.Parameters.AddWithValue("@CENTRO_ID", centro.id);
                        exists = (int)command.ExecuteScalar() > 0;
                    }

                    //Crear trabajo
                    if (!exists)
                    {
                        string id = ComputeStringHash(puesto.categoryId + centro.id + DateTime.Now);
                        string signLink = id.Substring(0, 10);

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            if (transaction != null)
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                            }
                            command.CommandText = "INSERT INTO trabajos (id, categoryId, centroId, signLink) VALUES " +
                                    "(@ID, @CATEGORY_ID, @CENTRO_ID, @SIGN_LINK)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@CATEGORY_ID", puesto.categoryId);
                            command.Parameters.AddWithValue("@CENTRO_ID", centro.id);
                            command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public static UpdateResult deleteCentro(string centroId, SqlConnection conn, SqlTransaction transaction, string securityToken)
        {
            UpdateResult result = new UpdateResult() { failed = false, result = null };
            string fullName = FindCentroFullName(centroId);
            //Desasignar los candidatos que tengan este centro
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET centroId = NULL WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido desasignar los candidatos del centro" };
                }
            }
            //Eliminar las incidencias de horas extra
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM incidencia_horas_extra WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar los incidencias de horas extra del centro" };
                }
            }
            //Eliminar los eventos de las incidencias de falta de asistencia
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE E FROM incidencia_falta_asistencia_eventos E INNER JOIN incidencia_falta_asistencia I ON(E.incidenceId = I.id) WHERE I.centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar las incidencias del centro" };
                }
            }
            //Eliminar las incidencias de falta de asistencia
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM incidencia_falta_asistencia WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar las faltas de asistencia del centro" };
                }
            }
            //Eliminar las notas que sean de este centro
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM company_notes WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar las notas del centro" };
                }
            }
            //Eliminar los permisos que tengan los usuarios de cliente sobre este centro
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM client_user_centros WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar los permisos sobre el centro" };
                }
            }
            //Eliminar todos sus trabajos
            if (!result.failed)
            {
                try
                {
                    List<string> workIds = new List<string>();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "SELECT id FROM trabajos WHERE centroId LIKE @ID";
                        command.Parameters.AddWithValue("@ID", centroId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                workIds.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }
                    }
                    for (int i = 0; i < workIds.Count; i++)
                    {
                        WorkController.deleteWorkInTransaction(conn, transaction, workIds[i]);
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar los trabajos del centro" };
                }
            }
            //Eliminar todos sus checks
            if (!result.failed)
            {
                try
                {
                    List<string> checkIds = new List<string>();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "SELECT id FROM candidate_checks WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                checkIds.Add(reader.GetString(reader.GetOrdinal("id")));
                            }
                        }
                    }
                    for (int i = 0; i < checkIds.Count; i++)
                    {
                        CandidateChecksController.deleteCheckInTransaction(conn, transaction, checkIds[i]);
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar la verificación del centro" };
                }
            }
            //Eliminar las asignaciones de horarios de este centro
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE HA FROM horarios_asignacion HA INNER JOIN horarios H ON(HA.horarioId = H.id) WHERE H.centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar las asignaciones de horarios del centro" };
                }
            }
            //Eliminar los horarios de este centro
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE horarios WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar los horarios del centro" };
                }
            }
            //Eliminar las vacaciones de este centro
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE candidate_vacaciones WHERE centroId = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar las vacaciones del centro" };
                }
            }
            //Eliminar el centro
            if (!result.failed)
            {
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM centros WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", centroId);
                        if (command.ExecuteNonQuery() == 0)
                        {
                            result.failed = true;
                            result.result = new { error = "Error 4890, centro no encontrado" };
                        }
                        else
                        {
                            result.failed = false;
                            result.result = new { error = false };

                            LogToDB(LogType.CENTRO_DELETED, $"Centro {fullName} eliminado", FindUsernameBySecurityToken(securityToken, conn), conn);
                        }
                    }
                }
                catch (Exception)
                {
                    result.failed = true;
                    result.result = new { error = "Error 5900, no se han podido eliminar el centro" };
                }
            }

            return result;
        }

        //------------------------------WorkController Inicio------------------------------
        public static bool deleteWorkInTransaction(SqlConnection conn, SqlTransaction transaction, string workId)
        {
            bool failed = false;

            //Obtener la ID de la empresa a la que pertenece el trabajo
            string companyId = null, centroId = null, signLink = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "SELECT C.companyId, C.id, T.signLink FROM trabajos T INNER JOIN centros C ON(T.centroId = C.id) WHERE T.id = @ID";
                    command.Parameters.AddWithValue("@ID", workId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            companyId = reader.GetString(reader.GetOrdinal("companyId"));
                            signLink = reader.GetString(reader.GetOrdinal("signLink"));
                            centroId = reader.GetString(reader.GetOrdinal("id"));
                        }
                        else
                        {
                            failed = true;
                        }
                    }
                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            //Eliminar las entregas de cuestionarios para este trabajo, los arhivos
            List<string> submissions = new List<string>();
            using (SqlCommand command = conn.CreateCommand())
            {
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "SELECT id FROM emision_cuestionarios WHERE trabajoId LIKE @ID";
                    command.Parameters.AddWithValue("@ID", workId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            submissions.Add(reader.GetString(reader.GetOrdinal("id")));
                        }
                    }
                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            //Eliminar las entregas de cuestionarios para este trabajo
            using (SqlCommand command = conn.CreateCommand())
            {
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM emision_cuestionarios WHERE trabajoId LIKE @ID";
                    command.Parameters.AddWithValue("@ID", workId);
                    command.ExecuteNonQuery();

                }
                catch (Exception)
                {
                    failed = true;
                }
            }

            //Eliminar las asociaciones de formularios del trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM vinculos_trabajos_formularios WHERE trabajoId LIKE @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Antes de borar los documentos, borrar las descargas de estos codumentos
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE TD FROM category_documents_downloads TD INNER JOIN category_documents T ON TD.documentId = T.id WHERE T.trabajoid = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Al borrar un trabajo se eliminenen todos los doumentos asociados
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM category_documents WHERE trabajoid = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Quitar los lastSignLink de los candidatos
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET lastSignLink = NULL WHERE lastSignLink = @SIGNLINK";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.Parameters.AddWithValue("@SIGNLINK", signLink);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Eliminar los plueses preestablecidos del trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM trabajos_pluses WHERE trabajoid = @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Borrar el registro de fichaje de este trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM workshifts WHERE signLink = @SIGNLINK";
                        command.Parameters.AddWithValue("@SIGNLINK", signLink);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Eliminar el trabajo
            if (!failed)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM trabajos WHERE id LIKE @ID";
                        command.Parameters.AddWithValue("@ID", workId);
                        command.ExecuteNonQuery();
                        DeleteDir(new[] { "work", workId });
                        foreach (string submitId in submissions)
                        {
                            DeleteDir(new[] { "questionnaire_submissions", submitId });
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
            }

            //Eliminar la carpeta del trabajo
            if (!failed)
            {
                DeleteDir(new[] { "companies", companyId, "centro", centroId, "work", workId });
            }

            return failed;
        }
        //------------------------------WorkController Fin---------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------

    }
}
