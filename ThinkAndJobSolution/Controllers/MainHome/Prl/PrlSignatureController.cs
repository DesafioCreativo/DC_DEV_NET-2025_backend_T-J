using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers._Model.Candidate;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers.MainHome.Prl.PrlController;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/sign")]
    [ApiController]
    [Authorize]
    public class PRLSignatureController : ControllerBase
    {

        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "work/{signLink}")]
        public IActionResult GetWork(string signLink)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                bool existsCandidate = false;

                //Comprobar que el candidato existe
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT id FROM candidatos WHERE id LIKE @CANDIDATE_ID";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            existsCandidate = true;
                        }
                        else
                        {
                            result = new
                            {
                                error = "Error 3934, el candidato no esta registrado."
                            };
                        }
                    }
                }

                if (existsCandidate)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {

                        command.CommandText = "SELECT T.id, CA.name FROM trabajos T INNER JOIN categories CA ON(T.categoryId = CA.id) WHERE T.signLink LIKE @SIGN_LINK";
                        command.Parameters.AddWithValue("@SIGN_LINK", signLink);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    name = reader.GetString(reader.GetOrdinal("name"))
                                };
                            }
                            else
                            {
                                result = new { error = "Error 4934, sin trabajo asignado" };
                            }
                        }
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "work/list-tests/{signLink}/{type?}")]
        public IActionResult ListTests(string signLink, string type = null)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (signLink.Length != 10)
            {
                result = new
                {
                    error = "Error 4201, el signlink no tiene un formato correcto."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    //Comprobar que el candidato existe y obtener su trabajo, categoria y nombre
                    bool existsCandidate = false;
                    string workId = null, categoryId = null, categoryName = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT T.id as workId, CA.id as categoryId, CA.name as categoryName " +
                            "FROM candidatos C " +
                            "INNER JOIN trabajos T ON(T.signLink = C.lastSignLink) " +
                            "INNER JOIN categories CA ON(T.categoryId = CA.id) " +
                            "WHERE C.id LIKE @CANDIDATE_ID";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                existsCandidate = true;
                                workId = reader.GetString(reader.GetOrdinal("workId"));
                                categoryId = reader.GetString(reader.GetOrdinal("categoryId"));
                                categoryName = reader.GetString(reader.GetOrdinal("categoryName"));
                            }
                            else
                            {
                                result = new
                                {
                                    error = "Error 3934, el candidato no esta registrado."
                                };
                            }
                        }
                    }

                    //Aqui no se puede forzar a que se lo haya descargado all
                    if (existsCandidate)
                    {
                        //Obtener los datos que se piden
                        using (SqlCommand command = conn.CreateCommand())
                        {

                            command.CommandText =
                                "SELECT \n" +
                                "F.id as FormId, \n" +
                                "F.nombre as FormName, \n" +
                                "F.detalles as FormDetails, \n" +
                                "F.tipo as FormType, \n" +
                                "F.preguntas, \n" +
                                "(SELECT COUNT(*) FROM emision_cuestionarios EC WHERE EC.formularioId = F.id AND EC.trabajoId = @WORK AND EC.candidatoId = @CANDIDATE) as Approved \n" +
                                "FROM (\n" +
                                "SELECT VTF.formularioId FROM vinculos_trabajos_formularios VTF WHERE VTF.trabajoId = @WORK\n" +
                                "UNION\n" +
                                "SELECT VCF.formularioId FROM vinculos_categorias_formularios VCF WHERE VCF.categoryId = @CATEGORY\n" +
                                ") VF INNER JOIN formularios F ON(F.id = VF.formularioId) \n" +
                                "WHERE (@TYPE IS NULL OR F.tipo = @TYPE)";

                            command.Parameters.AddWithValue("@WORK", workId);
                            command.Parameters.AddWithValue("@CATEGORY", categoryId);
                            command.Parameters.AddWithValue("@CANDIDATE", candidateId);
                            command.Parameters.AddWithValue("@TYPE", HelperMethods.TestTypeFilter(type));

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                List<object> questionnaire = new List<object>();

                                while (reader.Read())
                                {
                                    questionnaire.Add(new
                                    {
                                        workId = workId,
                                        workName = categoryId,
                                        formId = reader.GetString(reader.GetOrdinal("FormId")),
                                        formName = reader.GetString(reader.GetOrdinal("FormName")),
                                        formDetails = reader.GetString(reader.GetOrdinal("FormDetails")),
                                        nQuestions = PrlController.parseQuestions(reader.GetString(reader.GetOrdinal("preguntas"))).Count,
                                        approved = reader.GetInt32(reader.GetOrdinal("Approved")),
                                        type = HelperMethods.PrettyCase(reader.GetString(reader.GetOrdinal("FormType")))
                                    });
                                }

                                result = questionnaire;
                            }
                        }
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "work/get-test/{formId}")]
        public IActionResult GetTest(string formId)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            try
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    //Obtener los datos que se piden
                    using (SqlCommand command = conn.CreateCommand())
                    {

                        command.CommandText =
                            "SELECT \n" +
                            "F.id as FormId, \n" +
                            "F.nombre as FormName, \n" +
                            "F.detalles as FormDetails, \n" +
                            "F.tipo as FormType, \n" +
                            "F.preguntas \n" +
                            "FROM Formularios F WHERE F.id = @ID ";
                        command.Parameters.AddWithValue("@ID", formId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            List<object> questionnaire = new List<object>();
                            if (reader.Read())
                            {
                                result = new
                                {
                                    error = false,
                                    test = new
                                    {
                                        formId = reader.GetString(reader.GetOrdinal("FormId")),
                                        formName = reader.GetString(reader.GetOrdinal("FormName")),
                                        formDetails = reader.GetString(reader.GetOrdinal("FormDetails")),
                                        questions = parseQuestions(reader.GetString(reader.GetOrdinal("preguntas"))),
                                        type = HelperMethods.PrettyCase(reader.GetString(reader.GetOrdinal("FormType")))
                                    }
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5010, no se ha podido obtener el formulario." };
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "submit/{formId}")]
        public async Task<IActionResult> SubmitTestResults(string formId)
        {
            string candidateId = Cl_Security.getSecurityInformation(User, "candidateId");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();
                string workId = null;
                bool existsCandidate = false;
                //Comprobar si el candidato existe
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT T.id FROM candidatos C INNER JOIN trabajos T ON(C.lastSignLink = T.signLink) WHERE C.id LIKE @CANDIDATE_ID";
                    command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            workId = reader.GetString(reader.GetOrdinal("id"));
                            existsCandidate = true;
                        }
                        else
                        {
                            result = new
                            {
                                error = "Error 3934, el candidato no esta registrado o no tiene trabajo."
                            };
                        }
                    }
                }
                //Obtener el tipo del test
                object tipoTest = null;
                if (existsCandidate)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT tipo FROM formularios WHERE id = @FORM_ID";
                        command.Parameters.AddWithValue("@FORM_ID", formId);
                        tipoTest = command.ExecuteScalar();
                    }
                }
                //Comprobar que ha descargado toda la formacion
                bool canContinue = false;
                if (existsCandidate)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT documentsDownloaded = CASE WHEN EXISTS(\n" + //Aqui no se puede usar calc_documents_downloaded, porque no discrimina el tipo de documento
                                              "SELECT TR.id, TR.type FROM (\n" +
                                              "SELECT CD.id, CD.type FROM category_documents CD WHERE CD.trabajoId = @WORK_ID\n" +
                                              "UNION ALL\n" +
                                              "SELECT CD.id, CD.type FROM category_documents CD INNER JOIN categories CA ON CD.categoryId = CA.id INNER JOIN trabajos T ON CA.id = T.categoryId WHERE T.id = @WORK_ID\n" +
                                              ") TR WHERE\n" +
                                              "TR.type = @TYPE AND\n" +
                                              "NOT EXISTS(SELECT * FROM category_documents_downloads TD WHERE TD.documentId = TR.id AND TD.candidateId = @CANDIDATE_ID)\n" +
                                              ") THEN 0 ELSE 1 END";
                        command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                        command.Parameters.AddWithValue("@WORK_ID", workId);
                        command.Parameters.AddWithValue("@TYPE", tipoTest);

                        canContinue = ((Int32)command.ExecuteScalar()) == 1;
                    }
                    if (!canContinue)
                    {
                        result = new
                        {
                            error = "Error 3935, no se han descargado todos los documentos formativos."
                        };
                    }
                }

                if (canContinue)
                {
                    using StreamReader reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                    string data = await reader.ReadToEndAsync();
                    JsonElement json = JsonDocument.Parse(data).RootElement;
                    if (json.TryGetProperty("dni", out JsonElement dniJson) && json.TryGetProperty("signBase64Image", out JsonElement signBase64ImageJson) &&
                        json.TryGetProperty("formAnswers", out JsonElement formAnswersJson))
                    {
                        string dni = dniJson.GetString();
                        string signBase64Image = signBase64ImageJson.GetString();
                        var formAnswers = PrlController.parseQuestions(formAnswersJson);
                        using (SqlCommand command = conn.CreateCommand())
                        {

                            command.CommandText =
                                "INSERT INTO emision_cuestionarios(id, trabajoId, candidatoId, respuestasJSON, fechaFirma, formularioId) " +
                                "VALUES (@ID, @WORK_ID, @CANDIDATE_ID, @ANSWERS, @DATE, @FORM_ID)";

                            string submitId = HelperMethods.ComputeStringHash(
                                        dni +
                                        candidateId +
                                        formId +
                                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
                                );

                            command.Parameters.AddWithValue("@ID", submitId);
                            command.Parameters.AddWithValue("@WORK_ID", workId);
                            command.Parameters.AddWithValue("@CANDIDATE_ID", candidateId);
                            command.Parameters.AddWithValue("@FORM_ID", formId);
                            command.Parameters.AddWithValue("@ANSWERS", JsonSerializer.Serialize(formAnswers));
                            command.Parameters.AddWithValue("@DATE", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                            if (command.ExecuteNonQuery() > 0)
                            {
                                HelperMethods.SaveFile(new[] { "questionnaire_submissions", submitId, "signBase64Image" }, signBase64Image);
                                result = new
                                {
                                    error = false
                                };
                            }
                            else
                            {
                                result = new
                                {
                                    error = "Error 3103, no se ha podido emitir el formulario."
                                };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list/")]
        public async Task<IActionResult> ListSubmits()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Certificate.ListSubmits", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using System.IO.StreamReader readerBody = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("from", out JsonElement fromJson) && json.TryGetProperty("to", out JsonElement toJson) &&
                    json.TryGetProperty("key", out JsonElement keyJson) && json.TryGetProperty("state", out JsonElement stateJson) &&
                    json.TryGetProperty("page", out JsonElement pageJson) && json.TryGetProperty("perpage", out JsonElement perpageJson) &&
                    json.TryGetProperty("type", out JsonElement typeJson) &&
                    json.TryGetProperty("sortColumn", out JsonElement sortColumnJson) && json.TryGetProperty("sortDesc", out JsonElement sortDescJson))
                {
                    DateTime? from;
                    try
                    {
                        from = DateTimeOffset.FromUnixTimeSeconds(fromJson.GetInt32()).ToLocalTime().Date;
                    }
                    catch (InvalidOperationException)
                    {
                        from = null;
                    }
                    DateTime? to;
                    try
                    {
                        to = DateTimeOffset.FromUnixTimeSeconds(toJson.GetInt32()).ToLocalTime().Date.AddDays(1).AddSeconds(-1);
                    }
                    catch (InvalidOperationException)
                    {
                        to = null;
                    }
                    string key = keyJson.GetString();
                    string state = stateJson.GetString();
                    string type = typeJson.GetString();
                    int page = Int32.Parse(pageJson.GetString());
                    int perpage = Int32.Parse(perpageJson.GetString());
                    string sort = HelperMethods.FormatSort(new() {
                        { "work", "CA.name" },
                        { "dni", "C.dni" },
                        { "candidate", "CONCAT(C.apellidos, ' ', C.nombre)" },
                        { "company", "CAST(E.nombre as varchar(50))" },
                        { "state", "C.calc_state_" + type.ToLower() + "_tests" },
                        { "date", "C.calc_last_date_test_submit" }
                    }, "date", sortColumnJson, sortDescJson);
                    if (type != null) type = HelperMethods.TestTypeFilter(type).ToString();
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        List<SubmitsQuery> entries = new List<SubmitsQuery>();
                        //Agregar los candidatos
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT T.id as tId, state = C.calc_state_" + type.ToLower() + "_tests, \n" +
                                "lastDate = C.calc_last_date_test_submit,\n" +
                                "documentsDownloaded = C.calc_documents_downloaded,\n" +
                                "C.*,\n" +
                                "CA.name as trabajo,\n" +
                                "T.id as trabajoId,\n" +
                                "E.nombre as empresa,\n" +
                                "E.id as empresaId,\n" +
                                "CE.alias as centro,\n" +
                                "CE.id as centroIdd\n" +
                                "FROM candidatos C\n" +
                                "INNER JOIN trabajos T ON T.signLink = C.lastSignLink\n" +
                                "INNER JOIN categories CA ON T.categoryId = CA.id\n" +
                                "INNER JOIN centros CE ON CE.id = T.centroId\n" +
                                "INNER JOIN empresas E ON E.id = CE.companyId\n" +
                                "WHERE (@FROM IS NULL OR @FROM < C.calc_last_date_test_submit) AND\n" +
                                "(@TO IS NULL OR @TO > C.calc_last_date_test_submit) AND\n" +
                                "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI OR C.dni LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY) AND\n" +
                                "(@STATE IS NULL OR @STATE = C.calc_state_" + type.ToLower() + "_tests)\n" +
                                sort + "\n" +
                                "OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY\n";
                            command.Parameters.AddWithValue("@FROM", ((object)from) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@TO", ((object)to) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@STATE", ((object)state) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@TYPE", ((object)type) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@OFFSET", page * perpage);
                            command.Parameters.AddWithValue("@LIMIT", perpage);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    ExtendedCandidateData candidate = new ExtendedCandidateData();
                                    candidate.Read(reader);
                                    SubmitsQuery submitData = new SubmitsQuery
                                    {
                                        candidate = candidate,
                                        centro = reader.GetString(reader.GetOrdinal("centro")),
                                        centroId = reader.GetString(reader.GetOrdinal("centroIdd")),
                                        company = reader.GetString(reader.GetOrdinal("empresa")),
                                        companyId = reader.GetString(reader.GetOrdinal("empresaId")),
                                        work = reader.GetString(reader.GetOrdinal("trabajo")),
                                        workId = reader.GetString(reader.GetOrdinal("trabajoId")),
                                        pendingForms = new List<QuestData>(),
                                        submittedForms = new List<QuestData>(),
                                        state = reader.GetString(reader.GetOrdinal("state"))
                                    };
                                    entries.Add(submitData);
                                }
                            }
                        }
                        //Agregar los cuestionarios
                        foreach (SubmitsQuery entry in entries)
                        {
                            List<QuestData> allQuestData = new List<QuestData>();
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT\n" +
                                    "F.id as formId,\n" +
                                    "F.nombre as form,\n" +
                                    "F.tipo as tipo,\n" +
                                    "F.requiereCertificado as requiereCertificado\n" +
                                    "FROM (\n" +
                                    "SELECT VTF.formularioId FROM vinculos_trabajos_formularios VTF WHERE VTF.trabajoId = @WORK_ID\n" +
                                    "UNION ALL\n" +
                                    "SELECT VCF.formularioId FROM vinculos_categorias_formularios VCF INNER JOIN categories CA ON(VCF.categoryId = CA.id) INNER JOIN trabajos T ON(T.categoryId = CA.id) AND T.id = @WORK_ID\n" +
                                    ") VF\n" +
                                    "INNER JOIN formularios as F ON VF.formularioId = F.id\n" +
                                    "WHERE (@TYPE IS NULL OR F.tipo = @TYPE)";
                                command.Parameters.AddWithValue("@WORK_ID", entry.workId);
                                command.Parameters.AddWithValue("@TYPE", ((object)type) ?? DBNull.Value);
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        allQuestData.Add(new QuestData
                                        {
                                            submitId = null,
                                            date = 0,
                                            hasCertificate = false,
                                            form = reader.GetString(reader.GetOrdinal("form")),
                                            formId = reader.GetString(reader.GetOrdinal("formId")),
                                            type = reader.GetString(reader.GetOrdinal("tipo")),
                                            certNeeded = reader.GetInt32(reader.GetOrdinal("requiereCertificado")) == 1
                                        });
                                    }
                                }
                            }
                            //Comprobar que ha sido entregado, en ese caso rellenar
                            for (int i = 0; i < allQuestData.Count; i++)
                            {
                                QuestData questData = allQuestData[i];
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "SELECT\n" +
                                        "EC.id as id,\n" +
                                        "EC.fechaFirma as fechaFirma,\n" +
                                        "EC.certificado\n" +
                                        "FROM emision_cuestionarios EC\n" +
                                        "INNER JOIN formularios F ON F.id = EC.formularioId\n" +
                                        "WHERE EC.formularioId LIKE @FORM_ID AND\n" +
                                        "EC.candidatoId LIKE @CANDIDATE_ID AND\n" +
                                        "EC.trabajoId LIKE @WORK_ID";
                                    command.Parameters.AddWithValue("@FORM_ID", questData.formId);
                                    command.Parameters.AddWithValue("@WORK_ID", entry.workId);
                                    command.Parameters.AddWithValue("@CANDIDATE_ID", entry.candidate.id);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (!reader.Read())
                                        {
                                            entry.pendingForms.Add(questData);
                                        }
                                        else
                                        {
                                            questData.submitId = reader.GetString(reader.GetOrdinal("id"));
                                            questData.date = reader.GetInt64(reader.GetOrdinal("fechaFirma"));
                                            questData.hasCertificate = reader.GetInt32(reader.GetOrdinal("certificado")) == 1;
                                            entry.submittedForms.Add(questData);
                                        }
                                    }
                                }
                            }
                        }
                        result = new
                        {
                            error = false,
                            entries
                        };
                    }
                }
                else
                {
                    result = new
                    {
                        error = "Error 4086, No se han especificado todos los parametros de filtrados."
                    };
                }
            }
            return Ok(result);
        }
        
        [HttpPost]
        [Route(template: "count/")]
        public async Task<IActionResult> ListSubmitsCount()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Certificate.ListSubmitsCount", securityToken).Acceso)
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
                if (json.TryGetProperty("from", out JsonElement fromJson) && json.TryGetProperty("to", out JsonElement toJson) &&
                    json.TryGetProperty("key", out JsonElement keyJson) && json.TryGetProperty("state", out JsonElement stateJson) &&
                    json.TryGetProperty("type", out JsonElement typeJson))
                {
                    DateTime? from;
                    try
                    {
                        from = DateTimeOffset.FromUnixTimeSeconds(fromJson.GetInt32()).ToLocalTime().Date;
                    }
                    catch (InvalidOperationException)
                    {
                        from = null;
                    }
                    DateTime? to;
                    try
                    {
                        to = DateTimeOffset.FromUnixTimeSeconds(toJson.GetInt32()).ToLocalTime().Date.AddDays(1).AddSeconds(-1);
                    }
                    catch (InvalidOperationException)
                    {
                        to = null;
                    }
                    string key = keyJson.GetString();
                    string state = stateJson.GetString();
                    string type = typeJson.GetString();
                    if (type != null) type = HelperMethods.TestTypeFilter(type).ToString();
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT COUNT(*)\n" +
                                "FROM candidatos C\n" +
                                "INNER JOIN trabajos T ON T.signLink = C.lastSignLink\n" +
                                "INNER JOIN categories CA ON T.categoryId = CA.id\n" +
                                "INNER JOIN centros CE ON CE.id = T.centroId\n" +
                                "INNER JOIN empresas E ON E.id = CE.companyId\n" +
                                "WHERE (@FROM IS NULL OR @FROM < C.calc_last_date_test_submit) AND\n" +
                                "(@TO IS NULL OR @TO > C.calc_last_date_test_submit) AND\n" +
                                "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) COLLATE Latin1_General_CI_AI LIKE @KEY COLLATE Latin1_General_CI_AI OR C.dni LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY) AND\n" +
                                "(@STATE IS NULL OR @STATE = C.calc_state_" + type.ToLower() + "_tests)\n";
                            command.Parameters.AddWithValue("@FROM", ((object)from) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@TO", ((object)to) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@STATE", ((object)state) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@TYPE", ((object)type) ?? DBNull.Value);
                            result = command.ExecuteScalar();
                        }
                    }
                }
                else
                {
                    result = new
                    {
                        error = "Error 4086, No se han especificado todos los parametros de filtrados."
                    };
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "download-excel/")]
        public async Task<IActionResult> ListSubmitsExcel(string securityToken)
        {
            //DateTime start = DateTime.Now;
            if (!HelperMethods.HasPermission("Certificate.ListSubmitsExcel", securityToken).Acceso)
                return new NoContentResult();

            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            List<SubmitsQuery> entries = new List<SubmitsQuery>();
            bool includeTests = false;
            if (json.TryGetProperty("from", out JsonElement fromJson) && json.TryGetProperty("to", out JsonElement toJson) &&
                json.TryGetProperty("key", out JsonElement keyJson) && json.TryGetProperty("state", out JsonElement stateJson) &&
                json.TryGetProperty("type", out JsonElement typeJson) && json.TryGetProperty("includeTests", out JsonElement includeTestsJson) &&
                json.TryGetProperty("sortColumn", out JsonElement sortColumnJson) && json.TryGetProperty("sortDesc", out JsonElement sortDescJson) &&
                json.TryGetProperty("maxResults", out JsonElement maxResultsJson))
            {
                DateTime? from = HelperMethods.GetJsonUnixDate(fromJson);
                DateTime? to = HelperMethods.GetJsonUnixDate(toJson);
                string key = keyJson.GetString();
                string state = stateJson.GetString();
                string type = typeJson.GetString();
                includeTests = HelperMethods.GetJsonBool(includeTestsJson) ?? false;
                string sort = HelperMethods.FormatSort(new() {
                        { "work", "CA.name" },
                        { "dni", "C.dni" },
                        { "candidate", "CONCAT(C.apellidos, ' ', C.nombre)" },
                        { "company", "CAST(E.nombre as varchar(50))" },
                        { "state", "C.calc_state_" + type.ToLower() + "_tests" },
                        { "date", "C.calc_last_date_test_submit" }
                    }, "date", sortColumnJson, sortDescJson);

                if (type != null) type = HelperMethods.TestTypeFilter(type).ToString();
                int? maxResults = HelperMethods.GetJsonInt(maxResultsJson);

                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();

                    //Agregar los candidatos
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT T.id as tId, statePrl = C.calc_state_prl_tests, stateTraining = C.calc_state_training_tests, \n" +
                            "lastDate = C.calc_last_date_test_submit,\n" +
                            "registryDate = C.date,\n" +
                            "lastAccessDate = C.ultimoAcceso,\n" +
                            "documentsDownloaded = C.calc_documents_downloaded,\n" +
                            "C.*,\n" +
                            "CA.name as trabajo,\n" +
                            "T.id as trabajoId,\n" +
                            "E.nombre as empresa,\n" +
                            "E.id as empresaId,\n" +
                            "CE.alias as centro,\n" +
                            "CE.id as centroIdd\n" +
                            "FROM candidatos C\n" +
                            "INNER JOIN trabajos T ON T.signLink = C.lastSignLink\n" +
                            "INNER JOIN categories CA ON T.categoryId = CA.id\n" +
                            "INNER JOIN centros CE ON CE.id = T.centroId\n" +
                            "INNER JOIN empresas E ON E.id = CE.companyId\n" +
                            "WHERE (@FROM IS NULL OR @FROM < C.calc_last_date_test_submit) AND\n" +
                            "(@TO IS NULL OR @TO > C.calc_last_date_test_submit) AND\n" +
                            "(@KEY IS NULL OR CONCAT(C.nombre, ' ', C.apellidos) LIKE @KEY OR C.dni LIKE @KEY OR CA.name LIKE @KEY OR E.nombre LIKE @KEY) AND\n" +
                            "(@STATE IS NULL OR @STATE = C.calc_state_" + type.ToLower() + "_tests)\n" +
                            sort + "\n" + (maxResults == null ? "" : $"OFFSET 0 ROWS FETCH NEXT {maxResults.Value} ROWS ONLY");

                        command.Parameters.AddWithValue("@FROM", ((object)from) ?? DBNull.Value);
                        command.Parameters.AddWithValue("@TO", ((object)to) ?? DBNull.Value);
                        command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                        command.Parameters.AddWithValue("@STATE", ((object)state) ?? DBNull.Value);
                        command.Parameters.AddWithValue("@TYPE", ((object)type) ?? DBNull.Value);   //Usado unicamente para filtrar

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                ExtendedCandidateData candidate = new ExtendedCandidateData();
                                candidate.Read(reader);
                                candidate.birth = reader.IsDBNull(reader.GetOrdinal("lastDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("lastDate")); //Guardar aqui la fecha del ultimo test realizado
                                candidate.lastAccess = reader.IsDBNull(reader.GetOrdinal("lastAccessDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("lastAccessDate")); //Guardar aqui la fecha de ultimo acceso
                                candidate.date = reader.IsDBNull(reader.GetOrdinal("registryDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("registryDate")); //Guardar aqui la fecha de registro
                                string statePrl = reader.GetString(reader.GetOrdinal("statePrl"));
                                string stateTraining = reader.GetString(reader.GetOrdinal("stateTraining"));
                                string stateGlobal = stateTraining == "pending" ? "pending" : statePrl;
                                SubmitsQuery submitData = new SubmitsQuery
                                {
                                    candidate = candidate,
                                    centro = reader.GetString(reader.GetOrdinal("centro")),
                                    centroId = reader.GetString(reader.GetOrdinal("centroIdd")),
                                    company = reader.GetString(reader.GetOrdinal("empresa")),
                                    companyId = reader.GetString(reader.GetOrdinal("empresaId")),
                                    work = reader.GetString(reader.GetOrdinal("trabajo")),
                                    workId = reader.GetString(reader.GetOrdinal("trabajoId")),
                                    pendingForms = null,
                                    submittedForms = new List<QuestData>(),
                                    state = stateGlobal
                                };
                                entries.Add(submitData);
                            }
                        }
                    }

                    //Agregar los cuestionarios
                    if (includeTests)
                    {
                        foreach (SubmitsQuery entry in entries)
                        {
                            List<QuestData> allQuestData = new List<QuestData>();

                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "SELECT\n" +
                                    "F.id as formId,\n" +
                                    "F.nombre as form,\n" +
                                    "F.tipo as tipo,\n" +
                                    "F.requiereCertificado as requiereCertificado\n" +
                                    "FROM (\n" +
                                    "SELECT VTF.formularioId FROM vinculos_trabajos_formularios VTF WHERE VTF.trabajoId = @WORK_ID\n" +
                                    "UNION ALL\n" +
                                    "SELECT VCF.formularioId FROM vinculos_categorias_formularios VCF INNER JOIN categories CA ON(VCF.categoryId = CA.id) INNER JOIN trabajos T ON(T.categoryId = CA.id) AND T.id = @WORK_ID\n" +
                                    ") VF\n" +
                                    "INNER JOIN formularios as F ON VF.formularioId = F.id";

                                command.Parameters.AddWithValue("@WORK_ID", entry.workId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        allQuestData.Add(new QuestData
                                        {
                                            submitId = null,
                                            date = 0,
                                            hasCertificate = false,
                                            form = reader.GetString(reader.GetOrdinal("form")),
                                            formId = reader.GetString(reader.GetOrdinal("formId")),
                                            type = reader.GetString(reader.GetOrdinal("tipo")),
                                            certNeeded = reader.GetInt32(reader.GetOrdinal("requiereCertificado")) == 1
                                        });
                                    }
                                }
                            }

                            //Comprobar que ha sido entregado, en ese caso rellenar
                            for (int i = 0; i < allQuestData.Count; i++)
                            {
                                QuestData questData = allQuestData[i];
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "SELECT\n" +
                                        "EC.id as id,\n" +
                                        "EC.fechaFirma as fechaFirma,\n" +
                                        "EC.certificado\n" +
                                        "FROM emision_cuestionarios EC\n" +
                                        "INNER JOIN formularios F ON F.id = EC.formularioId\n" +
                                        "WHERE EC.formularioId LIKE @FORM_ID AND\n" +
                                        "EC.candidatoId LIKE @CANDIDATE_ID AND\n" +
                                        "EC.trabajoId LIKE @WORK_ID";

                                    command.Parameters.AddWithValue("@FORM_ID", questData.formId);
                                    command.Parameters.AddWithValue("@WORK_ID", entry.workId);
                                    command.Parameters.AddWithValue("@CANDIDATE_ID", entry.candidate.id);

                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            questData.submitId = reader.GetString(reader.GetOrdinal("id"));
                                            questData.date = reader.GetInt64(reader.GetOrdinal("fechaFirma"));
                                            questData.hasCertificate = reader.GetInt32(reader.GetOrdinal("certificado")) == 1;
                                        }
                                        entry.submittedForms.Add(questData);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else return new NoContentResult();
            //Console.WriteLine($"Conn CLOSED {entries.Count}: {(DateTime.Now-start).TotalSeconds}");
            //start = DateTime.Now;

            //Generar el excel
            IWorkbook workbook = new XSSFWorkbook();
            string tmpDir = HelperMethods.GetTemporaryDirectory();
            var thicc = BorderStyle.Medium;
            var thin = BorderStyle.Thin;
            var none = BorderStyle.None;
            var bgGreen = new XSSFColor(new byte[] { 226, 239, 218 });
            var bgYellow = new XSSFColor(new byte[] { 253, 254, 204 });
            var bgRed = new XSSFColor(new byte[] { 254, 211, 204 });
            var bgBlue = new XSSFColor(new byte[] { 206, 225, 242 });

            //Fuentes
            IFont fontTitle = workbook.CreateFont();
            fontTitle.FontName = "Century Gothic";
            fontTitle.FontHeightInPoints = 14;
            fontTitle.Color = NPOI.SS.UserModel.IndexedColors.Black.Index;

            IFont fontNormal = workbook.CreateFont();
            fontNormal.FontName = "Century Gothic";
            fontNormal.FontHeightInPoints = 10;
            fontNormal.Color = NPOI.SS.UserModel.IndexedColors.Black.Index;

            IFont fontStatus = workbook.CreateFont();
            fontStatus.FontName = "Century Gothic";
            fontStatus.FontHeightInPoints = 11;
            fontStatus.Color = NPOI.SS.UserModel.IndexedColors.Black.Index;

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

            XSSFCellStyle bodyStatusGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStatusGreenStyle.SetFont(fontStatus);
            bodyStatusGreenStyle.FillForegroundColorColor = bgGreen;
            bodyStatusGreenStyle.FillPattern = FillPattern.SolidForeground;
            bodyStatusGreenStyle.BorderTop = none;
            bodyStatusGreenStyle.BorderBottom = thin;
            bodyStatusGreenStyle.BorderRight = none;
            bodyStatusGreenStyle.BorderLeft = none;
            bodyStatusGreenStyle.Alignment = HorizontalAlignment.Center;
            bodyStatusGreenStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStatusYellowStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStatusYellowStyle.SetFont(fontStatus);
            bodyStatusYellowStyle.FillForegroundColorColor = bgYellow;
            bodyStatusYellowStyle.FillPattern = FillPattern.SolidForeground;
            bodyStatusYellowStyle.BorderTop = none;
            bodyStatusYellowStyle.BorderBottom = thin;
            bodyStatusYellowStyle.BorderRight = none;
            bodyStatusYellowStyle.BorderLeft = none;
            bodyStatusYellowStyle.Alignment = HorizontalAlignment.Center;
            bodyStatusYellowStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle bodyStatusRedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            bodyStatusRedStyle.SetFont(fontStatus);
            bodyStatusRedStyle.FillForegroundColorColor = bgRed;
            bodyStatusRedStyle.FillPattern = FillPattern.SolidForeground;
            bodyStatusRedStyle.BorderTop = none;
            bodyStatusRedStyle.BorderBottom = thin;
            bodyStatusRedStyle.BorderRight = none;
            bodyStatusRedStyle.BorderLeft = none;
            bodyStatusRedStyle.Alignment = HorizontalAlignment.Center;
            bodyStatusRedStyle.VerticalAlignment = VerticalAlignment.Center;

            XSSFCellStyle testStartStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStartStyle.SetFont(fontNormal);
            testStartStyle.BorderTop = none;
            testStartStyle.BorderBottom = none;
            testStartStyle.BorderRight = none;
            testStartStyle.BorderLeft = thin;
            testStartStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStyle.SetFont(fontNormal);
            testStyle.BorderTop = none;
            testStyle.BorderBottom = none;
            testStyle.BorderRight = none;
            testStyle.BorderLeft = none;
            testStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStatusGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStatusGreenStyle.SetFont(fontNormal);
            testStatusGreenStyle.FillForegroundColorColor = bgGreen;
            testStatusGreenStyle.FillPattern = FillPattern.SolidForeground;
            testStatusGreenStyle.BorderTop = none;
            testStatusGreenStyle.BorderBottom = none;
            testStatusGreenStyle.BorderRight = thin;
            testStatusGreenStyle.BorderLeft = none;
            testStatusGreenStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStatusYellowStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStatusYellowStyle.SetFont(fontNormal);
            testStatusYellowStyle.FillForegroundColorColor = bgYellow;
            testStatusYellowStyle.FillPattern = FillPattern.SolidForeground;
            testStatusYellowStyle.BorderTop = none;
            testStatusYellowStyle.BorderBottom = none;
            testStatusYellowStyle.BorderRight = thin;
            testStatusYellowStyle.BorderLeft = none;
            testStatusYellowStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStatusRedStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStatusRedStyle.SetFont(fontNormal);
            testStatusRedStyle.FillForegroundColorColor = bgRed;
            testStatusRedStyle.FillPattern = FillPattern.SolidForeground;
            testStatusRedStyle.BorderTop = none;
            testStatusRedStyle.BorderBottom = none;
            testStatusRedStyle.BorderRight = thin;
            testStatusRedStyle.BorderLeft = none;
            testStatusRedStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStartLastStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStartLastStyle.SetFont(fontNormal);
            testStartLastStyle.BorderTop = none;
            testStartLastStyle.BorderBottom = thin;
            testStartLastStyle.BorderRight = none;
            testStartLastStyle.BorderLeft = thin;
            testStartLastStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testLastStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testLastStyle.SetFont(fontNormal);
            testLastStyle.BorderTop = none;
            testLastStyle.BorderBottom = thin;
            testLastStyle.BorderRight = none;
            testLastStyle.BorderLeft = none;
            testLastStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStatusGreenLastStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStatusGreenLastStyle.SetFont(fontNormal);
            testStatusGreenLastStyle.FillForegroundColorColor = bgGreen;
            testStatusGreenLastStyle.FillPattern = FillPattern.SolidForeground;
            testStatusGreenLastStyle.BorderTop = none;
            testStatusGreenLastStyle.BorderBottom = thin;
            testStatusGreenLastStyle.BorderRight = thin;
            testStatusGreenLastStyle.BorderLeft = none;
            testStatusGreenLastStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStatusYellowLastStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStatusYellowLastStyle.SetFont(fontNormal);
            testStatusYellowLastStyle.FillForegroundColorColor = bgYellow;
            testStatusYellowLastStyle.FillPattern = FillPattern.SolidForeground;
            testStatusYellowLastStyle.BorderTop = none;
            testStatusYellowLastStyle.BorderBottom = thin;
            testStatusYellowLastStyle.BorderRight = thin;
            testStatusYellowLastStyle.BorderLeft = none;
            testStatusYellowLastStyle.Alignment = HorizontalAlignment.Left;

            XSSFCellStyle testStatusRedLastStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            testStatusRedLastStyle.SetFont(fontNormal);
            testStatusRedLastStyle.FillForegroundColorColor = bgRed;
            testStatusRedLastStyle.FillPattern = FillPattern.SolidForeground;
            testStatusRedLastStyle.BorderTop = none;
            testStatusRedLastStyle.BorderBottom = thin;
            testStatusRedLastStyle.BorderRight = thin;
            testStatusRedLastStyle.BorderLeft = none;
            testStatusRedLastStyle.Alignment = HorizontalAlignment.Left;

            //Hojas
            ISheet sheet = workbook.CreateSheet("Candidatos");

            //Tamaños de filas y columnas
            ICell cell;
            IRow row;
            sheet.SetColumnWidth(1, 11 * 256);
            sheet.SetColumnWidth(2, 40 * 256);
            sheet.SetColumnWidth(3, 15 * 256);
            sheet.SetColumnWidth(4, 40 * 256);
            sheet.SetColumnWidth(5, 25 * 256);
            sheet.SetColumnWidth(6, 30 * 256);
            sheet.SetColumnWidth(7, 40 * 256);
            sheet.SetColumnWidth(8, 40 * 256);
            sheet.SetColumnWidth(9, 15 * 256);
            sheet.SetColumnWidth(10, 22 * 256);
            sheet.SetColumnWidth(11, 25 * 256);
            sheet.SetColumnWidth(12, 32 * 256);
            sheet.SetColumnWidth(13, 45 * 256);
            sheet.SetColumnWidth(14, 13 * 256);
            sheet.SetColumnWidth(15, 20 * 256);
            sheet.SetColumnWidth(16, 15 * 256);

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
            cell.SetCellValue("Nombre");
            cell = row.CreateCell(3);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("Teléfono");
            cell = row.CreateCell(4);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("Correo electrónico");
            cell = row.CreateCell(5);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("Registro de datos");
            cell = row.CreateCell(6);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("Empresa");
            cell = row.CreateCell(7);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("Centro");
            cell = row.CreateCell(8);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("Trabajo");
            cell = row.CreateCell(9);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("Estado");
            cell = row.CreateCell(10);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("F. Registro");
            cell = row.CreateCell(11);
            cell.CellStyle = headerStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("F. Último acceso");
            cell = row.CreateCell(12);
            cell.CellStyle = headerEndStyle;
            cell.SetCellType(CellType.String);
            cell.SetCellValue("F. Último test realizado");
            if (includeTests)
            {
                cell = row.CreateCell(13);
                cell.CellStyle = headerStartStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Test");
                cell = row.CreateCell(14);
                cell.CellStyle = headerStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Tipo");
                cell = row.CreateCell(15);
                cell.CellStyle = headerStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Fecha");
                cell = row.CreateCell(16);
                cell.CellStyle = headerEndStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue("Estado");
            }

            //Escribir los candidatos
            int r = 2;
            foreach (SubmitsQuery entry in entries)
            {
                int startR = r;
                List<IRow> rows = new();
                for (int i = 0; i < Math.Max(entry.submittedForms.Count, 1); i++)
                    rows.Add(sheet.CreateRow(r++));

                //Poner los datos del candidato
                cell = rows[0].CreateCell(1);
                cell.CellStyle = bodyStartStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.candidate.dni);
                cell = rows[0].CreateCell(2);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue($"{entry.candidate.surname}, {entry.candidate.name}".ToUpper());
                cell = rows[0].CreateCell(3);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.candidate.phone);
                cell = rows[0].CreateCell(4);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.candidate.email);
                cell = rows[0].CreateCell(5);
                cell.CellStyle = entry.candidate.allDataFilledIn ? bodyStatusGreenStyle : bodyStatusRedStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.candidate.allDataFilledIn ? "COMPLETO" : "INCOMPLETO");
                cell = rows[0].CreateCell(6);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.company);
                cell = rows[0].CreateCell(7);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.centro);
                cell = rows[0].CreateCell(8);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.work);
                cell = rows[0].CreateCell(9);
                cell.CellStyle = entry.state == "certificated" ? bodyStatusGreenStyle : (entry.state == "approved" ? bodyStatusYellowStyle : bodyStatusRedStyle);
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.state == "certificated" ? "Certificado" : (entry.state == "approved" ? "Aprobado" : "Pendiente"));
                cell = rows[0].CreateCell(10);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.candidate.date.Year < 2020 ? "" : entry.candidate.date.ToString("dd/MM/yyyy HH:mm:ss"));
                cell = rows[0].CreateCell(11);
                cell.CellStyle = bodyStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue(entry.candidate.lastAccess.Year < 2020 ? "" : entry.candidate.lastAccess.ToString("dd/MM/yyyy HH:mm:ss"));
                cell = rows[0].CreateCell(12);
                cell.CellStyle = bodyEndStyle;
                cell.SetCellType(CellType.String);
                cell.SetCellValue((entry.candidate.birth != null && entry.candidate.birth.Value.Year < 2020) ? "" : entry.candidate.birth.Value.ToString("dd/MM/yyyy HH:mm:ss"));

                //Crear el resto de celdas
                for (int i = 1; i < rows.Count; i++)
                {
                    rows[i].CreateCell(1).CellStyle = bodyStartStyle;
                    for (int j = 2; j <= 11; j++)
                        rows[i].CreateCell(j).CellStyle = bodyStyle;
                    rows[i].CreateCell(12).CellStyle = bodyEndStyle;
                }

                //Unir las celdas
                if (rows.Count > 1)
                    for (int j = 1; j <= 12; j++)
                        sheet.AddMergedRegionUnsafe(new CellRangeAddress(startR, r - 1, j, j));

                //Poner los tests
                if (includeTests)
                {
                    entry.submittedForms.Sort((f1, f2) => f1.form.CompareTo(f2.form));
                    for (int i = 0; i < entry.submittedForms.Count; i++)
                    {
                        bool isLast = (i == entry.submittedForms.Count - 1);
                        QuestData test = entry.submittedForms[i];

                        cell = rows[i].CreateCell(13);
                        cell.CellStyle = isLast ? testStartLastStyle : testStartStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(test.form);
                        cell = rows[i].CreateCell(14);
                        cell.CellStyle = isLast ? testLastStyle : testStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(test.type == "PRL" ? "Prevención" : "Formación");
                        cell = rows[i].CreateCell(15);
                        cell.CellStyle = isLast ? testLastStyle : testStyle;
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(test.submitId == null ? "" : DateTimeOffset.FromUnixTimeSeconds(test.date).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"));
                        cell = rows[i].CreateCell(16);
                        cell.CellStyle = isLast ? (test.submitId == null ? testStatusRedLastStyle : ((test.hasCertificate || !test.certNeeded) ? testStatusGreenLastStyle : testStatusYellowLastStyle)) : (test.submitId == null ? testStatusRedStyle : ((test.hasCertificate || !test.certNeeded) ? testStatusGreenStyle : testStatusYellowStyle));
                        cell.SetCellType(CellType.String);
                        cell.SetCellValue(test.submitId == null ? "PENDIENTE" : (test.hasCertificate ? "CERTIFICADO" : "APROBADO"));
                    }
                }
            }

            //Filtros
            sheet.SetAutoFilter(new CellRangeAddress(1, r - 1, 1, 10));

            //Guardado
            //sheet.ProtectSheet("1234"); //No protejer porque entonces nos e podria usar el filtro y orden
            string fileName = "ListSubmits.xlsx";
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
            //Console.WriteLine($"Excel generated {entries.Count}: {(DateTime.Now - start).TotalSeconds}");

            return response;
        }

        [HttpGet]
        [Route(template: "data/{signatureId}/")]
        public IActionResult GetSignatureData(string signatureId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Certificate.SignatureData", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {
                using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT\n" +
                            "C.dni as candidateDNI,\n" +
                            "E.nombre as company,\n" +
                            "E.id as companyId,\n" +
                            "CE.alias as centro,\n" +
                            "EC.fechaFirma as fecha,\n" +
                            "C.nombre as candidateName,\n" +
                            "C.apellidos as candidateSurname,\n" +
                            "CA.name as work,\n" +
                            "F.nombre as form,\n" +
                            "EC.respuestasJSON as answers\n" +
                            "FROM emision_cuestionarios AS EC\n" +
                            "INNER JOIN trabajos AS T ON T.id = EC.trabajoId\n" +
                            "INNER JOIN categories AS CA ON CA.id = T.categoryId\n" +
                            "INNER JOIN candidatos AS C ON C.id = EC.candidatoId\n" +
                            "INNER JOIN formularios AS F ON F.id = EC.formularioId\n" +
                            "INNER JOIN centros AS CE ON CE.id = T.centroId\n" +
                            "INNER JOIN empresas AS E ON E.id = CE.companyId\n" +
                            "WHERE EC.id LIKE @SIGN_ID";
                        command.Parameters.AddWithValue("@SIGN_ID", signatureId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new
                                {
                                    error = false,
                                    signature = new SignatureData()
                                    {
                                        candidateDNI = reader.GetString(reader.GetOrdinal("candidateDNI")),
                                        company = reader.GetString(reader.GetOrdinal("company")),
                                        centro = reader.GetString(reader.GetOrdinal("centro")),
                                        date = reader.GetInt64(reader.GetOrdinal("fecha")),
                                        candidateName = reader.GetString(reader.GetOrdinal("candidateName")),
                                        candidateSurname = reader.GetString(reader.GetOrdinal("candidateSurname")),
                                        work = reader.GetString(reader.GetOrdinal("work")),
                                        form = reader.GetString(reader.GetOrdinal("form")),
                                        questions = PrlController.parseQuestions(reader.GetString(reader.GetOrdinal("answers"))),
                                        signImage = HelperMethods.ReadFile(new[] { "questionnaire_submissions", signatureId, "signBase64Image" }),
                                        companyLogo = HelperMethods.ReadFile(new[] { "companies", reader.GetString(reader.GetOrdinal("companyId")), "icon" })
                                    }
                                };
                            }
                            else
                            {
                                result = new
                                {
                                    error = "Error 4318, formulario no encontrado."
                                };
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }


        [HttpPost]
        [Route(template: "list/revoke/")]
        public async Task<IActionResult> RevokeSubmitsList()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HelperMethods.HasPermission("Certificate.RevokeSubmitsList", securityToken).Acceso)
            {
                result = new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                };
            }
            else
            {

                using System.IO.StreamReader reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
                string data = await reader.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;
                if (json.TryGetProperty("ids", out JsonElement idsJsonArray))
                {
                    using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
                    {
                        conn.Open();
                        bool failed = false;
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            foreach (var id in idsJsonArray.EnumerateArray())
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "DELETE FROM emision_cuestionarios WHERE id LIKE @SUBMIT_ID";
                                    command.Parameters.AddWithValue("@SUBMIT_ID", id.GetString());
                                    try
                                    {
                                        string nombre = HelperMethods.FindNameBySubmitId(id.GetString(), conn, transaction);
                                        command.ExecuteNonQuery();
                                        HelperMethods.DeleteDir(new[] { "questionnaire_submissions", id.GetString() });
                                        HelperMethods.LogToDB(HelperMethods.LogType.CANDIDATE_CERTIFICATE_REVOKE, "Revocado el certificado de " + nombre, HelperMethods.FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);
                                    }
                                    catch (Exception)
                                    {
                                        failed = true;
                                        result = new
                                        {
                                            error = "Error 3933, no se ha podido eliminar la emisión."
                                        };
                                    }
                                }

                                if (failed)
                                {
                                    break;
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
                            }
                        }
                    }
                }
            }
            return Ok(result);
        }



        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "Clases"
        private struct QuestData
        {
            public string submitId { get; set; }
            public string form { get; set; }
            public string formId { get; set; }
            public long date { get; set; }
            public bool hasCertificate { get; set; }
            public string type { get; set; }
            public bool certNeeded { get; set; }
        }
        private struct SubmitsQuery
        {
            public ExtendedCandidateData candidate { get; set; }
            public string work { get; set; }
            public string workId { get; set; }
            public string centro { get; set; }
            public string centroId { get; set; }
            public string company { get; set; }
            public string companyId { get; set; }
            public List<QuestData> submittedForms { get; set; }
            public List<QuestData> pendingForms { get; set; }
            public string state { get; set; }
        }

        private struct SignatureData
        {
            public string candidateDNI { get; set; }
            public string company { get; set; }
            public string centro { get; set; }
            public Int64 date { get; set; }
            public string candidateName { get; set; }
            public string candidateSurname { get; set; }
            public string work { get; set; }
            public string form { get; set; }
            public List<PrlController.Question> questions { get; set; }
            public string signImage { get; set; }
            public string companyLogo { get; set; }
        }
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static List<Question> parseQuestions(JsonElement json)
        {
            List<Question> questions = new();
            if (json.ValueKind != JsonValueKind.Array) return questions;

            foreach (JsonElement itemJson in json.EnumerateArray())
            {
                if (itemJson.TryGetProperty("type", out JsonElement typeJson) && itemJson.TryGetProperty("question", out JsonElement questionJson))
                {
                    Question question = new Question()
                    {
                        type = typeJson.GetString(),
                        question = questionJson.GetString()
                    };

                    switch (question.type)
                    {
                        case "truefalse":
                            if (itemJson.TryGetProperty("answer", out JsonElement answerJson))
                            {
                                question.answer = answerJson.GetBoolean();
                            }
                            else continue;
                            if (itemJson.TryGetProperty("candidateAnswer", out JsonElement candidateAnswerJson))
                                question.candidateAnswer = candidateAnswerJson.GetBoolean();
                            break;
                        case "select":
                        case "selectmulti":
                            if (itemJson.TryGetProperty("answers", out JsonElement answersJson) && answersJson.ValueKind == JsonValueKind.Array)
                            {
                                question.answers = new();
                                foreach (JsonElement answareJson in answersJson.EnumerateArray())
                                {
                                    if (answareJson.TryGetProperty("text", out JsonElement textJson) && answareJson.TryGetProperty("correct", out JsonElement correctJson))
                                    {
                                        Answer answare = new Answer()
                                        {
                                            text = textJson.GetString(),
                                            correct = correctJson.GetBoolean()
                                        };
                                        if (itemJson.TryGetProperty("candidateCorrect", out JsonElement candidateCorrectJson))
                                            answare.candidateCorrect = candidateCorrectJson.GetBoolean();
                                        question.answers.Add(answare);
                                    }
                                }
                            }
                            else continue;
                            break;
                        default:
                            continue;
                    }

                    questions.Add(question);
                }
            }

            return questions;
        }
        public static List<Question> parseQuestions(string jsonString)
        {
            return parseQuestions(JsonDocument.Parse(jsonString).RootElement);
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
