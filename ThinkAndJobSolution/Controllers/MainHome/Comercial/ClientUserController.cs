using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Helper;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using ThinkAndJobSolution.Controllers.Client;
using Microsoft.AspNetCore.Authorization;
using ThinkAndJobSolution.Controllers.Candidate;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.CPDController;
using ThinkAndJobSolution.Utils;
using System.Text.Json;
using System.Text;
using static ThinkAndJobSolution.Controllers._Helper.Ohers.EventMailer;
using Microsoft.AspNetCore.Http.HttpResults;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;
using ThinkAndJobSolution.Controllers._Model.Client;

namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/client-user")]
    [ApiController]
    [Authorize]
    public class ClientUserController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        //[Route(template: "get-access/{clientToken}")]
        [Route(template: "get-access/")]
        //public IActionResult GetAccess(string clientToken)
        public IActionResult GetAccess()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            ClientUserAccess access = getClientUserAccess(clientToken, true);
             
            if (access.exists)
            {
                return Ok(new { error = false, access });
            }
            else
            {
                return Ok(new { error = "Error 4846, usuario no encontrado" });
            }                
        }

        [HttpGet]
        //[Route(template: "get-for-client/{clientToken}")]
        [Route(template: "get-for-client/")]
        //public IActionResult GetForClient(string clientToken)
        public IActionResult GetForClient()
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
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT * FROM client_users WHERE token = @TOKEN AND visible = 1";

                        command.Parameters.AddWithValue("@TOKEN", clientToken);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string userId = reader.GetString(reader.GetOrdinal("id"));
                                result = new
                                {
                                    error = false,
                                    user = new ClientUser
                                    {
                                        id = userId,
                                        username = reader.GetString(reader.GetOrdinal("username")),
                                        email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                        accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                                        activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1,
                                        photo = ReadFile(new[] { "clientuser", userId, "photo" })
                                    }
                                };
                            }
                            else
                            {
                                result = new { error = "Error 4860, usuario de cliente no encontrado" };
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener informacion del usuario" };
                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-dashboard-for-client")]
        public IActionResult GetDashboardForClient()
        {
            ClientDashboard dashboard;
            try
            {
                //var autoLoginToken = User.Claims.FirstOrDefault(c => c.Type == "autoLoginToken")?.Value;
                string clientToken = Cl_Security.getSecurityInformation(User, "token");
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    ClientUserAccess access = getClientUserAccess(clientToken, false, conn);
                    if (!access.exists)
                        return new JsonResult(new { error = "Error 4846, usuario no encontrado" }) { StatusCode = 404 };

                    if (!access.activo)
                        return new JsonResult(new { error = "Error 1002, No se disponen de los privilegios suficientes." }) { StatusCode = 403 };

                    dashboard = getClientDashboard(clientToken, access, conn);
                }
            }
            catch (Exception)
            {
                return new JsonResult(new { error = "Error 5688, no se ha podido obtener la información" }) { StatusCode = 500 };
            }

            return Ok(new { error = false, dashboard });


        }

        [HttpGet]
        [Route(template: "user-list/")]
        public IActionResult ListForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, null, null, out ClientUserAccess access) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
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
                        users = listUsersForClientByIntersection(clientToken, conn).Where(u => u.accessOrdering >= access.accessOrdering)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5871, no han podido listar los usuarios" };
                }

            }

            return Ok(result);
        }

        //---HEINRRICH 20250224 INICIO--
        //------------------------------clientToken------------------------------
        [HttpPatch]
        [Route(template: "assoc-centros-for-client/{userId}")]
        public async Task<IActionResult> AssocCentrosForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetStringList("centros", out List<string> centroIds) &&
                json.TryGetStringList("restrictedDashboard", out List<string> restrictedDashboard) &&
                json.TryGetStringList("restrictedPermissons", out List<string> restrictedPermissons))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, conn, transaction, out ClientUserAccess access) == null)
                        {
                            return Ok(new
                            {
                                error = "Error 1002, No se disponen de los privilegios suficientes."
                            });                            
                        }
                        try
                        {
                            int accessOrdering = 100;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText =
                                    "SELECT AL.ordering FROM client_users U INNER JOIN client_access_levels AL ON(U.accessLevel = AL.id) WHERE U.id = @ID";

                                command.Parameters.AddWithValue("@ID", userId);

                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        accessOrdering = reader.GetInt32(reader.GetOrdinal("ordering"));
                                    }
                                    else
                                    {
                                        return Ok(new
                                        {
                                            error = "Error 4872, no se ha encontrado el usuario."
                                        });
                                    }
                                }
                            }
                            if (accessOrdering <= access.accessOrdering)
                            {
                                return Ok(new
                                {
                                    error = "Error 1002, No se disponen de los privilegios suficientes."
                                });                                
                            }

                            //Eliminar centro pasados
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText =
                                    "DELETE FROM client_user_centros WHERE clientUserId = @USER";

                                command.Parameters.AddWithValue("@USER", userId);
                                command.ExecuteNonQuery();
                            }

                            //Agrecar los nuevos centros
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText =
                                    "INSERT INTO client_user_centros (centroId, clientUserId) VALUES (@CENTRO, @USER)";

                                command.Parameters.AddWithValue("@USER", userId);
                                command.Parameters.Add("@CENTRO", System.Data.SqlDbType.VarChar);
                                foreach (string centroId in centroIds)
                                {
                                    command.Parameters["@CENTRO"].Value = centroId;
                                    command.ExecuteNonQuery();
                                }
                            }

                            setClientRestrictions(userId, restrictedPermissons, restrictedDashboard, conn, transaction);

                            transaction.Commit();
                            result = new { error = false };

                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5874, no se ha podido asignar los permisos" };
                        }
                    }
                }
            }
            return Ok(result);
        }

        [HttpPost]
        [Route(template: "create-for-client/")]
        public async Task<IActionResult> CreateForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, null, null, out ClientUserAccess access) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });
            }
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("username", out JsonElement usernameJson) &&
                json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("accessLevel", out JsonElement accessLevelJson) &&
                json.TryGetProperty("pwd", out JsonElement pwdJson) &&
                json.TryGetProperty("centros", out JsonElement centrosJson))
            {

                string username = usernameJson.GetString();
                string email = emailJson.GetString();
                string accessLevel = accessLevelJson.GetString();
                string pwd = pwdJson.GetString();
                List<string> centros = GetJsonStringList(centrosJson);

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    bool failed = false;

                    try
                    {
                        int accessOrdering = FindAccessOrderingByAccessLevel(accessLevel, conn);
                        if (accessOrdering <= access.accessOrdering)
                        {
                            return Ok(new
                            {
                                error = "Error 1002, No se disponen de los privilegios suficientes."
                            });                            
                        }
                        string dniUsedBy = CheckLOGINunique(username, null, conn);
                        if (dniUsedBy != null)
                        {
                            failed = true;
                            result = new { error = $"Error 4848, el nombre de usuario '{username}' ya está en uso por {dniUsedBy}." };
                        }
                        if (email != null)
                        {
                            string emailUsedBy = CheckEMAILunique(email, null, conn);
                            if (emailUsedBy != null)
                            {
                                failed = true;
                                result = new { error = $"Error 4848, el email '{email}' ya está en uso por {emailUsedBy}." };
                            }
                        }
                        string creador = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT creador FROM client_users WHERE token = @TOKEN";
                            command.Parameters.AddWithValue("@TOKEN", clientToken);
                            creador = (string)command.ExecuteScalar();
                        }

                        if (!failed)
                        {
                            string id = ComputeStringHash(username + DateTime.Now);
                            string token = ComputeStringHash(username + DateTime.Now.Millisecond + "a");
                            pwd ??= CreatePassword();
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "INSERT INTO client_users (id, token, pwd, username, email, accessLevel, creador) VALUES (@ID, @TOKEN, @PWD, @USERNAME, @EMAIL, @ACCESS, @CREADOR)";
                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@TOKEN", token);
                                command.Parameters.AddWithValue("@PWD", EncryptString(pwd));
                                command.Parameters.AddWithValue("@USERNAME", username);
                                command.Parameters.AddWithValue("@EMAIL", (object)email ?? DBNull.Value);
                                command.Parameters.AddWithValue("@ACCESS", accessLevel);
                                command.Parameters.AddWithValue("@CREADOR", creador);
                                command.ExecuteNonQuery();
                            }

                            //Agrecar los centros
                            foreach (string centro in centros)
                            {
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.CommandText = "INSERT INTO client_user_centros (centroId, clientUserId) VALUES(@CENTRO, @USER)";
                                    command.Parameters.AddWithValue("@USER", id);
                                    command.Parameters.AddWithValue("@CENTRO", centro);
                                    command.ExecuteNonQuery();
                                }
                            }
                            result = new
                            {
                                error = false,
                                pwd,
                                id
                            };
                            if (email != null)
                            {
                                SendEmail(new Email()
                                {
                                    template = "clientUserRegister",
                                    inserts = new Dictionary<string, string>() { { "url", AccessController.getAutoLoginUrl("cl", id, token, conn, null) }, { "pwd", pwd }, { "username", username } },
                                    toEmail = email,
                                    toName = username,
                                    subject = "Bienvenid@ a THINKANDJOB",
                                    priority = EmailPriority.IMMEDIATE
                                });
                            }
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5872, no se ha podido crear el usuario" };
                    }

                }
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "delete-for-client/{userId}")]
        public async Task<IActionResult> DeleteForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, null, null, out ClientUserAccess access) == null)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
                });                
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                bool failed = false;
                try
                {
                    //Comprobar si el usuario existe y que nivel de permisos tiene
                    string username = null;
                    int accessOrdering = 100;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT U.username, AL.ordering FROM client_users U INNER JOIN client_access_levels AL ON(U.accessLevel = AL.id) WHERE U.id = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                username = reader.GetString(reader.GetOrdinal("username"));
                                accessOrdering = reader.GetInt32(reader.GetOrdinal("ordering"));
                            }
                            else
                            {
                                failed = true;
                                result = new { error = "Error 4872, no se ha encontrado el usuario" };
                            }
                        }
                    }

                    if (!failed)
                    {
                        if (accessOrdering <= access.accessOrdering)
                        {
                            return Ok(new
                            {
                                error = "Error 1002, No se disponen de los privilegios suficientes."
                            });
                        }
                        deleteClientUser(conn, userId, username, null);

                        result = new { error = false };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido eliminar el usuario" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "generate-bulk-template-for-client/")]
        public IActionResult GenerateBulkTemplate(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, conn, null, out ClientUserAccess access) == null)
                    {
                        return Ok(new
                        {
                            error = "Error 1002, No se disponen de los privilegios suficientes."
                        });
                    }

                    string tmpDir = GetTemporaryDirectory();

                    const int FREE_SPACES = 100;

                    XSSFWorkbook workbook = new XSSFWorkbook();
                    IDataFormat dataFormatCustom = workbook.CreateDataFormat();

                    SetExcelTemplateId(workbook, "cl-users-bulk");

                    //Fuentes
                    IFont fontTitle = workbook.CreateFont();
                    fontTitle.FontName = "Century Gothic";
                    fontTitle.IsBold = true;
                    fontTitle.FontHeightInPoints = 15;
                    fontTitle.Color = NPOI.SS.UserModel.IndexedColors.Black.Index;

                    IFont fontTitleRed = workbook.CreateFont();
                    fontTitleRed.FontName = "Century Gothic";
                    fontTitleRed.IsBold = true;
                    fontTitleRed.FontHeightInPoints = 15;
                    fontTitleRed.Color = NPOI.SS.UserModel.IndexedColors.Red.Index;

                    IFont fontHeader = workbook.CreateFont();
                    fontHeader.FontName = "Century Gothic";
                    fontHeader.IsBold = true;
                    fontHeader.FontHeightInPoints = 13;
                    fontHeader.Color = NPOI.SS.UserModel.IndexedColors.White.Index;

                    IFont fontBlack = workbook.CreateFont();
                    fontBlack.FontName = "Century Gothic";
                    fontBlack.IsBold = true;
                    fontBlack.FontHeightInPoints = 10;
                    fontBlack.Color = NPOI.SS.UserModel.IndexedColors.Black.Index;

                    //Formatos
                    ICellStyle titleStyle = workbook.CreateCellStyle();
                    titleStyle.SetFont(fontTitle);

                    ICellStyle titleRedStyle = workbook.CreateCellStyle();
                    titleRedStyle.SetFont(fontTitleRed);

                    XSSFCellStyle headerGreenStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                    headerGreenStyle.SetFont(fontHeader);
                    headerGreenStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 112, 173, 71 });
                    headerGreenStyle.FillPattern = FillPattern.SolidForeground;
                    headerGreenStyle.BorderTop = BorderStyle.Thin;
                    headerGreenStyle.BorderBottom = BorderStyle.Thin;
                    headerGreenStyle.BorderRight = BorderStyle.Thin;
                    headerGreenStyle.BorderLeft = BorderStyle.Thin;
                    headerGreenStyle.Alignment = HorizontalAlignment.Center;

                    XSSFCellStyle headerGreen45Style = (XSSFCellStyle)workbook.CreateCellStyle();
                    headerGreen45Style.SetFont(fontHeader);
                    headerGreen45Style.FillForegroundColorColor = new XSSFColor(new byte[] { 112, 173, 71 });
                    headerGreen45Style.FillPattern = FillPattern.SolidForeground;
                    headerGreen45Style.BorderTop = BorderStyle.Thin;
                    headerGreen45Style.BorderBottom = BorderStyle.Thin;
                    headerGreen45Style.BorderRight = BorderStyle.Thin;
                    headerGreen45Style.BorderLeft = BorderStyle.Thin;
                    headerGreen45Style.Alignment = HorizontalAlignment.Center;
                    headerGreen45Style.Rotation = 45;

                    XSSFCellStyle headerGreen45AltStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                    headerGreen45AltStyle.SetFont(fontHeader);
                    headerGreen45AltStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 148, 209, 107 });
                    headerGreen45AltStyle.FillPattern = FillPattern.SolidForeground;
                    headerGreen45AltStyle.BorderTop = BorderStyle.Thin;
                    headerGreen45AltStyle.BorderBottom = BorderStyle.Thin;
                    headerGreen45AltStyle.BorderRight = BorderStyle.Thin;
                    headerGreen45AltStyle.BorderLeft = BorderStyle.Thin;
                    headerGreen45AltStyle.Alignment = HorizontalAlignment.Center;
                    headerGreen45AltStyle.Rotation = 45;

                    XSSFCellStyle freeStyle = (XSSFCellStyle)workbook.CreateCellStyle();
                    freeStyle.FillForegroundColorColor = new XSSFColor(new byte[] { 226, 239, 218 });
                    freeStyle.FillPattern = FillPattern.SolidForeground;
                    freeStyle.BorderTop = BorderStyle.Thin;
                    freeStyle.BorderBottom = BorderStyle.Thin;
                    freeStyle.BorderRight = BorderStyle.Thin;
                    freeStyle.BorderLeft = BorderStyle.Thin;
                    freeStyle.Alignment = HorizontalAlignment.Center;
                    freeStyle.IsLocked = false;

                    ISheet sheet = workbook.CreateSheet("Usuarios");

                    //Tamaños de filas y columnas
                    ICell cell;
                    IRow row, rowB, rowZ;
                    sheet.SetColumnWidth(1, 30 * 256);
                    sheet.SetColumnWidth(2, 30 * 256);
                    sheet.SetColumnWidth(3, 30 * 256);
                    sheet.SetColumnWidth(4, 25 * 256);

                    //Avisos de la primera pagina
                    string[] messages = new string[] {
                        "• No es necesario que marque los centros a los que no tiene acceso.",
                        "• El nombre de usaurio debe ser único y contener de 6 a 25 caracteres.",
                        "• El email es opcional. Si se especifica debe ser únco.",
                        "• La contraseña es opcional. Si se deja en blanco se generará una aleatoriamente.",
                        "• Si se especifica la contraseña, esta debe tener 8 caracteres y estar compuesta por números y letras.",
                        "• El campo email y contraseña no pueden estar vacios a la vez."
                    };
                    for (int i = 0; i < messages.Length; i++)
                    {
                        int iRow = i + 1;
                        row = sheet.CreateRow(iRow);
                        cell = row.CreateCell(1);
                        cell.SetCellValue(messages[i]);
                        cell.CellStyle = i == messages.Length - 1 ? titleRedStyle : titleStyle;
                        sheet.AddMergedRegion(new CellRangeAddress(iRow, iRow, 1, 10));
                    }

                    //Cabecera
                    row = sheet.CreateRow(8);
                    rowB = sheet.CreateRow(9);
                    rowZ = sheet.CreateRow(10 + FREE_SPACES);

                    cell = row.CreateCell(1);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("Nombre de usuario");
                    rowB.CreateCell(1).CellStyle = headerGreenStyle;
                    sheet.AddMergedRegion(new CellRangeAddress(8, 9, 1, 1));
                    cell = row.CreateCell(2);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("Email (opcional)");
                    rowB.CreateCell(2).CellStyle = headerGreenStyle;
                    sheet.AddMergedRegion(new CellRangeAddress(8, 9, 2, 2));
                    cell = row.CreateCell(3);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("Contraseña (opcional)");
                    rowB.CreateCell(3).CellStyle = headerGreenStyle;
                    sheet.AddMergedRegion(new CellRangeAddress(8, 9, 3, 3));
                    cell = row.CreateCell(4);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("Nivel de acceso");
                    rowB.CreateCell(4).CellStyle = headerGreenStyle;
                    sheet.AddMergedRegion(new CellRangeAddress(8, 9, 4, 4));

                    cell = rowB.CreateCell(5);
                    cell.CellStyle = headerGreenStyle;
                    cell.SetCellValue("Permisos");

                    //Cabecera de permisos
                    int permCol = 5;
                    bool light = false;
                    foreach (Empresa empresa in access.empresas)
                    {
                        foreach (Centro centro in empresa.centros)
                        {
                            cell = row.CreateCell(permCol);
                            cell.CellStyle = light ? headerGreen45AltStyle : headerGreen45Style;
                            cell.SetCellValue(empresa.nombre + " - " + centro.alias);

                            cell = rowZ.CreateCell(permCol);
                            cell.SetCellValue(centro.id);

                            permCol++;
                        }
                        light = !light;
                    }
                    for (int i = 6; i < permCol; i++)
                    {
                        cell = rowB.CreateCell(i);
                        cell.CellStyle = headerGreenStyle;
                    }
                    if (permCol - 1 > 5)
                        sheet.AddMergedRegion(new CellRangeAddress(9, 9, 5, permCol - 1));

                    cell = rowZ.CreateCell(1);
                    cell.SetCellValue("ID");

                    //Padding
                    for (int i = permCol; i < permCol + 20; i++)
                    {
                        cell = row.CreateCell(i);
                    }

                    //Selector de si o no
                    XSSFDataValidationHelper dataValidationHelper = new((XSSFSheet)sheet);
                    IDataValidationConstraint dataValidationConstraint = dataValidationHelper.CreateExplicitListConstraint(new string[] { "Sí", "No" });
                    IDataValidationConstraint dataValidationConstraintLevels = dataValidationHelper.CreateExplicitListConstraint(ClientUserAccessController.listAccessLevels(conn).Where(l => l.ordering > access.accessOrdering).Select(l => l.id).ToArray());

                    //Poner espacios libres
                    int nRow = 10;
                    for (int i = 0; i < FREE_SPACES; i++)
                    {
                        row = sheet.CreateRow(nRow + i);
                        for (int j = 1; j < permCol; j++)
                        {
                            cell = row.CreateCell(j);
                            cell.SetCellType(NPOI.SS.UserModel.CellType.String);
                            if (j > 4)
                                cell.SetCellValue("No");
                            cell.CellStyle = freeStyle;
                        }
                    }

                    IDataValidation dataValidationLevels = dataValidationHelper.CreateValidation(dataValidationConstraintLevels, new CellRangeAddressList(nRow, nRow + FREE_SPACES - 1, 4, 4));
                    dataValidationLevels.ShowErrorBox = true;
                    dataValidationLevels.CreateErrorBox("Opción inválida", "Por favor, seleccione el nivel de acceso del usuario.");
                    dataValidationLevels.ShowPromptBox = true;
                    sheet.AddValidationData(dataValidationLevels);

                    CellRangeAddressList yesNoRange = new CellRangeAddressList(nRow, nRow + FREE_SPACES - 1, 5, permCol - 1);
                    IDataValidation dataValidation = dataValidationHelper.CreateValidation(dataValidationConstraint, yesNoRange);
                    dataValidation.ShowErrorBox = true;
                    dataValidation.CreateErrorBox("Opción inválida", "Por favor, seleccione si quiere que el usuario tenga acceso o no al centro.");
                    dataValidation.ShowPromptBox = true;
                    sheet.AddValidationData(dataValidation);

                    rowZ.ZeroHeight = true;

                    //Formato condicional
                    XSSFSheetConditionalFormatting sCF = (XSSFSheetConditionalFormatting)sheet.SheetConditionalFormatting;

                    //Gris si No
                    XSSFConditionalFormattingRule cfGray = (XSSFConditionalFormattingRule)sCF.CreateConditionalFormattingRule(ComparisonOperator.Equal, "\"No\"");
                    XSSFPatternFormatting fillGreen = (XSSFPatternFormatting)cfGray.CreatePatternFormatting();
                    fillGreen.FillBackgroundColor = NPOI.SS.UserModel.IndexedColors.Grey40Percent.Index;
                    fillGreen.FillPattern = FillPattern.SolidForeground;

                    //Verde si Sí
                    XSSFConditionalFormattingRule cfYellow = (XSSFConditionalFormattingRule)sCF.CreateConditionalFormattingRule(ComparisonOperator.Equal, "\"Sí\"");
                    XSSFPatternFormatting fillRed = (XSSFPatternFormatting)cfYellow.CreatePatternFormatting();
                    fillRed.FillBackgroundColor = NPOI.SS.UserModel.IndexedColors.Gold.Index;
                    fillRed.FillPattern = FillPattern.SolidForeground;

                    sCF.AddConditionalFormatting(new CellRangeAddress[] { new CellRangeAddress(nRow, nRow + FREE_SPACES - 1, 5, permCol - 1) }, cfGray, cfYellow);

                    sheet.ProtectSheet("1234");

                    //Guardado
                    string fileName = "Plantilla.xlsx";
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
            }
            catch (Exception)
            {
                //Console.WriteLine(e.StackTrace);
            }
            return new NoContentResult();
        }

        [HttpGet]
        [Route(template: "get-centros-for-client/{userId}/")]
        public async Task<IActionResult> GetCentrosForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                bool failed = false;
                string otherClientToken = null;

                try
                {
                    //Obtener el clientToken del usuario a consultar
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT token FROM client_users WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                otherClientToken = reader.GetString(reader.GetOrdinal("token"));
                    }

                    if (otherClientToken == null)
                    {
                        failed = true;
                        result = new { error = "4846, usuario no encontrado" };
                    }

                    if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, conn, null, out ClientUserAccess myAccess) == null)
                        return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });

                    if (!failed)
                    {
                        ClientUserAccess otherAccess = getClientUserAccess(otherClientToken, false, conn);

                        result = new { error = false, empresas = otherAccess.empresas };
                    }

                }
                catch (Exception)
                {
                    result = new { error = "Error 5879, no se han podido obtener los centros del usuario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-incidences-for-client/{userId}/")]
        public async Task<IActionResult> GetIncidencesForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();


                if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, conn) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }

                try
                {
                    //Obtener el username y verificar que existe
                    string username = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT username FROM client_users WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                username = reader.GetString(reader.GetOrdinal("username"));
                    }

                    if (username == null)
                    {
                        result = new { error = "Error 4872, no se ha encontrado el usuario" };
                    }
                    else
                    {
                        result = new { error = false, incidences = listIncidencesByClientUser(username, conn) };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se han podido obtener las incidencias del usuario" };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "get-other-for-client/{userId}/")]
        public IActionResult GetOther(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, conn) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT * FROM client_users WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new
                                {
                                    error = false,
                                    user = new ClientUser
                                    {
                                        id = reader.GetString(reader.GetOrdinal("id")),
                                        username = reader.GetString(reader.GetOrdinal("username")),
                                        email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                        accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                                        activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1,
                                        photo = ReadFile(new[] { "clientuser", userId, "photo" }),
                                        visible = reader.GetInt32(reader.GetOrdinal("visible")) == 1,
                                        lastLogin = reader.IsDBNull(reader.GetOrdinal("lastLogin")) ? null : reader.GetDateTime(reader.GetOrdinal("lastLogin"))
                                    }
                                };
                            }
                            else
                            {
                                result = new { error = "Error 4860, usuario de cliente no encontrado" };
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener informacion del usuario" };
                }

            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-photo-for-client/{userId}/")]
        public IActionResult GetPictureForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, null) == null)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    result = new { error = false, photo = ReadFile(new[] { "clientuser", userId, "photo" }) };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener informacion del usuario" };
                }

            }
            return Ok(result);
        }


        [HttpGet]
        [Route(template: "get-top-incidences-for-client/")]
        public IActionResult GetTopIncidencesForClien()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_INCIDENCIAS) == null)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<IncidenceNotAttendController.NotAttendIncidence> notAttends = new();
                    List<IncidenceExtraHoursController.ExtraHoursIncidence> extraHours = new();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT TOP 5 I.id, I.number, I.centroId, I.candidateId, I.title, I.date, I.category, I.state, I.closed, I.creationTime, I.baja, I.hasCandidateUnread, I.hasClientUnread, " +
                            "E.nombre as companyName, E.cif as companyCif, E.id as companyId, NULLIF(TRIM(CONCAT(C.nombre, ' ', C.apellidos)), '') as candidateName, C.dni " +
                            "FROM incidencia_falta_asistencia I " +
                            "INNER JOIN centros CE ON(I.centroId = CE.id) " +
                            "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                            "LEFT OUTER JOIN candidatos C ON(I.candidateId = C.id) " +
                            "INNER JOIN client_user_centros CUC ON(I.centroId = CUC.centroId) " +
                            "INNER JOIN client_users CU ON(CUC.clientUserId = CU.id) WHERE " +
                            "CU.token = @TOKEN AND " +
                            "I.hasClientUnread = 1 " +
                            "ORDER BY I.creationTime DESC";

                        command.Parameters.AddWithValue("@TOKEN", clientToken);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                notAttends.Add(new IncidenceNotAttendController.NotAttendIncidence()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    number = reader.GetInt32(reader.GetOrdinal("number")),
                                    companyId = reader.GetString(reader.GetOrdinal("companyId")),
                                    centroId = reader.GetString(reader.GetOrdinal("centroId")),
                                    companyName = reader.GetString(reader.GetOrdinal("companyName")),
                                    companyCif = reader.GetString(reader.GetOrdinal("companyCif")),
                                    candidateId = reader.IsDBNull(reader.GetOrdinal("candidateId")) ? null : reader.GetString(reader.GetOrdinal("candidateId")),
                                    candidateName = reader.IsDBNull(reader.GetOrdinal("candidateName")) ? null : reader.GetString(reader.GetOrdinal("candidateName")),
                                    candidateDni = reader.IsDBNull(reader.GetOrdinal("dni")) ? null : reader.GetString(reader.GetOrdinal("dni")),
                                    date = reader.GetDateTime(reader.GetOrdinal("date")),
                                    category = reader.GetString(reader.GetOrdinal("category")),
                                    state = reader.GetString(reader.GetOrdinal("state")),
                                    closed = reader.GetInt32(reader.GetOrdinal("closed")) == 1,
                                    creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                                    baja = reader.GetInt32(reader.GetOrdinal("baja")) == 1,
                                    hasCandidateUnread = reader.GetInt32(reader.GetOrdinal("hasCandidateUnread")) == 1,
                                    hasClientUnread = reader.GetInt32(reader.GetOrdinal("hasClientUnread")) == 1
                                });
                            }
                        }
                    }


                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT TOP 5 I.id, I.number, I.centroId, I.day, I.state, I.closed, I.creationTime, I.createdBy, CE.alias as centroAlias, " +
                            "E.id as companyId, E.nombre as clientName, E.cif, E.id as companyId " +
                            "FROM incidencia_horas_extra I " +
                            "INNER JOIN centros CE ON(I.centroId = CE.id) " +
                            "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                            "INNER JOIN client_user_centros CUC ON(I.centroId = CUC.centroId) " +
                            "INNER JOIN client_users CU ON(CUC.clientUserId = CU.id) WHERE " +
                            "CU.token = @TOKEN AND " +
                            "I.closed = 0 " +
                            "ORDER BY I.creationTime DESC";

                        command.Parameters.AddWithValue("@TOKEN", clientToken);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                extraHours.Add(new IncidenceExtraHoursController.ExtraHoursIncidence()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    number = reader.GetInt32(reader.GetOrdinal("number")),
                                    companyId = reader.GetString(reader.GetOrdinal("companyId")),
                                    centroId = reader.GetString(reader.GetOrdinal("centroId")),
                                    centroAlias = reader.GetString(reader.GetOrdinal("centroAlias")),
                                    day = reader.GetDateTime(reader.GetOrdinal("day")),
                                    state = reader.GetString(reader.GetOrdinal("state")),
                                    creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                                    createdBy = reader.GetString(reader.GetOrdinal("createdBy"))
                                });
                            }
                        }
                    }

                    result = new { error = false, notAttends, extraHours };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se han podido obtener las incidencias del usuario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-user-restrictions-for-client/{userId}/")]
        public IActionResult GetUserRestrictionsForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                if (ClientHasPermission(clientToken, null, null, null, conn) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }

                try
                {
                    if (tryGetClientUserRestrictions(userId, out HashSet<string> permissions, out HashSet<string> restrictedPermissons, out HashSet<string> dashboard, out HashSet<string> restrictedDashboard, conn))
                    {
                        result = new { error = false, permissions, restrictedPermissons, dashboard, restrictedDashboard };
                    }
                    else
                    {
                        result = new { error = "Error 4701, usuario no encontrado" };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener informacion del usuario" };
                }

            }

            return Ok(result);
        }


        [HttpPatch]
        [Route(template: "grant-centro-for-client/")]
        public async Task<IActionResult> GrantCentroForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson))
            {
                string userId = userIdJson.GetString();
                string centroId = centroIdJson.GetString();

                if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_USUARIOS) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }
                result = grantCentro(userId, centroId);
            }
            return Ok(result);
        }


        [HttpPatch]
        [Route(template: "grant-company-for-client/")]
        public async Task<IActionResult> GrantCompanyForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson))
            {
                string userId = userIdJson.GetString();
                string companyId = companyIdJson.GetString();

                if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_USUARIOS) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }

                result = grantCompany(userId, companyId);
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "list-for-client/{centroId}/")]
        public IActionResult ListByCentroForClient(string centroId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (ClientHasPermission(clientToken, null, centroId, null) == null)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    result = new
                    {
                        error = false,
                        users = listByCentro(centroId, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5871, no han podido listar los usuarios" };
                }

            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "revoke-centro-for-client/")]
        public async Task<IActionResult> RevokeCentroForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson))
            {
                string userId = userIdJson.GetString();
                string centroId = centroIdJson.GetString();

                if (ClientHasPermission(clientToken, null, centroId, CL_PERMISSION_USUARIOS) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }

                result = revokeCentro(userId, centroId);
            }

            return Ok(result);
        }


        [HttpPatch]
        [Route(template: "revoke-company-for-client/")]
        public async Task<IActionResult> RevokeCompanyForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson))
            {
                string userId = userIdJson.GetString();
                string companyId = companyIdJson.GetString();
                if (ClientHasPermission(clientToken, companyId, null, CL_PERMISSION_USUARIOS) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }
                result = revokeCompany(userId, companyId);
            }

            return Ok(result);
        }


        [HttpPut]
        [Route(template: "update-for-client/{userId}/")]
        public async Task<IActionResult> UpdateForClient(string userId)
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, null, null, out ClientUserAccess access) == null)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("username", out JsonElement username) &&
                json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("accessLevel", out JsonElement accessLevelJson) &&
                json.TryGetProperty("visible", out JsonElement visibleJson) &&
                json.TryGetProperty("activo", out JsonElement activoJson) &&
                json.TryGetProperty("newPwd", out JsonElement newPwdJson) &&
                json.TryGetProperty("photo", out JsonElement photoJson))
            {
                ClientUser user = new ClientUser()
                {
                    id = userId,
                    username = username.GetString(),
                    email = emailJson.GetString(),
                    accessLevel = accessLevelJson.GetString(),
                    activo = GetJsonBool(activoJson),
                    visible = GetJsonBool(visibleJson),
                    pwd = newPwdJson.GetString(),
                    photo = photoJson.GetString()
                };

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    bool failed = false;

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        int accessOrdering = 100;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText =
                                "SELECT AL.ordering FROM client_users U INNER JOIN client_access_levels AL ON(U.accessLevel = AL.id) WHERE U.id = @ID";

                            command.Parameters.AddWithValue("@ID", userId);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    accessOrdering = reader.GetInt32(reader.GetOrdinal("ordering"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new { error = "Error 4872, no se ha encontrado el usuario" };
                                }
                            }
                        }

                        if (!failed)
                        {
                            if (accessOrdering <= access.accessOrdering)
                            {
                                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });                                
                            }

                            UpdateResult updateResult = updateClientUser(conn, transaction, user);
                            result = updateResult.result;
                            failed = updateResult.failed;
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
            }

            return Ok(result);
        }


        [HttpPut]
        [Route(template: "update-self-for-client/")]
        public async Task<IActionResult> UpdateSelfForClient()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("user", out JsonElement userJson) &&
                userJson.TryGetProperty("photo", out JsonElement photoJson) &&
                userJson.TryGetProperty("oldPwd", out JsonElement oldPwdJson) &&
                userJson.TryGetProperty("newPwd", out JsonElement newPwdJson))
            {
                string oldPwd = oldPwdJson.GetString();
                ClientUser user = new ClientUser()
                {
                    pwd = newPwdJson.GetString(),
                    photo = photoJson.GetString(),
                    accessLevel = null,
                    activo = null,
                    visible = null
                };

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    bool failed = false;

                    string accessLevel = null;
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        //Comprobar la pass
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;

                            command.CommandText = "SELECT id, accessLevel FROM client_users WHERE token = @TOKEN AND pwd = @PWD AND activo = 1";
                            command.Parameters.AddWithValue("@TOKEN", clientToken);
                            command.Parameters.AddWithValue("@PWD", EncryptString(oldPwd));

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    user.id = reader.GetString(reader.GetOrdinal("id"));
                                    accessLevel = reader.GetString(reader.GetOrdinal("accessLevel"));
                                }
                                else
                                {
                                    failed = true;
                                    result = new { error = "Error 4843, contraseña incorrecta o usuario no encontrado" };
                                }
                            }
                        }

                        if (!failed)
                        {
                            try
                            {
                                if (json.TryGetStringList("restrictedDashboard", out List<string> restrictedDashboard) && accessLevel == "Administrador")
                                {
                                    setClientRestrictions(user.id, null, restrictedDashboard, conn, transaction);
                                }
                            }
                            catch (Exception)
                            {
                                failed = true;
                                result = new { error = "Error 5387, No se han podido configurar los avisos a mostrar." };
                            }
                        }

                        if (!failed)
                        {
                            UpdateResult updateResult = updateClientUser(conn, transaction, user);
                            failed = updateResult.failed;
                            result = updateResult.result;
                        }


                        if (failed)
                        {
                            transaction.Rollback();
                        }
                        else
                        {
                            LogToDB(LogType.CLIENTUSER_UPDATED_BY_CLIENT, $"Usuario de cliente {FindUserClientEmailByClientToken(clientToken, conn, transaction)} actualizado por sí mismo", null, conn, transaction);

                            transaction.Commit();
                        }
                    }
                }
            }

            return Ok(result);
        }


        [HttpPost]
        [Route("upload-bulk-template-for-client/")]
        public async Task<IActionResult> UploadBulkTemplate()
        {
            string clientToken = Cl_Security.getSecurityInformation(User, "token");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };
            try
            {
                if (ClientHasPermission(clientToken, null, null, CL_PERMISSION_USUARIOS, null, null, out ClientUserAccess access) == null)
                {
                    return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
                }

                using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
                string data = await readerBody.ReadToEndAsync();
                JsonElement json = JsonDocument.Parse(data).RootElement;

                if (json.TryGetProperty("xlsx", out JsonElement xlsxJson))
                {
                    string xlsxString = xlsxJson.GetString();
                    byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
                    string tmpDir = GetTemporaryDirectory();
                    string tmpFile = Path.Combine(tmpDir, "template.xlsx");
                    System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);

                    XSSFWorkbook workbook = new XSSFWorkbook(tmpFile);
                    ISheet sheet = workbook.GetSheetAt(0);

                    if (!CheckExcelTemplateId(workbook, "cl-users-bulk"))
                        return Ok(new { error = "Error 4010, plantilla incorrecta." });

                    List<string> errorList = new List<string>();
                    const int MAX_DEEP_SEARCH = 1000;
                    int lastRow = -1;

                    IRow row;
                    ICell cell;
                    DataFormatter formatter = new DataFormatter();

                    //Buscar las IDS
                    for (int i = 10; i < 10 + MAX_DEEP_SEARCH; i++)
                    {
                        row = sheet.GetRow(i);
                        if (row == null) continue;

                        cell = row.GetCell(1);
                        if (cell == null) continue;

                        if ("ID".Equals(cell.StringCellValue))
                        {
                            lastRow = i;
                            break;
                        }
                    }

                    if (lastRow == -1)
                        return Ok(new { error = "Error 4011, no se pudo procesar la plantilla." });

                    row = sheet.GetRow(lastRow);

                    List<string> ids = new();
                    for (int i = 5; i < 5 + MAX_DEEP_SEARCH; i++)
                    {
                        cell = row.GetCell(i);
                        if (cell == null || string.IsNullOrWhiteSpace(formatter.FormatCellValue(cell)))
                            break;

                        ids.Add(formatter.FormatCellValue(cell));
                    }

                    //Comprobar que tenga permiso para todos los centros de la plantilla
                    if (ids.Any(id => access.empresas.All(e => e.centros.All(c => c.id != id))))
                        return Ok(new { error = "Error 4011, la plantilla contiene centros para los que no tiene permiso" });

                    //Leer los usuarios
                    List<Tuple<ClientUser, List<string>, bool>> users = new();
                    for (int i = 10; i < lastRow; i++)
                    {
                        row = sheet.GetRow(i);
                        if (row == null) continue;

                        ClientUser user = new()
                        {
                            username = formatter.FormatCellValue(row.GetCell(1))?.Trim(),
                            email = formatter.FormatCellValue(row.GetCell(2))?.Trim(),
                            pwd = formatter.FormatCellValue(row.GetCell(3))?.Trim(),
                            accessLevel = formatter.FormatCellValue(row.GetCell(4))?.Trim(),
                        };

                        if (user.username != null && string.IsNullOrWhiteSpace(user.username)) user.username = null;
                        if (user.email != null && string.IsNullOrWhiteSpace(user.email)) user.email = null;
                        if (user.pwd != null && string.IsNullOrWhiteSpace(user.pwd)) user.pwd = null;
                        if (user.accessLevel != null && string.IsNullOrWhiteSpace(user.accessLevel)) user.accessLevel = null;

                        if (user.username == null && user.accessLevel == null)
                            continue;

                        List<string> missing = new();
                        if (user.username == null) missing.Add("nombre de usuario");
                        if (user.accessLevel == null) missing.Add("nivel de acceso");

                        if (missing.Count > 0)
                        {
                            errorList.Add($"Error fila {i + 1}: No se ha especificado: {string.Join(", ", missing)}");
                            continue;
                        }

                        if (user.email == null && user.pwd == null)
                        {
                            errorList.Add($"Error fila {i + 1}: Por favor, introduzca una contraseña o un email dónde recibir la contraseña autogenerada.");
                            continue;
                        }

                        List<string> centros = new();
                        for (int j = 0; j < ids.Count; j++)
                        {
                            if ("Sí".Equals(formatter.FormatCellValue(row.GetCell(5 + j))))
                                centros.Add(ids[j]);
                        }

                        if (centros.Count == 0)
                        {
                            errorList.Add($"Error fila {i + 1}: El usuario debe tener, al menos, acceso a un centro.");
                            continue;
                        }

                        if (user.pwd != null && !CheckPwdIsSafe(user.pwd))
                        {
                            errorList.Add($"Error fila {i + 1}: La contraseña debe tener al menos 8 caracteres y estar compuesta por númreos y letras.");
                            continue;
                        }

                        users.Add(new(user, centros, false));
                    }

                    workbook.Close();
                    Directory.Delete(tmpDir, true);

                    //Crear los usuarios
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();

                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                string creador = null;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "SELECT creador FROM client_users WHERE token = @TOKEN";
                                    command.Parameters.AddWithValue("@TOKEN", clientToken);
                                    creador = (string)command.ExecuteScalar();
                                }

                                for (int i = 0; i < users.Count; i++)
                                {
                                    if (CheckLOGINunique(users[i].Item1.username, null, conn, transaction) != null)
                                    {
                                        errorList.Add($"El nombre de usuario '{users[i].Item1.username}' ya está en uso.");
                                        continue;
                                    }
                                    if (users[i].Item1.email != null && CheckEMAILunique(users[i].Item1.email, null, conn, transaction) != null)
                                    {
                                        errorList.Add($"El email '{users[i].Item1.email}' ya está en uso.");
                                        continue;
                                    }

                                    users[i] = new(users[i].Item1, users[i].Item2, true);
                                }

                                foreach (var user in users)
                                {
                                    if (!user.Item3)
                                        continue;

                                    string id = ComputeStringHash(user.Item1.username + DateTime.Now);
                                    string token = ComputeStringHash(user.Item1.username + DateTime.Now.Millisecond + "a");
                                    string pwd = user.Item1.pwd ?? CreatePassword();
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText =
                                            "INSERT INTO client_users (id, token, pwd, username, email, accessLevel, creador) VALUES (@ID, @TOKEN, @PWD, @USERNAME, @EMAIL, @ACCESS, @CREADOR)";

                                        command.Parameters.AddWithValue("@ID", id);
                                        command.Parameters.AddWithValue("@TOKEN", token);
                                        command.Parameters.AddWithValue("@PWD", EncryptString(pwd));
                                        command.Parameters.AddWithValue("@USERNAME", user.Item1.username);
                                        command.Parameters.AddWithValue("@EMAIL", (object)user.Item1.email ?? DBNull.Value);
                                        command.Parameters.AddWithValue("@ACCESS", user.Item1.accessLevel);
                                        command.Parameters.AddWithValue("@CREADOR", creador);

                                        command.ExecuteNonQuery();
                                    }

                                    //Agrecar los centros
                                    foreach (string centro in user.Item2)
                                    {
                                        using (SqlCommand command = conn.CreateCommand())
                                        {
                                            command.Connection = conn;
                                            command.Transaction = transaction;
                                            command.CommandText = "INSERT INTO client_user_centros (centroId, clientUserId) VALUES(@CENTRO, @USER)";
                                            command.Parameters.AddWithValue("@USER", id);
                                            command.Parameters.AddWithValue("@CENTRO", centro);
                                            command.ExecuteNonQuery();
                                        }
                                    }

                                    if (user.Item1.email != null)
                                    {
                                        SendEmail(new Email()
                                        {
                                            template = "clientUserRegister",
                                            inserts = new Dictionary<string, string>() { { "url", AccessController.getAutoLoginUrl("cl", id, token, conn, null) }, { "pwd", pwd }, { "username", user.Item1.username } },
                                            toEmail = user.Item1.email,
                                            toName = user.Item1.username,
                                            subject = "Bienvenid@ a THINKANDJOB",
                                            priority = EmailPriority.MODERATE
                                        });
                                    }
                                }

                                transaction.Commit();
                                result = new { error = false, errorList };
                            }
                            catch (Exception)
                            {
                                transaction.Rollback();
                                result = new { error = "Error 5822, no se han podido crear los usuarios" };
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5821, no se ha podido procesar el documento" };
            }

            //return new JsonResult(result);
            return Ok(result);
        }

        //------------------------------securityToken------------------------------

        [HttpPatch]
        [Route(template: "assoc-centros/{userId}/")]
        public async Task<IActionResult> AssocCentros(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("ClientUser.AssocCentros", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetStringList("centros", out List<string> centroIds))
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            assocCentros(userId, centroIds, conn, transaction);

                            transaction.Commit();
                            result = new { error = false };

                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            result = new { error = "Error 5874, no se ha podido asignar los permisos" };
                        }
                    }
                }
            }

            return Ok(result);
        }



        [HttpPost]
        [Route(template: "create/")]
        public async Task<IActionResult> Create()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("ClientUser.Create", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });                
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("username", out JsonElement usernameJson) &&
                json.TryGetProperty("email", out JsonElement emailJson) &&
                json.TryGetProperty("accessLevel", out JsonElement accessLevelJson) &&
                json.TryGetProperty("pwd", out JsonElement pwdJson))
            {

                string username = usernameJson.GetString();
                string email = emailJson.GetString();
                string accessLevel = accessLevelJson.GetString();
                string pwd = pwdJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    bool failed = false;

                    try
                    {
                        string dniUsedBy = CheckLOGINunique(username, null, conn);
                        if (dniUsedBy != null)
                        {
                            failed = true;
                            result = new { error = $"Error 4848, el nombre de usuario '{username}' ya está en uso por {dniUsedBy}." };
                        }

                        if (email != null)
                        {
                            string emailUsedBy = CheckEMAILunique(email, null, conn);
                            if (emailUsedBy != null)
                            {
                                failed = true;
                                result = new { error = $"Error 4848, el email '{email}' ya está en uso por {emailUsedBy}." };
                            }
                        }

                        string creador = null;
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT DocID FROM users WHERE securityToken = @TOKEN";
                            command.Parameters.AddWithValue("@TOKEN", securityToken);
                            creador = (string)command.ExecuteScalar();
                        }

                        if (!failed)
                        {
                            string id = ComputeStringHash(username + DateTime.Now);
                            string token = ComputeStringHash(username + DateTime.Now.Millisecond + "a");
                            pwd ??= CreatePassword();
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText =
                                    "INSERT INTO client_users (id, token, pwd, username, email, accessLevel, creador) VALUES (@ID, @TOKEN, @PWD, @USERNAME, @EMAIL, @ACCESS, @CREADOR)";

                                command.Parameters.AddWithValue("@ID", id);
                                command.Parameters.AddWithValue("@TOKEN", token);
                                command.Parameters.AddWithValue("@PWD", EncryptString(pwd));
                                command.Parameters.AddWithValue("@USERNAME", username);
                                command.Parameters.AddWithValue("@EMAIL", (object)email ?? DBNull.Value);
                                command.Parameters.AddWithValue("@ACCESS", accessLevel);
                                command.Parameters.AddWithValue("@CREADOR", creador);

                                command.ExecuteNonQuery();
                            }
                            result = new
                            {
                                error = false,
                                pwd,
                                id
                            };

                            LogToDB(LogType.CLIENTUSER_CREATED, $"Usuario de cliente {username} creado", FindUsernameBySecurityToken(securityToken, conn), conn);
                            if (email != null)
                            {
                                SendEmail(new Email()
                                {
                                    template = "clientUserRegister",
                                    inserts = new Dictionary<string, string>() { { "url", AccessController.getAutoLoginUrl("cl", id, token, conn, null) }, { "pwd", pwd }, { "username", username } },
                                    toEmail = email,
                                    toName = username,
                                    subject = "Bienvenid@ a THINKANDJOB",
                                    priority = EmailPriority.IMMEDIATE
                                });
                            }
                        }
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5872, no se ha podido crear el usuario" };
                    }

                }
            }

            return Ok(result);
        }


        [HttpPost]
        [Route(template: "create-admin-for-company/{companyId}/")]
        public IActionResult CreateAdminForCompany(string companyId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            if (!HasPermission("ClientUser.CreateAdminForCompany", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    createAdminClientUserFromCompanyId(companyId, securityToken, conn);
                    result = new { error = false };
                }
                catch (Exception e)
                {
                    result = new { error = e.Message };
                }
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "{userId}/")]
        public IActionResult Delete(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("ClientUser.Delete", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                bool failed = false;
                try
                {
                    //Comprobar si el usuario existe
                    string username = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT username FROM client_users WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                username = reader.GetString(reader.GetOrdinal("username"));
                            }
                            else
                            {
                                failed = true;
                                result = new { error = "Error 4872, no se ha encontrado el usuario" };
                            }
                        }
                    }

                    if (!failed)
                    {
                        deleteClientUser(conn, userId, username, securityToken);

                        result = new { error = false };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido eliminar el usuario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "{userId}")]
        public IActionResult Get(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("ClientUser.Get", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT * FROM client_users WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new
                                {
                                    error = false,
                                    user = new ClientUser
                                    {
                                        id = reader.GetString(reader.GetOrdinal("id")),
                                        username = reader.GetString(reader.GetOrdinal("username")),
                                        email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                        accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                                        activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1,
                                        photo = ReadFile(new[] { "clientuser", userId, "photo" }),
                                        visible = reader.GetInt32(reader.GetOrdinal("visible")) == 1,
                                        lastLogin = reader.IsDBNull(reader.GetOrdinal("lastLogin")) ? null : reader.GetDateTime(reader.GetOrdinal("lastLogin"))
                                    }
                                };
                            }
                            else
                            {
                                result = new { error = "Error 4860, usuario de cliente no encontrado" };
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener informacion del usuario" };
                }

            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "get-access-level-restrictions/{accessLevel}/")]
        public IActionResult GetAccessLevelRestrictions(string accessLevel)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("ClientUser.GetAccessLevelRestrictions", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    List<string> permissions = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT permission FROM client_access_permissions WHERE accessLevel = @LEVEL";
                        command.Parameters.AddWithValue("@LEVEL", accessLevel);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                permissions.Add(reader.GetString(reader.GetOrdinal("permission")));
                            }
                        }
                    }
                    List<string> dashboard = new();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT type FROM client_access_dashboard WHERE accessLevel = @LEVEL";
                        command.Parameters.AddWithValue("@LEVEL", accessLevel);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dashboard.Add(reader.GetString(reader.GetOrdinal("type")));
                            }
                        }
                    }
                    result = new { error = false, permissions, dashboard };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5703, no han podido obtener informacion del nivel de acceso" };
                }

            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "get-incidences/{userId}/")]
        public IActionResult GetIncidences(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("ClientUser.GetIncidences", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    //Obtener el username y verificar que existe
                    string username = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT username FROM client_users WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                username = reader.GetString(reader.GetOrdinal("username"));
                    }

                    if (username == null)
                    {
                        result = new { error = "Error 4872, no se ha encontrado el usuario" };
                    }
                    else
                    {
                        result = new { error = false, incidences = listIncidencesByClientUser(username, conn) };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se han podido obtener las incidencias del usuario" };
                }
            }

            return Ok(result);
        }


        [HttpGet]
        [Route(template: "/get-centros/{userId}/")]
        public IActionResult GetCentros(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("ClientUser.GetCentros", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                bool failed = false;
                string clientToken = null;

                try
                {
                    //Obtener el clientToken
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT token FROM client_users WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", userId);
                        using (SqlDataReader reader = command.ExecuteReader())
                            if (reader.Read())
                                clientToken = reader.GetString(reader.GetOrdinal("token"));
                    }

                    if (clientToken == null)
                    {
                        failed = true;
                        result = new { error = "4846, usuario no encontrado" };
                    }

                    if (!failed) result = new { error = false, access = getClientUserAccess(clientToken, false, conn) };

                }
                catch (Exception)
                {
                    result = new { error = "Error 5879, no se han podido obtener los centros del usuario" };
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-pwd/{userId}/")]
        public IActionResult GetPwd(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("ClientUser.GetPwd", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });                
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                try
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT pwd FROM client_users WHERE id = @ID";

                        command.Parameters.AddWithValue("@ID", userId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = new { error = false, pwd = DecryptString(reader.GetString(reader.GetOrdinal("pwd"))) };
                            }
                            else
                            {
                                result = new { error = "Error 4872, no se ha encontrado el usuario" };
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido eliminar el usuario" };
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-user-restrictions/{userId}/")]
        public IActionResult GetUserRestrictions(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };
            if (!HasPermission("ClientUser.GetUserRestrictions", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1002, No se disponen de los privilegios suficientes." });
            }
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    if (tryGetClientUserRestrictions(userId, out HashSet<string> permissions, out HashSet<string> restrictedPermissons, out HashSet<string> dashboard, out HashSet<string> restrictedDashboard, conn))
                    {
                        result = new { error = false, permissions, restrictedPermissons, dashboard, restrictedDashboard };
                    }
                    else
                    {
                        result = new { error = "Error 4701, usuario no encontrado" };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5701, no han podido obtener informacion del usuario" };
                }
            }
            return Ok(result);
        }
        //---HEINRRICH 20250224 FIN--



        //---JEAN 20250225 INICIO--

        [HttpPatch]
        [Route(template: "grant-centro/")]
        public async Task<IActionResult> GrantCentro()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };


            if (!HasPermission("ClientUser.GrantCentro", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson))
            {
                result = grantCentro(userIdJson.GetString(), centroIdJson.GetString());
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "grant-company/")]
        public async Task<IActionResult> GrantCompany()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            if (!HasPermission("ClientUser.GrantCompany", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson))
            {
                result = grantCompany(userIdJson.GetString(), companyIdJson.GetString());
            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list/")]
        public async Task<IActionResult> List()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ClientUser.List", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("key", out JsonElement keyJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) && json.TryGetProperty("centroId", out JsonElement centroIdJson) &&
                json.TryGetProperty("page", out JsonElement pageJson) && json.TryGetProperty("perpage", out JsonElement perpageJson))
            {

                string key = keyJson.GetString();
                string companyId = companyIdJson.GetString();
                string centroId = centroIdJson.GetString();
                int page = Int32.Parse(pageJson.GetString());
                int perpage = Int32.Parse(perpageJson.GetString());

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        string creador = null;
                        if (!access.EsJefe)
                        {
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.CommandText = "SELECT DocID FROM users WHERE securityToken = @TOKEN";
                                command.Parameters.AddWithValue("@TOKEN", securityToken);
                                creador = (string)command.ExecuteScalar();
                            }
                        }

                        List<ClientUser> users = new List<ClientUser>();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT U.id, U.username, U.email, U.accessLevel, U.activo, U.visible " +
                                "FROM client_users U " +
                                "WHERE " +
                                "(@CREADOR IS NULL OR U.creador = @CREADOR) AND " +
                                "(@KEY IS NULL OR U.username LIKE @KEY OR U.email LIKE @KEY) AND " +
                                "(@COMPANY IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN centros C ON (CUC.centroId = C.id) INNER JOIN empresas E ON(C.companyId = E.id) WHERE CUC.clientUserId = U.id AND E.id = @COMPANY)) AND " +
                                "(@CENTRO  IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN centros C ON (CUC.centroId = C.id) WHERE CUC.clientUserId = U.id AND C.id = @CENTRO)) " +
                                "ORDER BY U.username " +
                                "OFFSET @OFFSET ROWS FETCH NEXT @LIMIT ROWS ONLY";


                            command.Parameters.AddWithValue("@CREADOR", (object)creador ?? DBNull.Value);
                            command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@COMPANY", ((object)companyId) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);
                            command.Parameters.AddWithValue("@OFFSET", page * perpage);
                            command.Parameters.AddWithValue("@LIMIT", perpage);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string id = id = reader.GetString(reader.GetOrdinal("id"));
                                    users.Add(new ClientUser
                                    {
                                        id = id,
                                        username = reader.GetString(reader.GetOrdinal("username")),
                                        email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                                        accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                                        activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1,
                                        visible = reader.GetInt32(reader.GetOrdinal("visible")) == 1,
                                        photo = ReadFile(new[] { "clientuser", id, "photo" })
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
                        result = new { error = "Error 5871, no han podido listar los usuarios" };
                    }

                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list/{centroId}/")]
        public IActionResult ListByCentro(string centroId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("ClientUser.ListByCentro", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1002, No se disponen de los privilegios suficientes."
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
                        users = listByCentro(centroId, conn)
                    };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5871, no han podido listar los usuarios" };
                }

            }

            return Ok(result);
        }

        [HttpPost]
        [Route(template: "list-count/")]
        public async Task<IActionResult> ListCount()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };

            ResultadoAcceso access = HasPermission("ClientUser.ListCount", securityToken);
            if (!access.Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("key", out JsonElement keyJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson) && json.TryGetProperty("centroId", out JsonElement centroIdJson))
            {

                string key = keyJson.GetString();
                string companyId = companyIdJson.GetString();
                string centroId = centroIdJson.GetString();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    string creador = null;
                    if (!access.EsJefe)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText = "SELECT DocID FROM users WHERE securityToken = @TOKEN";
                            command.Parameters.AddWithValue("@TOKEN", securityToken);
                            creador = (string)command.ExecuteScalar();
                        }
                    }

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT COUNT(*) " +
                            "FROM client_users U " +
                            "WHERE " +
                            "(@CREADOR IS NULL OR U.creador = @CREADOR) AND " +
                            "(@KEY IS NULL OR U.username LIKE @KEY OR U.email LIKE @KEY) AND " +
                            "(@COMPANY IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN centros C ON (CUC.centroId = C.id) INNER JOIN empresas E ON(C.companyId = E.id) WHERE CUC.clientUserId = U.id AND E.id = @COMPANY)) AND " +
                            "(@CENTRO  IS NULL OR EXISTS(SELECT * FROM client_user_centros CUC INNER JOIN centros C ON (CUC.centroId = C.id) WHERE CUC.clientUserId = U.id AND C.id = @CENTRO)) ";

                        command.Parameters.AddWithValue("@CREADOR", (object)creador ?? DBNull.Value);
                        command.Parameters.AddWithValue("@KEY", ((object)(key == null ? null : ("%" + key + "%"))) ?? DBNull.Value);
                        command.Parameters.AddWithValue("@COMPANY", ((object)companyId) ?? DBNull.Value);
                        command.Parameters.AddWithValue("@CENTRO", ((object)centroId) ?? DBNull.Value);

                        result = command.ExecuteScalar();
                    }
                }
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "revoke-centro/")]
        public async Task<IActionResult> RevokeCentro()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            if (!HasPermission("ClientUser.RevokeCentro", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("centroId", out JsonElement centroIdJson))
            {
                result = revokeCentro(userIdJson.GetString(), centroIdJson.GetString());
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "revoke-company/")]
        public async Task<IActionResult> RevokeCompany()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            if (!HasPermission("ClientUser.RevokeCompany", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("userId", out JsonElement userIdJson) &&
                json.TryGetProperty("companyId", out JsonElement companyIdJson))
            {
                result = revokeCompany(userIdJson.GetString(), companyIdJson.GetString());
            }

            return Ok(result);
        }

        [HttpPatch]
        [Route(template: "set-pwd/{userId}/")]
        public async Task<IActionResult> SetPwd(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("ClientUser.SetPwd", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("pwd", out JsonElement pwdJson))
            {

                string pwd = pwdJson.GetString();

                if (pwd == null) pwd = CreatePassword();

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    try
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "UPDATE client_users SET pwd = @PWD WHERE id = @ID";

                            command.Parameters.AddWithValue("@ID", userId);
                            command.Parameters.AddWithValue("@PWD", EncryptString(pwd));

                            command.ExecuteNonQuery();
                        }
                        result = new
                        {
                            error = false,
                            pwd
                        };
                    }
                    catch (Exception)
                    {
                        result = new { error = "Error 5874, no se ha podido cambiar la contraseña" };
                    }

                }
            }

            return Ok(result);
        }

        [HttpPut]
        [Route(template: "update/{userId}/")]
        public async Task<IActionResult> Update(string userId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("ClientUser.Update", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("user", out JsonElement userJson) &&
                userJson.TryGetProperty("username", out JsonElement username) &&
                userJson.TryGetProperty("email", out JsonElement emailJson) &&
                userJson.TryGetProperty("accessLevel", out JsonElement accessLevelJson) &&
                userJson.TryGetProperty("visible", out JsonElement visibleJson) &&
                userJson.TryGetProperty("activo", out JsonElement activoJson) &&
                userJson.TryGetProperty("newPwd", out JsonElement newPwdJson) &&
                userJson.TryGetProperty("photo", out JsonElement photoJson) &&
                json.TryGetStringList("restrictedDashboard", out List<string> restrictedDashboard) &&
                json.TryGetStringList("restrictedPermissons", out List<string> restrictedPermissons))
            {
                ClientUser user = new ClientUser()
                {
                    id = userId,
                    username = username.GetString(),
                    email = emailJson.GetString(),
                    accessLevel = accessLevelJson.GetString(),
                    activo = GetJsonBool(activoJson),
                    visible = GetJsonBool(visibleJson),
                    pwd = newPwdJson.GetString(),
                    photo = photoJson.GetString()
                };

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            bool exists;
                            using (SqlCommand command = conn.CreateCommand())
                            {
                                command.Connection = conn;
                                command.Transaction = transaction;
                                command.CommandText = "SELECT COUNT(*) FROM client_users WHERE id = @ID";
                                command.Parameters.AddWithValue("@ID", userId);
                                exists = (int)command.ExecuteScalar() > 0;
                            }

                            if (!exists)
                            {
                                transaction.Rollback();
                                return Ok(new { error = "Error 5821, usuario de cliente no encontrado" });
                            }

                            UpdateResult updateResult = updateClientUser(conn, transaction, user);
                            result = updateResult.result;

                            if (updateResult.failed)
                            {
                                transaction.Rollback();
                                return Ok(result);
                            }

                            setClientRestrictions(userId, restrictedPermissons, restrictedDashboard, conn, transaction);

                            LogToDB(LogType.CLIENTUSER_UPDATED, $"Usuario de cliente {FindUserClientUsernameByClientId(userId, conn, transaction)} actualizado", FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);
                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                        }
                    }
                }
            }

            return Ok(result);
        }


        //---JEAN 20250225 FIN--

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------

        #region "CLASES"
        public struct ClientUser
        {
            public string id { get; set; }
            public string username { get; set; }
            public string email { get; set; }
            public string accessLevel { get; set; }
            public int accessOrdering { get; set; }
            public string pwd { get; set; }
            public bool? activo { get; set; }
            public string photo { get; set; }
            public bool? visible { get; set; }
            public string clientToken { get; set; }
            public DateTime? lastLogin { get; set; }
        }

        public struct ClientUserAccess
        {
            public string id { get; set; }
            public string username { get; set; }
            public string email { get; set; }
            public bool exists { get; set; }
            public bool activo { get; set; }
            public string accessLevel { get; set; }
            public int accessOrdering { get; set; }
            public bool isSuper { get; set; }
            public List<string> permissions { get; set; }
            public List<Empresa> empresas { get; set; }
        }

        public struct Empresa
        {
            public string id { get; set; }
            public string nombre { get; set; }
            public string cif { get; set; }
            public string companyGroupId { get; set; }
            public string icon { get; set; }
            public List<Centro> centros { get; set; }
        }

        public struct Centro
        {
            public string id { get; set; }
            public string alias { get; set; }
            public int nTrabajadores { get; set; }
            public string ccc { get; set; }
            public string domicilio { get; set; }
            public string cp { get; set; }
            public bool requiereChecks { get; set; }
        }

        public struct ClientDashboard
        {
            public bool empty { get; set; }

            public int pendingCommsClientClient { get; set; }

            public int pendingIncidencesNotAttend { get; set; }

            public int pendingCandidateIncidences { get; set; }

            public int candidatesWithoutHorario { get; set; }
            public int candidatesWithoutHorarioCentros { get; set; }
            public string firstCentrosWithCandidatesWithoutHorario { get; set; }

            public int pendingChecks { get; set; }
            public string firstPendingChecksCentroId { get; set; }

            public int pendingUploadingExtraHours { get; set; }

            public int pendingValidatingExtraHours { get; set; }


            public int pendingCondicionesEconomicas { get; set; }
            public string firstPendingCondicionesEconomicasCompanyId { get; set; }
            public string firstPendingCondicionesEconomicasCentroId { get; set; }

            public int pendingEvalDocs { get; set; }
            public string firstPendingEvalDocCompanyId { get; set; }
            public string firstPendingEvalDocCentroId { get; set; }

            public int pendingSignContratos { get; set; }
            public string firstPendingSignContratoCompanyId { get; set; }

            public int pendingCPDSign { get; set; }
            public string firstPendingCPDSignCompanyId { get; set; }
        }
        #endregion

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static ClientUserAccess getClientUserAccess(string clientToken, bool withIcon = false, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            ClientUserAccess access = new()
            {
                id = null,
                exists = false,
                activo = false,
                isSuper = false,
                accessLevel = null,
                accessOrdering = 100,
                username = null,
                permissions = new(),
                empresas = new()
            };
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                //Obtener el tipo de usuario
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT U.id, U.accessLevel, U.username, U.email, U.activo, AL.ordering " +
                        "FROM client_users U " +
                        "INNER JOIN client_access_levels AL ON(AL.id = U.accessLevel) " +
                        "WHERE token = @TOKEN";
                    command.Parameters.AddWithValue("@TOKEN", clientToken);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            access.id = reader.GetString(reader.GetOrdinal("id"));
                            access.username = reader.GetString(reader.GetOrdinal("username"));
                            access.email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email"));
                            access.accessLevel = reader.GetString(reader.GetOrdinal("accessLevel"));
                            access.accessOrdering = reader.GetInt32(reader.GetOrdinal("ordering"));
                            access.isSuper = reader.GetInt32(reader.GetOrdinal("ordering")) <= 3;
                            access.activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1;
                            access.exists = true;
                        }
                    }
                }

                //No se ha encontrado
                if (!access.exists) return access;

                //Obtener los permisos de su nivel de acceso
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT CAP.permission " +
                        "FROM client_access_permissions CAP " +
                        "WHERE CAP.accessLevel = @ACCESS AND " +
                        "NOT EXISTS (SELECT * FROM client_user_permission_restrictions CUPC WHERE CUPC.userId = @USER AND CUPC.permission = CAP.permission)";
                    command.Parameters.AddWithValue("@ACCESS", access.accessLevel);
                    command.Parameters.AddWithValue("@USER", access.id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            access.permissions.Add(reader.GetString(reader.GetOrdinal("permission")));
                        }
                    }
                }

                //Obtener las empresas que tiene asociadas
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT E.id as eId, E.nombre as eNombre, E.cif as eCif, E.grupoId as eGrupoId, " +
                        "C.id as cId, C.alias as cAlias, nTrabajadores = (SELECT COUNT(*) FROM candidatos WHERE centroId = C.id), C.ccc as cCcc, C.domicilio as cDomicilio, C.cp as cCp, " +
                        "requiereChecksCASE = CASE WHEN EXISTS(SELECT * FROM horarios H WHERE H.centroId = C.id) THEN 1 ELSE 0 END " +
                        "FROM empresas E INNER JOIN centros C ON(E.id = C.companyId) INNER JOIN client_user_centros CUC ON(C.id = CUC.centroId) " +
                        "WHERE CUC.clientUserId = @ID";
                    command.Parameters.AddWithValue("@ID", access.id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        Dictionary<string, Empresa> empresas = new();

                        while (reader.Read())
                        {
                            string eId = reader.GetString(reader.GetOrdinal("eId"));
                            string eNombre = reader.GetString(reader.GetOrdinal("eNombre"));
                            string eCif = reader.GetString(reader.GetOrdinal("eCif"));
                            string eGrupoId = reader.IsDBNull(reader.GetOrdinal("eGrupoId")) ? null : reader.GetString(reader.GetOrdinal("eGrupoId"));
                            string eIcon = withIcon ? ReadFile(new[] { "companies", eId, "icon" }) : null;
                            string cId = reader.GetString(reader.GetOrdinal("cId"));
                            string cAlias = reader.GetString(reader.GetOrdinal("cAlias"));
                            string cCcc = reader.IsDBNull(reader.GetOrdinal("cCcc")) ? null : reader.GetString(reader.GetOrdinal("cCcc"));
                            string cDomicilio = reader.IsDBNull(reader.GetOrdinal("cDomicilio")) ? null : reader.GetString(reader.GetOrdinal("cDomicilio"));
                            string cCp = reader.IsDBNull(reader.GetOrdinal("cCp")) ? null : reader.GetString(reader.GetOrdinal("cCp"));
                            int nTrabajadores = reader.GetInt32(reader.GetOrdinal("nTrabajadores"));
                            bool requiereChecks = reader.GetInt32(reader.GetOrdinal("requiereChecksCASE")) == 1;
                            Empresa empresa;
                            if (empresas.ContainsKey(eId)) empresa = empresas[eId];
                            else
                                empresa = empresas[eId] = new Empresa
                                { id = eId, nombre = eNombre, cif = eCif, companyGroupId = eGrupoId, icon = eIcon, centros = new() };
                            empresa.centros.Add(new Centro { id = cId, alias = cAlias, ccc = cCcc, domicilio = cDomicilio, cp = cCp, nTrabajadores = nTrabajadores, requiereChecks = requiereChecks });
                        }

                        access.empresas = empresas.Values.ToList();
                    }
                }

                if (lastConn == null) conn.Close();
            }
            catch (Exception)
            {

            }
            return access;
        }

        public static bool tryGetClientUserDashboardTypes(string userId, out HashSet<string> dashboard, SqlConnection conn = null, SqlTransaction transaction = null)
        {
            if (!tryGetClientUserRestrictions(userId, out _, out _, out HashSet<string> allDashboard, out HashSet<string> restrictedDashboard, conn, transaction))
            {
                dashboard = default;
                return false;
            }

            if (restrictedDashboard.Count == 0)
            {
                dashboard = allDashboard;
            }
            else
            {
                dashboard = allDashboard.Where(t => !restrictedDashboard.Contains(t)).ToHashSet();
            }
            return true;
        }
        public static bool tryGetClientUserRestrictions(string userId, out HashSet<string> permissions, out HashSet<string> restrictedPermissons, out HashSet<string> dashboard, out HashSet<string> restrictedDashboard, SqlConnection conn = null, SqlTransaction transaction = null)
        {
            //Obtener el nivel de acceso del usuario
            string accessLevel = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT accessLevel FROM client_users WHERE id = @USER";
                command.Parameters.AddWithValue("@USER", userId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        accessLevel = reader.GetString(reader.GetOrdinal("accessLevel"));
                    }
                }
            }

            if (accessLevel == null)
            {
                permissions = default;
                restrictedPermissons = default;
                dashboard = default;
                restrictedDashboard = default;
                return false;
            }

            permissions = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT permission FROM client_access_permissions WHERE accessLevel = @LEVEL";
                command.Parameters.AddWithValue("@LEVEL", accessLevel);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        permissions.Add(reader.GetString(reader.GetOrdinal("permission")));
                    }
                }
            }

            restrictedPermissons = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT permission FROM client_user_permission_restrictions WHERE userId = @USER";
                command.Parameters.AddWithValue("@USER", userId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        restrictedPermissons.Add(reader.GetString(reader.GetOrdinal("permission")));
                    }
                }
            }

            dashboard = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT type FROM client_access_dashboard WHERE accessLevel = @LEVEL";
                command.Parameters.AddWithValue("@LEVEL", accessLevel);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dashboard.Add(reader.GetString(reader.GetOrdinal("type")));
                    }
                }
            }

            restrictedDashboard = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }

                command.CommandText = "SELECT type FROM client_user_dashboard_restrictions WHERE userId = @USER";
                command.Parameters.AddWithValue("@USER", userId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        restrictedDashboard.Add(reader.GetString(reader.GetOrdinal("type")));
                    }
                }
            }

            return true;
        }

        public static ClientDashboard getClientDashboard(string clientToken, ClientUserAccess access, SqlConnection conn)
        {
            ClientDashboard dashboard = new ClientDashboard();
            string userId = access.id;
            if (!tryGetClientUserDashboardTypes(userId, out HashSet<string> types, conn))
            {
                dashboard.empty = true;
                return dashboard;
            }
            // Comunicaciones cliente - cliente
            if (types.Contains("ComunicacionesClienteCliente"))
            {
                dashboard.pendingCommsClientClient = CommunicationsClientClientController.countUnreadedTopics(userId, conn);
            }
            // Incidencias de falta de asistencia. Las que están pendientes de respuesta por parte del cliente
            if (types.Contains("IncidenciaFaltaAsistencia"))
            {
                dashboard.pendingIncidencesNotAttend += IncidenceNotAttendController.countIncidences(null, null, null, null, null,
                    null, null, "pendiente", false, null, null, null, null, null, clientToken);
                dashboard.pendingIncidencesNotAttend += IncidenceNotAttendController.countIncidences(null, null, null, null, null,
                    null, null, "pendiente-cliente", false, null, null, null, null, null, clientToken);
            }
            // Incidencias de trabajador. Las que tengan algo sin ver por parte del cliente
            if (types.Contains("IncidenciaTrabajador"))
            {
                dashboard.pendingCandidateIncidences += IncidenceGenericController.countIncidences(conn, null, null, null, null, null, null, false, null, null, null, null, true, null, null, null, clientToken);
            }
            // Candidatos sin horario para la semana que viene
            if (types.Contains("CandidatosSinHorario"))
            {
                List<HorariosController.CandidatesWithoutHorarios> withoutHorario = HorariosController.getCandidatesWithoutHorario(userId, null, DateTime.Now.AddDays(7).GetMonday(), conn, null);
                if (withoutHorario.Count != 0)
                {
                    dashboard.candidatesWithoutHorario = withoutHorario.Select(c => c.candidates.Count).Sum();
                    dashboard.candidatesWithoutHorarioCentros = withoutHorario.Count;
                    dashboard.firstCentrosWithCandidatesWithoutHorario = withoutHorario.Select(c => c.centroId).FirstOrDefault();
                }
            }
            // Agregar los centros sin checkeo completo
            if (types.Contains("CheckeoFaltante"))
            {
                List<CandidateChecksController.WarningMaster> warnings = CandidateChecksController.getCheckWarnings(access.id, DateTime.Now.AddDays(-1), conn);
                if (warnings.Count > 0)
                {
                    dashboard.pendingChecks = warnings.Count;
                    dashboard.firstPendingChecksCentroId = warnings[0].centroId;
                }
            }
            // Agregar los centros 
            if (types.Contains("ReporteExtrasFaltante") || types.Contains("ReporteExtrasSinValidar"))
            {
                DateTime now = DateTime.Now;
                //now = now.AddDays(-20);
                if (now.Day <= 5)
                {
                    DateTime lastDayLastMonth = new DateTime(now.Year, now.Month, 1).AddDays(-1);
                    List<IncidenceExtraHoursController.ExtraHourTotalReport> totales =
                        IncidenceExtraHoursController.listTotals(conn, lastDayLastMonth.Year,
                            lastDayLastMonth.Month, clientToken);

                    if (types.Contains("ReporteExtrasFaltante"))
                        dashboard.pendingUploadingExtraHours = totales.FindAll(r => r.state.Equals("sin-subir")).Count;

                    if (types.Contains("ReporteExtrasSinValidar"))
                        dashboard.pendingValidatingExtraHours = totales.FindAll(r => r.state.Equals("pendiente-validar")).Count;
                }
            }
            // Agregar los documentos de evaluacion pendientes de aportar
            if (types.Contains("DocumentoEvaluacion"))
            {
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT E.id as companyId, CE.id as centroId, T.id workId " +
                                            "FROM client_users CU INNER JOIN client_user_centros CUC ON(CU.id = CUC.clientUserId) " +
                                            "INNER JOIN centros CE ON(CUC.centroId = CE.id) " +
                                            "INNER JOIN empresas E ON(CE.companyId = E.id) " +
                                            "INNER JOIN trabajos T ON(CE.id = T.centroId) " +
                                            "WHERE CU.token = @TOKEN";
                    command.Parameters.AddWithValue("@TOKEN", clientToken);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string companyId = reader.GetString(reader.GetOrdinal("companyId"));
                            string centroId = reader.GetString(reader.GetOrdinal("centroId"));
                            string workId = reader.GetString(reader.GetOrdinal("workId"));
                            if (!ExistsFile(new[] { "companies", companyId, "centro", centroId, "work", workId, "evaldoc" }))
                            {
                                dashboard.pendingEvalDocs++;
                                if (dashboard.firstPendingEvalDocCompanyId == null)
                                {
                                    dashboard.firstPendingEvalDocCompanyId = companyId;
                                    dashboard.firstPendingEvalDocCentroId = centroId;
                                }
                            }
                        }
                    }
                }
            }
            // Agregar los contratos sin firmar
            if (types.Contains("FirmarContratos"))
            {
                foreach (Empresa empresa in access.empresas)
                {
                    List<CompanyContratoController.CompanyContrato> sinFirmar = CompanyContratoController.listContratos(conn, empresa.id).FindAll(c => c.closed && !c.signed);
                    dashboard.pendingSignContratos += sinFirmar.Count;
                    if (dashboard.firstPendingSignContratoCompanyId == null && sinFirmar.Count > 0)
                        dashboard.firstPendingSignContratoCompanyId = empresa.id;
                }
            }
            // Agregar los CPDs sin firmar
            if (types.Contains("FirmarCPDs"))
            {
                foreach (Empresa company in access.empresas)
                {
                    List<CPD> notSignedCPDs = listCPDs(conn, company.id, false);
                    dashboard.pendingCPDSign += notSignedCPDs.Count;
                    if (dashboard.firstPendingCPDSignCompanyId == null && notSignedCPDs.Count > 0)
                        dashboard.firstPendingCPDSignCompanyId = company.id;
                }
            }


            //---
            dashboard.empty = (dashboard.pendingCommsClientClient + dashboard.pendingIncidencesNotAttend + dashboard.pendingCandidateIncidences + dashboard.candidatesWithoutHorario + dashboard.pendingChecks + dashboard.pendingUploadingExtraHours + dashboard.pendingValidatingExtraHours + dashboard.pendingEvalDocs + dashboard.pendingSignContratos + dashboard.pendingCPDSign) == 0;

            return dashboard;
        }

        public static List<ClientUser> listUsersForClientByIntersection(string clientToken, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<ClientUser> users = new List<ClientUser>();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT U.*, CL.ordering FROM client_users U " +
                    "INNER JOIN client_access_levels CL ON(U.accessLevel = CL.id) " +
                    "WHERE EXISTS( " +
                    "SELECT CUC.centroId FROM client_user_centros CUC INNER JOIN client_users CU ON(CUC.clientUserId = CU.id) WHERE CU.token = @TOKEN " +
                    "INTERSECT " +
                    "SELECT CUC.centroId FROM client_user_centros CUC WHERE CUC.clientUserId = U.id " +
                    ") AND visible = 1 ";

                command.Parameters.AddWithValue("@TOKEN", clientToken);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new ClientUser
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            username = reader.GetString(reader.GetOrdinal("username")),
                            accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                            accessOrdering = reader.GetInt32(reader.GetOrdinal("ordering")),
                            email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                            activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1
                        });
                    }
                }
            }
            return users;
        }

        //---HEINRRICH 20250224 INICIO--
        public static void setClientRestrictions(string userId, List<string> permissionRestrictions, List<string> avisosRestrictions, SqlConnection conn, SqlTransaction transaction = null)
        {
            if (permissionRestrictions != null)
            {
                //Limpiar resticciones de permisos
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "DELETE FROM client_user_permission_restrictions WHERE userId = @ID";
                    command.Parameters.AddWithValue("@ID", userId);
                    command.ExecuteNonQuery();
                }

                //Insertar las nuevas restricciones de permisos
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "INSERT INTO client_user_permission_restrictions (userId, permission) VALUES (@ID, @PERMISSION)";
                    command.Parameters.AddWithValue("@ID", userId);
                    command.Parameters.Add("@PERMISSION", System.Data.SqlDbType.VarChar);
                    foreach (string permission in permissionRestrictions)
                    {
                        command.Parameters["@PERMISSION"].Value = permission;
                        command.ExecuteNonQuery();
                    }
                }
            }

            if (avisosRestrictions != null)
            {
                //Limpiar resticciones de avisos
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "DELETE FROM client_user_dashboard_restrictions WHERE userId = @ID";
                    command.Parameters.AddWithValue("@ID", userId);
                    command.ExecuteNonQuery();
                }

                //Insertar las nuevas restricciones de avisos
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "INSERT INTO client_user_dashboard_restrictions (userId, [type]) VALUES (@ID, @TYPE)";
                    command.Parameters.AddWithValue("@ID", userId);
                    command.Parameters.Add("@TYPE", System.Data.SqlDbType.VarChar);
                    foreach (string aviso in avisosRestrictions)
                    {
                        command.Parameters["@TYPE"].Value = aviso;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
        public static void deleteClientUser(SqlConnection conn, string userId, string username, string securityToken, SqlTransaction transaction = null)
        {
            //Eliminar sus permisos
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM client_user_centros WHERE clientUserId = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                command.ExecuteNonQuery();
            }

            //Eliminar sus codigos de recuperacion
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM recovery_codes WHERE clientUserId = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                command.ExecuteNonQuery();
            }

            //Eliminar sus permisos negados
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM client_user_permission_restrictions WHERE userId = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                command.ExecuteNonQuery();
            }

            //Eliminar sus avisos negados
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM client_user_dashboard_restrictions WHERE userId = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                command.ExecuteNonQuery();
            }

            //Obtener las comunicaciones cliente cliente en las que participa para borrarlos
            HashSet<string> topicIds = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT id FROM comunicaciones_cliente_cliente WHERE @ID IN (userIdFrom, userIdTo)";
                command.Parameters.AddWithValue("@ID", userId);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        topicIds.Add(reader.GetString(reader.GetOrdinal("id")));
            }
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT DISTINCT CCC.id " +
                    "FROM comunicaciones_cliente_cliente CCC " +
                    "INNER JOIN comunicaciones_cliente_cliente_mensajes CCCM ON(CCC.id = CCCM.topicId) " +
                    "WHERE CCCM.author = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        topicIds.Add(reader.GetString(reader.GetOrdinal("id")));
            }
            foreach (string topicId in topicIds)
                CommunicationsClientClientController.deleteTopic(topicId, conn, transaction);

            //Borrar al usuario cliente
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM client_users WHERE id = @ID";
                command.Parameters.AddWithValue("@ID", userId);
                command.ExecuteNonQuery();
            }

            DeleteDir(new[] { "clientuser", userId });

            if (securityToken != null)
                LogToDB(LogType.CLIENTUSER_DELETED, $"Usuario de cliente {username} eliminado", FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);
        }


        public static List<object> listIncidencesByClientUser(string username, SqlConnection conn, SqlTransaction transaction = null)
        {
            List<object> incidences = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "SELECT I.id, I.number, I.creationTime, I.[type], CA.dni, CE.id as centroId, CE.alias as centroAlias, EM.id as companyId, EM.nombre as companyName, GE.id as grupoId, GE.nombre as grupoNombre FROM ( " +
                    "SELECT I.id, I.number, I.creationTime, I.candidateId, I.centroId, [type] = 'notAttend' FROM incidencia_falta_asistencia I " +
                    "WHERE (SELECT TOP 1 author FROM incidencia_falta_asistencia_eventos E WHERE E.incidenceId = I.id ORDER BY E.time ASC) = @USERNAME " +
                    "UNION ALL " +
                    "SELECT I.id, I.number, I.creationTime, candidateId = NULL, I.centroId, [type] = 'extraHours' FROM incidencia_horas_extra I " +
                    "WHERE I.createdBy = @USERNAME " +
                    ") I LEFT OUTER JOIN centros CE ON(I.centroId = CE.id) " +
                    "LEFT OUTER JOIN empresas EM ON (CE.companyId = EM.id) " +
                    "LEFT OUTER JOIN grupos_empresariales GE ON(EM.grupoId = GE.id)" +
                    "LEFT OUTER JOIN candidatos CA ON(I.candidateId = CA.id) " +
                    "ORDER BY I.creationTime DESC";

                command.Parameters.AddWithValue("@USERNAME", username);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        incidences.Add(new
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            number = reader.GetInt32(reader.GetOrdinal("number")),
                            creationTime = reader.GetDateTime(reader.GetOrdinal("creationTime")),
                            type = reader.GetString(reader.GetOrdinal("type")),
                            dni = reader.IsDBNull(reader.GetOrdinal("dni")) ? null : reader.GetString(reader.GetOrdinal("dni")),
                            centroId = reader.IsDBNull(reader.GetOrdinal("centroId")) ? null : reader.GetString(reader.GetOrdinal("centroId")),
                            centroAlias = reader.IsDBNull(reader.GetOrdinal("centroAlias")) ? null : reader.GetString(reader.GetOrdinal("centroAlias")),
                            companyId = reader.IsDBNull(reader.GetOrdinal("companyId")) ? null : reader.GetString(reader.GetOrdinal("companyId")),
                            companyName = reader.IsDBNull(reader.GetOrdinal("companyName")) ? null : reader.GetString(reader.GetOrdinal("companyName")),
                            grupoId = reader.IsDBNull(reader.GetOrdinal("grupoId")) ? null : reader.GetString(reader.GetOrdinal("grupoId")),
                            grupoNombre = reader.IsDBNull(reader.GetOrdinal("grupoNombre")) ? null : reader.GetString(reader.GetOrdinal("grupoNombre"))
                        });
                    }
                }
            }

            return incidences;
        }

        public static object grantCentro(string userId, string centroId)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    //Eliminar la asociacion del centro, por si acaso existe ya
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "DELETE FROM client_user_centros WHERE clientUserId = @USER AND centroId = @CENTRO";

                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@CENTRO", centroId);
                        command.ExecuteNonQuery();
                    }
                    //Insertar la nueva asociacion
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "INSERT INTO client_user_centros (clientUserId, centroId) VALUES (@USER, @CENTRO)";

                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@CENTRO", centroId);
                        command.ExecuteNonQuery();
                    }
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido asignar el centro" };
                }
            }

            return result; ;
        }

        public static object grantCompany(string userId, string companyId)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    //Eliminar la asociacion de la empresa, por si acaso
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "DELETE CUC FROM client_user_centros CUC " +
                            "INNER JOIN centros C ON(CUC.centroId = C.id) " +
                            "INNER JOIN empresas E ON(C.companyId = E.id) " +
                            "WHERE CUC.clientUserId = @USER AND E.id = @COMPANY";

                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@COMPANY", companyId);
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "INSERT INTO client_user_centros (clientUserId, centroId) " +
                            "SELECT clientUserId = @USER, id as centroId " +
                            "FROM centros " +
                            "WHERE companyId = @COMPANY";

                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@COMPANY", companyId);
                        command.ExecuteNonQuery();
                    }
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido asignar la empresa" };
                }
            }

            return result; ;
        }

        public static List<ClientUser> listByCentro(string centroId, SqlConnection conn)
        {
            List<ClientUser> users = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT U.* " +
                    "FROM client_users U INNER JOIN client_user_centros CUC ON(U.id = CUC.clientUserId) " +
                    "WHERE CUC.centroId = @CENTRO AND visible = 1";

                command.Parameters.AddWithValue("@CENTRO", centroId);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new ClientUser
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            username = reader.GetString(reader.GetOrdinal("username")),
                            email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                            accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                            activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1
                        });
                    }
                }
            }

            return users;
        }

        public static object revokeCentro(string userId, string centroId)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    //Eliminar la asociacion del centro
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "DELETE FROM client_user_centros WHERE clientUserId = @USER AND centroId = @CENTRO";

                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@CENTRO", centroId);
                        command.ExecuteNonQuery();
                    }
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido desasignar el centro" };
                }
            }

            return result;
        }

        public static object revokeCompany(string userId, string companyId)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    //Eliminar la asociacion de la empresa
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "DELETE CUC FROM client_user_centros CUC " +
                            "INNER JOIN centros C ON(CUC.centroId = C.id) " +
                            "INNER JOIN empresas E ON(C.companyId = E.id) " +
                            "WHERE CUC.clientUserId = @USER AND E.id = @COMPANY";

                        command.Parameters.AddWithValue("@USER", userId);
                        command.Parameters.AddWithValue("@COMPANY", companyId);
                        command.ExecuteNonQuery();
                    }
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5874, no se ha podido desasignar la empresa" };
                }
            }

            return result;
        }

        private static UpdateResult updateClientUser(SqlConnection conn, SqlTransaction transaction, ClientUser data)
        {
            UpdateResult result = new()
            {
                failed = false,
                result = new { error = false }
            };

            try
            {
                //Actualizar email
                if (data.email != null)
                {
                    if (CheckEMAILunique(data.email, data.id, conn, transaction) == null)
                    {
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE client_users SET email = @VALUE WHERE id = @ID";
                            command.Parameters.AddWithValue("@ID", data.id);
                            command.Parameters.AddWithValue("@VALUE", data.email == "1234567" ? DBNull.Value : data.email);
                            command.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        result.failed = true;
                        result.result = new { error = $"Error 4842, el email {data.email} ya está en uso" };
                        return result;
                    }
                }

                //Actualizar username
                if (data.username != null)
                {
                    string oldUsername = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "SELECT username FROM client_users WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", data.id);
                        oldUsername = (string)command.ExecuteScalar();
                    }

                    using (SqlCommand command = new SqlCommand("RenameClientUser", conn))
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@OLDNAME", oldUsername);
                        command.Parameters.AddWithValue("@NEWNAME", data.username);
                        command.ExecuteNonQuery();
                    }
                }

                //Actualizar accessLevel
                if (data.accessLevel != null)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE client_users SET accessLevel = @VALUE WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", data.id);
                        command.Parameters.AddWithValue("@VALUE", data.accessLevel);
                        command.ExecuteNonQuery();
                    }

                    //Borrar sus restricciones de permisos y avisos
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM client_user_permission_restrictions WHERE userId = @ID";
                        command.Parameters.AddWithValue("@ID", data.id);
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM client_user_dashboard_restrictions WHERE userId = @ID";
                        command.Parameters.AddWithValue("@ID", data.id);
                        command.ExecuteNonQuery();
                    }
                }

                //Actualizar pwd
                if (data.pwd != null)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE client_users SET pwd = @VALUE WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", data.id);
                        command.Parameters.AddWithValue("@VALUE", EncryptString(data.pwd));
                        command.ExecuteNonQuery();
                    }
                }

                //Actualizar activo
                if (data.activo != null)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE client_users SET activo = @VALUE WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", data.id);
                        command.Parameters.AddWithValue("@VALUE", data.activo.Value ? 1 : 0);
                        command.ExecuteNonQuery();
                    }
                }

                //Actualizar visible
                if (data.visible != null)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE client_users SET visible = @VALUE WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", data.id);
                        command.Parameters.AddWithValue("@VALUE", data.visible.Value ? 1 : 0);
                        command.ExecuteNonQuery();
                    }
                }

                //Actualizar foto
                if (data.photo != null)
                {
                    DeleteFile(new[] { "clientuser", data.id, "photo" });
                    if (data.photo.Length != 7)
                    {
                        data.photo = LimitSquareImage(data.photo, true);
                        SaveFile(new[] { "clientuser", data.id, "photo" }, data.photo);
                    }
                }

            }
            catch (Exception)
            {
                result.failed = true;
                result.result = new { error = "Error 5804, no se han podido actualizar los datos" };
            }

            return result;
        }

        public static void assocCentros(string userId, List<string> centroIds, SqlConnection conn, SqlTransaction transaction = null)
        {
            //Eliminar centro pasados
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "DELETE FROM client_user_centros WHERE clientUserId = @USER";

                command.Parameters.AddWithValue("@USER", userId);
                command.ExecuteNonQuery();
            }

            //Agrecar los nuevos centros
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText =
                    "INSERT INTO client_user_centros (centroId, clientUserId) VALUES(@CENTRO, @USER)";

                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.Add("@CENTRO", System.Data.SqlDbType.VarChar);
                foreach (string centroId in centroIds)
                {
                    command.Parameters["@CENTRO"].Value = centroId;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string createAdminClientUserFromCompanyId(string companyId, string securityToken, SqlConnection conn, SqlTransaction transaction = null)
        {
            try
            {
                ExtendedCompanyData company = CompanyController.getCompany(conn, transaction, companyId);

                //Comprobar que la empresa tenga todos los datos
                if (String.IsNullOrEmpty(company.firmanteNombre) ||
                    String.IsNullOrEmpty(company.firmanteApellido1) ||
                    String.IsNullOrEmpty(company.firmanteCargo) ||
                    String.IsNullOrEmpty(company.firmanteEmail) ||
                    String.IsNullOrEmpty(company.firmanteDni) ||
                    String.IsNullOrEmpty(company.firmanteTelefono))
                {
                    throw new Exception("Error 4870, la empresa no tiene todos los datos sobre el usuario administrador");
                }

                string username = $"{company.firmanteNombre.Replace(" ", "").ToLower()}.{company.firmanteApellido1.Replace(" ", "").ToLower()}";

                //Comprobar que no existe ya
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT COUNT(*) FROM client_users WHERE email = @EMAIL";
                    command.Parameters.AddWithValue("@EMAIL", company.firmanteEmail);
                    if ((int)command.ExecuteScalar() > 0)
                    {
                        throw new Exception("Error 4871, ya existe un usuario administrador para esta empresa");
                    }
                }

                //Comprobar que la empresa tiene al menos un centro
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT COUNT(*) FROM centros WHERE companyId = @ID";
                    command.Parameters.AddWithValue("@ID", companyId);
                    if ((int)command.ExecuteScalar() == 0)
                    {
                        throw new Exception("Error 4872, la empresa no tiene centros de trabajo");
                    }
                }

                //Comprobar que sus datos son correctos
                string loginUsedBy = CheckLOGINunique(username, null, conn, transaction);
                if (loginUsedBy != null)
                {
                    throw new Exception($"Error 4848, el nombre de usuario {username} ya está en uso por {loginUsedBy}.");
                }

                string emailUsedBy = CheckEMAILunique(company.firmanteEmail, null, conn, transaction);
                if (emailUsedBy != null)
                {
                    throw new Exception($"Error 4848, el email {company.firmanteEmail} ya está en uso por {emailUsedBy}");
                }

                //Obtener dni del creador
                string creador = null;
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT DocID FROM users WHERE securityToken = @TOKEN";
                    command.Parameters.AddWithValue("@TOKEN", securityToken);
                    creador = (string)command.ExecuteScalar();
                }

                //Obtener el mayor nivel de acceso posible
                string accessLevel = null;
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "SELECT TOP 1 id FROM client_access_levels ORDER BY ordering ASC";
                    accessLevel = (string)command.ExecuteScalar() ?? "Administrador";
                }

                //Insertar el nuevo usuario CL
                string id = ComputeStringHash(company.firmanteNombre + DateTime.Now);
                string token = ComputeStringHash(company.firmanteNombre + DateTime.Now.Millisecond + "a");
                string pwd = CreatePassword();
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText =
                        "INSERT INTO client_users (id, token, pwd, email, username, accessLevel, creador) VALUES (@ID, @TOKEN, @PWD, @EMAIL, @USERNAME, @ACCESS, @CREADOR)";

                    command.Parameters.AddWithValue("@ID", id);
                    command.Parameters.AddWithValue("@TOKEN", token);
                    command.Parameters.AddWithValue("@PWD", EncryptString(pwd));
                    command.Parameters.AddWithValue("@EMAIL", company.firmanteEmail);
                    command.Parameters.AddWithValue("@USERNAME", username);
                    command.Parameters.AddWithValue("@ACCESS", accessLevel);
                    command.Parameters.AddWithValue("@CREADOR", creador);

                    command.ExecuteNonQuery();
                }

                //Asignarle los permisos
                addAdminClientPermissions(id, companyId, conn, transaction);

                //Guardar los y enviarle un correo con su contraseña
                LogToDB(LogType.CLIENTUSER_CREATED, $"Usuario de cliente administrador {username} creado", FindUsernameBySecurityToken(securityToken, conn, transaction), conn, transaction);
                SendEmail(new Email()
                {
                    template = "clientUserRegister",
                    inserts = new Dictionary<string, string>() { { "url", AccessController.getAutoLoginUrl("cl", id, token, conn, transaction) }, { "pwd", pwd }, { "username", username } },
                    toEmail = company.firmanteEmail,
                    toName = company.firmanteNombre,
                    subject = "Bienvenid@ a THINKANDJOB",
                    priority = EmailPriority.IMMEDIATE
                });

                return id;
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("Error "))
                    throw new Exception(e.Message);
                else
                    throw new Exception("Error 5870, no se ha podido crear al usuario administrador");
            }
        }
        public static void addAdminClientPermissions(string userId, string companyId, SqlConnection conn, SqlTransaction transaction = null)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "INSERT INTO client_user_centros (centroId, clientUserId) " +
                    "SELECT CE.id, userId = @USER FROM centros CE " +
                    "WHERE CE.companyId = @COMPANY AND " +
                    "NOT EXISTS(SELECT * FROM client_user_centros WHERE centroId = CE.id AND clientUserId = @USER) ";
                command.Parameters.AddWithValue("@USER", userId);
                command.Parameters.AddWithValue("@COMPANY", companyId);
                command.ExecuteNonQuery();
            }
        }

        public static List<ClientUser> listByAccessLevel(string accessLevel, SqlConnection conn)
        {
            List<ClientUser> users = new();
            using (SqlCommand command = conn.CreateCommand())
            {
                command.CommandText =
                    "SELECT U.* " +
                    "FROM client_users U WHERE U.accessLevel = @LEVEL";

                command.Parameters.AddWithValue("@LEVEL", accessLevel);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new ClientUser
                        {
                            id = reader.GetString(reader.GetOrdinal("id")),
                            username = reader.GetString(reader.GetOrdinal("username")),
                            email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
                            accessLevel = reader.GetString(reader.GetOrdinal("accessLevel")),
                            activo = reader.GetInt32(reader.GetOrdinal("activo")) == 1
                        });
                    }
                }
            }

            return users;
        }

        //---HEINRRICH 20250224 FIN--

        //---JEAN 20250225 INICIO--

        //---JEAN 20250225 FIN--



        //------------------------------------------FUNCIONES FIN---------------------------------------------



    }
}

//------------------------------------------ENDPOINTS INICIO------------------------------------------
//------------------------------------------ENDPOINTS FIN---------------------------------------------
//------------------------------------------CLASES INICIO---------------------------------------------
//------------------------------------------CLASES FIN------------------------------------------------
//------------------------------------------FUNCIONES INI---------------------------------------------
//------------------------------------------FUNCIONES FIN---------------------------------------------