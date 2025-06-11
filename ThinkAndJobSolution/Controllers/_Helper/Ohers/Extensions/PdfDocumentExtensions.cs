using PdfSharp.Pdf;

namespace ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions
{
    public static class PdfDocumentExtensions
    {
        public static bool IsSigned(this PdfDocument pdf)
        {
            return pdf.AcroForm != null && pdf.AcroForm.Fields != null && pdf.AcroForm.Elements.ContainsKey("/SigFlags") && pdf.AcroForm.Elements["/SigFlags"].ToString() == "3";
        }
    }
}
