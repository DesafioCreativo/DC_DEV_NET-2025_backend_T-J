using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Servicios.CandidatoService;
using ThinkAndJobSolution.Request;
using NPOI.SS.Formula.Functions;
using ThinkAndJobSolution.Controllers._Helper.Ohers;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Utils;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;

namespace ThinkAndJobSolution.Servicios
{
    public class CandidatoService : ICandidatoService
    {
        private readonly string _connectionString;
        //private readonly ILogger<CandidateService> _logger;
        //private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName = "your-s3-bucket-name";
        private readonly EmailSettings _emailSettings;

        public CandidatoService(IConfiguration configuration, IOptions<EmailSettings> emailSettings)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _emailSettings = emailSettings.Value;
        }

        //public CandidateService(IConfiguration configuration, ILogger<CandidateService> logger, IAmazonS3 s3Client)
        //{
        //    _connectionString = configuration.GetConnectionString("DefaultConnection");
        //    _logger = logger;
        //    _s3Client = s3Client;
        //}

        public async Task<IActionResult> RegistrarCandidatoAsync(RegisterCandidateRequest request)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (CheckDNINIECIFunique(request.Dni, null, conn) != null)
                    return new BadRequestObjectResult(new { error = $"Error 2251, el DNI {request.Dni} ya está en uso" });

                if (CheckEMAILunique(request.Email, null, conn) != null)
                    return new BadRequestObjectResult(new { error = $"Error 2252, el email {request.Email} ya está en uso" });

                //string centroId = await GetCentroIdBySignLinkAsync(conn, request.SignLink);

                //string? s3FileUrl = null;
                //if (request.Attachment != null && request.Attachment.Length > 0)
                //{
                //    s3FileUrl = await UploadToS3Async(request.Attachment);
                //    if (s3FileUrl == null)
                //        return new BadRequestObjectResult(new { error = "Error 6010, fallo subiendo el archivo" });
                //}

                using var transaction = conn.BeginTransaction();

                await InsertCandidateAsync(conn, transaction, request, "");

                transaction.Commit();

                // Enviar el email
                string code = ComputeStringHash(request.Email + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()).Substring(0, 8).ToUpper();
                var inserts = new Dictionary<string, string>
                {
                    ["url"] = InstallationConstants.PUBLIC_URL + "/#/email-activation/" + code
                };

                string error = EventMailer.SendEmail(new EventMailer.Email
                {
                    template = "register",
                    inserts = inserts,
                    toEmail = request.Email,
                    toName = request.Nombre + " " + request.Apellido1 + (string.IsNullOrWhiteSpace(request.Apellido2) ? "" : " " + request.Apellido2),
                    subject = "Bienvenid@ a THINKANDJOB",
                    priority = EventMailer.EmailPriority.IMMEDIATE
                }, _emailSettings);

                LogToDB(LogType.CANDIDATE_REGISTERED, $"Candidato registrado {request.Dni}", null, conn);

                return new OkObjectResult(new { error = false });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { error = "Error interno. Intente nuevamente." });
            }
        }

        private async Task InsertCandidateAsync(SqlConnection conn, SqlTransaction transaction, RegisterCandidateRequest request, string? s3FileUrl)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            cmd.CommandText = @"
                INSERT INTO candidatos (id, nombre, apellidos, apellido2, dni, email, telefono, pwd, terminosAceptados, categoryId, regionId, localidadId, curriculumVitae)
                VALUES (@ID, @NAME, @SURNAME1, @SURNAME2, @DNI, @EMAIL, @PHONE, @PWD, 1, @CATEGORYID, @REGIONID, @LOCALIDADID, @CURRICULUMVITAE)";

            string candidateId = ComputeStringHash($"{request.Dni}{request.Nombre}{request.Apellido1}{request.Email}{request.Telefono}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

            cmd.Parameters.AddWithValue("@ID", candidateId);
            cmd.Parameters.AddWithValue("@NAME", request.Nombre);
            cmd.Parameters.AddWithValue("@SURNAME1", request.Apellido1);
            cmd.Parameters.AddWithValue("@SURNAME2", string.IsNullOrWhiteSpace(request.Apellido2) ? "-" : request.Apellido2);
            cmd.Parameters.AddWithValue("@DNI", request.Dni);
            cmd.Parameters.AddWithValue("@EMAIL", request.Email);
            cmd.Parameters.AddWithValue("@PHONE", request.Telefono);
            cmd.Parameters.AddWithValue("@PWD", ComputeStringHash(request.Password));
            cmd.Parameters.AddWithValue("@CATEGORYID", request.CategoriaId);
            cmd.Parameters.AddWithValue("@REGIONID", request.RegionId);
            cmd.Parameters.AddWithValue("@LOCALIDADID", request.LocalidadId);
            cmd.Parameters.AddWithValue("@CURRICULUMVITAE", "");

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IActionResult> ActivarEmailCandidatoAsync(string code)
        {
            object result = new { error = "Error 2932, no se pudo procesar la petición." };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    bool failed = false;
                    string candidateId = null;
                    string email = null;

                    try
                    {
                        // 1. Verificar si el código existe
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "SELECT email, candidateId FROM email_changes_pending WHERE code = @CODE";
                            command.Parameters.AddWithValue("@CODE", code);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    email = reader.GetString(reader.GetOrdinal("email"));
                                    candidateId = reader.GetString(reader.GetOrdinal("candidateId"));
                                }
                            }
                        }

                        if (candidateId == null)
                        {
                            failed = true;
                            result = new { error = "Error 4510, la pedición de cambio de email no existe o ya fue realizada." };
                        }
                        else
                        {
                            // 2. Verificar unicidad del email
                            bool exists = false;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE email = @EMAIL AND id <> @CANDIDATE_ID";
                                command.Parameters.AddWithValue("@EMAIL", email);
                                command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                                var count = (int)await command.ExecuteScalarAsync();
                                exists = count > 0;
                            }

                            if (exists)
                            {
                                failed = true;
                                result = new { error = "Error 4510, ya existe un usuario con este email." };
                            }
                            else
                            {
                                // 3. Actualizar email
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "UPDATE candidatos SET email=@EMAIL, email_verified=1 WHERE id = @ID";
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    command.Parameters.AddWithValue("@EMAIL", email);
                                    await command.ExecuteNonQueryAsync();
                                }

                                // 4. Borrar códigos usados
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM email_changes_pending WHERE candidateId = @ID";
                                    command.Parameters.AddWithValue("@ID", candidateId);
                                    await command.ExecuteNonQueryAsync();
                                }

                                // 5. Registrar log
                                //await LogToDBAsync(LogType.CANDIDATE_EMAIL_CHANGED, "Email de candidato cambiado " + await FindDNIbyCandidateIdAsync(candidateId, conn, transaction), null, conn, transaction);

                                result = new { error = false };
                            }
                        }

                        if (failed)
                            transaction.Rollback();
                        else
                            transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        result = new { error = "Error 2932, no se pudo procesar la petición." };
                    }
                }
            }

            return new OkObjectResult(result);
        }

    }


    //private async Task<string?> UploadToS3Async(IFormFile file)
    //{
    //    try
    //    {
    //        var fileKey = $"candidates/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

    //        using var stream = file.OpenReadStream();
    //        var putRequest = new PutObjectRequest
    //        {
    //            BucketName = _bucketName,
    //            Key = fileKey,
    //            InputStream = stream,
    //            ContentType = file.ContentType
    //        };

    //        var response = await _s3Client.PutObjectAsync(putRequest);

    //        return response.HttpStatusCode == System.Net.HttpStatusCode.OK
    //            ? $"https://{_bucketName}.s3.amazonaws.com/{fileKey}"
    //            : null;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error subiendo archivo a S3");
    //        return null;
    //    }
    //}
}