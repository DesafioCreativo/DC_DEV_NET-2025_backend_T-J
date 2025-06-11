using PdfSharp.Pdf;
using System.Text.RegularExpressions;
using ThinkAndJobSolution.Controllers._Helper;
using static ThinkAndJobSolution.Controllers._Helper.InstallationConstants;
using ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions;

namespace ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents.WorkerDocuments.SeguridadLaboral
{
    internal class FichaPrevencionRiesgos : PDF
    {
        /// <summary>
        /// Discriminantes del documento.
        /// </summary>
        public static string[] DISCRIMINANTE = { "PREVENCIÓN DE RIESGOS LABORALES", "RIESGOS Y MEDIDAS PREVENTIVAS DEL PUESTO DE TRABAJO" };
        /// <summary>
        /// Constructor de la clase FichaPrevencionRiesgos.
        /// </summary>
        /// <param name="text"> Texto del documento. </param>
        /// <param name="directory"> Directorio del documento. </param>
        /// <param name="newDirectory"> Nuevo directorio del documento. </param>
        /// <param name="postProcessing"> Indica si se debe realizar el postprocesado. </param>
        public FichaPrevencionRiesgos(string filename, string newDirectory, bool postProcessing, ref int index, ref PdfDocument mainDoc) : base(filename, newDirectory, postProcessing, ref index, ref mainDoc)
        {
            TipoDocumento = TipoDocumento.FichaPrevencionRiesgos;
        }
        protected override void PostProcess(ref int index, ref PdfDocument mainDoc)
        {

            PdfDocument newDoc = new();
            string docText = "", docText_aux = "", filename = "";
            if (mainDoc.IsSigned())
            {
                newDoc = (PdfDocument)mainDoc.Clone();
            }
            else
            {
                PdfDocument newDoc_aux = new();
                // Añadimos las páginas al nuevo documento
                bool dobleNIF = false;
                while (!dobleNIF && index < mainDoc.PageCount)
                {
                    newDoc.AddPage(mainDoc.Pages[index]);
                    filename = Filename.Replace(".pdf", $"_AUX.pdf");
                    newDoc.Save(filename);
                    docText = HelperMethods.ExtractTextPDF(filename).ToUpper().Replace(".", "");
                    Match[] matches = Regex.Matches(docText, @"([0-9]{8}[A-Z]|[XYZ][0-9]{7}[A-Z])", RegexOptions.None, TimeSpan.FromSeconds(1)).ToArray();
                    // Sacamos otra lista con los nifs sin repetir de la lista anterior
                    List<string> matchesAux = new();
                    foreach (Match item in matches)
                    {
                        if (!matchesAux.Contains(item.Value))
                        {
                            matchesAux.Add(item.Value);
                        }
                    }

                    if (matchesAux.Count > 1)
                    {
                        docText = docText_aux;
                        newDoc = newDoc_aux;
                        dobleNIF = true;
                    }
                    else
                    {
                        newDoc_aux.AddPage(mainDoc.Pages[index]);
                        docText_aux = docText;
                        index++;
                    }
                }
            }

            // Añadimos el nuevo documento a la lista de documentos
            filename = Filename.Replace(".pdf", $"_AUX.pdf");
            newDoc.Save(filename);
            newDoc.Close();
            if (docText == "")
                docText = HelperMethods.ExtractTextPDF(filename).ToUpper().Replace(".", "");
            string patternFecha = @"(\d{1,2})DE(ENERO|FEBRERO|MARZO|ABRIL|MAYO|JUNIO|JULIO|AGOSTO|SEPTIEMBRE|OCTUBRE|NOVIEMBRE|DICIEMBRE)DE(\d{4})";
            string NIF = "";
            string[] FECHA = { "", "", "" };
            try
            {
                // Creamos una lista con todos los nifs que aparezcan en el documento que no sean el del representante
                List<Match> matches = Regex.Matches(docText, @"([0-9]{8}[A-Z]|[XYZ][0-9]{7}[A-Z])", RegexOptions.None, TimeSpan.FromSeconds(1)).ToList();
                List<Match> matchesAux = new();
                foreach (Match match in matches) if (!match.Value.Equals(NIF_REPRESENTANTE)) { matchesAux.Add(match); }
                matches = matchesAux;
                if (matches.Count > 0) NIF = matches.First().Value;
                // Fecha del documento
                FECHA = Regex.Matches(docText.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", ""), patternFecha, RegexOptions.None, TimeSpan.FromSeconds(1))[0].Value.Split("DE");
                if (FECHA[0].Length == 1) FECHA[0] = "0" + FECHA[0];
            }
            catch (Exception) { }
            string newFilename = $"{GetType().Name}_{NIF}_{FECHA[0]}-{HelperMethods.GetMonth(FECHA[1])}-{FECHA[2]}.pdf";
            string newDirectory = "";
            if (NewDirectory == null)
            {
                // Renombramos el documento reemplazando todo el nombre del documento por otro nuevo
                string[] strs = filename.Split(FOLDER_SPLITER);
                // El nuevo directorio será la composición de todos los elementos del array menos el último
                for (int k = 0; k < strs.Length - 1; k++)
                {
                    newDirectory += strs[k] + FOLDER_SPLITER;
                }
                newDirectory += newFilename;
            }
            else
            {
                newDirectory = NewDirectory + FOLDER_SPLITER + newFilename;
            }
            File.Move(filename, newDirectory, true);
        }
    }
}
