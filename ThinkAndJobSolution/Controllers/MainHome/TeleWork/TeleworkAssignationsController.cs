using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IO.Compression;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers._Model.Candidate;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers._Helper.Ohers.EventMailer;
using static ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions.JsonExtensions;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;

namespace ThinkAndJobSolution.Controllers.MainHome.TeleWork
{
    [Route("api/v1/tw-assignations")]
    [ApiController]
    [Authorize]
    public class TeleworkAssignationsController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        // Crear la asignación

        [HttpPost]
        [Route("create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.Create", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("receptorId", out string receptorId) &&
                json.TryGetString("category", out string category) &&
                json.TryGetString("warning", out string warning) &&
                json.TryGetDateOptional("programmedDate", out DateTime? programmedDate) &&
                json.TryGetString("estimatedCompletionTime", out string estimatedCompletionTime) &&
                json.TryGetProperty("notes", out JsonElement notesJson) &&
                json.TryGetStringList("candidates", out List<string> candidates))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            DateTime now = DateTime.Now;
                            string username = FindUsernameBySecurityToken(securityToken, conn, transaction);
                            string creatorId = FindUserIdBySecurityToken(securityToken, conn, transaction);

                            //Crear una asignacion para todos
                            int id;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText =
                                    "INSERT INTO telework_assignations (creatorId, receptorId, category, warning, programmedDate, estimatedCompletionTime) output INSERTED.ID VALUES (@CREATOR, @RECEPTOR, @CATEGORY, @WARNING, @PROGRAMMED, @ESTIMATED)";
                                command.Parameters.AddWithValue("@CREATOR", creatorId);
                                command.Parameters.AddWithValue("@RECEPTOR", receptorId);
                                command.Parameters.AddWithValue("@CATEGORY", category);
                                command.Parameters.AddWithValue("@WARNING", (object)warning ?? DBNull.Value);
                                command.Parameters.AddWithValue("@PROGRAMMED", programmedDate == null ? DBNull.Value : programmedDate.Value);
                                command.Parameters.AddWithValue("@ESTIMATED", (object)estimatedCompletionTime ?? DBNull.Value);
                                id = (int)command.ExecuteScalar();
                            }

                            //Crear una tarea para cada candidato
                            string state = "abierta";
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText =
                                    "INSERT INTO telework_assignations_tasks (assignationId, candidateId, state) VALUES (@ID, @CANDIDATE, @STATE)";
                                command.Parameters.Add("@ID", System.Data.SqlDbType.VarChar);
                                command.Parameters.Add("@CANDIDATE", System.Data.SqlDbType.VarChar);
                                command.Parameters.Add("@STATE", System.Data.SqlDbType.VarChar);
                                foreach (string candidateId in candidates)
                                {
                                    command.Parameters["@ID"].Value = id;
                                    command.Parameters["@CANDIDATE"].Value = candidateId;
                                    command.Parameters["@STATE"].Value = state;
                                    command.ExecuteNonQuery();
                                }
                            }

                            //Insertar las notas
                            List<TWAssignationNote> notes = parseNotes(notesJson);
                            foreach (TWAssignationNote note in notes)
                            {
                                createNote(id, username, note.text, note.attachments, conn, transaction);
                            }

                            transaction.Commit();
                            result = new { error = false, id };
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5760, no se han podido crear la asignación" };
                        }
                    }
                }
            }

            return Ok(result);
        }


        // Eliminar la asignación

        [HttpDelete]
        [Route("delete/{assignationId}/")]
        public IActionResult Delete(int assignationId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.Delete", securityToken).Acceso)
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
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM telework_assignations_tasks WHERE assignationId = @ID";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            command.ExecuteNonQuery();
                        }

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM telework_assignations_notes WHERE assignationId = @ID";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            command.ExecuteNonQuery();
                        }

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM telework_assignations WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            command.ExecuteNonQuery();
                        }

                        DeleteDir(new[] { "twassignations", assignationId.ToString() });

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5761, no se han podido borrar la asignación" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("bulk-delete/")]
        public async Task<IActionResult> BulkDelete()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.BulkDelete", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetStringList("ids", out List<string> ids))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        int? assignationId = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT assignationId FROM telework_assignations_tasks WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", ids[0]);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                    assignationId = reader.GetInt32(reader.GetOrdinal("assignationId"));
                            }
                        }

                        if (assignationId == null)
                        {
                            return Ok(new
                            {
                                error = "Error 4763, No se ha encontrado la tarea a la que pertenecen estas subtareas."
                            });
                        }

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "DELETE FROM telework_assignations_tasks WHERE id = @ID";
                            command.Parameters.Add("@ID", System.Data.SqlDbType.Int);
                            foreach (string id in ids)
                            {
                                command.Parameters["@ID"].Value = Int32.Parse(id);
                                command.ExecuteNonQuery();
                            }
                        }

                        int n;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT COUNT(*) FROM telework_assignations_tasks WHERE assignationId = @ID";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            n = (int)command.ExecuteScalar();
                        }

                        if (n == 0)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "DELETE FROM telework_assignations_notes WHERE assignationId = @ID";
                                command.Parameters.AddWithValue("@ID", assignationId);
                                command.ExecuteNonQuery();
                            }

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "DELETE FROM telework_assignations WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", assignationId);
                                command.ExecuteNonQuery();
                            }

                            DeleteDir(new[] { "twassignations", assignationId.ToString() });
                        }

                        result = new { error = false, assignIsDeleted = n == 0 };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5762, no se han podido borrar las tareas" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route("delete-note/{assignationId}/{noteId}/")]
        public IActionResult DeleteNote(int assignationId, int noteId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.DeleteNote", securityToken).Acceso)
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

                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM telework_assignations_notes WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", noteId);
                            command.ExecuteNonQuery();
                        }

                        DeleteFile(new[] { "twassignations", assignationId.ToString(), noteId.ToString() });

                        transaction.Commit();
                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5761, no se pudo borrar la nota" };
                    }
                }
            }

            return Ok(result);
        }

        // Modificacion

        [HttpPut]
        [Route("update/{assignationId}/")]
        public async Task<IActionResult> Update(string assignationId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.Update", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetString("receptorId", out string receptorId) &&
                json.TryGetString("category", out string category) &&
                json.TryGetString("warning", out string warning) &&
                json.TryGetDateOptional("programmedDate", out DateTime? programmedDate) &&
                json.TryGetString("estimatedCompletionTime", out string estimatedCompletionTime))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "UPDATE telework_assignations SET receptorId = @RECEPTOR, category = @CATEGORY, warning = @WARNING, programmedDate = @PROGRAMMED, estimatedCompletionTime = @ESTIMATED WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            command.Parameters.AddWithValue("@RECEPTOR", receptorId);
                            command.Parameters.AddWithValue("@CATEGORY", category);
                            command.Parameters.AddWithValue("@WARNING", (object)warning ?? DBNull.Value);
                            command.Parameters.AddWithValue("@PROGRAMMED", programmedDate == null ? DBNull.Value : programmedDate.Value);
                            command.Parameters.AddWithValue("@ESTIMATED", (object)estimatedCompletionTime ?? DBNull.Value);
                            command.ExecuteNonQuery();
                        }

                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5767, no se han podido actualizar la asignación" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route("add-notes/{assignationId}/")]
        public async Task<IActionResult> AddNotes(int assignationId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.AddNotes", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            List<TWAssignationNote> notes = parseNotes(await readerBody.ReadToEndAsync());

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    string username = FindUsernameBySecurityToken(securityToken, conn);

                    List<TWAssignationNote> newNotes = new();
                    foreach (TWAssignationNote note in notes)
                    {
                        newNotes.Add(createNote(assignationId, username, note.text, note.attachments, conn));
                    }

                    result = new { error = false, notes = newNotes };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5766, no se han podido agregar la nota" };
                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route("update-task/{taskId}/")]
        public async Task<IActionResult> UpdateTask(string taskId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.UpdateTask", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetBoolean("vissible", out bool vissible) && json.TryGetBoolean("closed", out bool closed) && json.TryGetBoolean("validated", out bool validated))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "UPDATE telework_assignations_tasks SET vissible = @VISSIBLE, closed = @CLOSED, validated = @VALIDATED WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", taskId);
                            command.Parameters.AddWithValue("@VISSIBLE", vissible ? 1 : 0);
                            command.Parameters.AddWithValue("@CLOSED", closed ? 1 : 0);
                            command.Parameters.AddWithValue("@VALIDATED", validated ? 1 : 0);
                            command.ExecuteNonQuery();
                        }

                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5768, no se han podido actualizar la tarea" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("add-tasks/{assignationId}/")]
        public async Task<IActionResult> AddTasks(string assignationId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.AddTasks", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetStringList("ids", out List<string> ids))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        foreach (string id in ids)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT COUNT(*) FROM telework_assignations_tasks WHERE assignationId = @ASSIGNATION AND candidateId = @ID";
                                command.Parameters.AddWithValue("@ASSIGNATION", assignationId);
                                command.Parameters.AddWithValue("@ID", id);
                                if ((int)command.ExecuteScalar() > 0) continue;
                            }

                            string state = "abierta";
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "INSERT INTO telework_assignations_tasks (assignationId, candidateId, state) VALUES (@ASSIGNATION, @CANDIDATE, @STATE)";
                                command.Parameters.AddWithValue("@ASSIGNATION", assignationId);
                                command.Parameters.AddWithValue("@CANDIDATE", id);
                                command.Parameters.AddWithValue("@STATE", state);
                                command.ExecuteNonQuery();
                            }
                        }

                        result = new { error = false };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5769, no se han podido agregar las tareas" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route("increment-emails/{taskId}/")]
        public async Task<IActionResult> IncrementEmails(string taskId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("TeleworkAssignations.IncrementEmails", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE telework_assignations_tasks SET emailsSent = emailsSent + 1 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", taskId);
                        command.ExecuteNonQuery();
                    }

                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5768, no se han podido actualizar la tarea" };
                }
            }

            return Ok(result);
        }

        // Obtener datos de la asignación

        [HttpGet]
        [Route("get/{assignationId}/")]
        public IActionResult Get(int assignationId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.Get", securityToken);
            if (!access.Acceso)
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
                    TWAssignation? assignation = getAssignation(assignationId, false, conn, null);

                    if (assignation == null)
                    {
                        result = new { error = "Error 4762, no se ha encontrado la asignación" };
                    }
                    else
                    {
                        result = new
                        {
                            error = false,
                            assignation
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5763, no se han podido obtener la asignación" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("get-mine/{assignationId}/")]
        public IActionResult GetMine(int assignationId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.GetMine", securityToken);
            if (!access.Acceso)
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
                    TWAssignation? assignationTMP = getAssignation(assignationId, true, conn, null);

                    if (assignationTMP == null)
                    {
                        result = new { error = "Error 4762, no se ha encontrado la asignación" };
                    }
                    else
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "UPDATE telework_assignations_tasks SET newsRevisioner = 0 WHERE assignationId = @ID";
                            command.Parameters.AddWithValue("@ID", assignationId);
                            command.ExecuteNonQuery();
                        }

                        TWAssignation assignation = assignationTMP.Value;
                        assignation.creatorId = "0b80202d67d487842eb3a912e47321445a89af8b078821c9e4c7067bcb5ef30d";
                        assignation.creatorName = "Javier López";

                        result = new
                        {
                            error = false,
                            assignation
                        };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5763, no se han podido obtener la asignación" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("get-task/{taskId}/")]
        public IActionResult GetTask(int taskId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.GetTask", securityToken);
            if (!access.Acceso)
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
                    TWAssignationTask? task = getTask(taskId, conn);

                    if (task == null)
                        return Ok(new { error = "Error 4762, no se ha encontrado la tarea" });

                    string newsTarget = getCreatorIdByTaskId(taskId, conn) == FindUserIdBySecurityToken(securityToken, conn) ? "newsCreator" : "newsRevisioner";
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = $"UPDATE telework_assignations_tasks SET {newsTarget} = 0 WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", taskId);
                        command.ExecuteNonQuery();
                    }

                    if (task.Value.rollbackData != null)
                    {
                        TWRollbackData data = task.Value.rollbackData.Value;

                        //Convertir los 7777777 en null
                        //if (data.photo != null && data.photo.Length == 7) data.photo = null;
                        //if (data.cv != null && data.cv.Length == 7) data.cv = null;

                        //Remplazar los blobs por el nombre del atributo para descargarlo
                        if (data.photo != null && data.photo.Length != 7) data.photo = "photo";
                        if (data.cv != null && data.cv.Length != 7) data.cv = "cv";

                        TWAssignationTask taskTMP = task.Value;
                        taskTMP.rollbackData = data;
                        task = taskTMP;
                    }

                    ExtendedCandidateData candidate = getCandidateStats(task.Value.candidateId, conn);

                    result = new
                    {
                        error = false,
                        task,
                        candidate
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5763, no se han podido obtener la tarea" };
                }
            }

            return Ok(result);
        }

        // Listado

        [HttpGet]
        [Route("list/")]
        public IActionResult List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.List", securityToken);
            if (!access.Acceso)
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
                        assignations = listAssignations(null, false, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5764, no se han podido listar las asignaciones" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("list-mine/")]
        public IActionResult ListMine()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.ListMine", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                DateTime now = DateTime.Now.Date;

                try
                {
                    string userId = FindUserIdBySecurityToken(securityToken, conn);

                    result = new
                    {
                        error = false,
                        assignations = listAssignations(userId, true, conn).Where(a => a.programmedDate == null || now >= a.programmedDate).Select(ass =>
                        {
                            ass.creatorId = "0b80202d67d487842eb3a912e47321445a89af8b078821c9e4c7067bcb5ef30d";
                            ass.creatorName = "Javier López";
                            return ass;
                        }).ToList()
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5764, no se han podido listar las asignaciones para el usuario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("list-users/{securityToken}")]
        public IActionResult ListTeleworkUsers()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.ListTeleworkUsers", securityToken);
            if (!access.Acceso)
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
                    List<object> users = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT U.id, CONCAT(U.name, ' ', U.surname) as fullName FROM permisos_modulo PM INNER JOIN users U ON PM.userId = U.id WHERE PM.modulo = 'TELETRABAJO' AND PM.jefe = 0";
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    name = reader.GetString(reader.GetOrdinal("fullName"))
                                });
                            }
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
                    result = new { error = "Error 5424, no se ha podido listar los usuarios" };
                }
            }

            return Ok(result);
        }

        // Acciones

        [HttpPost]
        [Route("apply-changes/{taskId}/")]
        public async Task<IActionResult> ApplyChanges(int taskId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.ApplyChanges", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string dataJson = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(dataJson).RootElement;

            if (!(json.TryGetString("state", out string state) && json.TryGetProperty("changes", out JsonElement changesJson) &&
                json.TryGetBoolean("closed", out bool closed) && json.TryGetString("problemText", out string problemText) &&
                json.TryGetString("problem", out string problem)))
                return Ok(result);

            TWRollbackData data = parseRollbackData(changesJson);

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        TWAssignationTask? taskTMP = getTask(taskId, conn, transaction);
                        if (taskTMP == null)
                            return Ok(new { error = "Error 4762, no se ha encontrado la revisión" });
                        TWAssignationTask task = taskTMP.Value;
                        TWRollbackData rollbackData = task.rollbackData ?? new TWRollbackData();
                        ExtendedCandidateData candidate = getCandidateStats(task.candidateId, conn, transaction);
                        CandidateStats changes = new CandidateStats() { id = task.candidateId };

                        if (!task.vissible && !access.EsJefe)
                            return Ok(new { error = "Error 4765, la tarea está cerrada. No se puede actualizar" });

                        if (data.name != null)
                        {
                            changes.name = data.name;
                            rollbackData.name ??= candidate.name;
                        }
                        if (data.surname != null)
                        {
                            changes.surname = data.surname;
                            rollbackData.surname ??= candidate.surname;
                        }
                        if (data.phone != null)
                        {
                            changes.phone = data.phone;
                            rollbackData.phone ??= candidate.phone;
                        }
                        if (data.email != null)
                        {
                            changes.email = data.email;
                            rollbackData.email ??= candidate.email;
                        }
                        if (data.nacionalidad != null)
                        {
                            changes.nacionalidad = data.nacionalidad;
                            rollbackData.nacionalidad ??= candidate.nacionalidad ?? "";
                        }
                        if (data.direccion != null)
                        {
                            changes.direccion = data.direccion;
                            rollbackData.direccion ??= candidate.direccion ?? "";
                        }
                        if (data.cp != null)
                        {
                            changes.cp = data.cp;
                            rollbackData.cp ??= candidate.cp ?? "";
                        }
                        if (data.provincia != null)
                        {
                            changes.provincia = data.provincia;
                            rollbackData.provincia ??= candidate.provincia ?? "";
                        }
                        if (data.localidad != null)
                        {
                            changes.localidad = data.localidad;
                            rollbackData.localidad ??= candidate.localidad ?? "";
                        }
                        if (data.birth != null)
                        {
                            changes.birth = data.birth;
                            rollbackData.birth ??= candidate.birth ?? DateTime.MinValue;
                        }
                        if (data.sexo != null)
                        {
                            changes.sexo = data.sexo;
                            rollbackData.sexo ??= candidate.sexo ?? ' ';
                        }
                        if (data.permisoTrabajoCaducidad != null)
                        {
                            changes.permisoTrabajoCaducidad = data.permisoTrabajoCaducidad;
                            rollbackData.permisoTrabajoCaducidad ??= candidate.permisoTrabajoCaducidad ?? DateTime.MinValue;
                        }
                        if (data.contactoNombre != null)
                        {
                            changes.contactoNombre = data.contactoNombre;
                            rollbackData.contactoNombre ??= candidate.contactoNombre ?? "";
                        }
                        if (data.contactoTelefono != null)
                        {
                            changes.contactoTelefono = data.contactoTelefono;
                            rollbackData.contactoTelefono ??= candidate.contactoTelefono ?? "";
                        }
                        if (data.contactoTipo != null)
                        {
                            changes.contactoTipo = data.contactoTipo;
                            rollbackData.contactoTipo ??= candidate.contactoTipo ?? "";
                        }

                        if (data.pwd != null)
                        {
                            string oldPwd = null, email = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT pwd, email FROM candidatos WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", task.candidateId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        oldPwd = reader.GetString(reader.GetOrdinal("pwd"));
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                    }
                                }
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE candidatos SET pwd=@PWD WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", task.candidateId);
                                command.Parameters.AddWithValue("@PWD", ComputeStringHash(data.pwd));
                                command.ExecuteNonQuery();
                            }
                            EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "passwordChanged",
                                toEmail = email,
                                subject = "[Think&Job] Contraseña cambiada",
                                priority = EventMailer.EmailPriority.MODERATE
                            });
                            rollbackData.pwd ??= oldPwd;
                        }

                        if (data.photo != null)
                        {
                            changes.photo = data.photo;
                            rollbackData.photo ??= ReadFile(new[] { "candidate", task.candidateId, "photo" }) ?? "7777777";
                        }
                        if (data.cv != null)
                        {
                            changes.cv = data.cv;
                            rollbackData.cv ??= ReadFile(new[] { "candidate", task.candidateId, "cv" }) ?? "7777777";
                        }

                        string newsTarget = getCreatorIdByTaskId(taskId, conn, transaction) == FindUserIdBySecurityToken(securityToken, conn, transaction) ? "newsRevisioner" : "newsCreator";
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = $"UPDATE telework_assignations_tasks SET rollbackData=@DATA, {newsTarget}=1, state=@STATE, problemText = @PTEXT, problem = @PROBLEM, closed = @CLOSED, rolledBack=0 WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", taskId);
                            command.Parameters.AddWithValue("@DATA", JsonSerializer.Serialize(rollbackData));
                            command.Parameters.AddWithValue("@STATE", state);
                            command.Parameters.AddWithValue("@PROBLEM", (object)problem ?? DBNull.Value);
                            command.Parameters.AddWithValue("@PTEXT", (object)problemText ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CLOSED", closed ? 1 : 0);
                            command.ExecuteNonQuery();
                        }

                        UpdateResult updateResut = updateCandidateData(conn, transaction, changes, false);

                        if (updateResut.failed)
                        {
                            transaction.Rollback();
                            result = updateResut.result;
                        }
                        else
                        {
                            transaction.Commit();
                            result = new
                            {
                                error = false
                            };
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5765, no se han podido realizar los cambios" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("rollback/{taskId}/{attribute}/")]
        public async Task<IActionResult> Rollback(int taskId, string attribute)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.ApplyChanges", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            if (attribute == "null")
                attribute = null;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        TWAssignationTask? taskTMP = getTask(taskId, conn, transaction);
                        if (taskTMP == null)
                            return Ok(new { error = "Error 4762, no se ha encontrado la revisión" });
                        TWAssignationTask task = taskTMP.Value;
                        TWRollbackData rollbackData = task.rollbackData ?? new TWRollbackData();
                        CandidateStats changes = new CandidateStats() { id = task.candidateId };

                        if (!task.vissible && !access.EsJefe)
                            return Ok(new { error = "Error 4765, la tarea está cerrada. No se puede actualizar" });

                        if (rollbackData.name != null && (attribute == null || attribute == "name"))
                        {
                            changes.name = rollbackData.name;
                            rollbackData.name = null;
                        }
                        if (rollbackData.surname != null && (attribute == null || attribute == "surname"))
                        {
                            changes.surname = rollbackData.surname;
                            rollbackData.surname = null;
                        }
                        if (rollbackData.phone != null && (attribute == null || attribute == "phone"))
                        {
                            changes.phone = rollbackData.phone;
                            rollbackData.phone = null;
                        }
                        if (rollbackData.email != null && (attribute == null || attribute == "email"))
                        {
                            changes.email = rollbackData.email;
                            rollbackData.email = null;
                            //La solicitud de cambio persiste
                        }
                        if (rollbackData.nacionalidad != null && (attribute == null || attribute == "nacionalidad"))
                        {
                            changes.nacionalidad = rollbackData.nacionalidad;
                            rollbackData.nacionalidad = null;
                        }
                        if (rollbackData.direccion != null && (attribute == null || attribute == "direccion"))
                        {
                            changes.direccion = rollbackData.direccion;
                            rollbackData.direccion = null;
                        }
                        if (rollbackData.cp != null && (attribute == null || attribute == "cp"))
                        {
                            changes.cp = rollbackData.cp;
                            rollbackData.cp = null;
                        }
                        if (rollbackData.provincia != null && (attribute == null || attribute == "provincia"))
                        {
                            changes.provincia = rollbackData.provincia;
                            rollbackData.provincia = null;
                        }
                        if (rollbackData.localidad != null && (attribute == null || attribute == "localidad"))
                        {
                            changes.localidad = rollbackData.localidad;
                            rollbackData.localidad = null;
                        }
                        if (rollbackData.birth != null && (attribute == null || attribute == "birth"))
                        {
                            changes.birth = rollbackData.birth;
                            rollbackData.birth = null;
                        }
                        if (rollbackData.sexo != null && (attribute == null || attribute == "sexo"))
                        {
                            changes.sexo = rollbackData.sexo;
                            rollbackData.sexo = null;
                        }
                        if (rollbackData.permisoTrabajoCaducidad != null && (attribute == null || attribute == "permisoTrabajoCaducidad"))
                        {
                            changes.permisoTrabajoCaducidad = rollbackData.permisoTrabajoCaducidad;
                            rollbackData.permisoTrabajoCaducidad = null;
                        }
                        if (rollbackData.contactoNombre != null && (attribute == null || attribute == "contactoNombre"))
                        {
                            changes.contactoNombre = rollbackData.contactoNombre;
                            rollbackData.contactoNombre = null;
                        }
                        if (rollbackData.contactoTelefono != null && (attribute == null || attribute == "contactoTelefono"))
                        {
                            changes.contactoTelefono = rollbackData.contactoTelefono;
                            rollbackData.contactoTelefono = null;
                        }
                        if (rollbackData.contactoTipo != null && (attribute == null || attribute == "contactoTipo"))
                        {
                            changes.contactoTipo = rollbackData.contactoTipo;
                            rollbackData.contactoTipo = null;
                        }

                        if (rollbackData.pwd != null && (attribute == null || attribute == "pwd"))
                        {
                            string email = null;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT pwd, email FROM candidatos WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", task.candidateId);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        email = reader.GetString(reader.GetOrdinal("email"));
                                    }
                                }
                            }
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE candidatos SET pwd=@PWD WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", task.candidateId);
                                command.Parameters.AddWithValue("@PWD", rollbackData.pwd);
                                command.ExecuteNonQuery();
                            }
                            EventMailer.SendEmail(new EventMailer.Email()
                            {
                                template = "passwordChanged",
                                toEmail = email,
                                subject = "[Think&Job] Contraseña cambiada",
                                priority = EventMailer.EmailPriority.MODERATE
                            });
                            rollbackData.pwd = null;
                        }

                        if (rollbackData.photo != null && (attribute == null || attribute == "photo"))
                        {
                            changes.photo = rollbackData.photo;
                            rollbackData.photo = null;
                        }
                        if (rollbackData.cv != null && (attribute == null || attribute == "cv"))
                        {
                            changes.cv = rollbackData.cv;
                            rollbackData.cv = null;
                        }

                        string newsTarget = getCreatorIdByTaskId(taskId, conn, transaction) == FindUserIdBySecurityToken(securityToken, conn, transaction) ? "newsRevisioner" : "newsCreator";
                        string setRolledBack = task.closed ? ", rolledBack = 1" : "";
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = $"UPDATE telework_assignations_tasks SET rollbackData=@DATA, {newsTarget}=1 {setRolledBack} WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", taskId);
                            command.Parameters.AddWithValue("@DATA", attribute == null ? DBNull.Value : JsonSerializer.Serialize(rollbackData));
                            command.ExecuteNonQuery();
                        }

                        UpdateResult updateResut = updateCandidateData(conn, transaction, changes, false);

                        if (updateResut.failed)
                        {
                            transaction.Rollback();
                            result = updateResut.result;
                        }
                        else
                        {
                            transaction.Commit();
                            result = new
                            {
                                error = false
                            };
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 5765, no se han podido realizar los cambios" };
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("download-attachment/{taskId}/{attachment}/")]
        public IActionResult DownloadAttachment(int taskId, string attachment)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("TeleworkAssignations.DownloadAttachment", securityToken);
            if (!access.Acceso)
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
                    TWAssignationTask? revision = getTask(taskId, conn);

                    if (!revision.HasValue || !revision.Value.rollbackData.HasValue)
                        return Ok(new { error = "Error 4763, cambios no encontrados" });

                    string value = null;

                    switch (attachment)
                    {
                        case "photo":
                            value = revision.Value.rollbackData.Value.photo;
                            break;
                        case "cv":
                            value = revision.Value.rollbackData.Value.cv;
                            break;
                    }

                    result = new
                    {
                        error = (object)(value == null ? "Error 4763, cambio no encontrado" : false),
                        value
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5763, no se han podido obtener los cambios" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("download-note/{assignationId}/{noteId}/")]
        public IActionResult DownloadNote(int assignationId, string noteId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            ResultadoAcceso access = HasPermission("TeleworkAssignations.DownloadNote", securityToken);
            if (!access.Acceso)
            {
                return new ForbidResult();
            }

            string base64 = ReadFile(new[] { "twassignations", assignationId.ToString(), noteId.ToString() });
            if (base64 == null)
            {
                return new NoContentResult();
            }

            if (base64.Contains(",")) base64 = base64.Split(",")[1];
            byte[] bytes = Convert.FromBase64String(base64);

            string contentType = "application/zip";
            HttpContext.Response.ContentType = contentType;
            var response = new FileContentResult(bytes, contentType)
            {
                FileDownloadName = $"nota{noteId}.zip"
            };

            return response;
        }

        [HttpPost]
        [Route(template: "list-candidates/")]
        public async Task<IActionResult> ListCandidates()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("TeleworkAssignations.ListCandidates", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("key", out JsonElement keyJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) && json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("tieneTrabajo", out JsonElement tieneTrabajoJson) &&
                json.TryGetProperty("cesionActiva", out JsonElement cesionActivaJson) && json.TryGetProperty("perfilCompleto", out JsonElement perfilCompletoJson))
            {

                string key = keyJson.GetString();
                string companyId = companyIdJson.GetString();
                string centroId = centroIdJson.GetString();
                bool? tieneTrabajo = GetJsonBool(tieneTrabajoJson);
                bool? cesionActiva = GetJsonBool(cesionActivaJson);
                bool? perfilCompleto = GetJsonBool(perfilCompletoJson);

                try
                {
                    List<TWCandidateData> candidates = listCandidates(companyId, centroId, tieneTrabajo, cesionActiva, perfilCompleto, key);
                    result = new { error = false, candidates };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5769, no han podido listar los candidatos" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------

        // Ayuda

        public struct TWAssignation
        {
            public int id { get; set; }
            public string receptorId { get; set; }
            public string receptorName { get; set; }
            public string receptorDni { get; set; }
            public string creatorId { get; set; }
            public string creatorName { get; set; }
            public string creatorDni { get; set; }
            public string category { get; set; } // missingData | simpleAdvise
            public string warning { get; set; }
            public DateTime creationTime { get; set; }
            public DateTime updateTime { get; set; }
            public DateTime? programmedDate { get; set; }
            public string estimatedCompletionTime { get; set; }
            public bool newsCreator { get; set; }
            public bool newsRevisioner { get; set; }
            public List<TWAssignationNote> notes { get; set; }
            public List<TWAssignationTask> tasks { get; set; }
            public List<string> companies { get; set; }
            public List<string> centros { get; set; }
            public bool problems { get; set; }
            public int completed { get; set; }
            public int nTasks { get; set; }
            public List<TWAssignationStateCounter> taskStateCounter { get; set; }
        }

        public struct TWAssignationTask
        {
            public int id { get; set; }
            public int assignationId { get; set; }
            public string candidateId { get; set; }
            public string candidateDNI { get; set; }
            public string candidateName { get; set; }
            public string candidatePhone { get; set; }
            public string candidateCompany { get; set; }
            public string candidateCompanyId { get; set; }
            public string candidateCentro { get; set; }
            public bool candidateFullProfile { get; set; }
            public string state { get; set; } // abierta, llamada-contestada, llamada-no-contestada, problema
            public bool rolledBack { get; set; }
            public bool closed { get; set; }
            public bool vissible { get; set; }
            public string problem { get; set; }
            public string problemText { get; set; }
            public int emailsSent { get; set; }
            public TWRollbackData? rollbackData { get; set; } //Lista de cambios con valores antiguos
            public bool newsCreator { get; set; }
            public bool newsRevisioner { get; set; }
            public bool validated { get; set; }
        }

        public struct TWAssignationNote
        {
            public int id { get; set; }
            public string username { get; set; }
            public string text { get; set; }
            public bool hasAttachments { get; set; }
            public DateTime time { get; set; }
            public List<Attatchment> attachments { get; set; }
        }

        public struct TWRollbackData
        {
            public string name { get; set; }
            public string surname { get; set; }
            public string phone { get; set; }
            public string email { get; set; }
            public string nacionalidad { get; set; }
            public string direccion { get; set; }
            public string cp { get; set; }
            public string provincia { get; set; }
            public string localidad { get; set; }
            public DateTime? birth { get; set; }
            public char? sexo { get; set; }
            public DateTime? permisoTrabajoCaducidad { get; set; }
            public string pwd { get; set; }
            public string contactoNombre { get; set; }
            public string contactoTelefono { get; set; }
            public string contactoTipo { get; set; }
            public string photo { get; set; }
            public string cv { get; set; }
        }

        public struct TWAssignationStateCounter
        {
            public string state { get; set; }
            public int number { get; set; }
        }

        public struct TWCandidateData
        {
            public string id { get; set; }
            public string dni { get; set; }
            public string fullName { get; set; }
            public string category { get; set; }
            public string email { get; set; }
            public bool fullProfile { get; set; }
            public string companyName { get; set; }
            public string centerName { get; set; }
        }


        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        public static List<TWAssignation> listAssignations(string receptorId, bool onlyMine, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<TWAssignation> assignations = new();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT TA.*, CONCAT(RU.name, ' ', RU.surname) as receptorName, RU.DocID as receptorDni, CONCAT(CU.name, ' ', CU.surname) as creatorName, CU.DocID as creatorDni " +
                    "FROM telework_assignations TA " +
                    "INNER JOIN users CU ON(TA.creatorId = CU.id) " +
                    "INNER JOIN users RU ON(TA.receptorId = RU.id) " +
                    "WHERE (@RECEPTOR IS NULL OR RU.id = @RECEPTOR) " +
                    "ORDER BY TA.id DESC";
                command.Parameters.AddWithValue("@RECEPTOR", (object)receptorId ?? DBNull.Value);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        assignations.Add(new TWAssignation()
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            creatorId = reader.GetString(reader.GetOrdinal("creatorId")),
                            creatorDni = reader.GetString(reader.GetOrdinal("creatorDni")),
                            creatorName = reader.GetString(reader.GetOrdinal("creatorName")),
                            receptorId = reader.GetString(reader.GetOrdinal("receptorId")),
                            receptorDni = reader.GetString(reader.GetOrdinal("receptorDni")),
                            receptorName = reader.GetString(reader.GetOrdinal("receptorName")),
                            category = reader.GetString(reader.GetOrdinal("category")),
                            warning = reader.IsDBNull(reader.GetOrdinal("warning")) ? null : reader.GetString(reader.GetOrdinal("warning")),
                            creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            updateTime = reader.GetDateTime(reader.GetOrdinal("updateTime")),
                            programmedDate = reader.IsDBNull(reader.GetOrdinal("programmedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("programmedDate")),
                            estimatedCompletionTime = reader.IsDBNull(reader.GetOrdinal("estimatedCompletionTime")) ? null : reader.GetString(reader.GetOrdinal("estimatedCompletionTime")),
                            companies = new(),
                            centros = new(),
                            tasks = new()
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
                    "SELECT TR.*, C.dni, CONCAT(C.nombre, ' ', C.apellidos) as nombre " +
                    "FROM telework_assignations_tasks TR " +
                    "INNER JOIN candidatos C ON(TR.candidateId = C.id) " +
                    "WHERE TR.assignationId = @ID";
                command.Parameters.Add("@ID", System.Data.SqlDbType.Int);

                for (int i = 0; i < assignations.Count; i++)
                {
                    TWAssignation assignation = assignations[i];
                    command.Parameters["@ID"].Value = assignation.id;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            assignation.tasks.Add(new TWAssignationTask()
                            {
                                id = reader.GetInt32(reader.GetOrdinal("id")),
                                candidateDNI = reader.GetString(reader.GetOrdinal("dni")),
                                candidateName = reader.GetString(reader.GetOrdinal("nombre")),
                                state = reader.GetString(reader.GetOrdinal("state")),
                                rolledBack = reader.GetInt32(reader.GetOrdinal("rolledBack")) == 1,
                                closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                                vissible = reader.GetInt32(reader.GetOrdinal("vissible")) == 1,
                                rollbackData = reader.IsDBNull(reader.GetOrdinal("rollbackData")) ? null : new TWRollbackData(), //Se descarga a parte
                                newsCreator = reader.GetInt32(reader.GetOrdinal("newsCreator")) == 1,
                                newsRevisioner = reader.GetInt32(reader.GetOrdinal("newsRevisioner")) == 1
                            });
                        }
                    }

                    if (onlyMine)
                        assignation.tasks = assignation.tasks.Where(t => t.vissible).ToList();

                    assignation.newsRevisioner = assignation.tasks.Any(c => c.newsRevisioner);
                    assignation.newsCreator = assignation.tasks.Any(c => c.newsCreator);
                    assignation.nTasks = assignation.tasks.Count;
                    assignation.completed = (int)Math.Round((assignation.tasks.Count(c => c.closed) / (float)assignation.nTasks) * 100.0);
                    assignation.problems = assignation.tasks.Any(c => c.state == "problema");
                    assignation.taskStateCounter = assignation.tasks.Select(c => c.state).GroupBy(s => s).Select(g => new TWAssignationStateCounter() { state = g.Key, number = g.Count() }).ToList();

                    assignation.tasks.Clear(); //Quitar para que no vaya al front
                    assignations[i] = assignation;
                }
            }

            assignations = assignations.Where(a => a.nTasks > 0).ToList();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT DISTINCT E.nombre " +
                    "FROM telework_assignations_tasks TR " +
                    "INNER JOIN candidatos C ON(TR.candidateId = C.id) " +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE TR.assignationId = @ASSIGNATION";
                command.Parameters.Add("@ASSIGNATION", System.Data.SqlDbType.Int);

                foreach (TWAssignation assignation in assignations)
                {
                    command.Parameters["@ASSIGNATION"].Value = assignation.id;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            assignation.companies.Add(reader.GetString(reader.GetOrdinal("nombre")));
                        }
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
                    "SELECT DISTINCT CE.alias " +
                    "FROM telework_assignations_tasks TR " +
                    "INNER JOIN candidatos C ON(TR.candidateId = C.id) " +
                    "INNER JOIN centros CE ON(C.centroId = CE.id) " +
                    "WHERE TR.assignationId = @ASSIGNATION";
                command.Parameters.Add("@ASSIGNATION", System.Data.SqlDbType.Int);

                foreach (TWAssignation assignation in assignations)
                {
                    command.Parameters["@ASSIGNATION"].Value = assignation.id;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            assignation.centros.Add(reader.GetString(reader.GetOrdinal("alias")));
                        }
                    }
                }
            }

            return assignations;
        }

        public static TWAssignation? getAssignation(int assignationId, bool onlyMine, SqlConnection conn, SqlTransaction transaction = null)
        {
            bool found = false;
            TWAssignation assignation = new TWAssignation();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT TA.*, CONCAT(RU.name, ' ', RU.surname) as receptorName, RU.DocID as receptorDni, CONCAT(CU.name, ' ', CU.surname) as creatorName, CU.DocID as creatorDni " +
                    "FROM telework_assignations TA " +
                    "INNER JOIN users CU ON(TA.creatorId = CU.id) " +
                    "INNER JOIN users RU ON(TA.receptorId = RU.id) " +
                    "WHERE TA.id = @ID";
                command.Parameters.AddWithValue("@ID", assignationId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        assignation = new TWAssignation()
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            creatorId = reader.GetString(reader.GetOrdinal("creatorId")),
                            creatorDni = reader.GetString(reader.GetOrdinal("creatorDni")),
                            creatorName = reader.GetString(reader.GetOrdinal("creatorName")),
                            receptorId = reader.GetString(reader.GetOrdinal("receptorId")),
                            receptorDni = reader.GetString(reader.GetOrdinal("receptorDni")),
                            receptorName = reader.GetString(reader.GetOrdinal("receptorName")),
                            category = reader.GetString(reader.GetOrdinal("category")),
                            warning = reader.IsDBNull(reader.GetOrdinal("warning")) ? null : reader.GetString(reader.GetOrdinal("warning")),
                            creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            updateTime = reader.GetDateTime(reader.GetOrdinal("updateTime")),
                            programmedDate = reader.IsDBNull(reader.GetOrdinal("programmedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("programmedDate")),
                            estimatedCompletionTime = reader.IsDBNull(reader.GetOrdinal("estimatedCompletionTime")) ? null : reader.GetString(reader.GetOrdinal("estimatedCompletionTime")),
                            notes = new(),
                            tasks = new()
                        };
                        found = true;
                    }
                }
            }

            if (!found) return null;

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT TR.*, C.dni, CONCAT(C.nombre, ' ', C.apellidos) as nombre, C.allDataFilledIn, E.id as candidateCompanyId, E.nombre as candidateCompany, CE.alias as candidateCentro " +
                    "FROM telework_assignations_tasks TR " +
                    "INNER JOIN candidatos C ON(TR.candidateId = C.id) " +
                    "LEFT OUTER JOIN centros CE ON(C.centroId = CE.id) " +
                    "LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE TR.assignationId = @ID " +
                    "ORDER BY closed, nombre ASC";
                command.Parameters.AddWithValue("@ID", assignationId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        assignation.tasks.Add(new TWAssignationTask()
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            candidateDNI = reader.GetString(reader.GetOrdinal("dni")),
                            candidateName = reader.GetString(reader.GetOrdinal("nombre")),
                            candidateFullProfile = reader.GetInt32(reader.GetOrdinal("allDataFilledIn")) == 1,
                            candidateCompany = reader.IsDBNull(reader.GetOrdinal("candidateCompany")) ? null : reader.GetString(reader.GetOrdinal("candidateCompany")),
                            candidateCompanyId = reader.IsDBNull(reader.GetOrdinal("candidateCompanyId")) ? null : reader.GetString(reader.GetOrdinal("candidateCompanyId")),
                            candidateCentro = reader.IsDBNull(reader.GetOrdinal("candidateCentro")) ? null : reader.GetString(reader.GetOrdinal("candidateCentro")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            rolledBack = reader.GetInt32(reader.GetOrdinal("rolledBack")) == 1,
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            vissible = reader.GetInt32(reader.GetOrdinal("vissible")) == 1,
                            emailsSent = reader.GetInt32(reader.GetOrdinal("emailsSent")),
                            rollbackData = reader.IsDBNull(reader.GetOrdinal("rollbackData")) ? null : new TWRollbackData(), //Se descarga a parte
                            problem = reader.IsDBNull(reader.GetOrdinal("problem")) ? null : reader.GetString(reader.GetOrdinal("problem")),
                            problemText = reader.IsDBNull(reader.GetOrdinal("problemText")) ? null : reader.GetString(reader.GetOrdinal("problemText")),
                            newsCreator = reader.GetInt32(reader.GetOrdinal("newsCreator")) == 1,
                            newsRevisioner = reader.GetInt32(reader.GetOrdinal("newsRevisioner")) == 1,
                            validated = reader.GetInt32(reader.GetOrdinal("validated")) == 1
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
                    "SELECT * FROM telework_assignations_notes WHERE assignationId = @ID ORDER BY time DESC";
                command.Parameters.AddWithValue("@ID", assignationId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        assignation.notes.Add(new TWAssignationNote()
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            text = reader.GetString(reader.GetOrdinal("text")),
                            username = reader.GetString(reader.GetOrdinal("username")),
                            time = reader.GetDateTime(reader.GetOrdinal("time")),
                            hasAttachments = reader.GetInt32(reader.GetOrdinal("hasAttachments")) == 1
                        });
                    }
                }
            }

            if (onlyMine)
                assignation.tasks = assignation.tasks.Where(t => t.vissible).ToList();

            assignation.newsRevisioner = assignation.tasks.Any(c => c.newsRevisioner);
            assignation.newsCreator = assignation.tasks.Any(c => c.newsCreator);
            assignation.nTasks = assignation.tasks.Count;
            assignation.completed = (int)Math.Round((assignation.tasks.Count(c => c.closed) / (float)assignation.nTasks) * 100.0);
            assignation.problems = assignation.tasks.Any(c => c.state == "problema");
            assignation.taskStateCounter = assignation.tasks.Select(c => c.state).GroupBy(s => s).Select(g => new TWAssignationStateCounter() { state = g.Key, number = g.Count() }).ToList();

            return assignation;
        }

        public static TWAssignationTask? getTask(int taskId, SqlConnection conn, SqlTransaction transaction = null)
        {
            bool found = false;
            TWAssignationTask revision = new TWAssignationTask();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText =
                    "SELECT TR.*, C.dni, CONCAT(C.nombre, ' ', C.apellidos) as nombre, C.telefono, C.allDataFilledIn, E.id as candidateCompanyId, E.nombre as candidateCompany, CE.alias as candidateCentro " +
                    "FROM telework_assignations_tasks TR " +
                    "INNER JOIN candidatos C ON(TR.candidateId = C.id) " +
                    "LEFT OUTER JOIN centros CE ON(C.centroId = CE.id) " +
                    "LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                    "WHERE TR.id = @ID";
                command.Parameters.AddWithValue("@ID", taskId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        revision = new TWAssignationTask()
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            candidateId = reader.GetString(reader.GetOrdinal("candidateId")),
                            candidateDNI = reader.GetString(reader.GetOrdinal("dni")),
                            candidateName = reader.GetString(reader.GetOrdinal("nombre")),
                            candidatePhone = reader.GetString(reader.GetOrdinal("telefono")),
                            candidateFullProfile = reader.GetInt32(reader.GetOrdinal("allDataFilledIn")) == 1,
                            candidateCompany = reader.IsDBNull(reader.GetOrdinal("candidateCompany")) ? null : reader.GetString(reader.GetOrdinal("candidateCompany")),
                            candidateCompanyId = reader.IsDBNull(reader.GetOrdinal("candidateCompanyId")) ? null : reader.GetString(reader.GetOrdinal("candidateCompanyId")),
                            candidateCentro = reader.IsDBNull(reader.GetOrdinal("candidateCentro")) ? null : reader.GetString(reader.GetOrdinal("candidateCentro")),
                            state = reader.GetString(reader.GetOrdinal("state")),
                            rolledBack = reader.GetInt32(reader.GetOrdinal("rolledBack")) == 1,
                            closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                            vissible = reader.GetInt32(reader.GetOrdinal("vissible")) == 1,
                            emailsSent = reader.GetInt32(reader.GetOrdinal("emailsSent")),
                            problem = reader.IsDBNull(reader.GetOrdinal("problem")) ? null : reader.GetString(reader.GetOrdinal("problem")),
                            problemText = reader.IsDBNull(reader.GetOrdinal("problemText")) ? null : reader.GetString(reader.GetOrdinal("problemText")),
                            rollbackData = reader.IsDBNull(reader.GetOrdinal("rollbackData")) ? null : parseRollbackDAta(reader.GetString(reader.GetOrdinal("rollbackData"))),
                            newsCreator = reader.GetInt32(reader.GetOrdinal("newsCreator")) == 1,
                            newsRevisioner = reader.GetInt32(reader.GetOrdinal("newsRevisioner")) == 1,
                            validated = reader.GetInt32(reader.GetOrdinal("validated")) == 1
                        };
                        found = true;
                    }
                }
            }

            return found ? revision : null;
        }

        public static string getCreatorIdByTaskId(int taskId, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            string result = "ERROR";
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT TA.creatorId FROM telework_assignations TA INNER JOIN telework_assignations_tasks TR ON(TR.assignationId = TA.id) WHERE TR.id = @ID";

                    command.Parameters.AddWithValue("@ID", taskId);

                    result = (string)command.ExecuteScalar();
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception) { }
            return result;
        }
        public static List<TWCandidateData> listCandidates(string companyId, string centroId, bool? tieneTrabajo, bool? cesionActiva, bool? perfilCompleto, string key)
        {
            List<TWCandidateData> candidates = new List<TWCandidateData>();
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText =
                        "SELECT C.*, TRIM(CONCAT(C.apellidos, ', ', C.nombre)) as fullName, " +
                        "CE.alias as centroAlias, " +
                        "T.id as idTrabajo, " +
                        "CA.name as nombreTrabajo, " +
                        "E.id as idEmpresa, " +
                        "E.nombre as nombreEmpresa, " +
                        "documentsDownloaded = C.calc_documents_downloaded " +
                        "FROM candidatos C " +
                        "LEFT OUTER JOIN trabajos T ON(C.lastSignLink = T.signLink) " +
                        "LEFT OUTER JOIN categories CA ON(T.categoryId = CA.id) " +
                        "LEFT OUTER JOIN centros CE ON(T.centroId = CE.id) " +
                        "LEFT OUTER JOIN empresas E ON(CE.companyId = E.id) " +
                        "WHERE C.test = 0 AND " +
                        "(@COMPANY IS NULL OR E.id = @COMPANY) AND " +
                        "(@CENTRO IS NULL OR C.centroId = @CENTRO) AND " +
                        "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI OR dni LIKE @KEY OR C.telefono LIKE @KEY OR C.email LIKE @KEY OR CA.name LIKE @KEY) AND " +
                        "(@TIENE_TRABAJO IS NULL OR (@TIENE_TRABAJO = 1 AND T.id IS NOT NULL) OR (@TIENE_TRABAJO = 0 AND T.id IS NULL)) AND " +
                        "(@CESION_ACTIVA IS NULL OR @CESION_ACTIVA = CASE WHEN C.active = 1 AND C.lastSignLink IS NOT NULL AND (C.fechaComienzoTrabajo IS NULL OR C.fechaComienzoTrabajo <= @NOW) THEN 1 ELSE 0 END) AND " +
                        "(@PERFIL_COMPLETO IS NULL OR @PERFIL_COMPLETO = C.allDataFilledIn) ";

                    command.Parameters.AddWithValue("@NOW", DateTime.Now);
                    command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@COMPANY", ((object)companyId) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);
                    command.Parameters.AddWithValue("@TIENE_TRABAJO", tieneTrabajo == null ? DBNull.Value : (tieneTrabajo.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@CESION_ACTIVA", cesionActiva == null ? DBNull.Value : (cesionActiva.Value ? 1 : 0));
                    command.Parameters.AddWithValue("@PERFIL_COMPLETO", perfilCompleto == null ? DBNull.Value : (perfilCompleto.Value ? 1 : 0));

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TWCandidateData candidate = new TWCandidateData();
                            candidate.id = reader.GetString(reader.GetOrdinal("id"));
                            candidate.dni = reader.GetString(reader.GetOrdinal("dni"));
                            candidate.fullName = reader.GetString(reader.GetOrdinal("fullName"));
                            candidate.email = reader.GetString(reader.GetOrdinal("email"));
                            candidate.category = reader.IsDBNull(reader.GetOrdinal("nombreTrabajo")) ? null : reader.GetString(reader.GetOrdinal("nombreTrabajo"));
                            candidate.companyName = reader.IsDBNull(reader.GetOrdinal("nombreEmpresa")) ? null : reader.GetString(reader.GetOrdinal("nombreEmpresa"));
                            candidate.centerName = reader.IsDBNull(reader.GetOrdinal("centroAlias")) ? null : reader.GetString(reader.GetOrdinal("centroAlias"));
                            candidate.fullProfile = reader.GetInt32(reader.GetOrdinal("allDataFilledIn")) == 1;
                            candidates.Add(candidate);
                        }
                    }
                }
            }

            return candidates;
        }
        public static TWAssignationNote createNote(int assignationId, string username, string text, List<Attatchment> attachments, SqlConnection conn, SqlTransaction transaction = null)
        {
            int noteId;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "INSERT INTO telework_assignations_notes (assignationId, username, text, hasAttachments) output INSERTED.ID VALUES (@ID, @USER, @TEXT, @ATTR)";
                command.Parameters.AddWithValue("@ID", assignationId);
                command.Parameters.AddWithValue("@USER", username);
                command.Parameters.AddWithValue("@TEXT", text);
                command.Parameters.AddWithValue("@ATTR", attachments.Count > 0 ? 1 : 0);
                noteId = (int)command.ExecuteScalar();
            }

            string tmpDir = GetTemporaryDirectory();
            foreach (Attatchment attr in attachments)
            {
                string base64 = attr.base64;
                if (base64.Contains(",")) base64 = base64.Split(",")[1];
                byte[] bytes = Convert.FromBase64String(base64);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(tmpDir, attr.filename), bytes);
            }
            string tmpZipDir = GetTemporaryDirectory();
            string tmpOutZip = System.IO.Path.Combine(tmpZipDir, "zip.zip");
            ZipFile.CreateFromDirectory(tmpDir, tmpOutZip);
            byte[] zipBytes = System.IO.File.ReadAllBytes(tmpOutZip);
            Directory.Delete(tmpDir, true);
            Directory.Delete(tmpZipDir, true);
            SaveFile(new[] { "twassignations", assignationId.ToString(), noteId.ToString() }, "" + Convert.ToBase64String(zipBytes));

            return new TWAssignationNote()
            {
                id = noteId,
                username = username,
                hasAttachments = attachments.Count > 0,
                text = text,
                time = DateTime.Now
            };
        }

        public static List<TWAssignationNote> parseNotes(JsonElement json)
        {
            List<TWAssignationNote> notas = new();
            if (json.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement notaJson in json.EnumerateArray())
                {
                    if (notaJson.TryGetString("text", out string text) &&
                        notaJson.TryGetProperty("attachments", out JsonElement attachmentsJson))
                    {
                        TWAssignationNote note = new()
                        {
                            text = text,
                            attachments = new()
                        };
                        foreach (JsonElement noteJson in attachmentsJson.EnumerateArray())
                        {
                            if (noteJson.TryGetString("filename", out string filename) &&
                                noteJson.TryGetString("base64", out string base64))
                            {
                                note.attachments.Add(new Attatchment()
                                {
                                    filename = filename,
                                    base64 = base64
                                });
                            }
                        }
                        notas.Add(note);
                    }
                }
            }
            return notas;
        }
        public static List<TWAssignationNote> parseNotes(string json)
        {
            return parseNotes(JsonDocument.Parse(json).RootElement);
        }

        public static TWRollbackData parseRollbackData(JsonElement json)
        {
            TWRollbackData data = new TWRollbackData();

            if (json.TryGetString("name", out string name)) data.name = name;
            if (json.TryGetString("surname", out string surname)) data.surname = surname;
            if (json.TryGetString("phone", out string phone)) data.phone = phone;
            if (json.TryGetString("email", out string email)) data.email = email;
            if (json.TryGetString("nacionalidad", out string nacionalidad)) data.nacionalidad = nacionalidad;
            if (json.TryGetString("direccion", out string direccion)) data.direccion = direccion;
            if (json.TryGetString("cp", out string cp)) data.cp = cp;
            if (json.TryGetString("provincia", out string provincia)) data.provincia = provincia;
            if (json.TryGetString("localidad", out string localidad)) data.localidad = localidad;
            if (json.TryGetDateTime("birth", out DateTime birth)) data.birth = birth;
            if (json.TryGetChar("sexo", out char sexo)) data.sexo = sexo;
            if (json.TryGetDateTime("permisoTrabajoCaducidad", out DateTime permisoTrabajoCaducidad)) data.permisoTrabajoCaducidad = permisoTrabajoCaducidad;
            if (json.TryGetString("pwd", out string pwd)) data.pwd = pwd;
            if (json.TryGetString("contactoNombre", out string contactoNombre)) data.contactoNombre = contactoNombre;
            if (json.TryGetString("contactoTelefono", out string contactoTelefono)) data.contactoTelefono = contactoTelefono;
            if (json.TryGetString("contactoTipo", out string contactoTipo)) data.contactoTipo = contactoTipo;
            if (json.TryGetString("photo", out string photo)) data.photo = photo;
            if (json.TryGetString("cv", out string cv)) data.cv = cv;

            return data;
        }
        public static TWRollbackData parseRollbackDAta(string json)
        {
            return parseRollbackData(JsonDocument.Parse(json).RootElement);
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
