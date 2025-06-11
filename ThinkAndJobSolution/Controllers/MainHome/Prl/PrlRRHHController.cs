using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/RRHH")]
    [ApiController]
    [Authorize]
    public class PrlRRHHController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "download-horas-contrato-template/{securityToken}")]
        public IActionResult GenerateTemplateHorasContrato(string securityToken)
        {
            if (!HasPermission("Contratos.GenerateTemplateHorasContrato", securityToken).Acceso)
            {
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            }
            string fileName = "PlantillaContratos.xlsx";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "ExcelTemplates", fileName);
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            HttpContext.Response.ContentType = contentType;
            FileContentResult response = new FileContentResult(System.IO.File.ReadAllBytes(filePath), contentType)
            {
                FileDownloadName = fileName
            };
            return response;
        }
        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
