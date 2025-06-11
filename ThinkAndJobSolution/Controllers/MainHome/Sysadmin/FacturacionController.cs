using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Controllers._Model.Facturacion;
using ThinkAndJobSolution.Utils;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.MainHome.Sysadmin
{
    [Route("api/v1/facturacion")]
    [ApiController]
    [Authorize]
    public class FacturacionController : ControllerBase
    {
        //------------------------------------------ENDPOINTS INICIO------------------------------------------
        [HttpGet]
        [Route(template: "get-basescotizacion/")]
        public IActionResult GetBasesCotizacion()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            if (!HasPermission("Facturacion.GetBasesCotizacion", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });            

            try
            {
                return Ok(new BasesCotizacion(GetSysConfig(null, null, "bases-cotizacion"), true));
            }
            catch (Exception)
            {
                return Ok(new BasesCotizacion());
            }
        }


        [HttpPost]
        [Route(template: "set-basescotizacion/")]
        public async Task<IActionResult> SetBasesCotizacion()
        {
            string securityToken = Cl_Security.getSecurityInformation(User, "securityToken");
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };
            if (!HasPermission("Facturacion.SetBasesCotizacion", securityToken).Acceso)
                return Ok(new { error = "Error 1001, No se disponen de los privilegios suficientes." });
            using StreamReader readerBody = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string data = await readerBody.ReadToEndAsync();
            try
            {
                BasesCotizacion basesCotizacion = new BasesCotizacion(data);
                SetSysConfig(null, null, "bases-cotizacion", basesCotizacion.Serialize());
                LogToDB(LogType.BASES_COTIZACION_UPDATED, "Bases de cotización actualizadas", FindUsernameBySecurityToken(securityToken));
                result = new { error = false };
            }
            catch (Exception)
            {
                result = new { error = "Error 5580, no se han podido actualizar las bases de cotización" };
            }

            return Ok(result);
        }
        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------
        //------------------------------------------FUNCIONES FIN---------------------------------------------
    }
}
