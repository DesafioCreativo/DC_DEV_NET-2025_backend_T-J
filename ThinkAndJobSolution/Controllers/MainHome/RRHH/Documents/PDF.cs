using PdfSharp.Pdf.IO;
using PdfSharp.Pdf;
using ThinkAndJobSolution.Controllers._Helper;
using ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents.WorkerDocuments.Certificados;
using ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents.WorkerDocuments.Contratos;
using ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents.WorkerDocuments.NominasYFiniquitos;
using ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents.WorkerDocuments.Otros;
using ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents.WorkerDocuments.SeguridadLaboral;

namespace ThinkAndJobSolution.Controllers.MainHome.RRHH.Documents
{
    /// <summary>
    /// Clase base de los documentos PDF.
    /// </summary>
    public abstract class PDF
    {
        /// <summary>
        /// Nombre del .pdf.
        /// </summary>
        public string Filename { get; }
        /// <summary>
        /// Directorio donde se guardarán los documentos extraídos.
        /// </summary>
        public string NewDirectory { get; }
        /// <summary>
        /// Tipo de documento.
        /// </summary>
        public TipoDocumento TipoDocumento { get; set; }


        /// <summary>
        /// Constructor base de la clase PDF.
        /// </summary>
        /// <param name="text"> Texto del PDF </param>
        /// <param name="directory"> Directorio del .pdf </param>
        public PDF(string filename, string newDirectory, bool postProcessing, ref int index, ref PdfDocument mainDoc)
        {
            Filename = filename;
            NewDirectory = newDirectory;
            if (postProcessing)
                PostProcess(ref index, ref mainDoc);
        }


        /// <summary>
        /// Genera un objeto PDF a partir del texto del PDF y su directorio.
        /// La función lo clasifica en función de su contenido.
        /// </summary>
        /// <param name="pdfText"> Texto del PDF </param>
        /// <param name="directory"> Directorio del .pdf </param>
        /// <param name="newDirectory"> Directorio donde se guardarán los documentos extraídos </param>
        /// <param name="postProcessing"> Indica si se debe postprocesar el texto del PDF o si solo se desea clasificar </param>
        /// <param name="tipoDocumento"> Tipo de documento (-1 para null, 0 para Otros, 1 o más para el resto) </param>
        /// <returns></returns>
        public static PDF Create(TipoDocumento tipoDoc, string filename, string newDirectory, bool postProcessing, ref int index, ref PdfDocument mainDoc)
        {
            switch (tipoDoc)
            {
                default:
                    return null;
                case TipoDocumento.Otro:
                    return new Otro(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CertINEM:
                    return new CertINEM(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CertIRPF:
                    return new CertIRPF(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CBajaVoluntaria:
                    return new CBajaVoluntaria(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CJCategoria:
                    return new CJCategoria(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLCEventual:
                    return new CLCEventual(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLCEventualParcial:
                    return new CLCEventualParcial(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLCFijoDiscontinuo:
                    return new CLCFijoDiscontinuo(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLCSustitucion:
                    return new CLCSustitucion(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLCSustitucionParcial:
                    return new CLCSustitucionParcial(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLProrroga:
                    return new CLProrroga(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLTraFijoDiscontinuo:
                    return new CLTraFijoDiscontinuo(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLTraIndefinido:
                    return new CLTraIndefinido(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.CLVencimiento:
                    return new CLVencimiento(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.OrdenServicio:
                    return new OrdenServicio(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.Nomina:
                    return new Nomina(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.Finiquito:
                    return new Finiquito(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.ExamenDeSalud:
                    return new ExamenDeSalud(filename, newDirectory, postProcessing, ref index, ref mainDoc);
                case TipoDocumento.FichaPrevencionRiesgos:
                    return new FichaPrevencionRiesgos(filename, newDirectory, postProcessing, ref index, ref mainDoc);
            }
        }
        /// <summary>
        /// Postprocesa el texto del PDF para extraer información relevante.
        /// </summary>
        protected abstract void PostProcess(ref int index, ref PdfDocument mainDoc);
        /// <summary>
        /// Obtiene el tipo de documento a partir del texto del PDF.
        /// </summary>
        /// <param name="pdfText"> Texto del PDF. </param>
        /// <returns> Tipo de documento. </returns>
        public static TipoDocumento GetDocType(string pdfText)
        {
            switch (pdfText)
            {
                // Certificados
                case string a when CertINEM.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CertINEM;
                case string a when CertIRPF.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CertIRPF;

                // Contratos
                case string a when CBajaVoluntaria.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CBajaVoluntaria;
                case string a when CLVencimiento.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLVencimiento;
                case string a when CJCategoria.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CJCategoria;
                case string a when CLProrroga.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLProrroga;
                case string a when OrdenServicio.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.OrdenServicio;
                case string a when CLTraIndefinido.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLTraIndefinido;
                case string a when CLTraFijoDiscontinuo.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLTraFijoDiscontinuo;
                case string a when CLCFijoDiscontinuo.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLCFijoDiscontinuo;
                case string a when CLCSustitucion.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLCSustitucion;
                case string a when CLCSustitucionParcial.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLCSustitucionParcial;
                case string a when CLCEventual.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLCEventual;
                case string a when CLCEventualParcial.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.CLCEventualParcial;

                // Nóminas y Finiquitos
                case string a when Nomina.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.Nomina;
                case string a when Finiquito.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.Finiquito;

                // Seguridad Laboral
                case string a when ExamenDeSalud.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.ExamenDeSalud;
                case string a when FichaPrevencionRiesgos.DISCRIMINANTE.All(d => a.ToUpper().Contains(d)):
                    return TipoDocumento.FichaPrevencionRiesgos;

                // Otros
                default:
                    if (pdfText != null)
                        return TipoDocumento.Otro;
                    else
                        return TipoDocumento.Null;
            }
        }
        /// <summary>
        /// Extrae el texto de un PDF desde una página inicial hasta una página final.
        /// </summary>
        /// <param name="filename"> Nombre del archivo. </param>
        /// <param name="ini"> Página inicial. </param>
        /// <param name="fin"> Página final. </param>
        /// <returns> Texto extraído. </returns>
        public static string ExtractTextPDF(string filename, int ini, int fin = -1)
        {
            PdfDocument mainDoc = PdfReader.Open(filename, PdfDocumentOpenMode.Import);
            PdfDocument newDoc = new();
            int pageCount = ini + 1;
            for (int i = ini; i < (fin == -1 ? pageCount : fin); i++)
                newDoc.AddPage(mainDoc.Pages[i]);
            string newFile = filename.Replace(".pdf", $"_{ini}.pdf");
            newDoc.Save(newFile);
            newDoc.Close();
            string textExtracted = HelperMethods.ExtractTextPDF(newFile);
            File.Delete(newFile);
            return textExtracted;
        }

        /// <summary>
        ///  Extrae el texto de un PDF desde una página inicial hasta una página final.
        /// </summary>
        /// <param name="mainDoc"> Documento principal. </param>
        /// <param name="filename"> Nombre del archivo. </param>
        /// <param name="ini"> Página inicial. </param>
        /// <param name="fin"> Página final. </param>
        /// <returns> Texto extraído. </returns>
        public static string ExtractTextPDF(ref PdfDocument mainDoc, string filename, int ini, int fin = -1)
        {
            PdfDocument newDoc = new();
            int pageCount = ini + 1;
            for (int i = ini; i < (fin == -1 ? pageCount : fin); i++)
                newDoc.AddPage(mainDoc.Pages[i]);
            string newFile = filename.Replace(".pdf", $"_{ini}.pdf");
            newDoc.Save(newFile);
            newDoc.Close();
            string textExtracted = HelperMethods.ExtractTextPDF(newFile);
            File.Delete(newFile);
            return textExtracted;
        }
    }

    /// <summary>
    /// Enumeración de los tipos de documentos.
    /// </summary>
    public enum TipoDocumento
    {
        Null = -1,
        Otro = 0,
        CertINEM = 1,
        CertIRPF = 2,
        CBajaVoluntaria = 3,
        CJCategoria = 4,
        CLCEventual = 5,
        CLCEventualParcial = 6,
        CLCFijoDiscontinuo = 7,
        CLCSustitucion = 8,
        CLCSustitucionParcial = 9,
        CLProrroga = 10,
        CLTraFijoDiscontinuo = 11,
        CLTraIndefinido = 12,
        CLVencimiento = 13,
        OrdenServicio = 14,
        Nomina = 15,
        Finiquito = 16,
        ExamenDeSalud = 17,
        FichaPrevencionRiesgos = 18
    }
}
