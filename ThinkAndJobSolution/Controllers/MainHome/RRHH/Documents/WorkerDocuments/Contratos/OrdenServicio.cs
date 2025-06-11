using System.Text.RegularExpressions;
using ThinkAndJobSolution.Controllers._Helper;
using PdfSharp.Pdf;
using static ThinkAndJobSolution.Controllers._Helper.InstallationConstants;

namespace ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents.WorkerDocuments.Contratos
{
    internal class OrdenServicio : PDF
    {
        /// <summary>
        /// Discriminantes del documento.
        /// </summary>
        public static string[] DISCRIMINANTE = { "ORDEN DE SERVICIO", "POR LA EMPRESA DE TRABAJO TEMPORAL" };
        /// <summary>
        /// Número de páginas por documento.
        /// </summary>
        private int pagesPerDocument = 1;
        /// <summary>
        /// Constructor de la clase OrdenServicio.
        /// </summary>
        /// <param name="text"> Texto del documento. </param>
        /// <param name="directory"> Directorio del documento. </param>
        /// <param name="newDirectory"> Nuevo directorio del documento. </param>
        /// <param name="postProcessing"> Indica si se debe realizar el postprocesado. </param>
        public OrdenServicio(string filename, string newDirectory, bool postProcessing, ref int index, ref PdfDocument mainDoc) : base(filename, newDirectory, postProcessing, ref index, ref mainDoc)
        {
            TipoDocumento = TipoDocumento.OrdenServicio;
        }

        /// <summary>
        /// Método que realiza el postprocesado del documento.
        /// </summary>
        protected override void PostProcess(ref int index, ref PdfDocument mainDoc)
        {
            PdfDocument newDoc = new();
            if (mainDoc.PageCount == pagesPerDocument)
            {
                newDoc = (PdfDocument)mainDoc.Clone();
            }
            else
            {
                // Añadimos las páginas al nuevo documento
                int maxPages = index + pagesPerDocument;
                for (int i = index; i < maxPages; i++)
                    newDoc.AddPage(mainDoc.Pages[i]);
            }

            // Añadimos el nuevo documento a la lista de documentos
            string filename = Filename.Replace(".pdf", $"_AUX.pdf");
            newDoc.Save(filename);
            newDoc.Close();
            string docText = HelperMethods.ExtractTextPDF(filename).ToUpper().Replace(".", "");
            string patternFecha = @"(\d{1,2})DE(ENERO|FEBRERO|MARZO|ABRIL|MAYO|JUNIO|JULIO|AGOSTO|SEPTIEMBRE|OCTUBRE|NOVIEMBRE|DICIEMBRE)DE(\d{4})";
            string NIF = "";
            string[] FECHA = { "", "", "" };
            try
            {
                // Creamos una lista con todos los nifs que aparezcan en el documento que no sean el del representante
                List<Match> matches = Regex.Matches(docText, @"[0-9]{8}[A-Z]", RegexOptions.None, TimeSpan.FromSeconds(1)).ToList();
                List<Match> matchesAux = new();
                foreach (Match match in matches) if (!match.Value.Equals(NIF_REPRESENTANTE)) { matchesAux.Add(match); }
                matches = matchesAux;
                if (matches.Count > 0) NIF = matches.First().Value;
                else
                {
                    matches = Regex.Matches(docText, @"[XYZ][0-9]{7}[A-Z]", RegexOptions.None, TimeSpan.FromSeconds(1)).ToList();
                    if (matches.Count > 0) NIF = matches.First().Value;
                }
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

            // Incrementamos el índice de documentos
            index += pagesPerDocument;
        }
    }
}
