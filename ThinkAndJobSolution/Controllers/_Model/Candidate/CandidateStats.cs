using Microsoft.Data.SqlClient;
using ThinkAndJobSolution.Controllers._Helper;

namespace ThinkAndJobSolution.Controllers._Model.Candidate
{
    public class CandidateStats : ExtendedCandidateData
    {

        public struct TestStatus
        {
            public string name { get; set; }
            public bool passed { get; set; }
            public bool certified { get; set; }
            public bool needsCert { get; set; }
            public string submitId { get; set; }
            public string centroAlias { get; set; }
            public string companyName { get; set; }
            public bool isCurrentWork { get; set; }
            public DateTime? submitDate { get; set; }
        }


        //Datos de control
        public bool active { get; set; }
        public bool paymentBlock { get; set; }
        public int warnings { get; set; }
        public string signLink { get; set; }
        public bool? test { get; set; }
        public string pendingEmail { get; set; }
        public bool cesionActiva { get; set; }

        //Informacion sobre su trabajo
        public DateTime? fechaComienzoTrabajo { get; set; }
        public DateTime? fechaFinTrabajo { get; set; }
        public string companyId { get; set; }
        public string companyName { get; set; }
        public string centroId { get; set; }
        public string centroAlias { get; set; }
        public string workId { get; set; }
        public string workName { get; set; }
        public bool documentsDownloaded { get; set; }
        public List<TestStatus> testsPRL { get; set; }
        public List<TestStatus> testsTraining { get; set; }

        //Sobre su cara
        public bool hasPhoto { get; set; }
        public bool hasFace { get; set; }

        //Sobre su tarjeta
        public string cardId { get; set; }

        public new void Read(SqlDataReader reader)
        {
            base.Read(reader);

            active = reader.GetInt32(reader.GetOrdinal("active")) == 1;
            paymentBlock = reader.GetInt32(reader.GetOrdinal("payment_block")) == 1;
            warnings = reader.IsDBNull(reader.GetOrdinal("warnings")) ? 0 : reader.GetInt32(reader.GetOrdinal("warnings"));
            signLink = reader.IsDBNull(reader.GetOrdinal("lastSignLink")) ? null : reader.GetString(reader.GetOrdinal("lastSignLink"));
            fechaComienzoTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaComienzoTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaComienzoTrabajo"));
            fechaFinTrabajo = reader.IsDBNull(reader.GetOrdinal("fechaFinTrabajo")) ? null : reader.GetDateTime(reader.GetOrdinal("fechaFinTrabajo"));
            test = reader.GetInt32(reader.GetOrdinal("test")) == 1;
            pendingEmail = reader.IsDBNull(reader.GetOrdinal("pendingEmail")) ? null : reader.GetString(reader.GetOrdinal("pendingEmail"));

            companyId = reader.IsDBNull(reader.GetOrdinal("idEmpresa")) ? null : reader.GetString(reader.GetOrdinal("idEmpresa"));
            companyName = reader.IsDBNull(reader.GetOrdinal("nombreEmpresa")) ? null : reader.GetString(reader.GetOrdinal("nombreEmpresa"));
            centroId = reader.IsDBNull(reader.GetOrdinal("centroId")) ? null : reader.GetString(reader.GetOrdinal("centroId"));
            centroAlias = reader.IsDBNull(reader.GetOrdinal("centroAlias")) ? null : reader.GetString(reader.GetOrdinal("centroAlias"));
            workId = reader.IsDBNull(reader.GetOrdinal("idTrabajo")) ? null : reader.GetString(reader.GetOrdinal("idTrabajo"));
            workName = reader.IsDBNull(reader.GetOrdinal("nombreTrabajo")) ? null : reader.GetString(reader.GetOrdinal("nombreTrabajo"));

            documentsDownloaded = reader.GetInt32(reader.GetOrdinal("documentsDownloaded")) == 1;
            testsPRL = new List<TestStatus>();
            testsTraining = new List<TestStatus>();

            hasPhoto = photo != null;
            hasFace = HelperMethods.ExistsFile(new[] { "candidate", id, "face" });

            cesionActiva = reader.GetInt32(reader.GetOrdinal("cesionActiva")) == 1;

            cardId = reader.IsDBNull(reader.GetOrdinal("cardId")) ? null : reader.GetInt32(reader.GetOrdinal("cardId")).ToString();
        }

        public void ReadTest(SqlDataReader reader, string centroAlias = null, string companyName = null, bool isCurrentWork = false)
        {
            TestStatus test = new TestStatus
            {
                name = reader.GetString(reader.GetOrdinal("nombre")),
                passed = !reader.IsDBNull(reader.GetOrdinal("submitId")),
                certified = !reader.IsDBNull(reader.GetOrdinal("certificado")) && reader.GetInt32(reader.GetOrdinal("certificado")) == 1,
                needsCert = reader.GetInt32(reader.GetOrdinal("requiereCertificado")) == 1,
                submitId = reader.IsDBNull(reader.GetOrdinal("submitId")) ? null : reader.GetString(reader.GetOrdinal("submitId")),
                submitDate = reader.IsDBNull(reader.GetOrdinal("fechaFirma")) ? null : new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(reader.GetInt64(reader.GetOrdinal("fechaFirma"))),
                centroAlias = centroAlias,
                companyName = companyName,
                isCurrentWork = isCurrentWork
            };

            if (reader.GetString(reader.GetOrdinal("tipo")).ToUpper().Equals("PRL"))
            {
                testsPRL.Add(test);
            }
            else
            {
                testsTraining.Add(test);
            }
        }
    }
}
