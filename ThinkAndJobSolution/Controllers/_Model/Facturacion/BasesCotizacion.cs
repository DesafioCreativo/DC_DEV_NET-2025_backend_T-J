using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;
using static ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions.JsonExtensions;

namespace ThinkAndJobSolution.Controllers._Model.Facturacion
{
    public class BasesCotizacion
    {
        public double SalarioMinimo { get; set; }
        public double CotMaxima { get; set; }

        //Contingencias
        public double ContingenciasComunesTrabajador { get; set; }
        public double ContingenciasComunesEmpresario { get; set; }
        public double DesempleoTrabajador { get; set; }
        public double DesempleoEmpresario { get; set; }
        public double FogasaEmpresario { get; set; }
        public double FormacionTrabajador { get; set; }
        public double FormacionEmpresario { get; set; }
        public double AtyepEmpresario { get; set; }

        //Otros que no cuentan para el Total SS
        public double ProteccionNoComunTrabajador { get; set; }
        public double HorasExtraTrabajador { get; set; }
        public double HorasExtraEmpresario { get; set; }

        //Totales
        public double TotalSSTrabajador { get; set; } //Calculado
        public double TotalSSEmpresario { get; set; } //Calculado

        //Otros
        public double BaseIRPFespecie { get; set; }
        public double PenalizacionContratoMenorUnMes { get; set; }
        public double Iva { get; set; }

        public void CalcularTotalSS()
        {
            TotalSSTrabajador = ContingenciasComunesTrabajador + DesempleoTrabajador + FormacionTrabajador;
            TotalSSEmpresario = ContingenciasComunesEmpresario + DesempleoEmpresario + FogasaEmpresario + FormacionEmpresario + AtyepEmpresario;
        }

        public BasesCotizacion(JsonElement json, bool minified = false)
        {
            if (json.TryGetDouble(minified ? "sm" : "salarioMinimo", out double salarioMinimo)) SalarioMinimo = Math.Round(salarioMinimo, 2);
            if (json.TryGetDouble(minified ? "cm" : "cotMaxima", out double cotMaxima)) CotMaxima = Math.Round(cotMaxima, 2);

            if (json.TryGetDouble(minified ? "cct" : "contingenciasComunesTrabajador", out double contingenciasComunesTrabajador)) ContingenciasComunesTrabajador = Math.Round(contingenciasComunesTrabajador, 2);
            if (json.TryGetDouble(minified ? "cce" : "contingenciasComunesEmpresario", out double contingenciasComunesEmpresario)) ContingenciasComunesEmpresario = Math.Round(contingenciasComunesEmpresario, 2);
            if (json.TryGetDouble(minified ? "dt" : "desempleoTrabajador", out double desempleoTrabajador)) DesempleoTrabajador = Math.Round(desempleoTrabajador, 2);
            if (json.TryGetDouble(minified ? "de" : "desempleoEmpresario", out double desempleoEmpresario)) DesempleoEmpresario = Math.Round(desempleoEmpresario, 2);
            if (json.TryGetDouble(minified ? "fe" : "fogasaEmpresario", out double fogasaEmpresario)) FogasaEmpresario = Math.Round(fogasaEmpresario, 2);
            if (json.TryGetDouble(minified ? "fot" : "formacionTrabajador", out double formacionTrabajador)) FormacionTrabajador = Math.Round(formacionTrabajador, 2);
            if (json.TryGetDouble(minified ? "foe" : "formacionEmpresario", out double formacionEmpresario)) FormacionEmpresario = Math.Round(formacionEmpresario, 2);
            if (json.TryGetDouble(minified ? "aee" : "atyepEmpresario", out double atyepEmpresario)) AtyepEmpresario = Math.Round(atyepEmpresario, 2);

            if (json.TryGetDouble(minified ? "pnc" : "proteccionNoComunTrabajador", out double proteccionNoComun)) ProteccionNoComunTrabajador = Math.Round(proteccionNoComun, 2);
            if (json.TryGetDouble(minified ? "het" : "horasExtraTrabajador", out double horasExtraTrabajador)) HorasExtraTrabajador = Math.Round(horasExtraTrabajador, 2);
            if (json.TryGetDouble(minified ? "hee" : "horasExtraEmpresario", out double horasExtraEmpresario)) HorasExtraEmpresario = Math.Round(horasExtraEmpresario, 2);

            if (json.TryGetDouble(minified ? "bie" : "baseIRPFespecie", out double baseIRPFespecie)) BaseIRPFespecie = Math.Round(baseIRPFespecie, 2);
            if (json.TryGetDouble(minified ? "pcmun" : "penalizacionContratoMenorUnMes", out double penalizacionContratoMenorUnMes)) PenalizacionContratoMenorUnMes = Math.Round(penalizacionContratoMenorUnMes, 2);
            if (json.TryGetDouble("iva", out double iva)) Iva = Math.Round(iva, 2);

            CalcularTotalSS();
        }

        public BasesCotizacion(string jsonString, bool minified = false) : this(JsonDocument.Parse(jsonString).RootElement, minified) { }

        public BasesCotizacion() { }

        public string Serialize()
        {
            Dictionary<string, object> json = new();

            json["sm"] = SalarioMinimo;
            json["cm"] = CotMaxima;

            json["cct"] = ContingenciasComunesTrabajador;
            json["cce"] = ContingenciasComunesEmpresario;
            json["dt"] = DesempleoTrabajador;
            json["de"] = DesempleoEmpresario;
            json["fe"] = FogasaEmpresario;
            json["fot"] = FormacionTrabajador;
            json["foe"] = FormacionEmpresario;
            json["aee"] = AtyepEmpresario;

            json["pnc"] = ProteccionNoComunTrabajador;
            json["het"] = HorasExtraTrabajador;
            json["hee"] = HorasExtraEmpresario;

            json["bie"] = BaseIRPFespecie;
            json["pcmun"] = PenalizacionContratoMenorUnMes;
            json["iva"] = Iva;

            return JsonSerializer.Serialize(json);
        }
    }
}
