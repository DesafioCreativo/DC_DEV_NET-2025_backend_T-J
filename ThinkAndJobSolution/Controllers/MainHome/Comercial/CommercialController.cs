using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using static ThinkAndJobSolution.Controllers._Helper.Constants;
using static ThinkAndJobSolution.Controllers.Candidate.CandidateController;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Controllers._Model.Client;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using static ThinkAndJobSolution.Controllers.MainHome.Comercial.CentroController;


namespace ThinkAndJobSolution.Controllers.MainHome.Comercial
{
    [Route("api/v1/COMMERCIAL")]
    [ApiController]
    [Authorize]
    public class CommercialController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        //------------------------------securityToken------------------------------

        [HttpGet]
        [Route(template: "download-workcenters-template/")]
        public IActionResult GenerateTemplateWorkcenters()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Centros.GenerateTemplateWorkcenters", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }

            string fileName = "PlantillaSubidaCentros.xlsx";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ExcelTemplates", fileName);
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            HttpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(filePath), contentType)
            {
                FileDownloadName = fileName
            };

            return response;
        }

        [HttpPost]
        [Route(template: "upload-workcenters-template/")]
        public async Task<IActionResult> BulkUpload()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Centros.BulkUpload", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();

            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("xlsx", out JsonElement xlsxJson))
            {
                List<ExtendedCentroData> centros = new();
                List<string> errorList = new();
                try
                {
                    string xlsxString = xlsxJson.GetString();
                    byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
                    string tmpDir = GetTemporaryDirectory();
                    string tmpFile = Path.Combine(tmpDir, "template.xlsx");
                    System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);

                    XSSFWorkbook workbook = new XSSFWorkbook(tmpFile);
                    ISheet sheet = workbook.GetSheetAt(0);
                    DataFormatter formatter = new DataFormatter();

                    if (!CheckExcelTemplateId(workbook, "bulk-centros"))
                        return Ok(new { error = "Error 4010, plantilla incorrecta." });

                    //Obtener los centros
                    int r = 1;
                    while (true)
                    {
                        IRow row = sheet.GetRow(r++);
                        if (row == null || row.GetCell(0) == null) break;

                        ExtendedCentroData centro = new ExtendedCentroData()
                        {
                            companyCif = formatter.FormatCellValue(row.GetCell(0))?.Trim(),
                            alias = formatter.FormatCellValue(row.GetCell(1))?.Trim(),
                            regimen = formatter.FormatCellValue(row.GetCell(2))?.Trim(),
                            domicilio = formatter.FormatCellValue(row.GetCell(3))?.Trim(),
                            cp = formatter.FormatCellValue(row.GetCell(4))?.Trim(),
                            poblacion = formatter.FormatCellValue(row.GetCell(5))?.Trim(),
                            provincia = formatter.FormatCellValue(row.GetCell(6))?.Trim(),
                            contactoNombre = formatter.FormatCellValue(row.GetCell(7))?.Trim(),
                            contactoApellido1 = formatter.FormatCellValue(row.GetCell(8))?.Trim(),
                            contactoApellido2 = formatter.FormatCellValue(row.GetCell(9))?.Trim(),
                            telefono = formatter.FormatCellValue(row.GetCell(10))?.Trim(),
                            email = formatter.FormatCellValue(row.GetCell(11))?.Trim(),
                            fechaAlta = row.GetCell(12)?.DateCellValue,
                            servicioPrevencionNombre = formatter.FormatCellValue(row.GetCell(13))?.Trim(),
                            convenio = formatter.FormatCellValue(row.GetCell(14))?.Trim(),
                            ccc = formatter.FormatCellValue(row.GetCell(15))?.Trim(),
                            cnae = formatter.FormatCellValue(row.GetCell(16))?.Trim(),
                            sociedad = formatter.FormatCellValue(row.GetCell(17))?.Trim()

                        };

                        if (string.IsNullOrWhiteSpace(centro.alias)) centro.alias = null;
                        if (centro.alias == null)
                        {
                            errorList.Add($"Error en la fila {r}: El alias es obligatorio.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(centro.regimen)) centro.regimen = null;
                        if (string.IsNullOrWhiteSpace(centro.domicilio)) centro.domicilio = null;
                        if (string.IsNullOrWhiteSpace(centro.cp)) centro.cp = null;
                        if (string.IsNullOrWhiteSpace(centro.provincia)) centro.provincia = null;
                        if (string.IsNullOrWhiteSpace(centro.poblacion)) centro.poblacion = null;
                        if (centro.cp != null)
                        {
                            if (searchCP(centro.cp, out Provincia provincia, out Localidad localidad))
                            {
                                if (centro.provincia == null) centro.provincia = provincia.nombre;
                                if (centro.poblacion == null) centro.poblacion = localidad.nombre;
                            }
                            else
                            {
                                errorList.Add($"Aviso en la fila {r}: Código postal '{centro.cp}' no encontrado.");
                            }
                        }
                        if (string.IsNullOrWhiteSpace(centro.contactoNombre)) centro.contactoNombre = null;
                        if (string.IsNullOrWhiteSpace(centro.contactoApellido1)) centro.contactoApellido1 = null;
                        if (string.IsNullOrWhiteSpace(centro.contactoApellido2)) centro.contactoApellido2 = null;
                        if (string.IsNullOrWhiteSpace(centro.telefono)) centro.telefono = null;
                        if (centro.telefono != null && !ValidatePhone(centro.telefono))
                        {
                            errorList.Add($"Error en la fila {r}: El teléfono '{centro.telefono}' no es válido.");
                            centro.telefono = null;
                        }
                        if (string.IsNullOrWhiteSpace(centro.email)) centro.email = null;
                        if (centro.email != null && !ValidateEmail(centro.email))
                        {
                            errorList.Add($"Error en la fila {r}: El email '{centro.email}' no es válido.");
                            centro.email = null;
                        }
                        if (string.IsNullOrWhiteSpace(centro.servicioPrevencionNombre)) centro.servicioPrevencionNombre = null;
                        if (string.IsNullOrWhiteSpace(centro.convenio)) centro.convenio = null;
                        if (string.IsNullOrWhiteSpace(centro.ccc)) centro.ccc = null;
                        if (string.IsNullOrWhiteSpace(centro.cnae)) centro.cnae = null;
                        if (string.IsNullOrWhiteSpace(centro.sociedad)) centro.sociedad = null;
                        if (centro.sociedad == null)
                        {
                            errorList.Add($"Error en la fila {r}: La sociedad es obligatoria.");
                            continue;
                        }
                        if (!string.IsNullOrWhiteSpace(row.GetCell(18)?.StringCellValue))
                            centro.workshiftRequiereFoto = row.GetCell(18)?.StringCellValue?.Trim() == "SÍ";
                        if (!string.IsNullOrWhiteSpace(row.GetCell(19)?.StringCellValue))
                            centro.workshiftRequiereUbicacion = row.GetCell(19)?.StringCellValue?.Trim() == "SÍ";

                        centros.Add(centro);
                    }

                    workbook.Close();
                    Directory.Delete(tmpDir, true);
                }
                catch (Exception)
                {
                    return Ok(new { error = "Error 5007, No se ha podido procesar el documento." });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int created = 0, updated = 0;
                            foreach (ExtendedCentroData centro in centros)
                            {
                                //Comprobar que el alias no exista
                                bool exists = false;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "SELECT id FROM centros WHERE alias = @ALIAS";
                                    command.Parameters.AddWithValue("@ALIAS", centro.alias);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            exists = true;
                                            centro.id = reader.GetString(reader.GetOrdinal("id"));
                                        }
                                    }
                                }

                                //Buscar la id de la empresa
                                string companyId = null;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "SELECT id FROM empresas WHERE cif LIKE @CIF";
                                    command.Parameters.AddWithValue("@CIF", centro.companyCif);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                        if (reader.Read())
                                            companyId = reader.GetString(reader.GetOrdinal("id"));
                                }

                                if (companyId == null)
                                {
                                    errorList.Add($"No se ha encontrado la empresa {centro.companyCif}");
                                    continue;
                                }

                                //Buscar la id del servicio de prevencion
                                if (centro.servicioPrevencionNombre != null)
                                {
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText =
                                            "SELECT id FROM servicios_prevencion WHERE nombre = @PREVENCION";
                                        command.Parameters.AddWithValue("@PREVENCION", centro.servicioPrevencionNombre);
                                        using (SqlDataReader reader = command.ExecuteReader())
                                            if (reader.Read())
                                                centro.servicioPrevencion = reader.GetInt32(reader.GetOrdinal("id"));
                                            else
                                                errorList.Add($"Servicio de prevención '{centro.servicioPrevencionNombre}' no encontrado.");
                                    }
                                }

                                //Insertar centro
                                if (!exists)
                                {
                                    centro.id = ComputeStringHash(centro.alias + companyId + DateTime.Now.Millisecond);
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText =
                                            "INSERT INTO centros (id, companyId, sociedad, alias) VALUES (@ID, @COMPANY, @SOCIEDAD, @ALIAS)";

                                        command.Parameters.AddWithValue("@ID", centro.id);
                                        command.Parameters.AddWithValue("@COMPANY", companyId);
                                        command.Parameters.AddWithValue("@SOCIEDAD", (object)centro.sociedad ?? DBNull.Value);
                                        command.Parameters.AddWithValue("@ALIAS", centro.alias);

                                        command.ExecuteNonQuery();
                                    }

                                    try
                                    {
                                        addPermissionToAdminCLWhenCentroIsCreated(conn, transaction, companyId, centro.id);
                                        addWorksToCenterFromLastClosedContrato(conn, transaction, companyId, centro.id);
                                    }
                                    catch (Exception) { }

                                    created++;
                                }
                                else
                                {
                                    updated++;
                                }

                                //Actualizar centro
                                UpdateResult update = CentroController.updateCentro(conn, transaction, centro);
                                if (update.failed)
                                {
                                    result = update.result;
                                    throw new Exception();
                                }
                            }

                            transaction.Commit();
                            result = new { error = false, created, updated, errorList };
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

        //Bulk

        [HttpGet]
        [Route(template: "download-companies-template/")]
        public IActionResult GenerateTemplateCompanies()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Company.GenerateTemplateCompanies", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }

            string fileName = "PlantillaSubidaEmpresas.xlsx";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ExcelTemplates", fileName);
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            HttpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(filePath), contentType)
            {
                FileDownloadName = fileName
            };

            return response;
        }

        [HttpPost]
        [Route(template: "upload-companies-template/")]
        public async Task<IActionResult> BulkUploadCompany()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición.",
            };


            if (!HasPermission("Company.BulkUpload", securityToken).Acceso)
            {
                return Ok(new
                {
                    error = "Error 1001, No se disponen de los privilegios suficientes."
                });
            }

            using StreamReader readerBody = new StreamReader(Request.Body, Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;

            if (json.TryGetProperty("xlsx", out JsonElement xlsxJson))
            {

                List<ExtendedCompanyData> companies = new();
                List<string> errorList = new();
                try
                {
                    string xlsxString = xlsxJson.GetString();
                    byte[] xlsxBinary = Convert.FromBase64String(xlsxString.Split(",")[1]);
                    string tmpDir = GetTemporaryDirectory();
                    string tmpFile = Path.Combine(tmpDir, "template.xlsx");
                    System.IO.File.WriteAllBytes(tmpFile, xlsxBinary);

                    XSSFWorkbook workbook = new XSSFWorkbook(tmpFile);
                    ISheet sheet = workbook.GetSheetAt(0);
                    DataFormatter formatter = new DataFormatter();

                    if (!CheckExcelTemplateId(workbook, "bulk-empresas"))
                        return Ok(new { error = "Error 4010, plantilla incorrecta." });

                    //Obtener las empresas
                    int r = 1;
                    while (true)
                    {
                        IRow row = sheet.GetRow(r++);
                        if (row == null || row.GetCell(0) == null) break;

                        ExtendedCompanyData company = new ExtendedCompanyData()
                        {
                            cif = formatter.FormatCellValue(row.GetCell(0))?.Trim(),
                            nombre = formatter.FormatCellValue(row.GetCell(1))?.Trim(),
                            cp = formatter.FormatCellValue(row.GetCell(2))?.Trim(),
                            direccion = formatter.FormatCellValue(row.GetCell(3))?.Trim(),
                            nombreRRHH = formatter.FormatCellValue(row.GetCell(4))?.Trim(),
                            apellido1RRHH = formatter.FormatCellValue(row.GetCell(5))?.Trim(),
                            apellido2RRHH = formatter.FormatCellValue(row.GetCell(6))?.Trim(),
                            telefonoRRHH = formatter.FormatCellValue(row.GetCell(7))?.Trim(),
                            emailRRHH = formatter.FormatCellValue(row.GetCell(8))?.Trim(),
                            web = formatter.FormatCellValue(row.GetCell(9))?.Trim(),
                            formaDePago = formatter.FormatCellValue(row.GetCell(10))?.Trim(),
                            diaCobro = null,
                            cuentaBancaria = formatter.FormatCellValue(row.GetCell(12))?.Trim(),
                            cuentaContable = formatter.FormatCellValue(row.GetCell(13))?.Trim(),
                            tva = formatter.FormatCellValue(row.GetCell(14))?.Trim(),
                            vies = null,
                            indemnizacion = formatter.FormatCellValue(row.GetCell(16))?.Trim(),
                            firmanteNombre = formatter.FormatCellValue(row.GetCell(17))?.Trim(),
                            firmanteApellido1 = formatter.FormatCellValue(row.GetCell(18))?.Trim(),
                            firmanteApellido2 = formatter.FormatCellValue(row.GetCell(19))?.Trim(),
                            firmanteDni = formatter.FormatCellValue(row.GetCell(20))?.Trim(),
                            firmanteEmail = formatter.FormatCellValue(row.GetCell(21))?.Trim(),
                            firmanteTelefono = formatter.FormatCellValue(row.GetCell(22))?.Trim(),
                            firmanteCargo = formatter.FormatCellValue(row.GetCell(23))?.Trim()
                        };
                        try
                        {
                            company.diaCobro = Int32.Parse(formatter.FormatCellValue(row.GetCell(11))?.Trim());
                        }
                        catch (Exception) { }

                        if (string.IsNullOrWhiteSpace(company.nombre)) company.nombre = null;
                        if (company.nombre == null)
                        {
                            errorList.Add($"Error en la fila {r}: El nombre es obligatorio.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(company.cp)) company.cp = null;
                        if (string.IsNullOrWhiteSpace(company.direccion)) company.direccion = null;
                        if (string.IsNullOrWhiteSpace(company.nombreRRHH)) company.nombreRRHH = null;
                        if (string.IsNullOrWhiteSpace(company.apellido1RRHH)) company.apellido1RRHH = null;
                        if (string.IsNullOrWhiteSpace(company.apellido2RRHH)) company.apellido2RRHH = null;
                        if (string.IsNullOrWhiteSpace(company.telefonoRRHH)) company.telefonoRRHH = null;
                        if (company.telefonoRRHH != null && !ValidatePhone(company.telefonoRRHH))
                        {
                            errorList.Add($"Error en la fila {r}: El telefono '{company.telefonoRRHH}' no es válido.");
                            company.telefonoRRHH = null;
                        }
                        if (string.IsNullOrWhiteSpace(company.emailRRHH)) company.emailRRHH = null;
                        if (company.emailRRHH != null && !ValidateEmail(company.emailRRHH))
                        {
                            errorList.Add($"Error en la fila {r}: El email '{company.emailRRHH}' no es válido.");
                            company.emailRRHH = null;
                        }
                        if (string.IsNullOrWhiteSpace(company.web)) company.web = null;
                        if (string.IsNullOrWhiteSpace(company.formaDePago)) company.formaDePago = null;
                        if (company.formaDePago != null)
                        {
                            company.formaDePago = company.formaDePago == "TRANSFERENCIA" ? "Transferencia bancaria" : "B2B";
                        }
                        if (string.IsNullOrWhiteSpace(company.tva)) company.tva = null;
                        if (row.GetCell(15)?.StringCellValue?.Trim() != null)
                        {
                            company.vies = row.GetCell(15)?.StringCellValue?.Trim() == "SÍ";
                        }
                        if (string.IsNullOrWhiteSpace(company.indemnizacion)) company.indemnizacion = null;
                        if (company.indemnizacion != null)
                        {
                            company.indemnizacion = company.indemnizacion == "CARGO EXTRA" ? "Cargo extra" : "Pago Mensual";
                        }
                        if (string.IsNullOrWhiteSpace(company.cuentaBancaria)) company.cuentaBancaria = null;
                        if (string.IsNullOrWhiteSpace(company.cuentaContable)) company.cuentaContable = null;
                        if (string.IsNullOrWhiteSpace(company.firmanteNombre)) company.firmanteNombre = null;
                        if (string.IsNullOrWhiteSpace(company.firmanteApellido1)) company.firmanteApellido1 = null;
                        if (string.IsNullOrWhiteSpace(company.firmanteApellido2)) company.firmanteApellido2 = null;
                        if (string.IsNullOrWhiteSpace(company.firmanteDni)) company.firmanteDni = null;
                        if (string.IsNullOrWhiteSpace(company.firmanteCargo)) company.firmanteCargo = null;
                        if (string.IsNullOrWhiteSpace(company.firmanteEmail)) company.firmanteEmail = null;
                        if (company.firmanteEmail != null && !ValidateEmail(company.firmanteEmail))
                        {
                            errorList.Add($"Error en la fila {r}: El email '{company.firmanteEmail}' no es válido.");
                            company.firmanteEmail = null;
                        }
                        if (string.IsNullOrWhiteSpace(company.firmanteTelefono)) company.firmanteTelefono = null;
                        if (company.firmanteTelefono != null && !ValidatePhone(company.firmanteTelefono))
                        {
                            errorList.Add($"Error en la fila {r}: El telefono '{company.firmanteTelefono}' no es válido.");
                            company.firmanteTelefono = null;
                        }

                        companies.Add(company);
                    }

                    workbook.Close();
                    Directory.Delete(tmpDir, true);
                }
                catch (Exception)
                {
                    return Ok(new { error = "Error 5005, No se ha podido procesar el documento." });
                }

                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int created = 0, updated = 0;
                            foreach (ExtendedCompanyData company in companies)
                            {
                                //Comprobar que el CIF no exista
                                bool exists = false;
                                using (SqlCommand command = conn.CreateCommand())
                                {
                                    command.Connection = conn;
                                    command.Transaction = transaction;
                                    command.CommandText = "SELECT id FROM empresas WHERE cif LIKE @CIF";
                                    command.Parameters.AddWithValue("@CIF", company.cif);
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            exists = true;
                                            company.id = reader.GetString(reader.GetOrdinal("id"));
                                        }
                                    }
                                }

                                //Insertar empresa
                                if (!exists)
                                {
                                    company.id = ComputeStringHash(company.cif + DateTime.Now.Millisecond);
                                    using (SqlCommand command = conn.CreateCommand())
                                    {
                                        command.Connection = conn;
                                        command.Transaction = transaction;
                                        command.CommandText =
                                            "INSERT INTO empresas (id, cif, nombre) VALUES (@ID, @CIF, @NOMBRE)";

                                        command.Parameters.AddWithValue("@ID", company.id);
                                        command.Parameters.AddWithValue("@CIF", company.cif);
                                        command.Parameters.AddWithValue("@NOMBRE", company.nombre);

                                        command.ExecuteNonQuery();
                                    }

                                    created++;
                                }
                                else
                                {
                                    updated++;
                                }

                                //Actualizar empresa
                                UpdateResult update = CompanyController.updateCompany(conn, transaction, company);
                                if (update.failed)
                                {
                                    result = update.result;
                                    throw new Exception();
                                }
                            }

                            transaction.Commit();
                            result = new { error = false, created, updated, errorList };
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

        //------------------------------clientToken-------------------------------


        //------------------------------------------ENDPOINTS FIN---------------------------------------------

        //------------------------------------------CLASES INICIO---------------------------------------------
        #region "CLASES"
        #endregion
        //------------------------------------------CLASES FIN------------------------------------------------

        //------------------------------------------FUNCIONES INI---------------------------------------------



        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
