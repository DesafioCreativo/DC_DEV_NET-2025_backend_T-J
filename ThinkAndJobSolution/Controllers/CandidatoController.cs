using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Interfaces;
using ThinkAndJobSolution.Request;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers
{
    [ApiController]
    [Route("api/v1/candidate/")]
    public class CandidateController : ControllerBase
    {
        private readonly ICandidatoService _candidateService;

        public CandidateController(ICandidatoService candidateService)
        {
            _candidateService = candidateService;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> RegistrarCandidato([FromForm] RegisterCandidateRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Error 4000, datos inválidos." });
            }

            try
            {
                var response = await _candidateService.RegistrarCandidatoAsync(request);
                return response;
            }
            catch (Exception)
            {
                return Ok(new { error = "Error 5811, no se ha podido leer la categoria" });
            }

        }

        [HttpGet]
        [Route(template: "activate-email-change/{code}")]
        public IActionResult ActivarEmailCandidato(string code)
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
    }
}
