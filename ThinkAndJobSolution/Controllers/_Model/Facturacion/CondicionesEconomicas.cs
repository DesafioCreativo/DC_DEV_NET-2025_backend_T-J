using System.Text.Json;
using static ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions.JsonExtensions;

namespace ThinkAndJobSolution.Controllers._Model.Facturacion
{

    public class CondicionesEconomicas
    {
        public string Id { get; set; }
        public string CompanyId { get; set; }
        public string PuestoId { get; set; }
        public string Nombre { get; set; }
        public double SalarioBaseMensual { get; set; }
        public List<PagaExtra> PagasExtra { get; set; }
        public List<PlusFijo> PlusesFijos { get; set; }
        public double PrecioHoraExtraFuerzaMayor { get; set; }
        public double PrecioHoraExtraNoFuerzaMayor { get; set; }
        public double HorasJornadaSemanal { get; set; }

        public CondicionesEconomicas(JsonElement json, bool minified = false)
        {
            if (json.TryGetString("id", out string id)) Id = id;
            if (json.TryGetString("companyId", out string companyId)) CompanyId = companyId;
            if (json.TryGetString("puestoId", out string puestoId)) PuestoId = puestoId;
            if (json.TryGetString("nombre", out string nombre)) Nombre = nombre;
            if (json.TryGetDouble(minified ? "sbm" : "salarioBaseMensual", out double salarioBaseMensual)) SalarioBaseMensual = Math.Round(salarioBaseMensual, 2);
            if (json.TryGetDouble(minified ? "pef" : "precioHoraExtraFuerzaMayor", out double precioHoraExtraFuerzaMayor)) PrecioHoraExtraFuerzaMayor = Math.Round(precioHoraExtraFuerzaMayor, 2);
            if (json.TryGetDouble(minified ? "pen" : "precioHoraExtraNoFuerzaMayor", out double precioHoraExtraNoFuerzaMayor)) PrecioHoraExtraNoFuerzaMayor = Math.Round(precioHoraExtraNoFuerzaMayor, 2);
            if (json.TryGetDouble(minified ? "hjs" : "horasJornadaSemanal", out double horasJornadaSemanal)) HorasJornadaSemanal = Math.Round(horasJornadaSemanal, 2);

            PagasExtra = new List<PagaExtra>();
            if (json.TryGetProperty(minified ? "pe" : "pagasExtra", out JsonElement pagasExtraJson) && pagasExtraJson.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pagaExtraJson in pagasExtraJson.EnumerateArray())
                {
                    PagaExtra pagaExtra = new();
                    if (pagaExtraJson.TryGetString(minified ? "no" : "nombre", out string pNombre)) pagaExtra.Nombre = pNombre;
                    if (pagaExtraJson.TryGetString(minified ? "ba" : "base", out string pBase)) pagaExtra.Base = pBase;
                    if (pagaExtraJson.TryGetDouble(minified ? "po" : "porcentaje", out double pPorcentaje)) pagaExtra.Porcentaje = Math.Round(pPorcentaje, 2);
                    pagaExtra.Pluses = new();
                    if (pagaExtraJson.TryGetProperty(minified ? "pl" : "pluses", out JsonElement plusesJson) && plusesJson.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement plusJson in plusesJson.EnumerateArray())
                        {
                            PlusPagaExtra plus = new();
                            if (plusJson.TryGetString(minified ? "no" : "nombre", out string plNombre)) plus.Nombre = plNombre;
                            if (plusJson.TryGetInt32(minified ? "ca" : "cantidad", out int plCantidad)) plus.Cantidad = plCantidad;
                            if (minified)
                                plus.Activo = plus.Cantidad > 0;
                            else
                                if (plusJson.TryGetBoolean("activo", out bool plActivo)) plus.Activo = plActivo;
                            pagaExtra.Pluses.Add(plus);
                        }
                    }
                    if (minified)
                    {
                        if (pagaExtraJson.TryGetInt32("me", out int pMes))
                        {
                            pagaExtra.Mes = pMes;
                            pagaExtra.Prorrateada = false;
                        }
                        else pagaExtra.Prorrateada = true;
                    }
                    else
                    {
                        if (pagaExtraJson.TryGetBoolean("prorrateada", out bool pProrrateada)) pagaExtra.Prorrateada = pProrrateada;
                        if (pagaExtraJson.TryGetInt32("mes", out int pMes)) pagaExtra.Mes = pMes;
                    }
                    PagasExtra.Add(pagaExtra);
                }
            }

            PlusesFijos = new List<PlusFijo>();
            if (json.TryGetProperty(minified ? "pf" : "plusesFijos", out JsonElement plusesFijosJson) && plusesFijosJson.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement plusFijoJson in plusesFijosJson.EnumerateArray())
                {
                    PlusFijo plusFijo = new();
                    if (plusFijoJson.TryGetString(minified ? "no" : "nombre", out string pNombre)) plusFijo.Nombre = pNombre;
                    if (plusFijoJson.TryGetBoolean(minified ? "ex" : "extraordinario", out bool pExtraordinario)) plusFijo.Extraordinario = pExtraordinario;
                    if (plusFijoJson.TryGetDouble(minified ? "pu" : "precioPorUnidad", out double pPrecioPorUnidad)) plusFijo.PrecioPorUnidad = Math.Round(pPrecioPorUnidad, 2);
                    if (plusFijoJson.TryGetString(minified ? "fa" : "formaAplicacion", out string pFormaAplicacion)) plusFijo.FormaAplicacion = pFormaAplicacion;
                    if (plusFijoJson.TryGetInt32("unidades", out int pUnidades)) plusFijo.Unidades = pUnidades;
                    PlusesFijos.Add(plusFijo);
                }
            }
        }

        public CondicionesEconomicas(string jsonString, bool minified = false) : this(JsonDocument.Parse(jsonString).RootElement, minified) { }

        public CondicionesEconomicas() { }

        public string Serialize()
        {
            Dictionary<string, object> json = new();

            json["sbm"] = SalarioBaseMensual;
            json["pef"] = PrecioHoraExtraFuerzaMayor;
            json["pen"] = PrecioHoraExtraNoFuerzaMayor;
            json["hjs"] = HorasJornadaSemanal;

            List<object> pe = new();
            foreach (PagaExtra pagaExtra in PagasExtra)
            {
                Dictionary<string, object> pagaExtraJson = new();
                pagaExtraJson["no"] = pagaExtra.Nombre;
                pagaExtraJson["ba"] = pagaExtra.Base;
                pagaExtraJson["po"] = pagaExtra.Porcentaje;
                List<object> pluses = new();
                foreach (PlusPagaExtra plus in pagaExtra.Pluses)
                {
                    if (plus.Activo && plus.Cantidad > 0)
                    {
                        Dictionary<string, object> plusJson = new();
                        plusJson["no"] = plus.Nombre;
                        plusJson["ca"] = plus.Cantidad;
                        pluses.Add(plusJson);
                    }
                }
                pagaExtraJson["pl"] = pluses;
                pagaExtraJson["me"] = pagaExtra.Prorrateada ? null : pagaExtra.Mes;
                pe.Add(pagaExtraJson);
            }
            json["pe"] = pe;

            List<object> pf = new();
            foreach (PlusFijo plusFijo in PlusesFijos)
            {
                Dictionary<string, object> plusFijoJson = new();
                plusFijoJson["no"] = plusFijo.Nombre;
                plusFijoJson["ex"] = plusFijo.Extraordinario;
                plusFijoJson["pu"] = plusFijo.PrecioPorUnidad;
                plusFijoJson["fa"] = plusFijo.FormaAplicacion;
                pf.Add(plusFijoJson);
            }
            json["pf"] = pf;

            return JsonSerializer.Serialize(json);
        }

        public bool IsComplete()
        {
            if (string.IsNullOrEmpty(Nombre)) return false;
            if (SalarioBaseMensual == 0) return false;
            if (PrecioHoraExtraFuerzaMayor == 0) return false;
            if (PrecioHoraExtraNoFuerzaMayor == 0) return false;
            if (HorasJornadaSemanal == 0) return false;
            return true;
        }
    }

    public struct PagaExtra
    {
        public string Nombre { get; set; }
        public string Base { get; set; }
        public double Porcentaje { get; set; }
        public List<PlusPagaExtra> Pluses { get; set; }
        public bool Prorrateada { get; set; }
        public int? Mes { get; set; }
    }

    public struct PlusPagaExtra
    {
        public string Nombre { get; set; }
        public bool Activo { get; set; }
        public int Cantidad { get; set; }
    }

    public struct PlusFijo
    {
        public string Nombre { get; set; }
        public bool Extraordinario { get; set; }
        public double PrecioPorUnidad { get; set; }
        public string FormaAplicacion { get; set; } // todosLosDias (dias de contrato dentro del mes) | diasEfectivos (dentro del mes) | unicoMensual | variable
        public int Unidades { get; set; }
    }
}
