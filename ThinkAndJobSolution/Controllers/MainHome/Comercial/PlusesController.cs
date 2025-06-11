using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Management;
using System.Text.Json;
using System.Text;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using ThinkAndJobSolution.Utils;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/trabajo-plus")]
    [ApiController]
    [Authorize]
    public class PlusesController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "list/{trabajoId}")]
        public IActionResult List(string trabajoId)
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
                    result = new
                    {
                        error = false,
                        pluses = listPlusTypes(conn, null, trabajoId)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar los pluses" };
                }

            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list-by-candidate/{candidateId}")]
        public IActionResult ListByCandidate(string candidateId)
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
                    result = new
                    {
                        error = false,
                        pluses = listPlusTypesByCandidate(conn, null, candidateId)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar los pluses" };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "/list-by-candidates")]
        public async Task<IActionResult> ListByCandidates()
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            List<string> candidates = GetJsonStringList(json);
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new
                    {
                        error = false,
                        pluses = listPlusTypesByCandidates(conn, null, candidates)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5811, no han podido listar los pluses" };
                }

            }
            return Ok(result);
        }

        //Obtencion
        [HttpGet]
        [Route(template: "{plusId}")]
        public IActionResult Get(string plusId)
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
                    PlusType? plus = getPlusType(conn, null, plusId);
                    if (plus == null)
                    {
                        result = new { error = "Error 5812, plus no encontrada" };
                    }
                    else
                    {
                        result = new { error = false, plus };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener el plus" };
                }
            }
            return Ok(result);
        }


        //Actualización
        [HttpPut]
        [Route(template: "api/v1/trabajo-plus/{trabajoId}/")]
        public async Task<IActionResult> Update(string trabajoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("Category.Create", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("pluses", out JsonElement plusesJson))
            {
                List<PlusType> pluses = new();
                foreach (JsonElement plus in plusesJson.EnumerateArray())
                {
                    if (plus.TryGetProperty("concepto", out JsonElement conceptoJson) &&
                        plus.TryGetProperty("cantidad", out JsonElement cantidadJson) &&
                        plus.TryGetProperty("fraccionable", out JsonElement fraccionableJson) &&
                        plus.TryGetProperty("neto", out JsonElement netoJson) &&
                        plus.TryGetProperty("tipo", out JsonElement tipoJson) &&
                        plus.TryGetProperty("editable", out JsonElement editableJson))
                    {
                        if (GetJsonBool(editableJson) ?? false)
                        {
                            pluses.Add(new PlusType()
                            {
                                id = ComputeStringHash(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + plus.ToString()),
                                trabajoId = trabajoId,
                                concepto = conceptoJson.GetString(),
                                cantidad = GetJsonDouble(cantidadJson) ?? 0,
                                fraccionable = GetJsonBool(fraccionableJson) ?? false,
                                neto = GetJsonBool(netoJson) ?? false,
                                tipo = tipoJson.GetString()
                            });
                        }
                    }
                }
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        //Borrar los pluses de ese trabajo
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM trabajos_pluses WHERE trabajoId = @TRABAJO";
                            command.Parameters.AddWithValue("@TRABAJO", trabajoId);
                            command.ExecuteNonQuery();
                        }
                        //Insertar los nuevos pluses
                        foreach (PlusType plus in pluses)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "INSERT INTO trabajos_pluses (id, trabajoId, concepto, cantidad, fraccionable, neto, tipo) VALUES (@ID, @TRABAJO, @CONCEPTO, @CANTIDAD, @FRACCIONABLE, @NETO, @TIPO)";
                                command.Parameters.AddWithValue("@ID", plus.id);
                                command.Parameters.AddWithValue("@TRABAJO", plus.trabajoId);
                                command.Parameters.AddWithValue("@CONCEPTO", plus.concepto);
                                command.Parameters.AddWithValue("@CANTIDAD", plus.cantidad);
                                command.Parameters.AddWithValue("@FRACCIONABLE", plus.fraccionable ? 1 : 0);
                                command.Parameters.AddWithValue("@NETO", plus.neto ? 1 : 0);
                                command.Parameters.AddWithValue("@TIPO", plus.tipo);
                                command.ExecuteNonQuery();
                            }
                        }
                        result = new
                        {
                            error = false
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5892, no se han podido modificar los pluses" };
                    }

                }
            }
            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        public struct PlusType
        {
            public string id { get; set; }
            public string trabajoId { get; set; }
            public string concepto { get; set; }
            public double cantidad { get; set; }
            public bool fraccionable { get; set; }
            public bool neto { get; set; }
            public string tipo { get; set; }    //  horas | plus | vacaciones
            public bool editable { get; set; }
        }
        private static readonly List<PlusType> DEFAULT_TYPES = new()
        {
            new PlusType()
            {
                tipo = "horas",
                concepto = "Hora normal",
                cantidad = 0,
                fraccionable = true,
                neto = false,
                editable = false
            },
            new PlusType()
            {
                tipo = "horas",
                concepto = "Hora extra",
                cantidad = 0,
                fraccionable = true,
                neto = false,
                editable = false
            }
        };
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static List<PlusType> listPlusTypes(SqlConnection conn, SqlTransaction transaction, string trabajoId)
        {
            List<PlusType> categories = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT * FROM trabajos_pluses " +
                                      "WHERE (@TRABAJO IS NULL OR trabajoId = @TRABAJO)" +
                                      "ORDER BY concepto";
                command.Parameters.AddWithValue("@TRABAJO", (object)trabajoId ?? DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(new PlusType()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            trabajoId = reader.GetString(reader.GetOrdinal("trabajoId")),
                            concepto = reader.GetString(reader.GetOrdinal("concepto")),
                            cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                            fraccionable = reader.GetInt32(reader.GetOrdinal("fraccionable")) == 1,
                            neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1,
                            tipo = reader.GetString(reader.GetOrdinal("tipo")),
                            editable = true
                        });
                    }
                }
            }

            addDefaultTypes(categories);
            return categories;
        }
        public static List<PlusType> listPlusTypesByCandidate(SqlConnection conn, SqlTransaction transaction, string candidateId)
        {
            List<PlusType> categories = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT P.* FROM trabajos_pluses P " +
                                      "INNER JOIN trabajos T ON(P.trabajoId = T.id) " +
                                      "INNER JOIN candidatos C ON(T.signLink = C.lastSignLink) " +
                                      "WHERE C.id = @CANDIDATE " +
                                      "ORDER BY concepto";
                command.Parameters.AddWithValue("@CANDIDATE", candidateId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(new PlusType()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            trabajoId = reader.GetString(reader.GetOrdinal("trabajoId")),
                            concepto = reader.GetString(reader.GetOrdinal("concepto")),
                            cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                            fraccionable = reader.GetInt32(reader.GetOrdinal("fraccionable")) == 1,
                            neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1,
                            tipo = reader.GetString(reader.GetOrdinal("tipo")),
                            editable = true
                        });
                    }
                }
            }

            addDefaultTypes(categories);
            return categories;
        }
        public static List<PlusType> listPlusTypesByCandidates(SqlConnection conn, SqlTransaction transaction, List<string> candidates)
        {
            HashSet<string> works = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT T.id " +
                                      "FROM trabajos T " +
                                      "INNER JOIN candidatos C ON(T.signLink = C.lastSignLink) " +
                                      "WHERE C.id = @CANDIDATE ";
                command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);

                foreach (string candidate in candidates)
                {
                    command.Parameters["@CANDIDATE"].Value = candidate;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            works.Add(reader.GetString(reader.GetOrdinal("id")));
                        }
                    }
                }
            }

            List<List<PlusType>> categories = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT P.* FROM trabajos_pluses P " +
                                      "INNER JOIN trabajos T ON(P.trabajoId = T.id) " +
                                      "WHERE T.id = @WORK " +
                                      "ORDER BY concepto";
                command.Parameters.Add("@WORK", System.Data.SqlDbType.VarChar);

                foreach (string work in works)
                {
                    List<PlusType> workCategories = new();

                    command.Parameters["@WORK"].Value = work;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            workCategories.Add(new PlusType()
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                trabajoId = reader.GetString(reader.GetOrdinal("trabajoId")),
                                concepto = reader.GetString(reader.GetOrdinal("concepto")),
                                cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                                fraccionable = reader.GetInt32(reader.GetOrdinal("fraccionable")) == 1,
                                neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1,
                                tipo = reader.GetString(reader.GetOrdinal("tipo")),
                                editable = true
                            });
                        }
                    }

                    categories.Add(workCategories);
                }
            }

            if (categories.Count == 0)
                return new List<PlusType>();

            //Las categories del primer trabajo, tal que no exista ningun trabajo que no tenga otra categoria con el mismo ID.
            List<PlusType> categoriesInCommon = categories[0].Where(c => !categories.Any(w => !w.Any(c2 => c.id == c2.id || (c.concepto == c2.concepto && c.tipo == c2.tipo && c.cantidad == c2.cantidad && c.neto == c2.neto)))).ToList();

            addDefaultTypes(categoriesInCommon);
            return categoriesInCommon;
        }
        public static PlusType? getPlusType(SqlConnection conn, SqlTransaction transaction, string plusId)
        {
            PlusType? category = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT * FROM trabajos_pluses WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", plusId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        category = new PlusType()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            trabajoId = reader.GetString(reader.GetOrdinal("trabajoId")),
                            concepto = reader.GetString(reader.GetOrdinal("concepto")),
                            cantidad = reader.GetDouble(reader.GetOrdinal("cantidad")),
                            fraccionable = reader.GetInt32(reader.GetOrdinal("fraccionable")) == 1,
                            neto = reader.GetInt32(reader.GetOrdinal("neto")) == 1
                        };
                    }
                }
            }

            return category;
        }
        private static void addDefaultTypes(List<PlusType> list)
        {
            foreach (PlusType def in DEFAULT_TYPES)
            {
                if (!list.Any(p => p.tipo == def.tipo && p.concepto.Trim().ToLower().Equals(def.concepto.Trim().ToLower())))
                {
                    list.Add(def);
                }
            }
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
