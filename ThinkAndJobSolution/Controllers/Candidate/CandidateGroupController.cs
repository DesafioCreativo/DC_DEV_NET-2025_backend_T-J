using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;

namespace ThinkAndJobSolution.Controllers.Candidate
{
    [Route("api/v1/candidate-group")]
    [ApiController]
    [Authorize]
    public class CandidateGroupController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        
        [HttpGet]        
        [Route(template: "list-for-client/{centroId}/")]
        public IActionResult ListForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn) == null)
                    {
                        return Ok(new { error = "Error 1002, permisos insuficientes" });
                    }

                    result = new { error = false, groups = listGroups(centroId, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5820, no se han podido listar los grupos" };
                }
            }

            return Ok(result);
        }

        //Obtencion
        [HttpGet]
        [Route("get-for-client/{groupId}/")]
        public IActionResult GetForClient(string groupId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    CandidateGroup? group = getGroup(groupId, conn);
                    if (group == null)
                    {
                        return Ok(new { error = "Error 4820, grupo no encontrado" });
                    }
                    if (ClientHasPermission(clientToken, null, group.Value.centroId, CL_PERMISSION_HORARIOS, conn) == null)
                    {
                        return Ok(new { error = "Error 1002, permisos insuficientes" });
                    }
                    result = new { error = false, group = group.Value };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5821, no se ha podido obtener el grupo" };
                }
            }
            return Ok(result);
        }


        [HttpGet]
        [Route("get-for-edit-for-client/{groupId}/")]
        public IActionResult GetForEditForClient(string groupId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    CandidateGroup? groupTmp = getGroup(groupId, conn);
                    if (groupTmp == null)
                    {
                        return Ok(new { error = "Error 4820, grupo no encontrado" });
                    }
                    CandidateGroup group = groupTmp.Value;
                    if (ClientHasPermission(clientToken, null, group.centroId, CL_PERMISSION_HORARIOS, conn) == null)
                    {
                        return Ok(new { error = "Error 1002, permisos insuficientes" });
                    }
                    List<CLSimpleWorkerWithGroup> candidatos = listCandidatesWithGroup(group.centroId, conn);
                    List<CLSimpleWorkerWithGroupForEdit> candidatosForEdit = candidatos.Select(candidato =>
                    {
                        return new CLSimpleWorkerWithGroupForEdit
                        {
                            id = candidato.id,
                            dni = candidato.dni,
                            nombre = candidato.nombre,
                            fechaComienzoTrabajo = candidato.fechaComienzoTrabajo,
                            work = candidato.work,
                            groupName = candidato.group.HasValue ? candidato.group.Value.name : null,
                            inGroup = candidato.group.HasValue ? (candidato.group.Value.id == group.id ? 1 : 2) : 0
                        };
                    }).ToList();
                    group.candidates = null;
                    result = new { error = false, group, candidates = candidatosForEdit };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5821, no se ha podido obtener el grupo" };
                }
            }
            return Ok(result);
        }

        //Creacion
        [HttpPost]
        [Route("api/v1/candidate-group/create-for-client/")]
        public async Task<IActionResult> CreateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (!(
                json.TryGetString("name", out string name) &&
                json.TryGetString("centroId", out string centroId) &&
                json.TryGetString("picture", out string picture)
                ))
                return Ok(result);

            CandidateGroup group = new CandidateGroup() { name = name, centroId = centroId };

            if (json.TryGetStringList("candidates", out List<string> candidates))
                group.candidates = candidates.Select(id => new CLSimpleWorkerInfo() { id = id }).ToList();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        group.creator = ClientHasPermission(clientToken, null, group.centroId, CL_PERMISSION_HORARIOS, conn, transaction);
                        if (group.creator == null)
                            return Ok(new { error = "Error 1002, permisos insuficientes" });
                        //Comprobar que no exista otro grupo con el mismo nombre
                        if (groupExist(group.centroId, group.name, null, conn, transaction))
                            return Ok(new { error = "Error 4828, ya existe otro grupo con el mismo nombre" });
                        string id = createGroup(group, conn, transaction);
                        if (picture != null)
                            SaveFile(new[] { "candidate_group", id, "photo" }, LimitSquareImage(picture));
                        transaction.Commit();
                        result = new { error = false, id };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5822, no se ha podido crear el grupo" };
                    }
                }
            }
            return Ok(result);
        }

        //Actualización
        [HttpPut]
        [Route("update-for-client/{groupId}/")]
        public async Task<IActionResult> UpdateForClient(string groupId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (!(
                json.TryGetString("name", out string name) &&
                json.TryGetString("picture", out string picture)
                ))
                return Ok(result);

            CandidateGroup group = new CandidateGroup() { name = name, id = groupId };

            if (json.TryGetStringList("candidates", out List<string> candidates))
                group.candidates = candidates.Select(id => new CLSimpleWorkerInfo() { id = id }).ToList();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        //Buscar el grupo para obtener el centro
                        group.centroId = getCentroByGroup(groupId, conn, transaction);
                        if (group.centroId == null)
                            return Ok(new { error = "Error 4821, grupo no encontrado" });
                        //Comprobar permisos
                        if (ClientHasPermission(clientToken, null, group.centroId, CL_PERMISSION_HORARIOS, conn, transaction) == null)
                            return Ok(new { error = "Error 1002, permisos insuficientes" });
                        //Comprobar que no exista otro grupo con el mismo nombre
                        if (groupExist(group.centroId, group.name, groupId, conn, transaction))
                            return Ok(new { error = "Error 4829, ya existe otro grupo con el mismo nombre" });
                        //Actualizar el grupo
                        await updateGroup(group, conn, transaction);
                        if (picture != null)
                            SaveFile(new[] { "candidate_group", groupId, "photo" }, LimitSquareImage(picture));
                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5823, no se ha podido actualizar el grupo" };
                    }
                }
            }
            return Ok(result);
        }

        //Eliminacion
        [HttpDelete]
        [Route("delete-for-client/{groupId}/")]
        public IActionResult DeleteForClient(string groupId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string centroId = getCentroByGroup(groupId, conn, transaction);
                        if (centroId == null)
                            return Ok(new { error = "Error 4821, grupo no encontrado" });
                        if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn, transaction) == null)
                            return Ok(new { error = "Error 1002, permisos insuficientes" });
                        deleteGroup(groupId, conn, transaction);
                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5826, no se ha podido eliminar el grupo" };
                    }
                }
            }
            return Ok(result);
        }


        //Auxiliar
        [HttpGet]
        [Route("list-candidates-with-group-for-client/{centroId}/")]
        public IActionResult ListCandidatesWithGroupForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_HORARIOS, conn) == null)
                    {
                        return Ok(new { error = "Error 1002, permisos insuficientes" });
                    }

                    result = new { error = false, candidates = listCandidatesWithGroup(centroId, conn) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5827, no se han podido listar a los candidatos" };
                }
            }
            return Ok(result);
        }



        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------

        //Ayuda
        #region "Clases"
        public struct CandidateGroup
        {
            public string id { get; set; }
            public string centroId { get; set; }
            public string name { get; set; }
            public string creator { get; set; }
            public int nCandidates { get; set; }
            public List<CLSimpleWorkerInfo> candidates { get; set; }
        }

        public struct CLSimpleWorkerWithGroup
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public string dni { get; set; }
            public DateTime? fechaComienzoTrabajo { get; set; }
            public string work { get; set; }
            public CandidateGroup? group { get; set; }
        }

        public struct CLSimpleWorkerWithGroupForEdit
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public string dni { get; set; }
            public DateTime? fechaComienzoTrabajo { get; set; }
            public string work { get; set; }
            public int inGroup { get; set; } // 0: Sin grupo, 1: En el grupo por el que se pregunta, 2: En otro grupo
            public string groupName { get; set; }
        }
        #endregion

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        private List<CandidateGroup> listGroups(string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<CandidateGroup> groups = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CG.id, CG.name, CG.creator, nCandidates = (SELECT COUNT(*) FROM candidate_group_members CGM WHERE CGM.groupId = CG.id) " +
                    "FROM candidate_groups CG WHERE CG.centroId = @CENTRO";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        groups.Add(new CandidateGroup()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            creator = reader.GetString(reader.GetOrdinal("creator")),
                            nCandidates = reader.GetInt32(reader.GetOrdinal("nCandidates"))
                        });
                    }
                }
            }

            return groups;
        }
        private CandidateGroup? getGroup(string groupId, SqlConnection conn, SqlTransaction transaction = null)
        {
            CandidateGroup group;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT CG.id, CG.name, CG.creator, CG.centroId FROM candidate_groups CG WHERE CG.id = @ID";
                command.Parameters.AddWithValue("@ID", groupId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        group = new CandidateGroup()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            creator = reader.GetString(reader.GetOrdinal("creator")),
                            centroId = reader.GetString(reader.GetOrdinal("centroId")),
                            candidates = new()
                        };
                    }
                    else return null;
                }
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                            "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.dni, CA.name as work " +
                            "FROM candidatos C " +
                            "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                            "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "INNER JOIN candidate_group_members CGM ON(CGM.candidateId = C.id) " +
                            "WHERE CGM.groupId = @ID";
                command.Parameters.AddWithValue("@ID", groupId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        group.candidates.Add(new CLSimpleWorkerInfo
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            nombre = reader.GetString(reader.GetOrdinal("candidateName")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            work = reader.GetString(reader.GetOrdinal("work"))
                        });
                    }
                }
            }

            group.nCandidates = group.candidates.Count;

            return group;
        }
        public static List<CandidateGroup> listGroupsWithMembers(string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<CandidateGroup> groups = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT CG.id, CG.name, CG.creator " +
                    "FROM candidate_groups CG WHERE CG.centroId = @CENTRO";
                command.Parameters.AddWithValue("@CENTRO", centroId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        groups.Add(new CandidateGroup()
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name")),
                            creator = reader.GetString(reader.GetOrdinal("creator")),
                            centroId = centroId,
                            candidates = new()
                        });
                    }
                }
            }

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                            "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.dni, C.fechaComienzoTrabajo, CA.name as work " +
                            "FROM candidatos C " +
                            "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                            "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "INNER JOIN candidate_group_members CGM ON(CGM.candidateId = C.id) " +
                            "WHERE CGM.groupId = @ID";
                command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);

                groups = groups.Select(group =>
                {
                    command.Parameters["@ID"].Value = group.id;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            group.candidates.Add(new CLSimpleWorkerInfo
                            {
                                id = reader.GetString(reader.GetOrdinal("id")),
                                nombre = reader.GetString(reader.GetOrdinal("candidateName")),
                                dni = reader.GetString(reader.GetOrdinal("dni")),
                                fechaComienzoTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo")).Date,
                                work = reader.GetString(reader.GetOrdinal("work"))
                            });
                        }
                    }

                    group.nCandidates = group.candidates.Count;
                    return group;
                }).ToList();
            }

            return groups;
        }
        private List<string> getCandidatesByGroup(string groupId, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<string> candidates = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                            "SELECT CGM.candidateId " +
                            "FROM candidatos C " +
                            "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                            "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "INNER JOIN candidate_group_members CGM ON(CGM.candidateId = C.id) " +
                            "WHERE CGM.groupId = @ID";
                command.Parameters.AddWithValue("@ID", groupId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        candidates.Add(reader.GetString(reader.GetOrdinal("candidateId")));
                    }
                }
            }
            return candidates;
        }
        private string getCentroByGroup(string groupId, SqlConnection conn, SqlTransaction transaction = null)
        {
            string centroId = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText =
                    "SELECT centroId FROM candidate_groups WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", groupId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                        centroId = reader.GetString(reader.GetOrdinal("centroId"));
                }
            }
            return centroId;
        }
        private string getGroupName(string groupId, SqlConnection conn, SqlTransaction transaction = null)
        {
            string name = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                command.Connection = conn;
                command.Transaction = transaction;
                command.CommandText =
                    "SELECT name FROM candidate_groups WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", groupId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                        name = reader.GetString(reader.GetOrdinal("name"));
                }
            }
            return name;
        }
        private List<CLSimpleWorkerWithGroup> listCandidatesWithGroup(string centroId, SqlConnection conn, SqlTransaction transaction = null)
        {
            Dictionary<string, CLSimpleWorkerWithGroup> candidates = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                            "SELECT C.id, CONCAT(C.nombre, ' ', C.apellidos) as candidateName, C.dni, CA.name as work, " +
                            "CG.id as groupId, CG.name as groupName, CG.centroId " +
                            "FROM candidatos C " +
                            "INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                            "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "LEFT OUTER JOIN candidate_group_members CGM ON(CGM.candidateId = C.id) " +
                            "LEFT OUTER JOIN candidate_groups CG ON(CGM.groupId = CG.id) " +
                            "WHERE C.centroId = @ID " +
                            "ORDER BY candidateName";
                command.Parameters.AddWithValue("@ID", centroId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string candidateId = reader.GetString(reader.GetOrdinal("id"));
                        CandidateGroup? group = null;
                        if (!reader.IsDBNull(reader.GetOrdinal("groupId")))
                        {
                            group = new CandidateGroup()
                            {
                                id = reader.GetString(reader.GetOrdinal("groupId")),
                                name = reader.GetString(reader.GetOrdinal("groupName"))
                            };
                        }
                        candidates[candidateId] = new CLSimpleWorkerWithGroup()
                        {
                            id = candidateId,
                            nombre = reader.GetString(reader.GetOrdinal("candidateName")),
                            dni = reader.GetString(reader.GetOrdinal("dni")),
                            work = reader.GetString(reader.GetOrdinal("work")),
                            group = group
                        };
                    }
                }
            }

            return candidates.Values.ToList();
        }
        private bool groupExist(string centroId, string name, string ignoreId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT COUNT(*) FROM candidate_groups WHERE name = @NAME AND centroId = @CENTRO AND (@ID IS NULL OR id <> @ID)";
                command.Parameters.AddWithValue("@ID", (object)ignoreId ?? DBNull.Value);
                command.Parameters.AddWithValue("@NAME", name);
                command.Parameters.AddWithValue("@CENTRO", centroId);
                return (int)command.ExecuteScalar() > 0;
            }
        }
        private List<int> getHorariosAffectedByGroup(string groupId, SqlConnection conn, SqlTransaction transaction = null)
        {
            string centroId = getCentroByGroup(groupId, conn, transaction);
            string oldGroupName = getGroupName(groupId, conn, transaction);

            List<int> horarios = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT id FROM horarios WHERE grupo = @OLDNAME AND centroId = @CENTRO AND semana >= @MONDAY";
                command.Parameters.AddWithValue("@OLDNAME", oldGroupName);
                command.Parameters.AddWithValue("@CENTRO", centroId);
                command.Parameters.AddWithValue("@MONDAY", HorariosController.getLockDate());
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        horarios.Add(reader.GetInt32(reader.GetOrdinal("id")));
            }

            return horarios;
        }
        private string createGroup(CandidateGroup group, SqlConnection conn, SqlTransaction transaction = null)
        {
            group.id = ComputeStringHash(group.name + group.centroId + group.creator + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "INSERT INTO candidate_groups (id, name, centroId, creator) VALUES (@ID, @NAME, @CENTRO, @CREATOR)";
                command.Parameters.AddWithValue("@ID", group.id);
                command.Parameters.AddWithValue("@NAME", group.name);
                command.Parameters.AddWithValue("@CENTRO", group.centroId);
                command.Parameters.AddWithValue("@CREATOR", group.creator);
                command.ExecuteNonQuery();
            }

            if (group.candidates != null && group.candidates.Count > 0)
            {
                //Sacarlos del los gurpos en los que estuvieran
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "DELETE FROM candidate_group_members WHERE candidateId = @CANDIDATE";
                    command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                    foreach (CLSimpleWorkerInfo candidate in group.candidates)
                    {
                        command.Parameters["@CANDIDATE"].Value = candidate.id;
                        command.ExecuteNonQuery();
                    }
                }

                //Meterlos en el nuevo grupo
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO candidate_group_members (groupId, candidateId) VALUES (@ID, @CANDIDATE)";
                    command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                    command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                    foreach (CLSimpleWorkerInfo candidate in group.candidates)
                    {
                        command.Parameters["@ID"].Value = group.id;
                        command.Parameters["@CANDIDATE"].Value = candidate.id;
                        command.ExecuteNonQuery();
                    }
                }
            }

            return group.id;
        }
        private async Task updateGroup(CandidateGroup group, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "UPDATE candidate_groups SET name = @NAME WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", group.id);
                command.Parameters.AddWithValue("@NAME", group.name);
                command.ExecuteNonQuery();
            }

            //Eliminar los actuales
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM candidate_group_members WHERE groupId = @ID";
                command.Parameters.AddWithValue("@ID", group.id);
                command.ExecuteNonQuery();
            }

            if (group.candidates != null && group.candidates.Count > 0)
            {
                //Sacarlos del los gurpos en los que estuvieran
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "DELETE FROM candidate_group_members WHERE candidateId = @CANDIDATE";
                    command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                    foreach (CLSimpleWorkerInfo candidate in group.candidates)
                    {
                        command.Parameters["@CANDIDATE"].Value = candidate.id;
                        command.ExecuteNonQuery();
                    }
                }

                //Meterlos en el nuevo grupo
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO candidate_group_members (groupId, candidateId) VALUES (@ID, @CANDIDATE)";
                    command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                    command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                    foreach (CLSimpleWorkerInfo candidate in group.candidates)
                    {
                        command.Parameters["@ID"].Value = group.id;
                        command.Parameters["@CANDIDATE"].Value = candidate.id;
                        command.ExecuteNonQuery();
                    }
                }
            }

            //Sincronizacion de horarios
            //Obtener los horarios que necesitan sincronizar el grupo
            List<int> horarios = getHorariosAffectedByGroup(group.id, conn, transaction);
            Dictionary<DateTime, List<string>> affected = new();

            //Actualizar el nombre del grupo
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "UPDATE horarios SET grupo = @NAME WHERE id = @HORARIO";
                command.Parameters.Add("@NAME", System.Data.SqlDbType.VarChar);
                command.Parameters.Add("@HORARIO", System.Data.SqlDbType.Int);
                foreach (int horarioId in horarios)
                {
                    command.Parameters["@NAME"].Value = group.name;
                    command.Parameters["@HORARIO"].Value = horarioId;
                    command.ExecuteNonQuery();
                }
            }

            //Mantener las personas del grupo actualizadas
            foreach (int horarioId in horarios)
            {
                //Obtener el lunes de la semana que define este horario
                DateTime lunes;
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT semana FROM horarios WHERE id = @HORARIO";
                    command.Parameters.AddWithValue("@HORARIO", horarioId);
                    lunes = (DateTime)command.ExecuteScalar();
                }

                //Obtener las personas que tienen este horario
                List<string> alreadyHave = new();
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT candidateId FROM horarios_asignacion WHERE horarioId = @HORARIO";
                    command.Parameters.AddWithValue("@HORARIO", horarioId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            alreadyHave.Add(reader.GetString(reader.GetOrdinal("candidateId")));
                        }
                    }
                }
                List<string> toDelete = alreadyHave.Where(id => !group.candidates.Any(c => c.id == id)).ToList();
                List<string> toCreate = group.candidates.Where(c => !alreadyHave.Any(id => id == c.id)).Select(c => c.id).ToList();

                //Eliminar los que ya no estan en el grupo
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "DELETE FROM horarios_asignacion WHERE horarioId = @HORARIO AND candidateId = @CANDIDATE";
                    command.Parameters.AddWithValue("@HORARIO", horarioId);
                    command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                    foreach (string candidateId in toDelete)
                    {
                        command.Parameters["@CANDIDATE"].Value = candidateId;
                        command.ExecuteNonQuery();
                    }
                }

                //Agregar a las nuevas personas del grupo
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO horarios_asignacion (horarioId, candidateId, seen) VALUES(@HORARIO, @CANDIDATE, 0)";
                    command.Parameters.AddWithValue("@HORARIO", horarioId);
                    command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                    foreach (string candidateId in toCreate)
                    {
                        command.Parameters["@CANDIDATE"].Value = candidateId;
                        command.ExecuteNonQuery();
                    }
                }

                //Agregarlos a la lista de afectados
                if (!affected.ContainsKey(lunes))
                    affected.Add(lunes, new());

                affected[lunes].AddRange(toCreate);
            }

            foreach (KeyValuePair<DateTime, List<string>> effect in affected)
                await HorariosController.sendEmailsToAffected(effect.Value.ToHashSet(), effect.Key, conn, transaction);
        }
        private void deleteGroup(string groupId, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Obtener los horarios que necesitan sincronizar el grupo. Hay que hacerlo ahora porque luego el grupo no existira
            List<int> horarios = getHorariosAffectedByGroup(groupId, conn, transaction);

            //Eliminar los miembros del grupo
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM candidate_group_members WHERE groupId = @ID";
                command.Parameters.AddWithValue("@ID", groupId);
                command.ExecuteNonQuery();
            }

            //Eliminar el grupo
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM candidate_groups WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", groupId);
                command.ExecuteNonQuery();
            }

            //Sincronizacion de horarios
            //Desasignar a las personas con ese horario
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM horarios_asignacion WHERE horarioId = @HORARIO";
                command.Parameters.Add("@HORARIO", System.Data.SqlDbType.Int);
                foreach (int horarioId in horarios)
                {
                    command.Parameters["@HORARIO"].Value = horarioId;
                    command.ExecuteNonQuery();
                }
            }

            //Borrar esos horarios
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM horarios WHERE id = @HORARIO";
                command.Parameters.Add("@HORARIO", System.Data.SqlDbType.Int);
                foreach (int horarioId in horarios)
                {
                    command.Parameters["@HORARIO"].Value = horarioId;
                    command.ExecuteNonQuery();
                }
            }
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
