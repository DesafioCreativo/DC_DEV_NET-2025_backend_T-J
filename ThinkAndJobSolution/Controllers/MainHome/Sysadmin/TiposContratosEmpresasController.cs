using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.OfficeTools;
using ThinkAndJobSolution.Controllers.MainHome.Comercial;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Sysadmin
{
    [Route("api/v1/tipos-contratos-empresas")]
    [ApiController]
    [Authorize]
    public class TiposContratosEmpresasController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpPut]
        [Route(template: "update-contract-constants/{category}")]
        public async Task<IActionResult> UpdateContractConstants(string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("TiposContratosEmpresas.UpdateLimits", securityToken).Acceso)
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
                if (json.TryGetProperty("variables", out JsonElement variablesJson))
                {
                    double indemnizacionMin = variablesJson.GetProperty("indemnizacionMin").GetDouble();
                    double indemnizacionMax = variablesJson.GetProperty("indemnizacionMax").GetDouble();
                    double tarifaMin = variablesJson.GetProperty("tarifaMin").GetDouble();
                    double tarifaMax = variablesJson.GetProperty("tarifaMax").GetDouble();
                    string contactUserId = variablesJson.GetProperty("contactUserId").GetString();
                    if (indemnizacionMin > indemnizacionMax) return Ok(new { error = "Error 4551, la indemnización mínima no puede ser superior a la máxima" });
                    if (tarifaMin > tarifaMax) return Ok(new { error = "Error 4551, la tarifa mínima no puede ser superior a la máxima" });
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        try
                        {
                            //La indemnizacion no se actualiza de momento, no se usa
                            SetSysConfig(conn, null, "tarifa-min", tarifaMin.ToString(), category);
                            SetSysConfig(conn, null, "tarifa-max", tarifaMax.ToString(), category);
                            SetSysConfig(conn, null, "contact-user-id", contactUserId, category);
                            result = new { error = false };
                        }
                        catch (Exception)
                        {
                            result = new { error = "Error 5551, no se han podido establecer las variables de sistema" };
                        }
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet]
        [Route(template: "get-contract-constants/{category}")]
        public IActionResult GetContractConstants(string category)
        {
            object result = new { error = "5550, variables de sistema no encontradas" };
            try
            {
                result = new
                {
                    error = false,
                    contractConstants = getContractConstants(category)
                };
            }
            catch (Exception)
            {

            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "list/{category}")]
        public IActionResult ListTemplates( string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("TiposContratosEmpresas.ListTemplates", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    using (SqlCommand command = conn.CreateCommand())
                    {
                        List<TipoContratoEmpresa> tipos = new();
                        command.CommandText = "SELECT * FROM company_tipo_contratos WHERE categoria = @CAT ORDER BY nombre";
                        command.Parameters.AddWithValue("@CAT", category);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                tipos.Add(new TipoContratoEmpresa()
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    nombre = reader.GetString(reader.GetOrdinal("nombre"))
                                });
                            }
                        }

                        result = new { error = false, tipos };
                    }
                }
            }
            catch (Exception)
            {
                result = new { error = "Error 5506, no se ha podido listar las plantillas" };
            }
            return Ok(result);
        }

        [HttpGet]
        [Route(template: "download/{tipoId}")]
        public IActionResult DownloadTemplate(string tipoId)
        {
            return Ok(ReadFile(new[] { "contrato_empresa", tipoId, "template" }));
        }

        [HttpPost]
        [Route(template: "upload-template/{category}")]
        public async Task<IActionResult> UploadTemplate(string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("TiposContratosEmpresas.UploadTemplate", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            JsonElement json = JsonDocument.Parse(data).RootElement;
            if (json.TryGetProperty("template", out JsonElement templateJson) &&
                json.TryGetProperty("name", out JsonElement nameJson))
            {
                string template = templateJson.GetString();
                string name = nameJson.GetString();
                string id = ComputeStringHash(DateTime.Now + name);
                try
                {
                    using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                    {
                        conn.Open();
                        using (SqlCommand command = conn.CreateCommand())
                        {
                            command.CommandText =
                                "INSERT INTO company_tipo_contratos (id, nombre, categoria) VALUES (@ID, @NAME, @CAT)";
                            command.Parameters.AddWithValue("@ID", id);
                            command.Parameters.AddWithValue("@NAME", name);
                            command.Parameters.AddWithValue("@CAT", category);
                            command.ExecuteNonQuery();
                        }
                    }
                    SaveFile(new[] { "contrato_empresa", id, "template" }, template);
                    result = new { error = false };
                }
                catch (Exception)
                {
                    result = new { error = "Error 5505, no se ha podido guardar la plantilla" };
                }
            }
            return Ok(result);
        }


        [HttpDelete]
        [Route(template: "{tipoId}/")]
        public IActionResult DeleteTemplate(string tipoId)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("TiposContratosEmpresas.DeleteTemplate", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "UPDATE company_contratos SET tipoId = NULL WHERE tipoId = @ID";
                        command.Parameters.AddWithValue("@ID", tipoId);
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText =
                            "DELETE FROM company_tipo_contratos WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", tipoId);
                        command.ExecuteNonQuery();
                    }
                }
                DeleteDir(new[] { "contrato_empresa", tipoId });
                result = new { error = false };
            }
            catch (Exception)
            {
                result = new { error = "Error 5505, no se ha podido eliminar la plantilla" };
            }
            return Ok(result);
        }


        [HttpGet]
        [Route(template: "inserts/")]
        public IActionResult GetAvailableInserts(string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("TiposContratosEmpresas.GetAvailableInserts", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            result = new { error = false, inserts = CompanyContratoController.AVAILABLE_INSERTS.Select(i => new { key = i.Key, description = i.Value.description, type = i.Value.type }).ToList() };
            return Ok(result);
        }



        [HttpPost]
        [Route(template: "test-template/{firma}/")]
        public async Task<IActionResult> TestTemplate(bool firma, string category)
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            if (!HasPermission("TiposContratosEmpresas.TestTemplate", securityToken).Acceso)
            {
                return Ok(new{error = "Error 1001, No se disponen de los privilegios suficientes."});
            }
            StreamReader readerJson = new StreamReader(Request.Body, Encoding.UTF8);
            string template = await readerJson.ReadToEndAsync();

            try
            {
                List<Insert> inserts = new()
                {
                    new TextInsert("dia", "12"),
                    new TextInsert("mes", "06"),
                    new TextInsert("mes-letras", "Junio"),
                    new TextInsert("ano", "2022"),
                    new TextInsert("provincia", "Huelva"),
                    new TextInsert("localidad", "Campofrío"),
                    new TextInsert("ett-nombre", DEA_NOMBRE),
                    new TextInsert("ett-cif", DEA_CIF),
                    new TextInsert("ett-admin-nombre", DEA_ADMINISTRADOR_NOMBRE),
                    new TextInsert("ett-admin-dni", DEA_ADMINISTRADOR_DNI),
                    new TextInsert("admin-nombre", "BASILIO TELLEZ RODRIGUEZ"),
                    new TextInsert("admin-dni", "24681702B"),
                    new TextInsert("admin-telefono", "952040742"),
                    new TextInsert("admin-email", "prueba1@fruitshome.es"),
                    new TextInsert("cliente-nombre", "BLANCO LIMON, S.L."),
                    new TextInsert("cliente-cif", "B92880434"),
                    new ImageInsert("cliente-logo", "data:image/png;base64,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"),
                    new ImageInsert("thinkandjob-logo", "data:image/jpeg;base64,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"),
                    new ListInsert("puesto", new List<string>(){ "DEPENDIENTE/AYUDANTE DEPENDIENTE", "AYUDANTE DEPENDIENTE" }),
                    new TextInsert("duracion", "12"),
                    new TextInsert("duracion-letras", "DOCE"),
                    new TextInsert("tarifa", "105"),
                    new TextInsert("tarifa-letras", "CIENTO CINCO"),
                    new ListInsert("clausula", new List<string>(){ "Clausula 1", "Clausula 2"}),
                    new TextInsert("comercial-nombre", "Jorge M. Vázquez"),
                    new TextInsert("comercial-telefono", "673810581"),
                    new TextInsert("comercial-email", "jm.vazquez@thinkandjob.com"),
                    new TextInsert("contacto-nombre", "Yolanda Guadalupe Ferrer"),
                    new TextInsert("contacto-telefono", "612194774"),
                    new TextInsert("contacto-email", "lupita.ferrer@telenovelaesmeralda.com")
                };

                if (firma)
                {
                    inserts.Add(new ImageInsert("jorge-firma", "data:image/png;base64," + JORGE_FIRMA));
                    inserts.Add(new ImageInsert("cliente-firma", "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAiIAAAEzCAYAAAAM1miJAAAAAXNSR0IArs4c6QAAIABJREFUeF7tnQnYRt9Y7u9KGihlKmWIzDPJ7KgTjsoU4ohIA0UyFKfj1FFHJSEhrlDUQYrKGEc4KCcSooyReco8ljSd68d68tj/9/ve/e53D2vtfa/r+q5v2nvttX5rvXvf+1nP86wvkosJmIAJmIAJmIAJLETgixa6ri9rAiZgAiZgAiZgArIQ8SQwARMwARMwARNYjICFyGLofWETMAETMAETMAELEc8BEzABEzABEzCBxQhYiCyG3hc2ARMwARMwAROwEPEcMAETMAETMAETWIyAhchi6H1hEzABEzABEzABCxHPARMwARMwARMwgcUIWIgsht4XNgETMAETMAETsBDxHDABEzABEzABE1iMgIXIYuh9YRMwARMwARMwAQsRzwETMAETMAETMIHFCFiILIbeFzYBEzABEzABE7AQ8RwwARMwARMwARNYjICFyGLofWETMAETMAETMAELEc8BEzABEzABEzCBxQhYiCyG3hc2ARMwARMwAROwEPEcMAETMAETMAETWIyAhchi6H1hEzABEzABEzABCxHPARMwgdYIfI0kvi5QGn758p2/UeL/8XP+nn/+J0kXS52P8/vw+KgkvjjnbeUEfu+WD0k6Rzo2/z+396S27mpLXDuul7+/vXB5VWLRbVf8flq7P1baHNfqw8THmMAgAhYig7D5JBMwgYkJIC4QGt9UvkJsfNuR1+UBS+Eh/ElJZy0PXP520kM3Hty0pStq9jXng5LOuUOIZHHQFQb76tz1/2jbpyVdPLUzC7Yh9eZzgk8IGL7Hzwgf/v/qxPPY6/n8jRCwENnIQLubJlAZAR6QPDwRG4gMvvjbV0u64gltRUTw4IsHYrz1c3g8CPOp+djKur9Ic7IFJoRLCKuwImWrUIxR/tu1e7Y82PO9K1LyuPWszoetmYCFyJpH130zgToIIDIulwRHiI5u61hWeKskBAcPq3hg8T3evOvokVuRRUr+OSxY/I0xP6kwnow1z6AYXwQmf2ceeLw3NMcsRDY02O6qCcxEgOUT3pz5vkt0hPn+heWBk0XHTE30ZWYigCCJORCCJZbXsH5d4ZR2hECJ78wbW1NmGrg5L2MhMidtX8sE1keAh0uIjhAeuZfx8AixgfhwMYEg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"));
                }

                await OfficeTools.prepareCombinedImages(template, inserts);
                result = new { error = false, file = await OfficeTools.applyTemplateProcessor(template, inserts, true) };
            }
            catch (Exception e)
            {
                result = new { error = "Error 5880, " + e.Message };
            }

            return Ok(result);
        }




        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        public struct TipoContratoEmpresa
        {
            public string id { get; set; }
            public string nombre { get; set; }
        }

        public struct ContractConstants
        {
            public double indemnizacionMin { get; set; }
            public double indemnizacionMax { get; set; }
            public double tarifaMin { get; set; }
            public double tarifaMax { get; set; }
            public string contactUserId { get; set; }
        }
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        public static ContractConstants getContractConstants(string category, SqlConnection conn = null, SqlTransaction transaction = null)
        {
            return new ContractConstants()
            {
                indemnizacionMin = Double.Parse(GetSysConfig(conn, transaction, "indemnizacion-min", category) ?? "0"),
                indemnizacionMax = Double.Parse(GetSysConfig(conn, transaction, "indemnizacion-max", category) ?? "0"),
                tarifaMin = Double.Parse(GetSysConfig(conn, transaction, "tarifa-min", category) ?? "0"),
                tarifaMax = Double.Parse(GetSysConfig(conn, transaction, "tarifa-max", category) ?? "0"),
                contactUserId = GetSysConfig(conn, transaction, "contact-user-id", category) ?? null,
            };
        }
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
