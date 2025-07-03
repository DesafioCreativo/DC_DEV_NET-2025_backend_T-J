using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Model.Candidate;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using ThinkAndJobSolution.Controllers._Helper.AnvizTools;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;
using iTextSharp.text.pdf;
using ThinkAndJobSolution.Controllers.MainHome.Comercial;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using ThinkAndJobSolution.Controllers.MainHome.Sysadmin;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    [Route("api/v1/candidates")]
    [ApiController]
    [Authorize]
    public class CandidateController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        //Listado

        [HttpGet]
        [Route(template: "list/{registered?}")]
        public IActionResult List(bool? registered)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };


            if (!HasPermission("Candidates.List", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                List<object> candidates = new List<object>();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (registered == null)
                    {

                        command.CommandText = "SELECT  *  \n" +
                                              "FROM( \n" +
                                              "    SELECT id, nombre, apellidos, dni, telefono, [date], warnings, lastWarning = null, email_verified, registered = 'true' FROM candidatos \n" +
                                              "    UNION ALL \n" +
                                              "    SELECT id, [name], lastname, dni, telf, [date], warnings, lastWarning, email_verified = 0, registered = 'false' FROM pre_candidates \n" +
                                              ") T \n" +
                                              "ORDER BY T.[date] DESC";
                    }
                    else if (registered == true)
                    {
                        command.CommandText =
                            "SELECT id, nombre, apellidos, dni, telefono, [date], warnings, lastWarning = null, email_verified, registered = 'true' FROM candidatos ORDER BY [date] DESC";
                    }
                    else if (registered == false)
                    {
                        command.CommandText =
                            "SELECT id, [name] as nombre, lastname as apellidos, dni, telf as telefono, [date], warnings, lastWarning, email_verified = 0, registered = 'false' FROM pre_candidates ORDER BY [date] DESC";
                    }

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool isRegistered = reader.GetString(reader.GetOrdinal("registered")).Equals("true");
                            candidates.Add(new
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("nombre")),
                                surname = reader.GetString(reader.GetOrdinal("apellidos")),
                                dni = reader.GetString(reader.GetOrdinal("dni")),
                                phone = reader.GetString(reader.GetOrdinal("telefono")),
                                date = reader.GetDateTime(reader.GetOrdinal("date")),
                                registered = isRegistered,
                                warnings = reader.IsDBNull(reader.GetOrdinal("warnings")) ? null : (Int32?)reader.GetInt32(reader.GetOrdinal("warnings")),
                                lastWarningDate = reader.IsDBNull(reader.GetOrdinal("lastWarning")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("lastWarning")),
                                emailVerified = reader.GetInt32(reader.GetOrdinal("email_verified")) == 1,
                            });
                        }
                    }
                }

                result = candidates;
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list-filtered/")]
        public async Task<IActionResult> ListFiltered()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };


            if (!HasPermission("Candidates.ListFiltered", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("cp", out JsonElement cpJson) && json.TryGetProperty("marginCP", out JsonElement marginCPJson) &&
                json.TryGetProperty("key", out JsonElement keyJson) &&
                json.TryGetProperty("localidad", out JsonElement localidadJson) && json.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) && json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("tieneTrabajo", out JsonElement tieneTrabajoJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) && json.TryGetProperty("perpage", out JsonElement perpageJson) &&
                json.TryGetProperty("sortColumn", out JsonElement sortColumnJson) && json.TryGetProperty("sortDesc", out JsonElement sortDescJson) &&
                json.TryGetProperty("faceStatus", out JsonElement faceStatusJson) && json.TryGetProperty("cesionStatus", out JsonElement cesionStatusJson))
            {

                int? cp = GetJsonInt(cpJson);
                int? marginCP = GetJsonInt(marginCPJson);
                string key = keyJson.GetString();
                string localidad = localidadJson.GetString();
                string provincia = provinciaJson.GetString();
                string companyId = companyIdJson.GetString();
                string centroId = centroIdJson.GetString();
                bool? tieneTrabajo = GetJsonBool(tieneTrabajoJson);
                int page = Int32.Parse(pageJson.GetString());
                int perpage = Int32.Parse(perpageJson.GetString());
                string faceStatus = faceStatusJson.GetString();
                string cesionStatus = cesionStatusJson.GetString();
                string sort = FormatSort(new() {
                        { "dni", "C.dni" },
                        { "candidate", "CONCAT(C.apellidos, ' ', C.nombre)" },
                        { "phone", "C.telefono" },
                        { "cp", "C.cp" },
                        { "provincia", "CAST(C.provincia as varchar(50))" },
                        { "localidad", "CAST(C.localidad as varchar(50))" },
                        { "date", "[date]" },
                        { "completo", "allDataFilledIn" }
                    }, "date", sortColumnJson, sortDescJson);

                try
                {
                    List<CandidateStats> candidates = listFiltered(key, provincia, localidad, companyId, centroId, cp, marginCP, tieneTrabajo, faceStatus, cesionStatus, page, perpage, sort);
                    result = new { error = false, candidates };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido listar candidatos" };
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list-filtered-count/")]
        public async Task<IActionResult> ListFilteredCount()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            if (!HasPermission("Candidates.ListFilteredCount", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("cp", out JsonElement cpJson) && json.TryGetProperty("marginCP", out JsonElement marginCPJson) &&
                json.TryGetProperty("key", out JsonElement keyJson) &&
                json.TryGetProperty("localidad", out JsonElement localidadJson) && json.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) && json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("tieneTrabajo", out JsonElement tieneTrabajoJson) && json.TryGetProperty("faceStatus", out JsonElement faceStatusJson) &&
                json.TryGetProperty("cesionStatus", out JsonElement cesionStatusJson))
            {

                int? cp = GetJsonInt(cpJson);
                int? marginCP = GetJsonInt(marginCPJson);
                string key = keyJson.GetString();
                string localidad = localidadJson.GetString();
                string provincia = provinciaJson.GetString();
                string companyId = companyIdJson.GetString();
                string centroId = centroIdJson.GetString();
                bool? tieneTrabajo = GetJsonBool(tieneTrabajoJson);
                string faceStatus = faceStatusJson.GetString();
                string cesionStatus = cesionStatusJson.GetString();

                result = new
                {
                    error = false,
                    count = listFilteredCount(key, provincia, localidad, companyId, centroId, cp, marginCP, tieneTrabajo, faceStatus, cesionStatus)
                };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list-filtered-for-client/")]
        public async Task<IActionResult> ListFilteredForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("key", out JsonElement keyJson) && json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) && json.TryGetProperty("perpage", out JsonElement perpageJson))
            {
                string key = keyJson.GetString();
                string centroId = centroIdJson.GetString();
                int page = Int32.Parse(pageJson.GetString());
                int perpage = Int32.Parse(perpageJson.GetString());

                if (centroId == null || ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_TRABAJADORES) == null)
                {
                    return Ok(new
                    {
                        error = "Error 1002, No se disponen de los privilegios suficientes."
                    });
                }

                try
                {
                    List<object> candidates = new();
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT C.id, C.nombre, C.apellidos, C.dni, C.telefono, C.email " +
                                "FROM candidatos C " +
                                "WHERE " +
                                "(@CENTRO IS NULL OR C.centroId = @CENTRO) AND " +
                                "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @KEY OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY) " +
                                "ORDER BY [date] DESC " +
                                "OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY";

                            command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@OFFSET", page * perpage);
                            command.Parameters.AddWithValue("@LIMIT", perpage);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string id = reader.GetString(reader.GetOrdinal("id"));
                                    candidates.Add(new
                                    {
                                        id,
                                        photo = ReadFile(new[] { "candidate", id, "photo" }),
                                        name = reader.GetString(reader.GetOrdinal("nombre")),
                                        surname = reader.GetString(reader.GetOrdinal("apellidos")),
                                        dni = reader.GetString(reader.GetOrdinal("dni")),
                                        phone = reader.GetString(reader.GetOrdinal("telefono")),
                                        email = reader.GetString(reader.GetOrdinal("email"))
                                    });
                                }
                            }
                        }
                    }

                    result = new
                    {
                        error = false,
                        candidates
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido listar candidatos" };
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list-filtered-count-for-client/")]
        public async Task<IActionResult> ListFilteredCountForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("key", out JsonElement keyJson) && json.TryGetProperty("centroId", out JsonElement centroIdJson))
            {
                string key = keyJson.GetString();
                string centroId = centroIdJson.GetString();

                if (centroId == null || ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_TRABAJADORES) == null)
                {
                    return Ok(new
                    {
                        error = "Error 1002, No se disponen de los privilegios suficientes."
                    });
                }

                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT COUNT(*) " +
                                "FROM candidatos C " +
                                "WHERE " +
                                "(@CENTRO IS NULL OR C.centroId = @CENTRO) AND " +
                                "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @KEY OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY)";

                            command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);

                            result = command.ExecuteScalar();
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido contar candidatos" };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "list-for-client/{centroId}/")]
        public IActionResult ListForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            if (centroId == null || ClientHasPermission(clientToken, null, centroId, null) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    result = new
                    {
                        error = false,
                        candidates = listForClient(centroId, conn)
                    };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5701, no han podido listar candidatos" };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-all-for-client/")]
        public IActionResult ListForAllClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, null) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }

            try
            {

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    List<CLSimpleWorkerInfo> candidateList = new();

                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as nombreCompleto, C.dni, C.email,C.telefono,C.centroId, C.cesionActiva, ce.companyId, C.fechaFinTrabajo, C.fechaComienzoTrabajo  " +
                                          " FROM candidatos C  " +
                                          " INNER JOIN centros CE ON(C.centroId = CE.id) " +
                                          " INNER JOIN client_user_centros CUC ON(C.centroId = CUC.centroId) " +
                                          " INNER JOIN client_users U ON(CUC.clientUserId = U.id) " +
                                          " WHERE U.token = @TOKEN AND " +
                                          " (C.fechaFinTrabajo IS NULL OR fechaFinTrabajo >= @TODAY) ";

                        cmd.Parameters.AddWithValue("@TOKEN", clientToken);
                        cmd.Parameters.AddWithValue("@TODAY", DateTime.Now.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                candidateList.Add(new()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    nombre = reader.GetString(reader.GetOrdinal("nombreCompleto")),
                                    dni = reader.GetString(reader.GetOrdinal("dni")),
                                    centroId = reader.GetString(reader.GetOrdinal("centroId")),
                                    cesionActiva = reader.GetInt32(reader.GetOrdinal("cesionActiva")) == 1,
                                    empresaId = reader.GetString(reader.GetOrdinal("companyId")),
                                    fechaComienzoTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo")),
                                    fechaFinTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaFinTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaFinTrabajo"))
                                });
                        }

                    }

                    result = new
                    {
                        error = false,
                        candidates = candidateList
                    };
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5701, no han podido listar candidatos" };
            }

            return Ok(result);
        }


        [HttpGet]
        [Route("list-by-company/{companyId}")]
        public IActionResult ListByCompany(string companyId)
        {
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                List<MinimalCandidateData> candidates = new();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as nombreCompleto, C.dni, C.telefono, C.email, C.cesionActiva " +
                        "FROM candidatos C " +
                        "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                        "INNER JOIN centros CE ON(T.centroId = CE.id) " +
                        "WHERE CE.companyId = @ID";
                    command.Parameters.AddWithValue("@ID", companyId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            candidates.Add(new MinimalCandidateData
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("nombreCompleto")),
                                dni = reader.GetString(reader.GetOrdinal("dni")),
                                phone = reader.GetString(reader.GetOrdinal("telefono")),
                                email = reader.GetString(reader.GetOrdinal("email")),
                                cesionActiva = reader.GetInt32(reader.GetOrdinal("cesionActiva")) == 1
                            });
                        }
                    }
                }

                result = candidates;

            }

            return Ok(result);
        }

        [HttpGet]
        [Route("list-by-centro/{centroId}")]
        public IActionResult ListByCentro(string centroId)
        {
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                List<MinimalCandidateData> candidates = new();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as nombreCompleto, C.dni, C.telefono, C.email, C.cesionActiva " +
                        "FROM candidatos C " +
                        "WHERE C.centroId = @ID";
                    //Esto otro se fija en el signLink, en lugar de en el centroId del candidato
                    /*
                    command.CommandText =
                        "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as nombreCompleto, C.dni, C.telefono, C.email " +
                        "FROM candidatos C " +
                        "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                        "WHERE T.centroId = @ID";
                    */
                    command.Parameters.AddWithValue("@ID", centroId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            candidates.Add(new MinimalCandidateData
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("nombreCompleto")),
                                dni = reader.GetString(reader.GetOrdinal("dni")),
                                phone = reader.GetString(reader.GetOrdinal("telefono")),
                                email = reader.GetString(reader.GetOrdinal("email")),
                                cesionActiva = reader.GetInt32(reader.GetOrdinal("cesionActiva")) == 1
                            });
                        }
                    }
                }

                result = candidates;

            }

            return Ok(result);
        }

        [HttpGet]
        [Route("fast-list/")]
        public IActionResult FastList()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            if (!HasPermission("Candidates.FastList", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                List<MinimalCandidateData> candidates = new();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as nombreCompleto, C.dni, C.telefono, C.email " +
                        "FROM candidatos C ";

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            candidates.Add(new MinimalCandidateData
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                name = reader.GetString(reader.GetOrdinal("nombreCompleto")),
                                dni = reader.GetString(reader.GetOrdinal("dni")),
                                phone = reader.GetString(reader.GetOrdinal("telefono")),
                                email = reader.GetString(reader.GetOrdinal("email"))
                            });
                        }
                    }
                }

                result = candidates;

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "fast-list-multi/")]
        public IActionResult FastListMulti()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Candidates.FastListMulti", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            try
            {
                List<MultiSelectorCandidateData> candidates = new List<MultiSelectorCandidateData>();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT C.id, C.dni, C.email, C.allDataFilledIn, C.cesionActiva, " +
                            "TRIM(CONCAT(C.apellidos, ', ', C.nombre)) as fullName, " +
                            "CE.alias as centroAlias, CE.id as centroId, " +
                            "CA.name as nombreTrabajo, " +
                            "E.nombre as nombreEmpresa, E.id as empresaId " +
                            "FROM candidatos C " +
                            "LEFT OUTER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                            "LEFT OUTER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "LEFT OUTER JOIN centros CE ON(T.centroId = CE.id) " +
                            "LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                            "WHERE C.test = 0";

                        command.Parameters.AddWithValue("@NOW", DateTime.Now);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                candidates.Add(new()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    dni = reader.GetString(reader.GetOrdinal("dni")),
                                    fullName = reader.GetString(reader.GetOrdinal("fullName")),
                                    email = reader.GetString(reader.GetOrdinal("email")),
                                    category = reader.IsDBNull(reader.GetOrdinal("nombreTrabajo")) ? null : reader.GetString(reader.GetOrdinal("nombreTrabajo")),
                                    companyName = reader.IsDBNull(reader.GetOrdinal("nombreEmpresa")) ? null : reader.GetString(reader.GetOrdinal("nombreEmpresa")),
                                    companyId = reader.IsDBNull(reader.GetOrdinal("empresaId")) ? null : reader.GetString(reader.GetOrdinal("empresaId")),
                                    centerName = reader.IsDBNull(reader.GetOrdinal("centroAlias")) ? null : reader.GetString(reader.GetOrdinal("centroAlias")),
                                    centerId = reader.IsDBNull(reader.GetOrdinal("centroId")) ? null : reader.GetString(reader.GetOrdinal("centroId")),
                                    fullProfile = reader.GetInt32(reader.GetOrdinal("allDataFilledIn")) == 1,
                                    cesionActiva = reader.GetInt32(reader.GetOrdinal("cesionActiva")) == 1
                                });
                            }
                        }
                    }
                }

                result = new { error = false, candidates };
            }
            catch (Exception)
            {
                result = new { error = "Error 5969, no han podido listar los candidatos" };
            }

            return Ok(result);
        }

        //Obtencion

        [HttpGet]
        [Route(template: "{signlink}/")]
        public IActionResult GetCandidate(string signlink)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                ExtendedCandidateData candidate = new ExtendedCandidateData();

                bool failed = false;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                               "SELECT C.* " +
                               "FROM candidatos C WHERE C.id = @ID AND C.lastSignLink = @SIGNLINK";
                    command.Parameters.AddWithValue("@ID", candidateId);
                    command.Parameters.AddWithValue("@SIGNLINK", signlink);


                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            candidate.Read(reader);
                        }
                        else
                        {
                            failed = true;
                            result = new { error = "Error 4861, no se ha encontrado al candidato o el signLink no coincide" };
                        }

                    }
                }

                if (!failed)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                                   "SELECT type, expiration FROM candidate_driving_license WHERE candidateId = @ID";
                        command.Parameters.AddWithValue("@ID", candidateId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                candidate.ReadDrivingLicense(reader);
                            }

                        }
                    }
                }

                if (!failed)
                {
                    result = new
                    {
                        error = false,
                        candidate = candidate
                    };
                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "stats/{candidateId}/")]
        public IActionResult GetCandidateStats(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            if (!HasPermission("Candidates.GetStats", securityToken).Acceso)
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
                    CandidateStats candidate = new CandidateStats();

                    bool failed = false;
                    try
                    {
                        candidate = getCandidateStats(candidateId, conn);
                        if (candidate == null)
                        {
                            failed = true;
                            result = new { error = "Error 4861, no se ha encontrado al candidato o este carece de datos internos imprescindibles" };
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new { error = "Error 5860, no se han podido obtener la informacion basica del candidato" };
                    }

                    //Obtener las licencias de conducir
                    if (!failed)
                    {
                        try
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                           "SELECT type, expiration FROM candidate_driving_license WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        candidate.ReadDrivingLicense(reader);
                                    }

                                }
                            }
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new { error = "Error 5861, no se han podido listar las licencias de conducir" };
                        }
                    }

                    //Obtener los test de PRL y Formacion de su trabajo actual
                    if (!failed && candidate.signLink != null)
                    {
                        try
                        {
                            //Obtener el trabajo del candidato, categoria y nombres de centro y alias
                            string trabajoId = null, categoryId = null, centroAlias = null, companyName = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT T.id as trabajoId, T.categoryId, CE.alias as centroAlias, E.nombre as companyName " +
                                    "FROM candidatos C " +
                                    "INNER JOIN trabajos T ON(c.lastSignLink = T.signLink) " +
                                    "INNER JOIN centros CE ON(T.centroId = CE.id) " +
                                    "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                                    "WHERE C.id = @CANDIDATO";
                                command.Parameters.AddWithValue("@CANDIDATO", candidateId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        trabajoId = reader.GetString(reader.GetOrdinal("trabajoId"));
                                        categoryId = reader.GetString(reader.GetOrdinal("categoryId"));
                                        centroAlias = reader.GetString(reader.GetOrdinal("centroAlias"));
                                        companyName = reader.GetString(reader.GetOrdinal("companyName"));
                                    }
                                }
                            }

                            if (trabajoId != null && categoryId != null && centroAlias != null && companyName != null)
                            {
                                //Obtener los tests minimos que deberia tener
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText =
                                               "SELECT F.nombre, F.tipo, EC.id as submitId, EC.certificado, EC.fechaFirma, F.requiereCertificado " +
                                               "FROM ( " +
                                               "SELECT VTF.formularioId FROM vinculos_trabajos_formularios VTF " +
                                               "WHERE VTF.trabajoId = @TRABAJO " +
                                               "UNION " +
                                               "SELECT VCF.formularioId FROM vinculos_categorias_formularios VCF " +
                                               "WHERE VCF.categoryId = @CATEGORY" +
                                               ") VF " +
                                               "INNER JOIN formularios F ON VF.formularioId = F.id " +
                                               "LEFT OUTER JOIN emision_cuestionarios EC ON(F.id = EC.formularioId AND EC.trabajoId = @TRABAJO AND EC.candidatoId = @CANDIDATO) ";
                                    command.Parameters.AddWithValue("@TRABAJO", trabajoId);
                                    command.Parameters.AddWithValue("@CATEGORY", categoryId);
                                    command.Parameters.AddWithValue("@CANDIDATO", candidateId);

                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            candidate.ReadTest(reader, centroAlias, companyName, true);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new { error = "Error 5862, no se han podido listar los tests requeridos del candidato" };
                        }
                    }

                    //Buscar otros tests que tenga, que no esten ya agregados
                    if (!failed)
                    {
                        try
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                            "SELECT F.nombre, F.tipo, EC.id as submitId, EC.certificado, EC.fechaFirma, F.requiereCertificado, " +
                                            "CE.alias as centroAlias, E.nombre as companyName " +
                                            "FROM emision_cuestionarios EC " +
                                            "INNER JOIN formularios F ON(EC.formularioId = F.id) " +
                                            "INNER JOIN trabajos T ON(EC.trabajoId = T.id) " +
                                            "INNER JOIN centros CE ON(T.centroId = CE.id) " +
                                            "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                                            "WHERE EC.candidatoId = @CANDIDATO";
                                command.Parameters.AddWithValue("@CANDIDATO", candidateId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        string submitId = reader.GetString(reader.GetOrdinal("submitId"));
                                        if (!candidate.testsPRL.Exists(t => t.submitId == submitId) && !candidate.testsTraining.Exists(t => t.submitId == submitId))
                                        {
                                            candidate.ReadTest(reader,
                                            reader.GetString(reader.GetOrdinal("centroAlias")),
                                            reader.GetString(reader.GetOrdinal("companyName")),
                                            false);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new { error = "Error 5862, no se han podido listar los tests adicionales del candidato" };
                        }
                    }

                    if (!failed)
                    {
                        result = new
                        {
                            error = false,
                            candidate
                        };
                    }

                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download-attachment/{signLink}")]
        public IActionResult DownloadAttachment(string signLink, string attachment)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            attachment = Uri.UnescapeDataString(attachment);
            object result;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                               "SELECT COUNT(*) " +
                               "FROM candidatos C WHERE C.id = @ID"; //  AND C.lastSignLink = @SIGNLINK
                    command.Parameters.AddWithValue("@ID", candidateId);
                    //command.Parameters.AddWithValue("@SIGNLINK", signLink);

                    if (command.ExecuteScalar() as Int32? != 1)
                    {
                        return Ok(new { error = "Error 4111, no se ha encontrado al usuario o no coincide el signlink." });
                    }
                }
            }

            if (attachment.StartsWith("driving-license-"))
            {
                string type = attachment.Substring("driving-license-".Length);
                result = new
                {
                    anverso = ReadFile(new[] { "candidate", candidateId, "driving_license", type, "anverso" }),
                    reverso = ReadFile(new[] { "candidate", candidateId, "driving_license", type, "reverso" })
                };
            }
            else
            {
                switch (attachment)
                {
                    case "foto":
                        result = ReadFile(new[] { "candidate", candidateId, "photo" });
                        break;
                    case "dni":
                        result = new
                        {
                            anverso = ReadFile(new[] { "candidate", candidateId, "dniAnverso" }),
                            reverso = ReadFile(new[] { "candidate", candidateId, "dniReverso" })
                        };
                        break;
                    case "cv":
                        result = ReadFile(new[] { "candidate", candidateId, "cv" });
                        break;
                    case "foto-cuenta-bancaria":
                        result = ReadFile(new[] { "candidate", candidateId, "foto_cuenta_bancaria" });
                        break;
                    case "foto-numero-seguridad-social":
                        result = ReadFile(new[] { "candidate", candidateId, "foto_numero_seguridad_social" });
                        break;
                    case "legal-representative-consent-dni-tutor":
                        result = new
                        {
                            anverso = ReadFile(new[] { "candidate", candidateId, "legal_representative_consent", "tutor_anverso" }),
                            reverso = ReadFile(new[] { "candidate", candidateId, "legal_representative_consent", "tutor_reverso" }),
                        };
                        break;
                    case "legal-representative-consent-dni-autorizacion":
                        result = ReadFile(new[] { "candidate", candidateId, "legal_representative_consent", "autorizacion" });
                        break;
                    case "foto-permiso-trabajo":
                        result = ReadFile(new[] { "candidate", candidateId, "foto_permiso_trabajo" });
                        break;
                    case "foto-discapacidad":
                        result = ReadFile(new[] { "candidate", candidateId, "foto_discapacidad" });
                        break;
                    case "modelo145":
                        byte[] modelo145 = generateModelo145(candidateId);
                        result = modelo145 == null ? null : ("data:application/pdf;base64," + Convert.ToBase64String(modelo145));
                        break;
                    default:
                        result = new { error = "Error 4111, tipo de adjunto desconocido." };
                        break;
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-picture/{candidateId}")]
        public IActionResult GetPicture(string candidateId)
        {
            return Ok(new { error = false, picture = ReadFile(new[] { "candidate", candidateId, "photo" }) });
        }

        [HttpGet]
        [Route(template: "get-face/")]
        public IActionResult GetFace()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            return Ok(new { error = false, face = ReadFile(new[] { "candidate", candidateId, "face" }) });
        }

        [HttpGet]
        [Route(template: "get-for-client/{candidateId}/")]
        public IActionResult GetCandidateForClient(string candidateId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            string centroId = null;
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                ExtendedCandidateData candidate = new ExtendedCandidateData();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                               "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.dni, C.fechaComienzoTrabajo, C.fechaFinTrabajo, C.cesionActiva, C.centroId, E.nombre as companyName, CE.alias as centroAlias, CA.name as categoryName, CG.name as groupName " +
                               "FROM candidatos C " +
                               "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                               "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                               "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                               "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                               "LEFT OUTER JOIN candidate_group_members CGM ON(CGM.candidateId = C.id) " +
                               "LEFT OUTER JOIN candidate_groups CG ON(CGM.groupId = CG.id) " +
                               "WHERE C.id = @ID ";
                    command.Parameters.AddWithValue("@ID", candidateId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = new
                            {
                                error = false,
                                worker = new
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    foto = ReadFile(new[] { "candidate", candidateId, "photo" }),
                                    nombre = reader.GetString(reader.GetOrdinal("candidateName")),
                                    dni = reader.GetString(reader.GetOrdinal("dni")),
                                    empresa = reader.GetString(reader.GetOrdinal("companyName")),
                                    centro = reader.GetString(reader.GetOrdinal("centroAlias")),
                                    puesto = reader.GetString(reader.GetOrdinal("categoryName")),
                                    fechaComienzoTrabajo = (object)(reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo")).Date),
                                    fechaFinTrabajo = (object)(reader.IsDBNull(reader.GetOrdinal("fechaFinTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaFinTrabajo")).Date),
                                    groupName = reader.IsDBNull(reader.GetOrdinal("groupName")) ? null : reader.GetString(reader.GetOrdinal("groupName")),
                                    cesionActiva = reader.GetInt32(reader.GetOrdinal("cesionActiva")) == 1
                                }
                            };
                            centroId = reader.GetString(reader.GetOrdinal("centroId"));
                        }
                        else
                        {
                            result = new { error = "Error 4861, candidato no encontrado o no esta asociado a ningun trabajo" };
                        }
                    }
                }

                if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_TRABAJADORES, conn) == null)
                {
                    result = new { error = "Error 1002, permisos insuficientes" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "missing-data/{candidateId}/")]
        public IActionResult GetMissingData(string candidateId)
        {
            //string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                ExtendedCandidateData candidate = new ExtendedCandidateData();

                bool failed = false;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                               "SELECT C.* " +
                               "FROM candidatos C WHERE C.id = @ID";
                    command.Parameters.AddWithValue("@ID", candidateId);


                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            candidate.Read(reader);
                        }
                        else
                        {
                            failed = true;
                            result = new { error = "Error 4861, no se ha encontrado al candidato o el signLink no coincide" };
                        }

                    }
                }

                if (!failed)
                {
                    result = new
                    {
                        error = false,
                        missingData = candidate.GetRequiredData()
                    };
                }

            }

            return Ok(result);
        }

        //Creacion

        [HttpPost]
        [Route(template: "register")]
        public async Task<IActionResult> RegisterCandidate()
        {
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("dni", out JsonElement dniJson) && json.TryGetProperty("name", out JsonElement nameJson) &&
                json.TryGetProperty("surname", out JsonElement surnameJson) && json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("phone", out JsonElement phoneJson) && json.TryGetProperty("password", out JsonElement passwordJson) &&
                json.TryGetProperty("signLink", out JsonElement signLinkJson) &&
                json.TryGetProperty("localidad", out JsonElement localidadJson) && json.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                json.TryGetProperty("direccion", out JsonElement direccionJson) && json.TryGetProperty("cp", out JsonElement cpJson))
            {

                string name = nameJson.GetString().Trim();
                string surname = surnameJson.GetString().Trim();
                string dni = dniJson.GetString().Trim().ToUpper();
                string email = emailJson.GetString().Trim();
                string phone = phoneJson.GetString().Trim();
                string signLink = signLinkJson.GetString();
                string localidad = localidadJson.GetString().Trim();
                string provincia = provinciaJson.GetString().Trim();
                string direccion = direccionJson.GetString().Trim();
                string cp = cpJson.GetString().Trim();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    bool exists = false;

                    //Comprobar si el dni esta en uso
                    if (CheckDNINIECIFunique(dni, null, conn) != null)
                    {
                        result = new
                        {
                            error = $"Error 2251, el dni {dni} ya está en uso"
                        };

                        exists = true;
                    }

                    //Comprobar si existe otro candidato con el mismo email
                    if (CheckEMAILunique(email, null, conn) != null)
                    {
                        result = new
                        {
                            error = $"Error 2252, el email {email} ya está en uso"
                        };

                        exists = true;
                    }

                    //Intento de inserccion
                    if (!exists)
                    {
                        int? preRegisterWarnings = null;

                        //Comprobar si tiene warnings
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT [date], warnings FROM pre_candidates WHERE dni like @DNI";

                            command.Parameters.AddWithValue("@DNI", dniJson.GetString());

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    preRegisterWarnings = reader.GetInt32(reader.GetOrdinal("warnings"));
                                }
                            }

                        }

                        bool failed = false;
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            //Tratar al precandidato
                            string centroId = null;
                            try
                            {
                                //Borrar al precandidato, si existe
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;

                                    command.CommandText =
                                        "DELETE FROM pre_candidates WHERE dni = @DNI";

                                    command.Parameters.AddWithValue("@DNI", dni); ;

                                    command.ExecuteNonQuery();
                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5530, no se ha poidido controlar el prerregistro"
                                };
                            }

                            //Si no se ha obtenido el centro por preCandidato, obtenerlo por el centro
                            if (!failed)// && centroId == null
                            {
                                try
                                {
                                    //Obtener el centro del trabajo
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;

                                        command.CommandText =
                                            "SELECT centroId FROM trabajos WHERE signLink = @SIGNLINK";

                                        command.Parameters.AddWithValue("@SIGNLINK", signLink); ;

                                        using (SqlDataReader reader = command.ExecuteReader())
                                            if (reader.Read())
                                                centroId = reader.GetString(reader.GetOrdinal("centroId"));
                                    }
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5530, no se ha poidido obtener el centro de trabajo"
                                    };
                                }
                            }

                            if (!failed)
                            {
                                //Comprobar los tamaños
                                string error = null;
                                if (!failed && name.Length > 200) { failed = true; error = "Error 4082, el nombre no puede superar los 200 caracteres."; }
                                if (!failed && surname.Length > 200) { failed = true; error = "Error 4082, los apellidos no pueden superar los 200 caracteres."; }
                                if (!failed && dni.Length > 10) { failed = true; error = "Error 4082, el dni no puede superar los 100 caracteres."; }
                                if (!failed && email.Length > 100) { failed = true; error = "Error 4082, el email no puede superar los 100 caracteres."; }
                                if (!failed && phone.Length > 20) { failed = true; error = "Error 4082, el teléfono no puede superar los 20 caracteres."; }

                                //Insertar al candidato
                                string id = ComputeStringHash(
                                        dniJson.GetString() +
                                        nameJson.GetString() +
                                        surnameJson.GetString() +
                                        emailJson.GetString() +
                                        phoneJson.GetString() +
                                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                                    );
                                if (!failed)
                                {
                                    try
                                    {
                                        //Insertar al nuevo candidato
                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            command.Connection = conn;
                                            command.Transaction = transaction;

                                            command.CommandText =
                                                "INSERT INTO candidatos (id, nombre, apellidos, dni, email, telefono, pwd, warnings, lastSignLink, terminosAceptados, localidad, provincia, direccion, cp, centroId) " +
                                                "VALUES (@ID, @NAME, @SURNAME, @DNI, @EMAIL, @PHONE, @PWD, @WARNINGS, @LAST_SIGN_LINK, 1, @LOCALIDAD, @PROVINCIA, @DIRECCION, @CP, @CENTRO)";

                                            command.Parameters.AddWithValue("@ID", id);
                                            command.Parameters.AddWithValue("@NAME", name);
                                            command.Parameters.AddWithValue("@SURNAME", surname);
                                            command.Parameters.AddWithValue("@DNI", dni);
                                            command.Parameters.AddWithValue("@EMAIL", email);
                                            command.Parameters.AddWithValue("@PHONE", phone);
                                            command.Parameters.AddWithValue("@PWD", ComputeStringHash(passwordJson.GetString()));
                                            command.Parameters.AddWithValue("@WARNINGS", (object)preRegisterWarnings ?? DBNull.Value);
                                            command.Parameters.AddWithValue("@LAST_SIGN_LINK", signLink);
                                            command.Parameters.AddWithValue("@LOCALIDAD", localidad);
                                            command.Parameters.AddWithValue("@PROVINCIA", provincia);
                                            command.Parameters.AddWithValue("@DIRECCION", direccion);
                                            command.Parameters.AddWithValue("@CP", cp);
                                            command.Parameters.AddWithValue("@CENTRO", (object)centroId ?? DBNull.Value);

                                            command.ExecuteNonQuery();
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        error = "Error 5082, problema registrando al usuario.";
                                    }
                                }

                                //Prepara el correo
                                if (!failed)
                                {
                                    try
                                    {
                                        string code = ComputeStringHash(email + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()).Substring(0, 8).ToUpper();

                                        Dictionary<string, string> inserts = new Dictionary<string, string>();
                                        inserts["url"] = InstallationConstants.PUBLIC_URL + "/email-activation/" + code;

                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            command.Connection = conn;
                                            command.Transaction = transaction;

                                            command.CommandText = "INSERT INTO email_changes_pending (code, email, candidateId) VALUES (@CODE, @EMAIL, @CANDIDATE_ID)";
                                            command.Parameters.AddWithValue("@CODE", code);
                                            command.Parameters.AddWithValue("@EMAIL", email);
                                            command.Parameters.AddWithValue("@CANDIDATE_ID", id);
                                            int rows = command.ExecuteNonQuery();
                                        }

                                        error = EventMailer.SendEmail(new EventMailer.Email()
                                        {
                                            template = "register",
                                            inserts = inserts,
                                            toEmail = email,
                                            toName = name + " " + surname,
                                            subject = "Bienvenid@ a THINKANDJOB",
                                            priority = EventMailer.EmailPriority.IMMEDIATE
                                        });
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        error = "Error 5081, problema preparando el correo.";
                                    }
                                }

                                if (error != null) failed = true;

                                if (failed)
                                {
                                    transaction.Rollback();
                                    result = new
                                    {
                                        error
                                    };
                                }
                                else
                                {
                                    transaction.Commit();

                                    result = new
                                    {
                                        error = false
                                    };

                                    LogToDB(LogType.CANDIDATE_REGISTERED, "Candidato registrado " + dni, null, conn);
                                }
                            }
                        }
                    }
                }
            }

            return Ok(result);
        }

        //Eliminacion

        [HttpGet]
        [Route(template: "delete/{candidateId}/")]
        public IActionResult Delete(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            if (!HasPermission("Candidates.Delete", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();

                using SqlTransaction transaction = conn.BeginTransaction();

                bool failed = false;
                bool isRegistered = false;
                string candidateDNI = null;

                //Determinar si esta registrado o no
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.Connection = conn;
                    command.Transaction = transaction;


                    command.CommandText = "SELECT dni FROM candidatos " +
                        "WHERE id = @ID";

                    command.Parameters.AddWithValue("@ID", candidateId);

                    try
                    {
                        using SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            isRegistered = true;
                            candidateDNI = reader.GetString(reader.GetOrdinal("dni"));
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5510, no se pudo determinar si el candidato estaba o no registrado."
                        };
                    }
                }

                if (!failed)
                {
                    //Borrarlo
                    if (isRegistered)
                    {
                        //Eliminar sus descargas de documentos
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM category_documents_downloads WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las descargas de documentos del candidato." };
                            }
                        }

                        //Eliminar su codigo de recuperacion
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM recovery_codes WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar los códigos de recuperación de contrseña del candidato." };
                            }
                        }

                        //Eliminar sus entregas
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM emision_cuestionarios WHERE candidatoId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las entregas de cuestionarios del candidato." };
                            }
                        }

                        //Eliminar sus cambios de email pendientes
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM email_changes_pending WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar los cambios de email pendientes del candidato." };
                            }
                        }

                        //Eliminar sus licencias de conducir si tiene
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_driving_license WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las licencias de conducir del candidato." };
                            }
                        }

                        //Eliminar el control horario
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM workshifts WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar el control horario del candidato." };
                            }
                        }

                        //Eliminar sus nominas
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM nominas WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las nóminas del candidato." };
                            }
                        }

                        //Eliminar sus contratos
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM contratos WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar los contratos del candidato." };
                            }
                        }

                        //Eliminar sus incidencias de candidato
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM incidencia_general WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las incidencias genéricas del candidato." };
                            }
                        }

                        //Eliminarlo de sus incidencias de falta de asistencia
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE incidencia_falta_asistencia SET candidateId = NULL WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las incidencias de fanta de asistencia del candidato." };
                            }
                        }

                        //Eliminar sus notas
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_notes WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las notas del candidato." };
                            }
                        }

                        //Eliminar su destinatario en comunicaciones cliente -> candidato
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM comunicaciones_cliente_candidato_destinatarios WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las comunicaciones del candidato." };
                            }
                        }

                        //Eliminar sus asignaciones de horarios
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM horarios_asignacion WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar los horarios del candidato." };
                            }
                        }

                        //Eliminar sus checks
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_checks WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar los checks del candidato." };
                            }
                        }

                        //Eliminar sus revisiones en teletrabajo
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM telework_assignations_tasks WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido borrar las tareas en teletrabajo." };
                            }
                        }

                        //Eliminarlo de los grupos en los que estuviera
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_group_members WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se ha podido desasignar de los grupos de candidatos." };
                            }
                        }

                        //Eliminar sus vacaiones
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_vacaciones WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido eliminar las vacaciones del candidato." };
                            }
                        }

                        //Eliminar sus vacaciones aceptadas
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_vacaciones_aceptadas WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido eliminar las vacaciones aceptadas del candidato." };
                            }
                        }

                        //Eliminar su historico de modelo145
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_modelo145_changes WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido eliminar el histórico del modelo145." };
                            }
                        }

                        //Eliminar sus asignaciones de documentos requeridos por plantilla
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM candidate_doc_template_asignation WHERE candidateId = @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se han podido eliminar las asignaciones a documentos requeridos por plantilla." };
                            }
                        }

                        //Eliminar al candidato
                        if (!failed)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM candidatos WHERE id LIKE @ID";
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    command.ExecuteNonQuery();
                                    DeleteDir(new[] { "candidate", candidateId });
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new { error = "Error 5511, No se han podido borrar al candidato." };
                                }
                            }
                        }
                    }
                    else
                    {
                        //Eliminar al prerregistrado
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "DELETE FROM pre_candidates WHERE id LIKE @ID";
                                command.Parameters.AddWithValue("@ID", candidateId);
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5511, No se ha podido borrar el prerregistro." };
                            }
                        }
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

                    LogToDB(LogType.CANDIDATE_DELETED, "Candidato eliminado " + candidateDNI, FindUsernameBySecurityToken(securityToken, conn), conn);
                }
            }

            return Ok(result);
        }

        //Actualizacion

        [HttpPut]
        //[Route(template: "/")]
        public async Task<IActionResult> UpdateCandidate()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("phone", out JsonElement phoneJson) && json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("oldPass", out JsonElement oldPassJson) && json.TryGetProperty("newPass", out JsonElement newPassJson) &&
                json.TryGetProperty("photo", out JsonElement photoJson) && json.TryGetProperty("cv", out JsonElement cvJson) &&
                json.TryGetProperty("drivingLicenses", out JsonElement drivingLicensesJson) && json.TryGetProperty("direccion", out JsonElement direccionJson) &&
                json.TryGetProperty("localidad", out JsonElement localidadJson) && json.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                json.TryGetProperty("cp", out JsonElement cpJson) && json.TryGetProperty("birth", out JsonElement birthJson) &&
                json.TryGetProperty("dniAnverso", out JsonElement dniAnversoJson) && json.TryGetProperty("dniReverso", out JsonElement dniReversoJson) &&
                json.TryGetProperty("cuentaBancaria", out JsonElement cuentaBancariaJson) && json.TryGetProperty("fotoCuentaBancaria", out JsonElement fotoCuentaBancariaJson) &&
                json.TryGetProperty("numeroSeguridadSocial", out JsonElement numeroSeguridadSocialJson) && json.TryGetProperty("fotoNumeroSeguridadSocial", out JsonElement fotoNumeroSeguridadSocialJson) &&
                json.TryGetProperty("legalRepresentativeConsent", out JsonElement legalRepresentativeConsentJson) && json.TryGetProperty("nacionalidad", out JsonElement nacionalidadJson) &&
                json.TryGetProperty("sexo", out JsonElement sexoJson) && json.TryGetProperty("modelo145", out JsonElement modelo145Json) &&
                json.TryGetProperty("permisoTrabajoCaducidad", out JsonElement permisoTrabajoCaducidadJson) && json.TryGetProperty("fotoPermisoTrabajo", out JsonElement fotoPermisoTrabajoJson) &&
                json.TryGetProperty("fotoDiscapacidad", out JsonElement fotoDiscapacidadJson) && json.TryGetProperty("contactoNombre", out JsonElement contactoNombreJson) &&
                json.TryGetProperty("contactoTelefono", out JsonElement contactoTelefonoJson) && json.TryGetProperty("contactoTipo", out JsonElement contactoTipoJson))
            {
                string phone = phoneJson.GetString();
                string email = emailJson.GetString();
                string oldPass = oldPassJson.GetString();
                string newPass = newPassJson.GetString();
                if (oldPass != null)
                {
                    oldPass = ComputeStringHash(oldPass);
                }
                if (newPass != null)
                {
                    newPass = ComputeStringHash(newPass);
                }
                string photo = photoJson.GetString();
                string cv = cvJson.GetString();
                string localidad = localidadJson.GetString();
                string provincia = provinciaJson.GetString();
                string direccion = direccionJson.GetString();
                string cp = cpJson.GetString();
                DateTime? birth = GetJsonDate(birthJson);
                string dniAnverso = dniAnversoJson.GetString();
                string dniReverso = dniReversoJson.GetString();
                string cuentaBancaria = cuentaBancariaJson.GetString();
                string fotoCuentaBancaria = fotoCuentaBancariaJson.GetString();
                string numeroSeguridadSocial = numeroSeguridadSocialJson.GetString();
                string fotoNumeroSeguridadSocial = fotoNumeroSeguridadSocialJson.GetString();
                string nacionalidad = nacionalidadJson.GetString();
                char? sexo = GetJsonChar(sexoJson);
                DateTime? permisoTrabajoCaducidad = GetJsonDate(permisoTrabajoCaducidadJson);
                string fotoPermisoTrabajo = fotoPermisoTrabajoJson.GetString();
                string fotoDiscapacidad = fotoDiscapacidadJson.GetString();
                string modelo145 = modelo145Json.GetString();
                List<ExtendedCandidateData.DrivingLicense> drivingLicenses = parseDrivingLicenses(drivingLicensesJson);
                ExtendedCandidateData.LegalRepresentativeConsent? legalRepresentativeConsent = parseLegalRepresentativeConsent(legalRepresentativeConsentJson);
                string contactoNombre = contactoNombreJson.GetString();
                string contactoTelefono = contactoTelefonoJson.GetString();
                string contactoTipo = contactoTipoJson.GetString();

                if (drivingLicenses == null && legalRepresentativeConsent == null)
                {
                    return Ok(new
                    {
                        error = "Error 2935, no se pudo procesar la petición por error de serializacion."
                    });
                }

                //Impedir que los candidatos puedan borrar datos
                if ((photo != null && photo.Length == 7) || (cv != null && cv.Length == 7) ||
                    (dniAnverso != null && dniAnverso.Length == 7) || (dniReverso != null && dniReverso.Length == 7) ||
                    (fotoCuentaBancaria != null && fotoCuentaBancaria.Length == 7) || (fotoNumeroSeguridadSocial != null && fotoNumeroSeguridadSocial.Length == 7) ||
                    (fotoPermisoTrabajo != null && fotoPermisoTrabajo.Length == 7) || (legalRepresentativeConsent.Value.autorizacion != null && legalRepresentativeConsent.Value.autorizacion.Length == 7) ||
                    (legalRepresentativeConsent.Value.tutorAnverso != null && legalRepresentativeConsent.Value.tutorAnverso.Length == 7) || (legalRepresentativeConsent.Value.tutorReverso != null && legalRepresentativeConsent.Value.tutorReverso.Length == 7) ||
                    (fotoDiscapacidad != null && fotoDiscapacidad.Length == 7))
                {
                    return Ok(new
                    {
                        error = "Error 4936, no se puede borrar un fichero adjunto."
                    });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {

                        bool failed = false;

                        //Comprobar si el candidato existe y tiene esta contraseña
                        ExtendedCandidateData prevData = new ExtendedCandidateData();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;

                            command.CommandText =
                                       "SELECT C.* " +
                                       "FROM candidatos C WHERE C.id = @ID AND C.pwd LIKE @OLD_PASS";
                            command.Parameters.AddWithValue("@ID", candidateId);
                            command.Parameters.AddWithValue("@OLD_PASS", oldPass);

                            try
                            {
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        prevData.Read(reader);
                                    }
                                    else
                                    {
                                        failed = true;
                                        result = new
                                        {
                                            error = "Error 4510, contraseña incorrecta o usuario no encontrado."
                                        };
                                    }

                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5510, no se pudo recuperar la informacion previa."
                                };
                            }
                        }

                        //Comprobar que no se cambien datos ya establecidos: iban y foto, nss y foto, birth
                        if (!failed)
                        {
                            if ((nacionalidad != null && prevData.nacionalidad != null) ||
                                (sexo != null && prevData.sexo != null) ||
                                (birth != null && prevData.birth != null) ||
                                (cuentaBancaria != null && prevData.cuentaBancaria != null) ||
                                (fotoCuentaBancaria != null && ExistsFile(new[] { "candidate", candidateId, "foto_cuenta_bancaria" })) ||
                                (numeroSeguridadSocial != null && prevData.numeroSeguridadSocial != null) ||
                                (fotoNumeroSeguridadSocial != null && ExistsFile(new[] { "candidate", candidateId, "foto_numero_seguridad_social" })) ||
                                (dniAnverso != null && ExistsFile(new[] { "candidate", candidateId, "dniAnverso" })) ||
                                (dniReverso != null && ExistsFile(new[] { "candidate", candidateId, "dniReverso" })) ||
                                (fotoPermisoTrabajo != null && ExistsFile(new[] { "candidate", candidateId, "foto_permiso_trabajo" })) ||
                                (fotoDiscapacidad != null && ExistsFile(new[] { "candidate", candidateId, "foto_discapacidad" })) ||
                                (legalRepresentativeConsent != null && legalRepresentativeConsent.Value.tutorAnverso != null && ExistsFile(new[] { "candidate", candidateId, "legal_representative_consent", "tutor_anverso" })) ||
                                (legalRepresentativeConsent != null && legalRepresentativeConsent.Value.tutorReverso != null && ExistsFile(new[] { "candidate", candidateId, "legal_representative_consent", "tutor_reverso" })) ||
                                (legalRepresentativeConsent != null && legalRepresentativeConsent.Value.autorizacion != null && ExistsFile(new[] { "candidate", candidateId, "legal_representative_consent", "autorizacion" }))
                            )
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 4936, no se puede sobreescribir un dato vital."
                                };
                            }
                        }

                        UpdateResult updateResult = new UpdateResult();
                        if (!failed)
                        {
                            //Actualizar pwd
                            if (!failed && newPass != null)
                            {
                                //Obtener el correo actual para enviarle una notificacion de cambio de pass
                                string currentEmail = null;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;


                                    command.CommandText = "SELECT email FROM candidatos " +
                                        "WHERE id = @ID";

                                    command.Parameters.AddWithValue("@ID", candidateId);

                                    try
                                    {
                                        currentEmail = (string)command.ExecuteScalar();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new
                                        {
                                            error = "Error 5514, no se pudo obtener el email actual del candidato."
                                        };
                                    }
                                }

                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    try
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText = "UPDATE candidatos SET pwd=@NEW_PWD WHERE id = @ID";
                                        command.Parameters.AddWithValue("@ID", candidateId);
                                        command.Parameters.AddWithValue("@NEW_PWD", newPass);
                                        command.ExecuteNonQuery();

                                        string emailError = EventMailer.SendEmail(new EventMailer.Email()
                                        {
                                            template = "passwordChanged",
                                            toEmail = currentEmail,
                                            subject = "[Think&Job] Contraseña cambiada",
                                            priority = EventMailer.EmailPriority.MODERATE
                                        });

                                        if (emailError != null)
                                        {
                                            failed = true;
                                            result = new
                                            {
                                                error = "Error 5516, " + emailError
                                            };
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new
                                        {
                                            error = "Error 5513, no se pudo actualizar la contraseña."
                                        };
                                    }
                                }
                            }

                            //Actualizar el resto de datos
                            if (!failed)
                            {
                                updateResult = updateCandidateData(conn, transaction, new CandidateStats
                                {
                                    id = candidateId,
                                    phone = phone,
                                    localidad = localidad,
                                    provincia = provincia,
                                    direccion = direccion,
                                    email = email,
                                    cp = cp,
                                    birth = birth,
                                    photo = photo,
                                    cv = cv,
                                    dniAnverso = dniAnverso,
                                    dniReverso = dniReverso,
                                    cuentaBancaria = cuentaBancaria,
                                    fotoCuentaBancaria = fotoCuentaBancaria,
                                    numeroSeguridadSocial = numeroSeguridadSocial,
                                    fotoNumeroSeguridadSocial = fotoNumeroSeguridadSocial,
                                    nacionalidad = nacionalidad,
                                    sexo = sexo,
                                    permisoTrabajoCaducidad = permisoTrabajoCaducidad,
                                    fotoPermisoTrabajo = fotoPermisoTrabajo,
                                    fotoDiscapacidad = fotoDiscapacidad,
                                    modelo145 = modelo145,
                                    drivingLicenses = drivingLicenses,
                                    legalRepresentativeConsent = legalRepresentativeConsent.Value,
                                    centroId = null,
                                    contactoNombre = contactoNombre,
                                    contactoTelefono = contactoTelefono,
                                    contactoTipo = contactoTipo

                                });
                                failed = updateResult.failed;
                                if (updateResult.result != null) result = updateResult.result;
                            }
                        }

                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            transaction.Commit();

                            //Comprobar si tiene todos los datos mínimos
                            result = new
                            {
                                error = false,
                                allDataFilledIn = updateResult.newData.allDataFilledIn,
                                emailSended = email != null,
                                faceUpdateTime = updateResult.faceUpdateTime,
                                faceStatus = updateResult.faceStatus
                            };
                            LogToDB(LogType.CANDIDATE_DATA_MODIFIED_BY_CANDIDATE, "Datos de candidado actualizados " + updateResult.newData.dni, null, conn, transaction);
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPut]
        [Route(template: "stats/{candidateId}/")]
        public async Task<IActionResult> UpdateCandidateStats(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            ResultadoAcceso acceso = HasPermission("Candidates.UpdateStats", securityToken);
            if (!acceso.Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using StreamReader readerBody = new(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("name", out JsonElement nameJson) &&
                    json.TryGetProperty("surname", out JsonElement surnameJson) &&
                    json.TryGetProperty("dni", out JsonElement dniJson) &&
                    json.TryGetProperty("phone", out JsonElement phoneJson) &&
                    json.TryGetProperty("email", out JsonElement emailJson) &&
                    json.TryGetProperty("banned", out JsonElement bannedJson) &&
                    json.TryGetProperty("termsAccepted", out JsonElement termsAcceptedJson) &&
                    json.TryGetProperty("photo", out JsonElement photoJson) &&
                    json.TryGetProperty("localidad", out JsonElement localidadJson) &&
                    json.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                    json.TryGetProperty("direccion", out JsonElement direccionJson) &&
                    json.TryGetProperty("cp", out JsonElement cpJson) &&
                    json.TryGetProperty("birth", out JsonElement birthJson) &&
                    json.TryGetProperty("cv", out JsonElement cvJson) &&
                    json.TryGetProperty("pass", out JsonElement passJson) &&
                    json.TryGetProperty("drivingLicenses", out JsonElement drivingLicensesJson) &&
                    json.TryGetProperty("dniAnverso", out JsonElement dniAnversoJson) &&
                    json.TryGetProperty("dniReverso", out JsonElement dniReversoJson) &&
                    json.TryGetProperty("cuentaBancaria", out JsonElement cuentaBancariaJson) &&
                    json.TryGetProperty("fotoCuentaBancaria", out JsonElement fotoCuentaBancariaJson) &&
                    json.TryGetProperty("numeroSeguridadSocial", out JsonElement numeroSeguridadSocialJson) &&
                    json.TryGetProperty("fotoNumeroSeguridadSocial", out JsonElement fotoNumeroSeguridadSocialJson) &&
                    json.TryGetProperty("legalRepresentativeConsent", out JsonElement legalRepresentativeConsentJson) &&
                    json.TryGetProperty("active", out JsonElement activeJson) &&
                    json.TryGetProperty("paymentBlock", out JsonElement paymentBlockJson) &&
                    json.TryGetProperty("nacionalidad", out JsonElement nacionalidadJson) &&
                    json.TryGetProperty("sexo", out JsonElement sexoJson) &&
                    json.TryGetProperty("modelo145", out JsonElement modelo145Json) &&
                    json.TryGetProperty("permisoTrabajoCaducidad", out JsonElement permisoTrabajoCaducidadJson) &&
                    json.TryGetProperty("fotoPermisoTrabajo", out JsonElement fotoPermisoTrabajoJson) &&
                    json.TryGetProperty("fotoDiscapacidad", out JsonElement fotoDiscapacidadJson) &&
                    json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                    json.TryGetProperty("contactoNombre", out JsonElement contactoNombreJson) &&
                    json.TryGetProperty("contactoTelefono", out JsonElement contactoTelefonoJson) &&
                    json.TryGetProperty("contactoTipo", out JsonElement contactoTipoJson) &&
                    json.TryGetProperty("fechaComienzoTrabajo", out JsonElement fechaComienzoTrabajoJson) &&
                    json.TryGetProperty("test", out JsonElement testJson) &&
                    json.TryGetProperty("cardId", out JsonElement cardIdJson))
                {
                    string name = nameJson.GetString();
                    string surname = surnameJson.GetString();
                    string dni = dniJson.GetString();
                    string phone = phoneJson.GetString();
                    string email = emailJson.GetString();
                    bool? banned = GetJsonBool(bannedJson);
                    bool? termsAccepted = GetJsonBool(termsAcceptedJson);
                    string photo = photoJson.GetString();
                    string localidad = localidadJson.GetString();
                    string provincia = provinciaJson.GetString();
                    string direccion = direccionJson.GetString();
                    string cp = cpJson.GetString();
                    DateTime? birth = GetJsonDate(birthJson);
                    string cv = cvJson.GetString();
                    string pass = passJson.GetString();
                    string dniAnverso = dniAnversoJson.GetString();
                    string dniReverso = dniReversoJson.GetString();
                    string cuentaBancaria = cuentaBancariaJson.GetString();
                    string fotoCuentaBancaria = fotoCuentaBancariaJson.GetString();
                    string numeroSeguridadSocial = numeroSeguridadSocialJson.GetString();
                    string fotoNumeroSeguridadSocial = fotoNumeroSeguridadSocialJson.GetString();
                    bool? active = GetJsonBool(activeJson);
                    bool? paymentBlock = GetJsonBool(paymentBlockJson);
                    string nacionalidad = nacionalidadJson.GetString();
                    char? sexo = GetJsonChar(sexoJson);
                    DateTime? permisoTrabajoCaducidad = GetJsonDate(permisoTrabajoCaducidadJson);
                    string fotoPermisoTrabajo = fotoPermisoTrabajoJson.GetString();
                    string fotoDiscapacidad = fotoDiscapacidadJson.GetString();
                    string modelo145 = modelo145Json.GetString();
                    List<ExtendedCandidateData.DrivingLicense> drivingLicenses = parseDrivingLicenses(drivingLicensesJson);
                    ExtendedCandidateData.LegalRepresentativeConsent? legalRepresentativeConsent = parseLegalRepresentativeConsent(legalRepresentativeConsentJson);
                    string centroId = centroIdJson.GetString();
                    string contactoNombre = contactoNombreJson.GetString();
                    string contactoTelefono = contactoTelefonoJson.GetString();
                    string contactoTipo = contactoTipoJson.GetString();
                    DateTime? fechaComienzoTrabajo = GetJsonDate(fechaComienzoTrabajoJson);
                    bool? test = GetJsonBool(testJson);
                    DateTime? periodoGracia = null;
                    DateTime? fechaFinTrabajo = null;
                    string cardId = cardIdJson.GetString();

                    if (json.TryGetProperty("periodoGracia", out JsonElement periodoGraciaJson))
                    {
                        periodoGracia = GetJsonDate(periodoGraciaJson);
                    }
                    if (json.TryGetProperty("fechaFinTrabajo", out JsonElement fechaFinTrabajoJson))
                    {
                        fechaFinTrabajo = GetJsonDate(fechaFinTrabajoJson);
                    }

                    if (drivingLicenses == null && legalRepresentativeConsent == null)
                    {
                        return Ok(new
                        {
                            error = "Error 2935, no se pudo procesar la petición por error de serializacion."
                        });
                    }

                    //No se pueden aplicar cambios a paymentBlock sin ser jefe
                    if (paymentBlock != null && !acceso.EsJefe)
                    {
                        return Ok(new
                        {
                            error = "Error 1002, Se requiere ser jefe para bloquear o desbloquear pagos."
                        });
                    }


                    using SqlConnection conn = new(CONNECTION_STRING);
                    conn.Open();

                    using SqlTransaction transaction = conn.BeginTransaction();

                    bool failed = false;
                    bool exists = false;

                    //Comprobar si el candidato existe
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;


                        command.CommandText = "SELECT COUNT(*) FROM candidatos " +
                            "WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", candidateId);

                        try
                        {
                            exists = (Int32)command.ExecuteScalar() > 0;
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5510, no se pudo determinar si el usuario existe."
                            };
                        }
                    }

                    UpdateResult updateResult = new();
                    if (!failed)
                    {
                        if (!exists)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 4510, usuario no encontrado."
                            };
                        }
                        else
                        {
                            //Actualizar banned
                            if (!failed && banned != null)
                            {
                                using SqlCommand command = conn.CreateCommand();
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE candidatos " +
                                        "SET banned=@BANNED " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@BANNED", banned == true ? 1 : 0);
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5511, no se pudo actualizar el estado baneado."
                                    };
                                }
                            }

                            //Actualizar active
                            if (!failed && active != null)
                            {
                                using SqlCommand command = conn.CreateCommand();
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE candidatos " +
                                        "SET active=@ACTIVE " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ACTIVE", active == true ? 1 : 0);
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5511, no se pudo actualizar el estado baneado."
                                    };
                                }
                            }

                            //Actualizar pass
                            if (!failed && pass != null)
                            {
                                string hash = ComputeStringHash(pass);
                                using SqlCommand command = conn.CreateCommand();
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE candidatos " +
                                        "SET pwd=@PASS " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@PASS", hash);
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5511, no se pudo actualizar la contraseña."
                                    };
                                }
                            }

                            //Actualizar terms accepted
                            if (!failed && termsAccepted != null)
                            {
                                using SqlCommand command = conn.CreateCommand();
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE candidatos " +
                                        "SET terminosAceptados=@TERMS_ACCEPTED " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@TERMS_ACCEPTED", termsAccepted == true ? 1 : 0);
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5511, no se pudo actualizar la aceptacion de los terminos."
                                    };
                                }
                            }

                            //Actualizar payment block
                            if (!failed && paymentBlock != null)
                            {
                                using SqlCommand command = conn.CreateCommand();
                                try
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE candidatos " +
                                        "SET payment_block=@PAYMENT_BLOCK " +
                                        "WHERE id = @ID";
                                    command.Parameters.AddWithValue("@PAYMENT_BLOCK", paymentBlock == true ? 1 : 0);
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    command.ExecuteNonQuery();
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5511, no se pudo actualizar el bloqueo de pagos."
                                    };
                                }
                            }

                            //Actualizar el resto de datos
                            if (!failed)
                            {
                                updateResult = updateCandidateData(conn, transaction, new CandidateStats()
                                {
                                    id = candidateId,
                                    dni = dni,
                                    name = name,
                                    surname = surname,
                                    phone = phone,
                                    email = email,
                                    localidad = localidad,
                                    provincia = provincia,
                                    direccion = direccion,
                                    cp = cp,
                                    birth = birth,
                                    photo = photo,
                                    cv = cv,
                                    dniAnverso = dniAnverso,
                                    dniReverso = dniReverso,
                                    cuentaBancaria = cuentaBancaria,
                                    fotoCuentaBancaria = fotoCuentaBancaria,
                                    numeroSeguridadSocial = numeroSeguridadSocial,
                                    fotoNumeroSeguridadSocial = fotoNumeroSeguridadSocial,
                                    nacionalidad = nacionalidad,
                                    sexo = sexo,
                                    permisoTrabajoCaducidad = permisoTrabajoCaducidad,
                                    fotoPermisoTrabajo = fotoPermisoTrabajo,
                                    fotoDiscapacidad = fotoDiscapacidad,
                                    modelo145 = modelo145,
                                    drivingLicenses = drivingLicenses,
                                    legalRepresentativeConsent = legalRepresentativeConsent.Value,
                                    centroId = centroId,
                                    contactoNombre = contactoNombre,
                                    contactoTelefono = contactoTelefono,
                                    contactoTipo = contactoTipo,
                                    fechaComienzoTrabajo = fechaComienzoTrabajo,
                                    fechaFinTrabajo = fechaFinTrabajo,
                                    test = test,
                                    periodoGracia = periodoGracia,
                                    cardId = cardId
                                }, false);
                                failed = updateResult.failed;
                                if (updateResult.result != null) result = updateResult.result;
                            }
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
                            emailSended = email != null,
                            updateResult.faceUpdateTime,
                            updateResult.faceStatus
                        };

                        LogToDB(LogType.CANDIDATE_DATA_MODIFIED, "Datos de candidado actualizados por trabajador " + updateResult.newData.dni, FindUsernameBySecurityToken(securityToken, conn), conn);
                    }
                }
            }
            return Ok(result);
        }

        //Acciones extra

        [HttpPatch]
        [Route(template: "change-current-work/{candidateId}/{workId}/")]
        public async Task<IActionResult> ChangeCurrentWork(string candidateId, string workId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };


            if (!HasPermission("Candidates.ChangeCurrentWork", securityToken).Acceso)
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
                        //Comprobar que el trabajo exista y obtener su signLink y centro
                        string signLink = null, centroId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT signLink, centroId FROM trabajos WHERE id = @WORK_ID";
                            command.Parameters.AddWithValue("@WORK_ID", workId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    signLink = reader.GetString(reader.GetOrdinal("signLink"));
                                    centroId = reader.GetString(reader.GetOrdinal("centroId"));
                                }
                            }
                        }

                        if (signLink == null || centroId == null)
                        {
                            transaction.Rollback();
                            return Ok(new { error = "Error 4333, No se ha encontrado el nuevo trabajo" });
                        }

                        //Obtener el trabajo actual para comprobar si coincide
                        string currentWorkId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText =
                                "SELECT T.id FROM candidatos C INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) WHERE C.id = @CANDIDATE_ID";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    currentWorkId = reader.GetString(reader.GetOrdinal("id"));
                        }

                        if (currentWorkId == workId)
                        {
                            transaction.Rollback();
                            return Ok(new { error = "Error 4334, el nuevo trabajo es el actual del candidato" });
                        }

                        //Desmarcar todos los avisos de ceses de este candidato
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText =
                                "UPDATE candidate_checks SET contractFinished = 0 WHERE candidateId = @CANDIDATE_ID AND contractFinished <> 0";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.ExecuteNonQuery();
                        }

                        //Efectuar el cambio (activando al trabajador)
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText =
                                "UPDATE candidatos SET lastSignLink = @SIGN_LINK, centroId = @CENTRO_ID, fechaComienzoTrabajo = getdate(), active = 1 WHERE id = @CANDIDATE_ID";
                            command.Parameters.AddWithValue("@SIGN_LINK", signLink);
                            command.Parameters.AddWithValue("@CENTRO_ID", centroId);
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.ExecuteNonQuery();
                        }

                        //Manejar la situacion
                        HandleCandidateExitWork(candidateId, currentWorkId, conn, transaction);
                        HandleCandidateJoinsWork(candidateId, workId, conn, transaction);

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5333, No se han podido cambiar de trabajo" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "remove-current-work/{candidateId}/")]
        public IActionResult RemoveCurrentWork(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };


            if (!HasPermission("Candidates.RemoveCurrentWork", securityToken).Acceso)
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
                        //Comprobar que el trabajador tenga un trabajo actual
                        string currentWorkId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText =
                                "SELECT T.id FROM candidatos C INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) WHERE C.id = @CANDIDATE_ID";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    currentWorkId = reader.GetString(reader.GetOrdinal("id"));
                        }

                        if (currentWorkId == null)
                        {
                            transaction.Rollback();
                            return Ok(new { error = "Error 4336, el candidato no tiene trabajo actualmente" });
                        }

                        //Desmarcar todos los avisos de ceses de este candidato
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText =
                                "UPDATE candidate_checks SET contractFinished = 0 WHERE candidateId = @CANDIDATE_ID AND contractFinished <> 0";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.ExecuteNonQuery();
                        }

                        //Eliminarle el trabajo actual
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText =
                                "UPDATE candidatos SET lastSignLink = NULL, centroId = NULL WHERE id = @CANDIDATE_ID";
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.ExecuteNonQuery();
                        }

                        //Manejar la situacion
                        HandleCandidateExitWork(candidateId, currentWorkId, conn, transaction);

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5334, No se han podido desasignar el trabajo" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "warning/{candidateId}/")]
        public IActionResult AddWarningPreCandidate(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };


            if (!HasPermission("Candidates.AddWarningPreCandidate", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                bool changed = false;

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "UPDATE pre_candidates SET warnings = warnings + 1, lastWarning = getdate() WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", candidateId);

                    try
                    {
                        int nChanges = command.ExecuteNonQuery();
                        if (nChanges != 1) throw new Exception();
                        changed = true;
                    }
                    catch (Exception)
                    {
                        changed = false;
                        result = new
                        {
                            error = "No se han producido cambios, provablemente ID invalido"
                        };
                    }
                }

                if (changed)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT id, warnings, lastWarning, dni FROM pre_candidates WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", candidateId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    warnings = reader.GetInt32(reader.GetOrdinal("warnings")),
                                    lastWarningDate = reader.GetDateTime(reader.GetOrdinal("lastWarning"))
                                };

                                LogToDB(LogType.CANDIDATE_WARNED, "Candidato prerregistrado avisado para registro " + reader.GetString(reader.GetOrdinal("dni")), FindUsernameBySecurityToken(securityToken, conn), conn);
                            }
                        }
                    }
                }
            }

            return Ok(result);

        }

        [HttpPost]
        [Route(template: "stored-sign/")]
        public async Task<IActionResult> UploadStoredSign()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("sign", out JsonElement signJson))
            {
                string sign = signJson.GetString();
                if (sign == null)
                {
                    return Ok(new
                    {
                        error = "Error 2935, no se pudo procesar la petición por error de serializacion."
                    });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    bool failed = false;

                    //Comprobar si el candidato existe y tiene esta contraseña
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", candidateId);

                        try
                        {
                            if ((Int32)command.ExecuteScalar() == 0)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 4510, candidato no encontrado."
                                };
                            }
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5510, no se pudo determinar si el usuario existe."
                            };
                        }
                    }

                    //Actualizar su ultima fecha de firmado
                    if (!failed)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE candidatos SET lastStoredSign = (getdate()) WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", candidateId);

                            try
                            {
                                command.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5510, no se pudo actualizar la fecha de la firma."
                                };
                            }
                        }
                    }

                    if (!failed)
                    {
                        sign = LimitImageSize(sign, false, 300);
                        SaveFile(new[] { "candidate", candidateId, "stored_sign" }, sign);
                        result = new { error = false };
                    }
                }

            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "activate-email-change/{code}")]
        public IActionResult ActivateEmailChange(string code)
        {
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    bool failed = false;
                    string candidateId = null;
                    string email = null;

                    //Comprobar si el codigo existe
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;


                        command.CommandText = "SELECT email, candidateId FROM email_changes_pending " +
                            "WHERE code = @CODE";

                        command.Parameters.AddWithValue("@CODE", code);

                        try
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    email = reader.GetString(reader.GetOrdinal("email"));
                                    candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                                }
                            }
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5514, no se pudo determinar si el cambio existe o no."
                            };
                        }
                    }

                    if (!failed)
                    {
                        if (candidateId == null)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 4510, la pedición de cambio de email no existe o ya fue realizada."
                            };
                        }
                        else
                        {
                            bool exists = false;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;


                                command.CommandText = "SELECT COUNT(*) FROM candidatos " +
                                    "WHERE email = @EMAIL AND id <> @CANDIDATE_ID";

                                command.Parameters.AddWithValue("@EMAIL", email);
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                                try
                                {
                                    exists = (Int32)command.ExecuteScalar() > 0;
                                }
                                catch (Exception)
                                {
                                    failed = true;
                                    result = new
                                    {
                                        error = "Error 5510, no se pudo determinar si el email es unico."
                                    };
                                }
                            }

                            if (exists)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 4510, ya existe un usuario con este email."
                                };
                            }

                            if (!failed)
                            {
                                //Actualizar el email
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    try
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText = "UPDATE candidatos SET email=@EMAIL, email_verified=1 WHERE id = @ID";
                                        command.Parameters.AddWithValue("@ID", candidateId);
                                        command.Parameters.AddWithValue("@EMAIL", email);
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new
                                        {
                                            error = "Error 5512, no se pudo actualizar el email."
                                        };
                                    }
                                }

                                if (!failed)
                                {
                                    //Borrar los codigos
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        try
                                        {
                                            command.Connection = conn;
                                            command.Transaction = transaction;
                                            command.CommandText = "DELETE FROM email_changes_pending WHERE candidateId = @ID";
                                            command.Parameters.AddWithValue("@ID", candidateId);
                                            command.ExecuteNonQuery();
                                        }
                                        catch (Exception)
                                        {
                                            failed = true;
                                            result = new
                                            {
                                                error = "Error 5512, no se pudo eliminar el codigo de cambio de email usado."
                                            };
                                        }
                                    }
                                }

                                if (!failed)
                                {
                                    result = new { error = false };
                                    LogToDB(LogType.CANDIDATE_EMAIL_CHANGED, "Email de candidato cambiado " + FindDNIbyCandidateId(candidateId, conn, transaction), null, conn, transaction);
                                }

                            }
                        }
                    }

                    if (failed)
                    {
                        transaction.Rollback();
                    }
                    else
                    {
                        transaction.Commit();
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-centros-in-company/")]
        public IActionResult ListCentrosInCompany()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

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
                        result = new { error = "Error 4251, candidato no encontrado" };
                    }
                    else
                    {
                        result = new
                        {
                            error = false,
                            centros = CentroController.listCentros(conn, null, companyId)
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

        [HttpGet]
        [Route(template: "resend-email-verify/{candidateId}/")]
        public IActionResult ResendEmailVerify(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            if (!HasPermission("Candidates.ResendEmailVerify", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    //Buscar el codigo mas reciente del candidato y sus datos
                    string code = null, email = null, name = null, surname = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT ECP.code, ECP.email, C.nombre, C.apellidos " +
                            "FROM email_changes_pending ECP INNER JOIN candidatos C ON(ECP.candidateId = C.id) " +
                            "WHERE C.id = @ID " +
                            "ORDER BY ECP.creationTime DESC";
                        command.Parameters.AddWithValue("@ID", candidateId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                code = reader.GetString(reader.GetOrdinal("code"));
                                email = reader.GetString(reader.GetOrdinal("email"));
                                name = reader.GetString(reader.GetOrdinal("nombre"));
                                surname = reader.GetString(reader.GetOrdinal("apellidos"));
                            }
                        }
                    }

                    if (code == null)
                    {
                        return Ok(new { error = "Error 4082, el candidato no tiene verificaciones de email pendientes." });
                    }

                    //Enviar el email y devolver el codigo
                    Dictionary<string, string> inserts = new Dictionary<string, string>();
                    inserts["url"] = InstallationConstants.PUBLIC_URL + "/email-activation/" + code;
                    string error = EventMailer.SendEmail(new EventMailer.Email()
                    {
                        template = "register",
                        inserts = inserts,
                        toEmail = email,
                        toName = name + " " + surname,
                        subject = "[Think&Job] Activar cambio email",
                        priority = EventMailer.EmailPriority.IMMEDIATE
                    });

                    if (error != null)
                    {
                        result = new { error = "Error 5081, " + error };
                    }
                    else
                    {
                        result = new { error = false, url = inserts["url"] };
                    }
                }

            }
            catch (Exception)
            {
                result = new { error = "Error 5081, problema preparando el correo." };
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("excel-for-client/{companyId}/")]
        public IActionResult DownloadExcelForClient(string companyId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            try
            {
                //Obtener los datos de los candiadatos a los que tiene acceso
                CandidateExcelSheet todos = new CandidateExcelSheet() { name = "Todos", candidates = new() };
                List<CandidateExcelSheet> centros = new() { };
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.dni, C.fechaComienzoTrabajo, C.fechaFinTrabajo, E.nombre as companyName, CE.alias as centroAlias,  CA.name as workName " +
                            "FROM candidatos C " +
                            "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                            "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                            "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                            "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "INNER JOIN client_user_centros CUC ON(CE.id = CUC.centroId) " +
                            "INNER JOIN client_users CU ON(CUC.clientUserId = CU.id) " +
                            "WHERE CU.token = @TOKEN AND E.id = @COMPANY " +
                            "ORDER BY companyName, centroAlias, candidateName";

                        command.Parameters.AddWithValue("@TOKEN", clientToken);
                        command.Parameters.AddWithValue("@COMPANY", companyId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                CandidateStats candidate = new CandidateStats()
                                {
                                    name = reader.GetString(reader.GetOrdinal("candidateName")),
                                    dni = reader.GetString(reader.GetOrdinal("dni")),
                                    fechaComienzoTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo")),
                                    fechaFinTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaFinTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaFinTrabajo")),
                                    companyName = reader.GetString(reader.GetOrdinal("companyName")),
                                    centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                                    workName = reader.GetString(reader.GetOrdinal("workName"))
                                };
                                todos.candidates.Add(candidate);
                                CandidateExcelSheet empresa = centros.FirstOrDefault(c => c.name == candidate.centroAlias);
                                if (empresa.name == null)
                                    centros.Add(new CandidateExcelSheet() { name = candidate.centroAlias, candidates = new() { candidate } });
                                else
                                    empresa.candidates.Add(candidate);
                            }
                        }
                    }
                }

                if (centros.Count > 1)
                    centros.Insert(0, todos);

                //Generar el excel
                IWorkbook workbook = new XSSFWorkbook();
                string tmpDir = GetTemporaryDirectory();
                var thicc = BorderStyle.Medium;
                var thin = BorderStyle.Thin;
                var none = BorderStyle.None;
                var bgBlue = new XSSFColor(new byte[] { 206, 225, 242 });

                //Fuentes
                IFont fontTitle = workbook.CreateFont();
                fontTitle.FontName = "Century Gothic";
                fontTitle.FontHeightInPoints = 14;
                fontTitle.Color = IndexedColors.Black.Index;

                IFont fontNormal = workbook.CreateFont();
                fontNormal.FontName = "Century Gothic";
                fontNormal.FontHeightInPoints = 10;
                fontNormal.Color = IndexedColors.Black.Index;

                //Formatos
                XSSFCellStyle headerStartStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerStartStyle.SetFont(fontTitle);
                headerStartStyle.FillForegroundColorColor = bgBlue;
                headerStartStyle.FillPattern = FillPattern.SolidForeground;
                headerStartStyle.BorderTop = thicc;
                headerStartStyle.BorderBottom = thicc;
                headerStartStyle.BorderRight = none;
                headerStartStyle.BorderLeft = thicc;
                headerStartStyle.Alignment = HorizontalAlignment.Left;
                headerStartStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle headerEndStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerEndStyle.SetFont(fontTitle);
                headerEndStyle.FillForegroundColorColor = bgBlue;
                headerEndStyle.FillPattern = FillPattern.SolidForeground;
                headerEndStyle.BorderTop = thicc;
                headerEndStyle.BorderBottom = thicc;
                headerEndStyle.BorderRight = thicc;
                headerEndStyle.BorderLeft = none;
                headerEndStyle.Alignment = HorizontalAlignment.Left;
                headerEndStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle headerStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                headerStyle.SetFont(fontTitle);
                headerStyle.FillForegroundColorColor = bgBlue;
                headerStyle.FillPattern = FillPattern.SolidForeground;
                headerStyle.BorderTop = thicc;
                headerStyle.BorderBottom = thicc;
                headerStyle.BorderRight = none;
                headerStyle.BorderLeft = none;
                headerStyle.Alignment = HorizontalAlignment.Left;
                headerStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle bodyStartStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyStartStyle.SetFont(fontNormal);
                bodyStartStyle.BorderTop = none;
                bodyStartStyle.BorderBottom = thin;
                bodyStartStyle.BorderRight = none;
                bodyStartStyle.BorderLeft = thin;
                bodyStartStyle.Alignment = HorizontalAlignment.Left;
                bodyStartStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle bodyEndStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyEndStyle.SetFont(fontNormal);
                bodyEndStyle.BorderTop = none;
                bodyEndStyle.BorderBottom = thin;
                bodyEndStyle.BorderRight = thin;
                bodyEndStyle.BorderLeft = none;
                bodyEndStyle.Alignment = HorizontalAlignment.Left;
                bodyEndStyle.VerticalAlignment = VerticalAlignment.Center;

                XSSFCellStyle bodyStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                bodyStyle.SetFont(fontNormal);
                bodyStyle.BorderTop = none;
                bodyStyle.BorderBottom = thin;
                bodyStyle.BorderRight = none;
                bodyStyle.BorderLeft = none;
                bodyStyle.Alignment = HorizontalAlignment.Left;
                bodyStyle.VerticalAlignment = VerticalAlignment.Center;

                foreach (CandidateExcelSheet centro in centros)
                {
                    //Hojas
                    ISheet sheet = workbook.CreateSheet(centro.name);

                    //Tamaños de filas y columnas
                    ICell cell;
                    IRow row;
                    sheet.SetColumnWidth(1, 11 * 256);
                    sheet.SetColumnWidth(2, 45 * 256);
                    sheet.SetColumnWidth(3, 40 * 256);
                    sheet.SetColumnWidth(4, 45 * 256);
                    sheet.SetColumnWidth(5, 40 * 256);
                    sheet.SetColumnWidth(6, 30 * 256);
                    sheet.SetColumnWidth(7, 30 * 256);

                    //Escribir cabecera
                    row = sheet.CreateRow(1);
                    row.HeightInPoints = 22;
                    cell = row.CreateCell(1);
                    cell.CellStyle = headerStartStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("DNI");
                    cell = row.CreateCell(2);
                    cell.CellStyle = headerStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("NOMBRE");
                    cell = row.CreateCell(3);
                    cell.CellStyle = headerStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("EMPRESA");
                    cell = row.CreateCell(4);
                    cell.CellStyle = headerStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("CENTRO DE TRABAJO");
                    cell = row.CreateCell(5);
                    cell.CellStyle = headerStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("PUESTO");
                    cell = row.CreateCell(6);
                    cell.CellStyle = headerStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("INCORPORACIÓN");
                    cell = row.CreateCell(7);
                    cell.CellStyle = headerEndStyle;
                    cell.SetCellType(CellType.String);
                    cell.SetCellValue("FINALIZACIÓN");

                    //Escribir las incidencias
                    int r = 2;

                    foreach (CandidateStats candidate in centro.candidates)
                    {
                        row = sheet.CreateRow(r);

                        //Poner los datos del candidato
                        cell = row.CreateCell(1);
                        cell.CellStyle = bodyStartStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(candidate.dni.ToUpper());
                        cell = row.CreateCell(2);
                        cell.CellStyle = bodyStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(candidate.name.ToUpper());
                        cell = row.CreateCell(3);
                        cell.CellStyle = bodyStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(candidate.companyName.ToUpper());
                        cell = row.CreateCell(4);
                        cell.CellStyle = bodyStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(candidate.centroAlias.ToUpper());
                        cell = row.CreateCell(5);
                        cell.CellStyle = bodyStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(candidate.workName.ToUpper());
                        cell = row.CreateCell(6);
                        cell.CellStyle = bodyStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(candidate.fechaComienzoTrabajo == null ? "" : candidate.fechaComienzoTrabajo.Value.ToString("dd/MM/yyyy"));
                        cell = row.CreateCell(7);
                        cell.CellStyle = bodyEndStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(candidate.fechaFinTrabajo == null ? "" : candidate.fechaFinTrabajo.Value.ToString("dd/MM/yyyy"));

                        r++;
                    }

                    //Filtros
                    sheet.SetAutoFilter(new CellRangeAddress(1, r - 1, 1, 7));
                }
                workbook.SetActiveSheet(0);

                //Guardado
                //sheet.ProtectSheet("1234"); //No protejer porque entonces nos e podria usar el filtro y orden
                string fileName = "Trabajadores.xlsx";
                string tmpFile = Path.Combine(tmpDir, fileName);
                FileStream file = new FileStream(tmpFile, FileMode.Create);
                workbook.Write(file);
                file.Close();

                string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                HttpContext.Response.ContentType = contentType;
                FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(tmpFile), contentType)
                {
                    FileDownloadName = fileName
                };

                Directory.Delete(tmpDir, true);

                return response;
            }
            catch (Exception)
            {
            }

            return new NoContentResult();
        }

        //Logs

        [HttpGet]
        [Route(template: "log/{onlyNew}/{page?}/{type?}")]
        public IActionResult ListLogs(bool onlyNew, int? page, string? type)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            return Ok(SysAdminController.listLogsFiltered(securityToken, page, type, false, onlyNew));
        }

        [HttpGet]
        [Route(template: "log/count-new/{type?}")]
        public IActionResult CountNewLogs(int? page, string? type)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            return Ok(SysAdminController.countNewLogsFiltered(securityToken, page, type, false));
        }

        [HttpGet]
        [Route(template: "log/mark-all-as-read/")]
        public IActionResult MarkAllLogsAsRead()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            try
            {
                using SqlConnection conn = new(CONNECTION_STRING);
                conn.Open();
                using SqlCommand command = conn.CreateCommand();
                /*command.CommandText =
                        "SELECT COUNT(*) FROM [logs] " +
                        "WHERE (@TYPE IS NULL OR type = @TYPE) AND " +
                        "((@SYSADMIN = 1 AND LEFT(type, 10) <> 'CANDIDATE_') OR (@SYSADMIN = 0 AND LEFT(type, 10) = 'CANDIDATE_')) AND " +
                        "readed = 0 ";*/
                command.CommandText = "UPDATE logs SET readed = 1 WHERE readed = 0";
                command.ExecuteNonQuery();
                return Ok(new { error = false });
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 2932, no se pudo procesar la petición." });
            }
        }

        //Modelo145

        [HttpPost]
        [Route(template: "modelo145/")]
        public async Task<IActionResult> GenerateModelo145()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            string modelo145String = null;

            if (json.TryGetProperty("modelo145", out JsonElement modelo145Json))
            {
                modelo145String = modelo145Json.ToString();
            }

            string contentType = "application/pdf";
            HttpContext.Response.ContentType = contentType;
            byte[] bytes = generateModelo145(candidateId, modelo145String);
            if (bytes == null) return new NoContentResult();
            var response = new FileContentResult(bytes, contentType)
            {
                FileDownloadName = "Modelo145.pdf"
            };

            return response;
        }

        [HttpGet]
        [Route(template: "modelo145-historico/")]
        public IActionResult GetModelo145Historico()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new { error = "Error 2932, no se pudo procesar la petición." };

            try
            {
                List<object> historico = new();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT * FROM candidate_modelo145_changes WHERE candidateId = @CANDIDATE ORDER BY [date] ASC";

                        command.Parameters.AddWithValue("@CANDIDATE", candidateId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                historico.Add(new
                                {
                                    modelo145 = reader.GetString(reader.GetOrdinal("modelo145")),
                                    date = reader.GetDateTime(reader.GetOrdinal("date"))
                                });
                            }
                        }
                    }

                }
                result = new { error = false, historico };

            }
            catch (Exception)
            {
                result = new { error = "Error 5089, no se ha podido obtener el histórico del modelo 145." };
            }

            return Ok(result);
        }

        //Notas de candidato

        [HttpPost]
        [Route(template: "api/v1/candidates/{candidateId}/note/")]
        public async Task<IActionResult> CreateNote(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            if (!HasPermission("Candidates.CreateNote", securityToken).Acceso)
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
                                    "INSERT INTO candidate_notes (candidateId, username, text) " +
                                    "VALUES (@CANDIDATE_ID, @USERNAME, @TEXT)";

                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
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
        [Route(template: "{candidateId}/note/{noteId}/")]
        public async Task<IActionResult> EditNote(string candidateId, string noteId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            if (!HasPermission("Candidates.EditNote", securityToken).Acceso)
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
                                    "UPDATE candidate_notes SET text = @TEXT, editdate = getdate() WHERE id = @ID AND candidateId = @CANDIDATE_ID";

                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
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
        [Route(template: "{candidateId}/note/{noteId}/")]
        public IActionResult DeleteNote(string candidateId, string noteId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            if (!HasPermission("Candidates.DeleteNote", securityToken).Acceso)
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
                                "DELETE FROM candidate_notes WHERE id = @NOTE_ID AND candidateId = @CANDIDATE_ID";

                            command.Parameters.AddWithValue("@NOTE_ID", noteId);
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

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
        [Route(template: "{candidateId}/note/")]
        public IActionResult ListNotes(string candidateId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición."
            };

            if (!HasPermission("Candidates.ListNotes", securityToken).Acceso)
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
                                "SELECT * FROM candidate_notes WHERE candidateId = @CANDIDATE_ID ORDER BY [date] DESC";

                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                List<CandidateNote> notes = new List<CandidateNote>();

                                while (reader.Read())
                                {
                                    notes.Add(new CandidateNote()
                                    {
                                        id = reader.GetInt32(reader.GetOrdinal("id")),
                                        username = reader.GetString(reader.GetOrdinal("username")),
                                        text = reader.GetString(reader.GetOrdinal("text")),
                                        date = reader.GetDateTime(reader.GetOrdinal("date")),
                                        editdate = reader.IsDBNull(reader.GetOrdinal("editdate")) ? null : reader.GetDateTime(reader.GetOrdinal("editdate"))
                                    });
                                }

                                result = new { error = false, notes = notes };
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


        //Actualizacion mediante automatizador

        [HttpPost]
        [Route(template: "update-automated/")]
        public async Task<IActionResult> UpdateAutomated()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se pudo procesar la petición." };

            if (!HasPermission("Candidates.UpdateAutomated", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            List<CandidateStats> candidatesToRegister = new();
            List<string> candidatesToStopWorking = new();
            List<Tuple<string, string, string, DateTime>> candidatesToChangeWork = new();

            HashSet<string> centrosNotFound = new(), puestosNotFound = new();
            HashSet<Tuple<string, string>> emailsYaRegistrados = new();
            int nRegistered = 0, nStopWorking = 0, nUpdateWorking = 0;

            try
            {
                //Parsear la lista de candiatos recibida
                List<UpdaterRow> rows = new();
                foreach (JsonElement rowJson in json.EnumerateArray())
                {
                    if (rowJson.TryGetProperty("dni", out JsonElement dniJson) &&
                        rowJson.TryGetProperty("name", out JsonElement nameJson) &&
                        rowJson.TryGetProperty("email", out JsonElement emailJson) &&
                        rowJson.TryGetProperty("phone", out JsonElement phoneJson) &&
                        rowJson.TryGetProperty("cp", out JsonElement cpJson) &&
                        rowJson.TryGetProperty("localidad", out JsonElement localidadJson) &&
                        rowJson.TryGetProperty("provincia", out JsonElement provinciaJson) &&
                        rowJson.TryGetProperty("direccion", out JsonElement direccionJson) &&
                        rowJson.TryGetProperty("centro", out JsonElement centroJson) &&
                        rowJson.TryGetProperty("puesto", out JsonElement puestoJson) &&
                        rowJson.TryGetProperty("alta", out JsonElement altaJson) &&
                        rowJson.TryGetProperty("baja", out JsonElement bajaJson))
                    {
                        DateTime? alta = GetJsonDate(altaJson);
                        if (alta == null) continue;
                        rows.Add(new UpdaterRow()
                        {
                            dni = dniJson.GetString(),
                            name = nameJson.GetString(),
                            email = emailJson.GetString(),
                            phone = phoneJson.GetString(),
                            cp = cpJson.GetString(),
                            localidad = localidadJson.GetString(),
                            provincia = provinciaJson.GetString(),
                            direccion = direccionJson.GetString(),
                            centro = centroJson.GetString(),
                            puesto = puestoJson.GetString(),
                            alta = alta.Value,
                            baja = GetJsonDate(bajaJson)
                        });
                    }
                }

                //Obtener una lista de DNIs registrados (Que no sean de pruebas)
                HashSet<string> registered = new();
                Dictionary<string, string> working = new();
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT dni, lastSignLink, active FROM candidatos WHERE test = 0";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string dni = reader.GetString(reader.GetOrdinal("dni"));
                                registered.Add(dni);
                                if (!reader.IsDBNull(reader.GetOrdinal("lastSignLink")) && reader.GetInt32(reader.GetOrdinal("active")) == 1)
                                    working[dni] = reader.GetString(reader.GetOrdinal("lastSignLink"));
                            }
                        }
                    }

                    List<string> noRegistrados = rows.Where(r => !working.ContainsKey(r.dni)).Select(r => r.dni).ToList();

                    //Buscar los puestos de trabajo de los candidatos
                    Dictionary<string, string> centrosReferencias = new();
                    Dictionary<string, string> puestosCache = new();

                    //Obtener la equivalencia de referencia -> centroId
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id, referenciaExterna FROM centros WHERE referenciaExterna IS NOT NULL";
                        using (SqlDataReader reader = command.ExecuteReader())
                            while (reader.Read())
                                centrosReferencias[reader.GetString(reader.GetOrdinal("referenciaExterna"))] = reader.GetString(reader.GetOrdinal("id"));
                    }

                    //Convertir las referencias de los centros a ids y los nombres de puestos a signLinks
                    rows = rows.Select(row =>
                    {
                        string reference = row.centro;
                        if (centrosReferencias.ContainsKey(row.centro))
                        {
                            row.centro = centrosReferencias[row.centro];
                        }
                        else
                        {
                            centrosNotFound.Add(row.centro);
                            row.centro = null;
                            row.puesto = null;
                            return row;
                        }

                        string combination = $"{row.centro}-{row.puesto}";
                        if (!puestosCache.ContainsKey(combination))
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT T.signLink FROM trabajos T INNER JOIN categories CA ON(T.categoryId = CA.id) WHERE T.centroId = @CENTRO AND (CA.name = @CATEGORY OR CA.details LIKE @CATEGORY)";
                                command.Parameters.AddWithValue("@CENTRO", row.centro);
                                command.Parameters.AddWithValue("@CATEGORY", row.puesto);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                        puestosCache[combination] = reader.GetString(reader.GetOrdinal("signLink"));
                                    else
                                    {
                                        puestosCache[combination] = null;
                                        puestosNotFound.Add($"{reference}|{row.puesto}");
                                    }
                                }
                            }
                        }
                        row.puesto = puestosCache[combination];

                        if (row.puesto != null && registered.Contains(row.dni) && (!working.ContainsKey(row.dni) || working[row.dni] != row.puesto))
                            candidatesToChangeWork.Add(new Tuple<string, string, string, DateTime>(row.dni, row.centro, row.puesto, row.alta));

                        return row;
                    }).ToList();

                    //Buscar los candidatos nuevos
                    DateTime now = DateTime.Now;
                    candidatesToRegister = rows.Where(row => !registered.Contains(row.dni)).Select(row =>
                    {
                        if (row.puesto == null)
                            return null;
                        if (row.baja != null && row.baja < now)
                            return null;
                        if (!row.name.Contains(", "))
                            return null;
                        string[] nameParts = row.name.Split(", ");
                        return new CandidateStats()
                        {
                            dni = row.dni,
                            name = nameParts[1],
                            surname = nameParts[0],
                            email = row.email,
                            phone = row.phone,
                            cp = row.cp,
                            localidad = Constants.validateLocalidad(row.localidad, conn, null, row.cp),
                            provincia = Constants.validateProvincia(row.provincia, conn, null, row.cp),
                            direccion = row.direccion,
                            date = row.alta,
                            centroId = row.centro,
                            signLink = row.puesto
                        };
                    }).Where(row => row != null).ToList();

                    //Buscar candidatos a los que quitarles el trabajo
                    candidatesToStopWorking = working.Keys.Where(dni => !rows.Any(r => r.dni == dni)).ToList();
                }
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5097, problema procesando candidatos." });
            }

            //Console.WriteLine("Register: " + candidatesToRegister.Count);
            //Console.WriteLine("Change: " + candidatesToChangeWork.Count);
            //Console.WriteLine(string.Join(", ", candidatesToChangeWork.Select(c => $"{c.Item1} {c.Item2} {c.Item3}").ToArray()));
            //Console.WriteLine("Stop: " + candidatesToStopWorking.Count);
            //return Json(new { error = false, centrosNotFound, puestosNotFound, nRegistered, nStopWorking, nUpdateWorking });

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                //Registrar a los nuevos candidatos
                foreach (CandidateStats candidate in candidatesToRegister)
                {
                    try
                    {
                        //Comprobar si el email esta en uso
                        string emailUnique = CheckEMAILunique(candidate.email, null, conn);
                        if (emailUnique != null)
                        {
                            emailsYaRegistrados.Add(new(candidate.email, emailUnique));
                            continue;
                        }


                        //Comprobar si tiene warnings
                        int? preRegisterWarnings = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT warnings FROM pre_candidates WHERE dni like @DNI";
                            command.Parameters.AddWithValue("@DNI", candidate.dni);
                            using (SqlDataReader reader = command.ExecuteReader())
                                if (reader.Read())
                                    preRegisterWarnings = reader.GetInt32(reader.GetOrdinal("warnings"));
                        }

                        //Borrar al precandidato, si existe
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM pre_candidates WHERE dni = @DNI";
                            command.Parameters.AddWithValue("@DNI", candidate.dni); ;
                            command.ExecuteNonQuery();
                        }

                        //Fixear el telefono
                        if (!candidate.phone.StartsWith("+") && candidate.phone.Contains(" "))
                            candidate.phone = candidate.phone.Replace(" ", "");

                        //Insertar al candidato
                        string id = ComputeStringHash(candidate.dni + candidate.name + candidate.surname + candidate.email + candidate.phone + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        string pwd = CreatePassword();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "INSERT INTO candidatos (id, nombre, apellidos, dni, email, telefono, pwd, warnings, lastSignLink, terminosAceptados, localidad, provincia, direccion, cp, centroId, fechaComienzoTrabajo) " +
                                "VALUES (@ID, @NAME, @SURNAME, @DNI, @EMAIL, @PHONE, @PWD, @WARNINGS, @LAST_SIGN_LINK, 1, @LOCALIDAD, @PROVINCIA, @DIRECCION, @CP, @CENTRO, @DATE)";

                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@NAME", candidate.name);
                            command.Parameters.AddWithValue("@SURNAME", candidate.surname);
                            command.Parameters.AddWithValue("@DNI", candidate.dni);
                            command.Parameters.AddWithValue("@EMAIL", candidate.email);
                            command.Parameters.AddWithValue("@PHONE", candidate.phone);
                            command.Parameters.AddWithValue("@PWD", ComputeStringHash(pwd));
                            command.Parameters.AddWithValue("@WARNINGS", (object)preRegisterWarnings ?? DBNull.Value);
                            command.Parameters.AddWithValue("@LAST_SIGN_LINK", (object)candidate.signLink ?? DBNull.Value);
                            command.Parameters.AddWithValue("@LOCALIDAD", (object)candidate.localidad ?? DBNull.Value);
                            command.Parameters.AddWithValue("@PROVINCIA", (object)candidate.provincia ?? DBNull.Value);
                            command.Parameters.AddWithValue("@DIRECCION", candidate.direccion);
                            command.Parameters.AddWithValue("@CP", candidate.cp);
                            command.Parameters.AddWithValue("@CENTRO", (object)candidate.centroId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@DATE", candidate.date);

                            command.ExecuteNonQuery();
                        }

                        //Solicitar la activación del email
                        string code = ComputeStringHash(candidate.email + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()).Substring(0, 8).ToUpper();

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO email_changes_pending (code, email, candidateId) VALUES (@CODE, @EMAIL, @CANDIDATE_ID)";
                            command.Parameters.AddWithValue("@CODE", code);
                            command.Parameters.AddWithValue("@EMAIL", candidate.email);
                            command.Parameters.AddWithValue("@CANDIDATE_ID", id);
                            int rows = command.ExecuteNonQuery();
                        }

                        EventMailer.SendEmail(new EventMailer.Email()
                        {
                            template = "registerAutomatic",
                            inserts = new() { { "url", InstallationConstants.PUBLIC_URL + "/email-activation/" + code }, { "user", candidate.dni }, { "pwd", pwd } },
                            toEmail = candidate.email,
                            toName = candidate.name + " " + candidate.surname,
                            subject = "Bienvenid@ a THINKANDJOB",
                            priority = EventMailer.EmailPriority.SLOWLANE
                        });

                        LogToDB(LogType.CANDIDATE_REGISTERED, "Candidato auto-registrado " + candidate.dni, null, conn);

                        nRegistered++;
                    }
                    catch (Exception)
                    {
                    }
                }

                //Quitarle el trabajo (No realmente, solo los desactiva)
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "UPDATE candidatos SET active = 0 WHERE dni = @DNI";
                    command.Parameters.Add("@DNI", System.Data.SqlDbType.VarChar);
                    foreach (string dni in candidatesToStopWorking)
                    {
                        try
                        {
                            command.Parameters["@DNI"].Value = dni;
                            command.ExecuteNonQuery();
                            nStopWorking++;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                //Actualizar el trabajo
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "UPDATE candidatos SET centroId = @CENTRO, lastSignLink = @SIGN_LINK, fechaComienzoTrabajo = @FECHA, active = 1 WHERE dni = @DNI";
                    command.Parameters.Add("@DNI", System.Data.SqlDbType.VarChar);
                    command.Parameters.Add("@CENTRO", System.Data.SqlDbType.VarChar);
                    command.Parameters.Add("@SIGN_LINK", System.Data.SqlDbType.VarChar);
                    command.Parameters.Add("@FECHA", System.Data.SqlDbType.DateTime);
                    foreach (Tuple<string, string, string, DateTime> change in candidatesToChangeWork)
                    {
                        try
                        {
                            command.Parameters["@DNI"].Value = change.Item1;
                            command.Parameters["@CENTRO"].Value = change.Item2;
                            command.Parameters["@SIGN_LINK"].Value = change.Item3;
                            command.Parameters["@FECHA"].Value = change.Item4;
                            command.ExecuteNonQuery();
                            nUpdateWorking++;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            result = new { error = false, centrosNotFound, puestosNotFound, emailsYaRegistrados, nRegistered, nStopWorking, nUpdateWorking };

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "update-automated-vto/")]
        public async Task<IActionResult> UpdateAutomatedVTO()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se pudo procesar la petición." };

            if (!HasPermission("Candidates.UpdateAutomatedVTO", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            List<Tuple<string, DateTime>> candidates = new();
            int nUpdated = 0;

            try
            {
                //Parsear la lista de candiatos recibida
                foreach (JsonElement rowJson in json.EnumerateArray())
                {
                    if (rowJson.TryGetProperty("dni", out JsonElement dniJson) &&
                        rowJson.TryGetProperty("vto", out JsonElement vtoJson))
                    {
                        DateTime? vto = GetJsonDate(vtoJson);
                        if (vto == null) continue;
                        candidates.Add(new(dniJson.GetString(), vto.Value));
                    }
                }
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5097, problema procesando candidatos." });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                //Actualizar el VTO
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "UPDATE candidatos SET permiso_trabajo_caducidad = @VTO WHERE dni = @DNI AND (permiso_trabajo_caducidad IS NULL OR permiso_trabajo_caducidad <> @VTO)";
                    command.Parameters.Add("@DNI", System.Data.SqlDbType.VarChar);
                    command.Parameters.Add("@VTO", System.Data.SqlDbType.Date);
                    foreach (Tuple<string, DateTime> candidate in candidates)
                    {
                        try
                        {
                            command.Parameters["@DNI"].Value = candidate.Item1;
                            command.Parameters["@VTO"].Value = candidate.Item2;
                            nUpdated += command.ExecuteNonQuery();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            result = new { error = false, nUpdated };

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "update-automated-baja/")]
        public async Task<IActionResult> UpdateAutomatedBaja()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se pudo procesar la petición." };

            if (!HasPermission("Candidates.UpdateAutomatedBaja", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            Dictionary<string, DateTime?> fechaBaja = new();
            List<Tuple<string, DateTime?>> rows = new();

            int nUpdated = 0;

            try
            {
                //Parsear la lista de candiatos recibida
                foreach (JsonElement rowJson in json.EnumerateArray())
                {
                    if (rowJson.TryGetProperty("dni", out JsonElement dniJson) &&
                        rowJson.TryGetProperty("baja", out JsonElement bajaJson))
                    {
                        rows.Add(new(dniJson.GetString(), GetJsonDate(bajaJson)));
                    }
                }
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5097, problema procesando candidatos." });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT dni, fechaFinTrabajo FROM candidatos";
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fechaBaja[reader.GetString(reader.GetOrdinal("dni"))] = reader.IsDBNull(reader.GetOrdinal("fechaFinTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaFinTrabajo"));
                        }
                    }
                }

                //Buscar candidatos a los que actualizarles la fecha de baja
                rows = rows.Where(row => fechaBaja.ContainsKey(row.Item1) && (fechaBaja[row.Item1].HasValue != row.Item2.HasValue || (fechaBaja[row.Item1].HasValue && row.Item2.HasValue && fechaBaja[row.Item1].Value != row.Item2))).ToList();

                //Actualizar el fechaFinTrabajo
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "UPDATE candidatos SET fechaFinTrabajo = @BAJA WHERE dni = @DNI";
                    command.Parameters.Add("@DNI", System.Data.SqlDbType.VarChar);
                    command.Parameters.Add("@BAJA", System.Data.SqlDbType.Date);
                    foreach (Tuple<string, DateTime?> candidate in rows)
                    {
                        try
                        {
                            command.Parameters["@DNI"].Value = candidate.Item1;
                            command.Parameters["@BAJA"].Value = candidate.Item2.HasValue ? candidate.Item2.Value : DBNull.Value;
                            command.ExecuteNonQuery();
                            nUpdated++;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            result = new { error = false, nUpdated };

            return Ok(result);
        }

        //Contadores rapidos

        [HttpGet]
        [Route(template: "get-resumen/")]
        public IActionResult GetResunen()
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    //Obtener si esta baneado y los datos sobre su trabajos
                    bool banned, allDataFilledIn;
                    string workId, workName, categoryId;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT C.banned, T.id as workId, CA.name as categoryName, CA.id as categoryId, C.allDataFilledIn " +
                            "FROM candidatos C " +
                            "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                            "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "WHERE C.id = @ID";
                        command.Parameters.AddWithValue("@ID", candidateId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                banned = reader.GetInt32(reader.GetOrdinal("banned")) == 1;
                                allDataFilledIn = reader.GetInt32(reader.GetOrdinal("allDataFilledIn")) == 1;
                                workId = reader.GetString(reader.GetOrdinal("workId"));
                                workName = reader.GetString(reader.GetOrdinal("categoryName"));
                                categoryId = reader.GetString(reader.GetOrdinal("categoryId"));
                            }
                            else
                            {
                                return Ok(new { error = "Error 4861, no se ha encontrado al candidato o no tiene trabajo" });
                            }
                        }
                    }

                    if (banned)
                        return Ok(new { error = false, banned });

                    CandidateResumen resumen = new()
                    {
                        pendingAccountData = allDataFilledIn ? 0 : 1
                    };

                    if (allDataFilledIn)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT " +
                                "prlTests = sum(case when F.tipo = 'PRL' then 1 else 0 end), " +
                                "trainingTests = sum(case when F.tipo = 'TRAINING' then 1 else 0 end), " +
                                "pendingPrlTests = sum(case when F.tipo = 'PRL' AND EC.id IS NULL then 1 else 0 end), " +
                                "pendingTrainingTests = sum(case when F.tipo = 'TRAINING' AND EC.id IS NULL then 1 else 0 end) " +
                                "FROM ( " +
                                "SELECT VTF.formularioId FROM vinculos_trabajos_formularios VTF WHERE VTF.trabajoId = @WORK " +
                                "UNION " +
                                "SELECT VCF.formularioId FROM vinculos_categorias_formularios VCF WHERE VCF.categoryId = @CATEGORY " +
                                ") VF INNER JOIN formularios F ON(F.id = VF.formularioId) " +
                                "LEFT OUTER JOIN emision_cuestionarios EC ON(EC.formularioId = F.id AND EC.trabajoId = @WORK AND EC.candidatoId = @CANDIDATE) ";
                            command.Parameters.AddWithValue("@WORK", workId);
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.Parameters.AddWithValue("@CANDIDATE", candidateId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                reader.Read();
                                resumen.prlTests = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                resumen.trainingTests = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                resumen.pendingPrlTests = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                resumen.pendingTrainingTests = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                            }
                        }

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT " +
                                "prlDocs = sum(case when CD.type = 'PRL' then 1 else 0 end), " +
                                "trainingDocs = sum(case when CD.type = 'TRAINING' then 1 else 0 end), " +
                                "pendingPrlDocs = sum(case when CD.type = 'PRL' AND CDD.candidateId IS NULL then 1 else 0 end), " +
                                "pendingTrainingDocs = sum(case when CD.type = 'TRAINING' AND CDD.candidateId IS NULL then 1 else 0 end) " +
                                "FROM category_documents CD " +
                                "LEFT OUTER JOIN category_documents_downloads CDD ON(CD.id = CDD.documentId AND CDD.candidateId = @CANDIDATE) " +
                                "WHERE CD.trabajoid = @WORK OR CD.categoryId = @CATEGORY ";
                            command.Parameters.AddWithValue("@WORK", workId);
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.Parameters.AddWithValue("@CANDIDATE", candidateId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                reader.Read();
                                resumen.prlDocs = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                resumen.trainingDocs = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                resumen.pendingPrlDocs = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                resumen.pendingTrainingDocs = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                            }
                        }

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT " +
                                "pendingContratos = (SELECT COUNT(*) FROM cdocuments WHERE candidateId = @CANDIDATE AND downloadDate is null AND category IN('CLVencimiento', 'CBajaVoluntaria', 'CLProrroga', 'OrdenServicio', 'CJCategoria', 'CLCSustitucion', 'CLCSustitucionParcial', 'CLTraIndefinido', 'CLCFijoDiscontinuo', 'CLCEventual'))," +
                                "pendingNominas = (SELECT COUNT(*) FROM cdocuments c WHERE c.candidateId = @CANDIDATE AND c.downloadDate is null AND category IN ('Nomina', 'Finiquito'))," +
                                "pendingSeguridad = (SELECT COUNT(*) FROM cdocuments c WHERE c.candidateId = @CANDIDATE AND c.downloadDate is null AND category IN ('ExamenDeSalud','FichaPrevencionRiesgos'))," +
                                "pendingCertif = (SELECT COUNT(*) FROM cdocuments c WHERE c.candidateId = @CANDIDATE AND c.downloadDate is null AND category IN ('CertINEM','CertIRPF'))," +
                                "pendingComunicaciones = (SELECT COUNT(*) FROM comunicaciones_cliente_candidato CCC INNER JOIN comunicaciones_cliente_candidato_destinatarios CCCD ON(CCC.id = CCCD.messageId) WHERE CCCD.candidateId = @CANDIDATE AND CCCD.new = 1), " +
                                "pendingDocs = (SELECT COUNT(*) FROM candidate_docs WHERE candidateId = @CANDIDATE AND (seenDate IS NULL OR canSign = 1)) + " +
                                "(SELECT COUNT(*) FROM candidate_doc_template CDT INNER JOIN candidate_doc_template_batch CDTB ON(CDT.id = CDTB.templateId) INNER JOIN candidate_doc_template_asignation CDTA ON(CDTB.id = CDTA.batchId) WHERE candidateId = @CANDIDATE AND (CDTA.seenDate IS NULL OR CDTA.canSign = 1)), " +
                                "pendingIncidenciasNotAttend = (SELECT COUNT(*) FROM incidencia_falta_asistencia I WHERE I.candidateId = @CANDIDATE AND (I.state = 'pendiente' OR I.state = 'pendiente-candidato' OR I.hasCandidateUnread = 1)), " +
                                "pendingIncidenciasGeneric = (SELECT COUNT(*) FROM incidencia_general I WHERE I.candidateId = @CANDIDATE AND I.hasCandidateUnread = 1)," +
                                "pendingHorarioWeeks = (SELECT COUNT(*) FROM horarios_asignacion HA WHERE HA.candidateId = @CANDIDATE AND HA.seen = 0)," +
                                "hasControlHorario = (SELECT COUNT(*) FROM ccontrol_horario CH WHERE CH.candidateId = @CANDIDATE AND MONTH(CH.uploadDate) = MONTH(GETDATE()) AND YEAR(CH.uploadDate) = YEAR(GETDATE())) ";

                            command.Parameters.AddWithValue("@WORK", workId);
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.Parameters.AddWithValue("@CANDIDATE", candidateId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                reader.Read();
                                resumen.pendingContratos = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                resumen.pendingNominas = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                resumen.pendingSeguridad = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                resumen.pendingCertif = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                                resumen.pendingComunicaciones = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                                resumen.pendingDocs = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                                resumen.pendingIncidencias = (reader.IsDBNull(6) ? 0 : reader.GetInt32(6)) + (reader.IsDBNull(6) ? 0 : reader.GetInt32(6));
                                resumen.pendingHorarioWeeks = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
                                resumen.hasControlHorario = reader.IsDBNull(9) ? 0 : reader.GetInt32(9);
                                resumen.pendingDocumentos = resumen.pendingNominas + resumen.pendingContratos + resumen.pendingSeguridad + resumen.pendingCertif + resumen.pendingDocs;

                            }
                        }
                    }

                    if (resumen.pendingTrainingDocs > 0) resumen.pendingTrainingTests = 0;
                    if (resumen.pendingPrlDocs > 0) resumen.pendingPrlTests = 0;

                    resumen.pendingTraining = resumen.pendingTrainingDocs + resumen.pendingTrainingTests;
                    resumen.pendingPrl = resumen.pendingPrlDocs + resumen.pendingPrlTests;

                    result = new { error = false, banned, resumen };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5869, no se ha podido obtener un resumen del estado" };
                }
            }

            return Ok(result);
        }

        //Otros

        [HttpGet]
        [Route("check-signlink/{signlink}")]
        public IActionResult CheckSignlink(string signlink)
        {
            object result = new
            {
                error = "Error 2932, no se pudo procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM trabajos WHERE signLink = @SIGNLINK";
                    command.Parameters.AddWithValue("@SIGNLINK", signlink);
                    result = new { error = false, exists = (int)command.ExecuteScalar() > 0 };
                }

            }

            return Ok(result);
        }





        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //Ayuda
        public struct CandidateNote
        {
            public int id { get; set; }
            public string username { get; set; }
            public string text { get; set; }
            public DateTime date { get; set; }
            public DateTime? editdate { get; set; }
        }
        public struct UpdateResult
        {
            public bool failed { get; set; }
            public object result { get; set; }
            public CandidateStats newData { get; set; }
            public int? faceUpdateTime { get; set; }
            public int? faceStatus; //0: Not found, 1: Rotated, 2: Perfect
        }
        public struct MinimalCandidateData
        {
            public string id { get; set; }
            public string name { get; set; }
            public string dni { get; set; }
            public string phone { get; set; }
            public string email { get; set; }
            public bool cesionActiva { get; set; }
        }
        public struct CandidateExcelSheet
        {
            public string name { get; set; }
            public List<CandidateStats> candidates { get; set; }
        }
        public struct UpdaterRow
        {
            public string dni { get; set; }
            public string name { get; set; }
            public string email { get; set; }
            public string phone { get; set; }
            public string cp { get; set; }
            public string localidad { get; set; }
            public string provincia { get; set; }
            public string direccion { get; set; }
            public string centro { get; set; }
            public string puesto { get; set; }
            public DateTime alta { get; set; }
            public DateTime? baja { get; set; }
        }
        public struct CandidateResumen
        {
            public int prlDocs { get; set; }
            public int prlTests { get; set; }
            public int trainingDocs { get; set; }
            public int trainingTests { get; set; }
            public int pendingTraining { get; set; }
            public int pendingPrl { get; set; }
            public int pendingPrlDocs { get; set; }
            public int pendingPrlTests { get; set; }
            public int pendingTrainingDocs { get; set; }
            public int pendingTrainingTests { get; set; }
            public int pendingContratos { get; set; }
            public int pendingNominas { get; set; }
            public int pendingSeguridad { get; set; }
            public int pendingCertif { get; set; }
            public int pendingDocumentos { get; set; }
            public int pendingIncidencias { get; set; }
            public int pendingComunicaciones { get; set; }
            public int pendingDocs { get; set; }
            public int pendingAccountData { get; set; }
            public int pendingHorarioWeeks { get; set; }
            public int hasControlHorario { get; set; }


        }

        public struct CLSimpleWorkerInfo
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public string dni { get; set; }
            public DateTime? fechaComienzoTrabajo { get; set; }
            public DateTime? fechaFinTrabajo { get; set; }
            public string work { get; set; }
            public string groupName { get; set; }
            public bool cesionActiva { get; set; }
            public string centroId { get; set; }
            public string empresaId { get; set; }

        }

        public struct MultiSelectorCandidateData
        {
            public string id { get; set; }
            public string dni { get; set; }
            public string fullName { get; set; }
            public string category { get; set; }
            public string email { get; set; }
            public bool fullProfile { get; set; }
            public bool cesionActiva { get; set; }
            public string companyName { get; set; }
            public string companyId { get; set; }
            public string centerName { get; set; }
            public string centerId { get; set; }
            public bool selected { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static List<CandidateStats> listFiltered(string key, string provincia, string localidad, string companyId, string centroId, int? cp, int? marginCP, bool? tieneTrabajo, string faceStatus, string cesionStatus, int page, int perpage, string sort = "ORDER BY [date] DESC")
        {
            calculateCPrange(cp, marginCP, out int? startCP, out int? endCP);
            calculateFaceStatus(faceStatus, out bool? hasPhoto, out bool? hasFace);
            calculateCesionStatus(cesionStatus, out bool? cesion);

            List<CandidateStats> candidates = new List<CandidateStats>();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT C.*, " +
                        "CE.alias as centroAlias, " +
                        "T.id as idTrabajo, " +
                        "CA.name as nombreTrabajo, " +
                        "E.id as idEmpresa, " +
                        "E.nombre as nombreEmpresa, " +
                        "documentsDownloaded = C.calc_documents_downloaded, " +
                        "pendingEmail = null " +
                        "FROM candidatos C " +
                        "LEFT OUTER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                        "LEFT OUTER JOIN categories CA ON(T.categoryId = CA.id) " +
                        "LEFT OUTER JOIN centros CE ON(T.centroId = CE.id) " +
                        "LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                        "WHERE (@PROVINCIA IS NULL OR @PROVINCIA LIKE C.provincia) AND " +
                        "(@LOCALIDAD IS NULL OR @LOCALIDAD LIKE C.localidad) AND " +
                        "(@COMPANY IS NULL OR E.id = @COMPANY) AND " +
                        "(@CENTRO IS NULL OR C.centroId = @CENTRO) AND " +
                        "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY) AND " +
                        "(@START_CP IS NULL OR (ISNUMERIC(C.cp) = 1 AND @START_CP <= CAST(C.cp AS int) AND @END_CP >= CAST(C.cp AS int))) AND " +
                        "(@TIENE_TRABAJO IS NULL OR (@TIENE_TRABAJO = 1 AND T.id IS NOT NULL) OR (@TIENE_TRABAJO = 0 AND T.id IS NULL)) AND " +
                        "(@HAS_PHOTO IS NULL OR @HAS_PHOTO = C.hasPhoto) AND (@HAS_FACE IS NULL OR @HAS_FACE = C.hasFace) AND " +
                        "(@CESION IS NULL OR @CESION = C.cesionActiva) " +
                        sort + " " +
                        "OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY";


                    command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@PROVINCIA", ((object)provincia) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@LOCALIDAD", ((object)localidad) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY", ((object)companyId) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@START_CP", ((object)startCP) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@END_CP", ((object)endCP) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@TIENE_TRABAJO", tieneTrabajo == null ? DBNull.Value : (tieneTrabajo.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@HAS_PHOTO", hasPhoto == null ? DBNull.Value : (hasPhoto.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@HAS_FACE", hasFace == null ? DBNull.Value : (hasFace.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@OFFSET", page * perpage);
                    command.Parameters.AddWithValue("@LIMIT", perpage);
                    command.Parameters.AddWithValue("@CESION", cesion == null ? DBNull.Value : (cesion.Value ? 1 : 0));

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CandidateStats candidate = new CandidateStats();
                            candidate.Read(reader);
                            candidates.Add(candidate);
                        }
                    }
                }
            }

            return candidates;
        }

        public static int listFilteredCount(string key, string provincia, string localidad, string companyId, string centroId, int? cp, int? marginCP, bool? tieneTrabajo, string faceStatus, string cesionStatus)
        {
            calculateCPrange(cp, marginCP, out int? startCP, out int? endCP);
            calculateFaceStatus(faceStatus, out bool? hasPhoto, out bool? hasFace);
            calculateCesionStatus(cesionStatus, out bool? cesion);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT COUNT(*) " +
                        "FROM candidatos C " +
                        "LEFT OUTER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                        "LEFT OUTER JOIN categories CA ON(T.categoryId = CA.id) " +
                        "LEFT OUTER JOIN centros CE ON(T.centroId = CE.id) " +
                        "LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                        "WHERE (@PROVINCIA IS NULL OR @PROVINCIA LIKE C.provincia) AND " +
                        "(@LOCALIDAD IS NULL OR @LOCALIDAD LIKE C.localidad) AND " +
                        "(@COMPANY IS NULL OR E.id = @COMPANY) AND " +
                        "(@CENTRO IS NULL OR C.centroId = @CENTRO) AND " +
                        "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI OR C.dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY) AND " +
                        "(@START_CP IS NULL OR (ISNUMERIC(C.cp) = 1 AND @START_CP <= CAST(C.cp AS int) AND @END_CP >= CAST(C.cp AS int))) AND " +
                        "(@TIENE_TRABAJO IS NULL OR (@TIENE_TRABAJO = 1 AND T.id IS NOT NULL) OR (@TIENE_TRABAJO = 0 AND T.id IS NULL)) AND " +
                        "(@HAS_PHOTO IS NULL OR @HAS_PHOTO = C.hasPhoto) AND (@HAS_FACE IS NULL OR @HAS_FACE = C.hasFace) AND " +
                        "(@CESION IS NULL OR @CESION = C.cesionActiva)";




                    command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@PROVINCIA", ((object)localidad) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@LOCALIDAD", ((object)provincia) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY", ((object)companyId) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@START_CP", ((object)startCP) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@END_CP", ((object)endCP) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@TIENE_TRABAJO", tieneTrabajo == null ? DBNull.Value : (tieneTrabajo.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@HAS_PHOTO", hasPhoto == null ? DBNull.Value : (hasPhoto.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@HAS_FACE", hasFace == null ? DBNull.Value : (hasFace.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@CESION", cesion == null ? DBNull.Value : (cesion.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@TODAY", DateTime.Now.Date);
                    return (int)command.ExecuteScalar();
                }
            }
        }
        public static List<CLSimpleWorkerInfo> listForClient(string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<CLSimpleWorkerInfo> candidates = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.dni, C.fechaComienzoTrabajo, C.fechaFinTrabajo, C.cesionActiva, CA.name as work, CG.name as groupName " +
                    "FROM candidatos C " +
                    "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                    "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                    "LEFT OUTER JOIN candidate_group_members CGM ON(CGM.candidateId = C.id) " +
                    "LEFT OUTER JOIN candidate_groups CG ON(CGM.groupId = CG.id) " +
                    "WHERE (@CENTRO IS NULL OR C.centroId = @CENTRO) " +
                    "ORDER BY candidateName";

                command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string id = reader.GetString(reader.GetOrdinal("id"));
                        candidates.Add(new CLSimpleWorkerInfo
                        {
                            id = id,
                            nombre = reader.GetString(reader.GetOrdinal("candidateName")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            fechaComienzoTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo")).Date,
                            fechaFinTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaFinTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaFinTrabajo")).Date,
                            work = reader.GetString(reader.GetOrdinal("work")),
                            groupName = reader.IsDBNull(reader.GetOrdinal("groupName")) ? null : reader.GetString(reader.GetOrdinal("groupName")),
                            cesionActiva = reader.GetInt32(reader.GetOrdinal("cesionActiva")) == 1
                        });
                    }
                }
            }
            return candidates;
        }

        private static void calculateCPrange(int? cp, int? marginCP, out int? startCP, out int? endCP)
        {
            if (cp != null && marginCP != null)
            {
                int lowerCP = (int)cp % 1000;
                int upperCP = (int)cp - lowerCP;

                startCP = Math.Max(lowerCP - (int)marginCP, 0);
                endCP = Math.Min(lowerCP + (int)marginCP, 999);

                startCP += upperCP;
                endCP += upperCP;
            }
            else
            {
                startCP = null;
                endCP = null;
            }
        }

        private static void calculateFaceStatus(string faceStatus, out bool? hasPhoto, out bool? hasFace)
        {
            switch (faceStatus)
            {
                case "noPhoto":
                    hasPhoto = false;
                    hasFace = null;
                    break;
                case "photoNoFace":
                    hasPhoto = true;
                    hasFace = false;
                    break;
                case "photo":
                    hasPhoto = true;
                    hasFace = null;
                    break;
                case "face":
                    hasPhoto = true;
                    hasFace = true;
                    break;
                default:
                    hasPhoto = null;
                    hasFace = null;
                    break;
            }
        }

        private static void calculateCesionStatus(string cesionStatus, out bool? cesion)
        {
            switch (cesionStatus)
            {
                case "todos":
                    cesion = null;
                    break;
                case "activo":
                    cesion = true;
                    break;
                case "noActivo":
                    cesion = false;
                    break;
                default:
                    cesion = null;
                    break;
            }
        }

        public static UpdateResult updateCandidateData(SqlConnection conn, SqlTransaction transaction, CandidateStats stats, bool updatedByCandidate = true)
        {
            bool failed = false;
            object result = null;
            int? faceUpdateTime = null;
            int? faceStatus = null;

            //Actualizar name
            if (!failed && stats.name != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET nombre=@NAME WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@NAME", stats.name);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el nombre."
                    };
                }
            }

            //Actualizar surname
            if (!failed && stats.surname != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos " +
                        "SET apellidos=@SURNAME " +
                        "WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@SURNAME", stats.surname);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el apellido."
                    };
                }
            }

            //Actualizar dni
            if (!failed && stats.dni != null)
            {
                //Comprobar si ya existe un candidato con ese dni
                string dniUsedBy = CheckDNINIECIFunique(stats.dni, stats.id, conn, transaction);
                if (dniUsedBy != null)
                {
                    string extraInfo = updatedByCandidate ? "" : $" por {dniUsedBy}";
                    failed = true;
                    result = new
                    {
                        error = $"Error 4511, el dni {stats.dni} ya está en uso{extraInfo}."
                    };
                }

                if (!failed)
                {
                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET dni=@DNI WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@DNI", stats.dni);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar el dni."
                        };
                    }
                }
            }

            //Actualizar telefono
            if (!failed && stats.phone != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET telefono=@PHONE WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@PHONE", stats.phone);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el telefono."
                    };
                }
            }

            //Actualizar email
            if (!failed && stats.email != null)
            {
                //Comprobar si ya existe un candidato con ese email
                string emailUsedBy = CheckEMAILunique(stats.email, stats.id, conn, transaction);
                if (emailUsedBy != null)
                {
                    string extraInfo = updatedByCandidate ? "" : $" por {emailUsedBy}";
                    failed = true;
                    result = new
                    {
                        error = $"Error 4511, el email {stats.email} ya está en uso{extraInfo}."
                    };
                }

                //Comprobar que no sea igual al que ya tiene
                bool isTheSame = true;
                try
                {
                    using SqlCommand command = conn.CreateCommand();
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE id = @CANDIDATE_ID AND email = @EMAIL";
                    command.Parameters.AddWithValue("@EMAIL", stats.email);
                    command.Parameters.AddWithValue("@CANDIDATE_ID", stats.id);
                    isTheSame = (int)command.ExecuteScalar() == 1;
                }
                catch (Exception)
                {
                }

                if (!failed && !isTheSame)
                {
                    string code = ComputeStringHash(stats.id + stats.email + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())[..8].ToUpper();

                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "INSERT INTO email_changes_pending (code, email, candidateId) VALUES (@CODE, @EMAIL, @CANDIDATE_ID)";
                        command.Parameters.AddWithValue("@CODE", code);
                        command.Parameters.AddWithValue("@EMAIL", stats.email);
                        command.Parameters.AddWithValue("@CANDIDATE_ID", stats.id);
                        int rows = command.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            Dictionary<string, string> inserts = new()
                            {
                                ["url"] = InstallationConstants.PUBLIC_URL + "/email-activation/" + code
                            };
                            string errorEmail = EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "emailChange",
                                inserts = inserts,
                                toEmail = stats.email,
                                subject = "[Think&Job] Activar cambio email",
                                priority = EventMailer.EmailPriority.IMMEDIATE
                            });
                            if (errorEmail != null)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5515, " + errorEmail
                                };
                            }
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5512, no se pudo crear la peticion para actualizar el email."
                        };
                    }
                }
            }

            //Actualizar localidad
            if (!failed && stats.localidad != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET localidad=@LOCALIDAD WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@LOCALIDAD", stats.localidad.Length == 0 ? DBNull.Value : stats.localidad); ;
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la localidad."
                    };
                }
            }

            //Actualizar provincia
            if (!failed && stats.provincia != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET provincia=@PROVINCIA WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@PROVINCIA", stats.provincia.Length == 0 ? DBNull.Value : stats.provincia);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la provincia."
                    };
                }
            }

            //Actualizar direccion
            if (!failed && stats.direccion != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET direccion=@DIRECCION WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@DIRECCION", stats.direccion.Length == 0 ? DBNull.Value : stats.direccion);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la direccion."
                    };
                }
            }

            //Actualizar cp
            if (!failed && stats.cp != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET cp=@CP WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@CP", stats.cp.Length == 0 ? DBNull.Value : stats.cp);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la direccion."
                    };
                }
            }

            //Actualizar birth
            if (!failed && stats.birth != null)
            {
                if (ExtendedCandidateData.CheckAgeValid(stats.birth))
                {
                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET nacimiento=@BIRTH WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@BIRTH", stats.birth.Value.Date == DateTime.MinValue.Date ? DBNull.Value : stats.birth.Value);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar la fecha de nacimiento."
                        };
                    }
                }
                else
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 4511, edad no admitida."
                    };
                }
            }

            //Actualizar photo
            if (!failed && stats.photo != null)
            {
                if (stats.photo.Length == 7)
                {
                    DeleteFile(new[] { "candidate", stats.id, "photo" });
                }
                else
                {
                    string photo = LimitSquareImage(stats.photo, true);
                    SaveFile(new[] { "candidate", stats.id, "photo" }, photo);
                }
            }


            //Actualizar cv
            if (!failed && stats.cv != null)
            {
                if (stats.cv.Length == 7)
                {
                    DeleteFile(new[] { "candidate", stats.id, "cv" });
                }
                else
                {
                    SaveFile(new[] { "candidate", stats.id, "cv" }, stats.cv);
                }
            }

            //Actualizar foto anverso del dni
            if (!failed && stats.dniAnverso != null)
            {
                if (stats.dniAnverso.Length == 7)
                {
                    DeleteFile(new[] { "candidate", stats.id, "dniAnverso" });
                }
                else
                {
                    string dniAnverso = LimitImageSize(stats.dniAnverso);
                    SaveFile(new[] { "candidate", stats.id, "dniAnverso" }, dniAnverso);
                }
            }

            //Actualizar foto reverso del dni
            if (!failed && stats.dniReverso != null)
            {
                if (stats.dniReverso.Length == 7)
                {
                    DeleteFile(new[] { "candidate", stats.id, "dniReverso" });
                }
                else
                {
                    string dniReverso = LimitImageSize(stats.dniReverso);
                    SaveFile(new[] { "candidate", stats.id, "dniReverso" }, dniReverso);
                }
            }

            //Actualizar Licencias de conducir
            if (!failed && stats.drivingLicenses != null)
            {
                foreach (ExtendedCandidateData.DrivingLicense license in stats.drivingLicenses)
                {
                    if (!failed)
                    {
                        using SqlCommand command = conn.CreateCommand();
                        try
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM candidate_driving_license WHERE candidateId = @ID AND type = @TYPE";
                            command.Parameters.AddWithValue("@ID", stats.id);
                            command.Parameters.AddWithValue("@TYPE", license.type);
                            command.ExecuteNonQuery();
                            DeleteDir(new[] { "candidate", stats.id, "driving_license", license.type });
                        }
                        catch (Exception)
                        {
                            failed = true;
                            result = new
                            {
                                error = "Error 5518, no se han podido eliminar las licencias de conducir antiguas."
                            };
                        }
                    }

                    if (!failed && license.anverso.Length != 7)
                    {
                        string licensePhotoAnverso = LimitImageSize(license.anverso);
                        string licensePhotoReverso = LimitImageSize(license.reverso);

                        if (!failed)
                        {
                            using SqlCommand command = conn.CreateCommand();
                            try
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "INSERT INTO candidate_driving_license (candidateId, type, expiration) VALUES (@ID, @TYPE, @EXPIRATION)";
                                command.Parameters.AddWithValue("@ID", stats.id);
                                command.Parameters.AddWithValue("@TYPE", license.type);
                                command.Parameters.AddWithValue("@EXPIRATION", license.expiration);
                                command.ExecuteNonQuery();
                                SaveFile(new[] { "candidate", stats.id, "driving_license", license.type, "anverso" }, licensePhotoAnverso);
                                SaveFile(new[] { "candidate", stats.id, "driving_license", license.type, "reverso" }, licensePhotoReverso);
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new
                                {
                                    error = "Error 5518, no se pudo insertar una imagen nueva."
                                };
                            }
                        }
                    }
                }
            }

            //Actualizar cuenta bancaria
            if (!failed && stats.cuentaBancaria != null)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET cuenta_bancaria=@CUENTA_BANCARIA WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@CUENTA_BANCARIA", stats.cuentaBancaria.Length == 0 ? DBNull.Value : stats.cuentaBancaria);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar la cuenta bancaria."
                        };
                    }
                }
                LogToDB(LogType.CANDIDATE_IBAN_CHANGE, "Cuanta bancaria actualizada " + FindDNIbyCandidateId(stats.id) + " " + stats.cuentaBancaria, null, conn, transaction);
            }

            //Actualizar foto cuenta bancaria
            if (!failed && stats.fotoCuentaBancaria != null)
            {
                if (stats.fotoCuentaBancaria.Length == 7)
                {
                    DeleteFile(new[] { "candidate", stats.id, "foto_cuenta_bancaria" });
                }
                else
                {
                    string fotoCuentaBancaria = LimitImageSize(stats.fotoCuentaBancaria);
                    SaveFile(new[] { "candidate", stats.id, "foto_cuenta_bancaria" }, fotoCuentaBancaria);
                }
            }

            //Actualizar numero de la seguridad social
            if (!failed && stats.numeroSeguridadSocial != null)
            {
                //Comprobar si ya existe un candidato con ese numero de la seguridad social
                bool used = false;
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.Connection = conn;
                    command.Transaction = transaction;


                    command.CommandText = "SELECT COUNT(*) FROM candidatos " +
                        "WHERE id <> @ID AND numero_seguridad_social = @NUMERO_SEGURIDAD_SOCIAL";

                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@NUMERO_SEGURIDAD_SOCIAL", stats.numeroSeguridadSocial);

                    try
                    {
                        used = (Int32)command.ExecuteScalar() > 0;
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5510, no se pudo determinar si el numero de la seguridad social es unico."
                        };
                    }
                }

                if (!failed && used && stats.numeroSeguridadSocial.Length != 0)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 4511, el numero de la seuridad social introducido ya está en uso."
                    };
                }

                if (!failed)
                {
                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET numero_seguridad_social=@NUMERO_SEGURIDAD_SOCIAL WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@NUMERO_SEGURIDAD_SOCIAL", stats.numeroSeguridadSocial.Length == 0 ? DBNull.Value : stats.numeroSeguridadSocial);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar el numero de la seguridad social."
                        };
                    }
                }
            }

            //Actualizar foto numero seguridad social
            if (!failed && stats.fotoNumeroSeguridadSocial != null)
            {
                if (stats.fotoNumeroSeguridadSocial.Length == 7)
                {
                    DeleteFile(new[] { "candidate", stats.id, "foto_numero_seguridad_social" });
                }
                else
                {
                    string fotoNumeroSeguridadSocial = LimitImageSize(stats.fotoNumeroSeguridadSocial);
                    SaveFile(new[] { "candidate", stats.id, "foto_numero_seguridad_social" }, fotoNumeroSeguridadSocial);
                }
            }

            //Actualizar Consentimineto legal del representante
            if (!failed)
            {
                if (stats.legalRepresentativeConsent.tutorAnverso != null && stats.legalRepresentativeConsent.tutorReverso != null)
                {
                    DeleteFile(new[] { "candidate", stats.id, "legal_representative_consent", "tutor_anverso" });
                    DeleteFile(new[] { "candidate", stats.id, "legal_representative_consent", "tutor_reverso" });
                    if (stats.legalRepresentativeConsent.tutorAnverso.Length != 7 && stats.legalRepresentativeConsent.tutorReverso.Length != 7)
                    {
                        string tutorAnverso = LimitImageSize(stats.legalRepresentativeConsent.tutorAnverso);
                        string tutorReverso = LimitImageSize(stats.legalRepresentativeConsent.tutorReverso);
                        SaveFile(new[] { "candidate", stats.id, "legal_representative_consent", "tutor_anverso" }, tutorAnverso);
                        SaveFile(new[] { "candidate", stats.id, "legal_representative_consent", "tutor_reverso" }, tutorReverso);
                    }
                }

                if (stats.legalRepresentativeConsent.autorizacion != null)
                {
                    if (stats.legalRepresentativeConsent.autorizacion != null)
                    {
                        DeleteFile(new[] { "candidate", stats.id, "legal_representative_consent", "autorizacion" });
                        if (stats.legalRepresentativeConsent.autorizacion.Length != 7)
                        {
                            string autorizacion = LimitImageSize(stats.legalRepresentativeConsent.autorizacion);
                            SaveFile(new[] { "candidate", stats.id, "legal_representative_consent", "autorizacion" }, autorizacion);
                        }
                    }
                }
            }

            //Actualizar nacionalidad
            if (!failed && stats.nacionalidad != null)
            {
                if (stats.nacionalidad.Length == 0 || Constants.checkPaisIso3Exists(stats.nacionalidad, conn, transaction))
                {
                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET nacionalidad=@NACIONALIDAD WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@NACIONALIDAD", stats.nacionalidad.Length == 0 ? DBNull.Value : stats.nacionalidad);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar la nacionalidad."
                        };
                    }
                }
                else
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 4511, no se ha encontrado al pais con codigo: " + stats.nacionalidad + "."
                    };
                }
            }

            //Actualizar sexo
            if (!failed && stats.sexo != null)
            {
                if (stats.sexo.Value == 'F' || stats.sexo.Value == 'M' || stats.sexo.Value == ' ')
                {
                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET sexo=@SEXO WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@SEXO", stats.sexo.Value == ' ' ? DBNull.Value : stats.sexo);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar la nacionalidad."
                        };
                    }
                }
                else
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 4511, sexo invalido."
                    };
                }
            }

            //Actualizar caducidad del permiso de trabajo
            if (!failed && stats.permisoTrabajoCaducidad != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET permiso_trabajo_caducidad=@CADUCIDAD WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@CADUCIDAD", stats.permisoTrabajoCaducidad.Value.Date == DateTime.MinValue.Date ? DBNull.Value : stats.permisoTrabajoCaducidad.Value);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la caducidad del permiso de trabajo."
                    };
                }
            }

            //Actualizar modelo145
            if (!failed && stats.modelo145 != null)
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET modelo145=@MODELO WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@MODELO", stats.modelo145);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar el modelo 145."
                        };
                    }
                }

                if (!failed)
                {
                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "INSERT INTO candidate_modelo145_changes (candidateId, modelo145, [date]) VALUES (@CANDIDATE, @MODELO, @NOW)";
                        command.Parameters.AddWithValue("@CANDIDATE", stats.id);
                        command.Parameters.AddWithValue("@MODELO", stats.modelo145);
                        command.Parameters.AddWithValue("@NOW", DateTime.Now);
                        command.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar el histórico del modelo145."
                        };
                    }
                }
            }

            //Actualizar centro de trabajo
            if (!failed && stats.centroId != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET centroId=@CENTRO WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@CENTRO", stats.centroId);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el centro de trabajo."
                    };
                }
            }

            //Actualizar centro de trabajo
            if (!failed && stats.centroId != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET centroId=@CENTRO WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@CENTRO", stats.centroId);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el centro de trabajo."
                    };
                }
            }

            //Actualizar Contacto Nombre
            if (!failed && stats.contactoNombre != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET contactoNombre=@CONTACTO_NOMBRE WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@CONTACTO_NOMBRE", stats.contactoNombre.Length == 0 ? DBNull.Value : stats.contactoNombre);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el nombre del contacto."
                    };
                }

            }

            //Actualizar número de tarjeta de identificación (cardId)
            if (!failed && stats.cardId != null)
            {
                //Comprobar si ya existe un candidato con esa tarjeta de identificación
                string cardIdUsedBy = checkCARDIDunique(stats.cardId, stats.id, conn, transaction);
                if (cardIdUsedBy != null)
                {
                    string extraInfo = updatedByCandidate ? "" : $" por {cardIdUsedBy}";
                    failed = true;
                    result = new
                    {
                        error = $"Error 4511, la tarjeta de identificación {stats.cardId} ya está en uso{extraInfo}."
                    };
                }
                if (!failed)
                {
                    using SqlCommand command = conn.CreateCommand();
                    try
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE candidatos SET cardId=@CARDID WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", stats.id);
                        command.Parameters.AddWithValue("@CARDID", stats.cardId);
                        command.ExecuteNonQuery();

                        // Si el centro al que pertenece este candidato tiene asociado un dispositivo anviz, lo eliminamos del dispositivo
                        // y lo añadimos de nuevo para que se actualice su tarjeta de identificación.
                        List<string> deviceIds = FindRFDevicesbyCandidateId(stats.id, conn, transaction);
                        if (deviceIds.Count > 0)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT C.* FROM candidatos C WHERE C.id = @ID";
                            using SqlDataReader reader = command.ExecuteReader();
                            while (reader.Read())
                            {
                                foreach (string deviceId in deviceIds)
                                {
                                    string idd = Dni2idd(reader.GetString(reader.GetOrdinal("dni")));
                                    AnvizTools.InsertRemoveTask(deviceId, idd);
                                    if (stats.cardId != "-1")
                                    {
                                        // Si su cardid es distinto de null, le establecemos su pass como los últimos 4 dígitos de su cardid.
                                        // Si cardid es null, simplemente pass también será null.
                                        AnvizTools.InsertRegisterTask(deviceId, new()
                                        {
                                            rjId = reader.GetString(reader.GetOrdinal("id")),
                                            dni = reader.GetString(reader.GetOrdinal("dni")),
                                            idd = idd,
                                            name = reader.GetString(reader.GetOrdinal("dni")),
                                            cardid = reader.IsDBNull(reader.GetOrdinal("cardId")) ? null : reader.GetInt32(reader.GetOrdinal("cardId")).ToString(),
                                            pass = reader.IsDBNull(reader.GetOrdinal("cardId")) ? null : reader.GetInt32(reader.GetOrdinal("cardId")).ToString()[4..],
                                            identity_type = AnvizTools.GetAnvizIdentityType(deviceId)
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        failed = true;
                        result = new
                        {
                            error = "Error 5511, no se pudo actualizar el nombre del contacto."
                        };
                    }
                }
            }

            //Actualizar Contacto Telefono
            if (!failed && stats.contactoTelefono != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET contactoTelefono=@CONTACTO_TELEFONO WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@CONTACTO_TELEFONO", stats.contactoTelefono.Length == 0 ? DBNull.Value : stats.contactoTelefono);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el telefono del contacto."
                    };
                }
            }

            //Actualizar Contacto Tipo
            if (!failed && stats.contactoTipo != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET contactoTipo=@CONTACTO_TIPO WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@CONTACTO_TIPO", stats.contactoTipo.Length == 0 ? DBNull.Value : stats.contactoTipo);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el tipo del contacto."
                    };
                }
            }

            //Actualizar foto permiso de trabajo
            if (!failed && stats.fotoPermisoTrabajo != null)
            {
                DeleteFile(new[] { "candidate", stats.id, "foto_permiso_trabajo" });
                if (stats.fotoPermisoTrabajo.Length != 7)
                {
                    string fotoPermisoTrabajo = LimitImageSize(stats.fotoPermisoTrabajo);
                    SaveFile(new[] { "candidate", stats.id, "foto_permiso_trabajo" }, fotoPermisoTrabajo);
                }
            }

            //Actualizar foto discapacidad
            if (!failed && stats.fotoDiscapacidad != null)
            {
                if (stats.fotoDiscapacidad.Length == 7)
                {
                    DeleteFile(new[] { "candidate", stats.id, "foto_discapacidad" });
                }
                else
                {
                    string fotoDiscapacidad = LimitImageSize(stats.fotoDiscapacidad);
                    SaveFile(new[] { "candidate", stats.id, "foto_discapacidad" }, fotoDiscapacidad);
                }
            }

            //Actualizar fecha comienzo trabajo
            if (!failed && stats.fechaComienzoTrabajo != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET fechaComienzoTrabajo=@FECHA_COMIENZO_TRABAJO WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@FECHA_COMIENZO_TRABAJO", stats.fechaComienzoTrabajo.Value.Date);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la fecha en la que comenzo a trabajar."
                    };
                }
            }

            //Actualizar fecha fin trabajo
            if (!failed && stats.fechaFinTrabajo != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET fechaFinTrabajo=@FECHA_FIN_TRABAJO WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@FECHA_FIN_TRABAJO", stats.fechaFinTrabajo.Value.Date);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la fecha en la que terminó de trabajar."
                    };
                }
            }

            //Actualizar flag test
            if (!failed && stats.test != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos SET test=@TEST WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.Parameters.AddWithValue("@TEST", stats.test.Value ? 1 : 0);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar la el marcados de pruebas."
                    };
                }
            }

            //Actualizar el peridodo de gracia
            if (!failed && stats.periodoGracia != null)
            {
                using SqlCommand command = conn.CreateCommand();
                try
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE candidatos " +
                        "SET periodoGracia=@PERIODO_GRACIA " +
                        "WHERE id = @ID";
                    command.Parameters.AddWithValue("@PERIODO_GRACIA", stats.periodoGracia.Value);
                    command.Parameters.AddWithValue("@ID", stats.id);
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    failed = true;
                    result = new
                    {
                        error = "Error 5511, no se pudo actualizar el periodo de gracia."
                    };
                }
            }

            CandidateStats newData = null;
            try
            {
                newData = getCandidateStats(stats.id, conn, transaction);
                using SqlCommand command = conn.CreateCommand();
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText = "UPDATE candidatos SET allDataFilledIn=@ALL_DATA_FILLEDIN, hasPhoto = @HAS_PHOTO, hasFace = @HAS_FACE WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", stats.id);
                command.Parameters.AddWithValue("@ALL_DATA_FILLEDIN", newData.allDataFilledIn ? 1 : 0);
                command.Parameters.AddWithValue("@HAS_PHOTO", newData.hasPhoto ? 1 : 0);
                command.Parameters.AddWithValue("@HAS_FACE", newData.hasFace ? 1 : 0);
                command.ExecuteNonQuery();
            }
            catch (Exception)
            {
                failed = true;
                result = new
                {
                    error = "Error 5512, no se han podido comprobar si el perfil está completo."
                };
            }

            if (!failed)
                result = new { error = false, newData, faceUpdateTime, faceStatus };
            return new UpdateResult { failed = failed, result = result, newData = newData, faceUpdateTime = faceUpdateTime, faceStatus = faceStatus };
        }

        public static CandidateStats getCandidateStats(string candidateId, SqlConnection conn, SqlTransaction transaction = null)
        {
            CandidateStats candidate = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                           "SELECT C.id, " +
                           "C.*," +
                           "CE.alias as centroAlias, " +
                           "T.id as idTrabajo, " +
                           "CA.name as nombreTrabajo, " +
                           "E.id as idEmpresa, " +
                           "E.nombre as nombreEmpresa, " +
                           "documentsDownloaded = C.calc_documents_downloaded, " +
                           "pendingEmail = (SELECT TOP 1 ECP.email FROM email_changes_pending ECP WHERE ECP.candidateId = C.id ORDER BY ECP.creationTime DESC) " +
                           "FROM candidatos C " +
                           "LEFT OUTER JOIN trabajos T ON C.lastSignLink = T.signLink " +
                           "LEFT OUTER JOIN categories CA ON T.categoryId = CA.id " +
                           "LEFT OUTER JOIN centros CE ON T.centroId = CE.id " +
                           "LEFT OUTER JOIN empresas E ON CE.companyId = E.id " +
                           "WHERE C.id = @ID";
                command.Parameters.AddWithValue("@ID", candidateId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        candidate = new CandidateStats();
                        candidate.Read(reader);
                    }
                }
            }
            return candidate;
        }

        private static byte[] generateModelo145(string candidateId, string modelo145String = null)
        {
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                string nombre = null, dni = null;
                int? nacimiento = null;
                string localidad = null;

                //Obtener los datos del canidato
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT CONCAT(nombre, ' ', apellidos) as nombreCompleto, dni, nacimiento, localidad, modelo145 FROM candidatos WHERE id = @CANDIDATE_ID";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            nombre = reader.GetString(reader.GetOrdinal("nombreCompleto"));
                            dni = reader.GetString(reader.GetOrdinal("dni"));
                            nacimiento = reader.IsDBNull(reader.GetOrdinal("nacimiento")) ? null : reader.GetDateTime(reader.GetOrdinal("nacimiento")).Year;
                            localidad = reader.IsDBNull(reader.GetOrdinal("localidad")) ? null : reader.GetString(reader.GetOrdinal("localidad"));
                            if (modelo145String == null) modelo145String = reader.IsDBNull(reader.GetOrdinal("modelo145")) ? null : reader.GetString(reader.GetOrdinal("modelo145"));
                        }
                    }
                }

                if (nacimiento == null || localidad == null || modelo145String == null)
                {
                    return null;
                }

                JsonElement modelo145 = JsonDocument.Parse(modelo145String).RootElement;
                if (modelo145.TryGetProperty("situacionFamiliar", out JsonElement situacionFamiliarJson) && modelo145.TryGetProperty("nifConyuge", out JsonElement nifConyugeJson) &&
                    modelo145.TryGetProperty("discapacidad", out JsonElement discapacidadJson) && modelo145.TryGetProperty("movilidadGeografica", out JsonElement movilidadGeograficaJson) &&
                    modelo145.TryGetProperty("obtencionRendimiento", out JsonElement obtencionRendimientoJson) && modelo145.TryGetProperty("descendientes", out JsonElement descendientesJson) &&
                    modelo145.TryGetProperty("ascendientes", out JsonElement ascendientesJson) && modelo145.TryGetProperty("compensatoriaConyuge", out JsonElement compensatoriaConyugeJson) &&
                    modelo145.TryGetProperty("alimentosHijos", out JsonElement alimentosHijosJson) && modelo145.TryGetProperty("adquisicionVivienda", out JsonElement adquisicionViviendaJson) &&
                    discapacidadJson.TryGetProperty("grado", out JsonElement discapacidadGradoJson) && discapacidadJson.TryGetProperty("movilidad", out JsonElement discapacidadMovilidadJson)
                   )
                {

                    using (MemoryStream ms = new MemoryStream())
                    {
                        PdfReader pdfReader = new PdfReader(Path.Combine(Directory.GetCurrentDirectory(), "Resources", "modelo145.pdf"));
                        PdfStamper pdfStamper = new PdfStamper(pdfReader, ms);
                        AcroFields pdfFormFields = pdfStamper.AcroFields;

                        //Datos basicos del candidato
                        pdfFormFields.SetField("NIF", dni.ToUpper());
                        pdfFormFields.SetField("NOMBRE", nombre.ToUpper());
                        pdfFormFields.SetField("PERCEPTOR_NOMBRE", localidad);
                        pdfFormFields.SetField("NACIMIENTO", nacimiento.Value.ToString());

                        //Datos planos
                        switch (situacionFamiliarJson.GetInt32())
                        {
                            case 1:
                                pdfFormFields.SetField("SITUACION_FAMILIAR_1", "X");
                                break;
                            case 2:
                                pdfFormFields.SetField("SITUACION_FAMILIAR_2", "X");
                                pdfFormFields.SetField("NIF_CONYUGUE", nifConyugeJson.GetString().ToUpper());
                                break;
                            case 3:
                                pdfFormFields.SetField("SITUACION_FAMILIAR_3", "X");
                                break;
                        }
                        switch (discapacidadGradoJson.GetInt32())
                        {
                            case 1:
                                pdfFormFields.SetField("DISCAPACIDAD_1", "X");
                                break;
                            case 2:
                                pdfFormFields.SetField("DISCAPACIDAD_2", "X");
                                break;
                        }
                        if (discapacidadMovilidadJson.GetBoolean())
                        {
                            pdfFormFields.SetField("DISCAPACIDAD_MOVILIDAD", "X");
                        }
                        DateTime? movilidadGeografica = GetJsonDate(movilidadGeograficaJson);
                        if (movilidadGeografica != null)
                        {
                            pdfFormFields.SetField("MOVILIDAD_GEOGRAFICA", movilidadGeografica.Value.Day + "/" + movilidadGeografica.Value.Month + "/" + movilidadGeografica.Value.Year);
                        }
                        if (obtencionRendimientoJson.GetBoolean())
                        {
                            pdfFormFields.SetField("OBTENCION_RENDIMIENTO", "X");
                        }
                        double? compensatoriaConyuge = GetJsonDouble(compensatoriaConyugeJson);
                        if (compensatoriaConyuge != null)
                        {
                            pdfFormFields.SetField("COMPENSATORIA_CONYUGUE", $"{compensatoriaConyuge.Value:0.00}");
                        }
                        double? alimentosHijos = GetJsonDouble(alimentosHijosJson);
                        if (alimentosHijos != null)
                        {
                            pdfFormFields.SetField("ALIMENTOS_HIJOS", $"{alimentosHijos.Value:0.00}");
                        }
                        if (adquisicionViviendaJson.GetBoolean())
                        {
                            pdfFormFields.SetField("ADQUISICION_VIVIENDA", "X");
                        }

                        //Descendientes
                        int row = 1;
                        foreach (JsonElement descendiente in descendientesJson.EnumerateArray())
                        {
                            if (descendiente.TryGetProperty("nacimiento", out JsonElement nacimientoJson) && descendiente.TryGetProperty("adopcion", out JsonElement adopcionJson) &&
                                descendiente.TryGetProperty("discapacidad", out JsonElement discapacidadDescendienteJson) && descendiente.TryGetProperty("entero", out JsonElement enteroJson) &&
                                discapacidadDescendienteJson.TryGetProperty("grado", out JsonElement gradoJson) && discapacidadDescendienteJson.TryGetProperty("movilidad", out JsonElement movilidadJson))
                            {
                                pdfFormFields.SetField("DESCENDIENTE_NACIMIENTO_" + row, nacimientoJson.GetInt32().ToString());
                                double? adopcion = GetJsonDouble(adopcionJson);
                                if (adopcion != null)
                                {
                                    pdfFormFields.SetField("DESCENDIENTE_ACOGIMIENTO_" + row, adopcion.Value.ToString());
                                }
                                switch (gradoJson.GetInt32())
                                {
                                    case 1:
                                        pdfFormFields.SetField("DESCENDIENTE_DISCAPACIDAD_1_" + row, "X");
                                        break;
                                    case 2:
                                        pdfFormFields.SetField("DESCENDIENTE_DISCAPACIDAD_2_" + row, "X");
                                        break;
                                }
                                if (movilidadJson.GetBoolean())
                                {
                                    pdfFormFields.SetField("DESCENDIENTE_MOVILIDAD_" + row, "X");
                                }
                                if (enteroJson.GetBoolean())
                                {
                                    pdfFormFields.SetField("DESCENDIENTE_ENTERO_" + row, "X");
                                }
                            }
                            if (row == 4) break;
                            row++;
                        }

                        //Ascendientes
                        row = 1;
                        foreach (JsonElement ascendiente in ascendientesJson.EnumerateArray())
                        {
                            if (ascendiente.TryGetProperty("nacimiento", out JsonElement nacimientoJson) &&
                                ascendiente.TryGetProperty("discapacidad", out JsonElement discapacidadAscendienteJson) && ascendiente.TryGetProperty("convivencia", out JsonElement convivenciaJson) &&
                                discapacidadAscendienteJson.TryGetProperty("grado", out JsonElement gradoJson) && discapacidadAscendienteJson.TryGetProperty("movilidad", out JsonElement movilidadJson))
                            {
                                pdfFormFields.SetField("ASCENDIENTE_NACIMIENTO_" + row, nacimientoJson.GetInt32().ToString());
                                switch (gradoJson.GetInt32())
                                {
                                    case 1:
                                        pdfFormFields.SetField("ASCENDIENTE_DISCAPACIDAD_1_" + row, "X");
                                        break;
                                    case 2:
                                        pdfFormFields.SetField("ASCENDIENTE_DISCAPACIDAD_2_" + row, "X");
                                        break;
                                }
                                if (movilidadJson.GetBoolean())
                                {
                                    pdfFormFields.SetField("ASCENDIENTE_MOVILIDAD_" + row, "X");
                                }
                                int? convivencia = GetJsonInt(convivenciaJson);
                                if (convivencia != null)
                                {
                                    pdfFormFields.SetField("ASCENDENTE_CONVIVENCIA_" + row, convivencia.ToString());
                                }
                            }
                            if (row == 2) break;
                            row++;
                        }

                        //Constantes de RTETT
                        pdfFormFields.SetField("EMPRESA", "DEA ESTRATEGIAS LABORALES ETT S.L.");
                        pdfFormFields.SetField("EMPRESA_NOMBRE", "Granada");

                        //Fechas actuales
                        DateTime now = DateTime.Now;
                        pdfFormFields.SetField("PERCEPTOR_DIA", now.Day.ToString());
                        pdfFormFields.SetField("EMPRESA_DIA", now.Day.ToString());
                        pdfFormFields.SetField("PERCEPTOR_MES", MESES[now.Month - 1]);
                        pdfFormFields.SetField("EMPRESA_MES", MESES[now.Month - 1]);
                        pdfFormFields.SetField("PERCEPTOR_ANO", now.Year.ToString());
                        pdfFormFields.SetField("EMPRESA_ANO", now.Year.ToString());

                        //Firmas
                        try
                        {
                            PdfContentByte underContent;
                            underContent = pdfStamper.GetOverContent(1);

                            string firma = ReadFile(new[] { "candidate", candidateId, "stored_sign" });
                            if (firma != null)
                            {
                                iTextSharp.text.Image firmaCandidato = iTextSharp.text.Image.GetInstance(Convert.FromBase64String(firma.Split(",")[1]));
                                firmaCandidato.ScaleToFit(100f, 53f);
                                firmaCandidato.SetAbsolutePosition(100, 55);
                                underContent.AddImage(firmaCandidato);
                            }


                            iTextSharp.text.Image firmaEmpresa = iTextSharp.text.Image.GetInstance(Convert.FromBase64String(DEA_FIRMA));
                            firmaEmpresa.ScaleToFit(120f, 50f);
                            firmaEmpresa.SetAbsolutePosition(400, 55);
                            underContent.AddImage(firmaEmpresa);
                        }
                        catch (Exception) { }


                        pdfStamper.FormFlattening = false;
                        pdfStamper.Close();
                        return ms.ToArray();
                    }
                }
            }
            return null;
        }

        private static List<ExtendedCandidateData.DrivingLicense> parseDrivingLicenses(JsonElement drivingLicensesJson)
        {
            List<ExtendedCandidateData.DrivingLicense> drivingLicenses = new List<ExtendedCandidateData.DrivingLicense>();

            foreach (JsonElement license in drivingLicensesJson.EnumerateArray())
            {
                try
                {
                    DateTime? expiration = GetJsonDate(license.GetProperty("expiration"));
                    if (expiration.HasValue)
                    {
                        drivingLicenses.Add(new ExtendedCandidateData.DrivingLicense()
                        {
                            type = license.GetProperty("type").GetString(),
                            expiration = expiration.Value,
                            anverso = license.GetProperty("anverso").GetString(),
                            reverso = license.GetProperty("reverso").GetString()
                        });
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return drivingLicenses;
        }

        private static ExtendedCandidateData.LegalRepresentativeConsent? parseLegalRepresentativeConsent(JsonElement legalRepresentativeConsentJson)
        {
            if (!legalRepresentativeConsentJson.ValueKind.Equals(JsonValueKind.Null))
            {
                ExtendedCandidateData.LegalRepresentativeConsent legalRepresentativeConsent = new();
                if (legalRepresentativeConsentJson.TryGetProperty("tutorAnverso", out JsonElement tutorAnversoJson) &&
                    legalRepresentativeConsentJson.TryGetProperty("tutorReverso", out JsonElement tutorReversoJson))
                {
                    legalRepresentativeConsent.tutorAnverso = tutorAnversoJson.GetString();
                    legalRepresentativeConsent.tutorReverso = tutorReversoJson.GetString();
                }
                if (legalRepresentativeConsentJson.TryGetProperty("autorizacion", out JsonElement autorizacionJson))
                {
                    legalRepresentativeConsent.autorizacion = autorizacionJson.GetString();
                }
                return legalRepresentativeConsent;
            }
            return null;
        }

        

        
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
