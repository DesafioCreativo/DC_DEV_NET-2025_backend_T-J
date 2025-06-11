using Microsoft.Data.SqlClient;
using System.Text.Json;
using ThinkAndJobSolution.Controllers._Helper;


namespace ThinkAndJobSolution.Controllers._Model.Candidate
{
    public class ExtendedCandidateData : CandidateData
    {
        public struct DrivingLicense
        {
            public string type { get; set; }
            public DateTime expiration { get; set; }
            public string anverso { get; set; }
            public string reverso { get; set; }
        }

        public struct LegalRepresentativeConsent
        {
            public string tutorAnverso { get; set; }
            public string tutorReverso { get; set; }
            public string autorizacion { get; set; }
        }

        public struct HasLegalRepresentativeConsent
        {
            public bool hasTutorDni { get; set; }
            public bool hasAutorizacion { get; set; }
        }

        //Datos extras post-registro
        public string photo { get; set; }
        public DateTime? birth { get; set; }
        public string cuentaBancaria { get; set; }
        public string numeroSeguridadSocial { get; set; }
        public List<DrivingLicense> drivingLicenses { get; set; }
        public string nacionalidad { get; set; }
        public char? sexo { get; set; }
        public DateTime? permisoTrabajoCaducidad { get; set; }
        public string modelo145 { get; set; }
        public string contactoNombre { get; set; }
        public string contactoTelefono { get; set; }
        public string contactoTipo { get; set; }

        //Ficheros adjuntos
        public string dniAnverso { get; set; }
        public string dniReverso { get; set; }
        public string cv { get; set; }
        public string fotoCuentaBancaria { get; set; }
        public string fotoNumeroSeguridadSocial { get; set; }
        public string fotoPermisoTrabajo { get; set; }
        public LegalRepresentativeConsent legalRepresentativeConsent { get; set; }
        public string fotoDiscapacidad { get; set; }

        //Has de Ficheros adjuntos
        public bool hasDniAnverso { get; set; }
        public bool hasDniReverso { get; set; }
        public bool hasCv { get; set; }
        public bool hasFotoCuentaBancaria { get; set; }
        public bool hasFotoNumeroSeguridadSocial { get; set; }
        public bool hasFotoPermisoTrabajo { get; set; }
        public HasLegalRepresentativeConsent hasLegalRepresentativeConsent { get; set; }
        public bool hasFotoDiscapacidad { get; set; }
        public bool hasStoredSign { get; set; }

        //Extras de control
        public bool allDataFilledIn { get; set; }
        public DateTime? periodoGracia { get; set; }
        public int diasPeriodoGracia { get; set; }

        public struct CandidateRequiredData //True signfica que lo tiene
        {
            public bool photo { get; set; }
            public bool modelo145 { get; set; }
            public bool birth { get; set; }
            public bool direccion { get; set; }
            public bool cp { get; set; }
            public bool localidad { get; set; }
            public bool provincia { get; set; }
            public bool nacionalidad { get; set; }
            public bool sexo { get; set; }
            public bool fotoDni { get; set; }
            public bool permisoTrabajoCaducidad { get; set; }
            public bool fotoPermisoTrabajo { get; set; }
            public bool legalRepresentativeConsentTutorDni { get; set; }
            public bool legalRepresentativeConsentAutorizacion { get; set; }
            public bool fotoDiscapacidad { get; set; }
            public bool cuentaBancaria { get; set; }
            public bool fotoCuentaBancaria { get; set; }
        }

        public CandidateRequiredData GetRequiredData()
        {
            bool estaEnPeriodoGracia = diasPeriodoGracia > 0;
            bool tieneDiscapacidad = false;



            if (modelo145 != null)

            {
                JsonElement modelo = JsonDocument.Parse(modelo145).RootElement;
                if (modelo.TryGetProperty("discapacidad", out JsonElement discapacidadJson) && discapacidadJson.TryGetProperty("grado", out JsonElement discapacidadGradoJson))
                {
                    tieneDiscapacidad = discapacidadGradoJson.GetInt32() > 0;
                }
            }


            return new CandidateRequiredData()
            {
                photo = estaEnPeriodoGracia ? true : (photo != null), //Si esta en el periodo de gracia, la foto no es obligatoria
                birth = birth != null,
                direccion = direccion != null,
                cp = cp != null,
                localidad = localidad != null,
                provincia = provincia != null,
                nacionalidad = nacionalidad != null,
                sexo = sexo != null,

                modelo145 = modelo145 != null,
                fotoDni = hasDniAnverso && hasDniReverso,
                permisoTrabajoCaducidad = (
                    !RequiresPermisoTrabajo(nacionalidad) || //Si no es español, requiere permiso para trabajar
                    permisoTrabajoCaducidad != null
                ),
                fotoPermisoTrabajo = (
                    !RequiresPermisoTrabajo(nacionalidad) || //Si no es español, requiere permiso para trabajar
                     hasFotoPermisoTrabajo
                ),
                legalRepresentativeConsentAutorizacion = (
                    !RequiresAuthorization(birth) ||  //Si es menor, necesita autorizacion
                    hasLegalRepresentativeConsent.hasAutorizacion
                ),
                legalRepresentativeConsentTutorDni = (
                    !RequiresAuthorization(birth) ||  //Si es menor, necesita autorizacion
                    hasLegalRepresentativeConsent.hasTutorDni
                ),
                fotoDiscapacidad = (
                    !tieneDiscapacidad || //Si tiene discapacidad, necesita la foto
                    hasFotoDiscapacidad
                ),
                cuentaBancaria = estaEnPeriodoGracia ? true : (cuentaBancaria != null), //Si está en el periodo de gracia, la cuenta bancaria no es obligatoria,
                fotoCuentaBancaria = estaEnPeriodoGracia ? true : (hasFotoCuentaBancaria) //Si está en el periodo de gracia, la foto de la cuenta bancaria no es obligatoria
            };
        }
        private void CheckHasAllDataFilledIn()
        {
            //Si tiene modelo145, buscar si ha marcado discapacidad
            CandidateRequiredData required = GetRequiredData();
            allDataFilledIn =
                required.birth &&
                required.direccion &&
                required.cp &&
                required.localidad &&
                required.provincia &&
                required.nacionalidad &&
                required.sexo &&
                required.modelo145 &&
                required.fotoDni &&
                required.permisoTrabajoCaducidad &&
                required.fotoPermisoTrabajo &&
                required.legalRepresentativeConsentAutorizacion &&
                required.legalRepresentativeConsentTutorDni &&
                required.fotoDiscapacidad &&
                required.cuentaBancaria &&
                required.fotoCuentaBancaria;
        }

        public static bool RequiresAuthorization(DateTime? birth)
        {
            if (birth == null) return false;
            int? edad = HelperMethods.CalculateAge(birth);
            return edad.HasValue && edad.Value <= 17;
        }
        public static bool RequiresPermisoTrabajo(string nacionalidad)
        {
            if (nacionalidad == null) return false;
            return !Constants.SCHENGEN_COUNTRIES.Contains(nacionalidad);
        }
        public static bool CheckAgeValid(DateTime? birth)
        {
            int? edad = HelperMethods.CalculateAge(birth);
            return !edad.HasValue || edad >= 16;
        }

        public new void Read(SqlDataReader reader)
        {
            base.Read(reader);

            photo = HelperMethods.ReadFile(new[] { "candidate", id, "photo" });
            birth = reader.IsDBNull(reader.GetOrdinal("nacimiento")) ? null : reader.GetDateTime(reader.GetOrdinal("nacimiento"));
            cuentaBancaria = reader.IsDBNull(reader.GetOrdinal("cuenta_bancaria")) ? null : reader.GetString(reader.GetOrdinal("cuenta_bancaria"));
            numeroSeguridadSocial = reader.IsDBNull(reader.GetOrdinal("numero_seguridad_social")) ? null : reader.GetString(reader.GetOrdinal("numero_seguridad_social"));
            drivingLicenses = new List<DrivingLicense>();
            nacionalidad = reader.IsDBNull(reader.GetOrdinal("nacionalidad")) ? null : reader.GetString(reader.GetOrdinal("nacionalidad"));
            sexo = reader.IsDBNull(reader.GetOrdinal("sexo")) ? null : reader.GetString(reader.GetOrdinal("sexo"))[0];
            permisoTrabajoCaducidad = reader.IsDBNull(reader.GetOrdinal("permiso_trabajo_caducidad")) ? null : reader.GetDateTime(reader.GetOrdinal("permiso_trabajo_caducidad"));
            modelo145 = reader.IsDBNull(reader.GetOrdinal("modelo145")) ? null : reader.GetString(reader.GetOrdinal("modelo145"));
            contactoNombre = reader.IsDBNull(reader.GetOrdinal("contactoNombre")) ? null : reader.GetString(reader.GetOrdinal("contactoNombre"));
            contactoTelefono = reader.IsDBNull(reader.GetOrdinal("contactoTelefono")) ? null : reader.GetString(reader.GetOrdinal("contactoTelefono"));
            contactoTipo = reader.IsDBNull(reader.GetOrdinal("contactoTipo")) ? null : reader.GetString(reader.GetOrdinal("contactoTipo"));

            hasDniAnverso = HelperMethods.ExistsFile(new[] { "candidate", id, "dniAnverso" });
            hasDniReverso = HelperMethods.ExistsFile(new[] { "candidate", id, "dniReverso" });
            hasCv = HelperMethods.ExistsFile(new[] { "candidate", id, "cv" });
            hasFotoCuentaBancaria = HelperMethods.ExistsFile(new[] { "candidate", id, "foto_cuenta_bancaria" });
            hasFotoNumeroSeguridadSocial = HelperMethods.ExistsFile(new[] { "candidate", id, "foto_numero_seguridad_social" });
            hasFotoPermisoTrabajo = HelperMethods.ExistsFile(new[] { "candidate", id, "foto_permiso_trabajo" });
            hasLegalRepresentativeConsent = new HasLegalRepresentativeConsent
            {
                hasTutorDni =
                    HelperMethods.ExistsFile(new[] { "candidate", id, "legal_representative_consent", "tutor_anverso" }) &&
                    HelperMethods.ExistsFile(new[] { "candidate", id, "legal_representative_consent", "tutor_reverso" }),
                hasAutorizacion =
                    HelperMethods.ExistsFile(new[] { "candidate", id, "legal_representative_consent", "autorizacion" })
            };
            hasFotoDiscapacidad = HelperMethods.ExistsFile(new[] { "candidate", id, "foto_discapacidad" });
            hasStoredSign = HelperMethods.ExistsFile(new[] { "candidate", id, "stored_sign" });

            periodoGracia = reader.IsDBNull(reader.GetOrdinal("periodoGracia")) ? null : reader.GetDateTime(reader.GetOrdinal("periodoGracia"));
            diasPeriodoGracia = periodoGracia.HasValue ? (periodoGracia.Value - DateTime.Now.Date).Days : 0;


            CheckHasAllDataFilledIn();
        }

        public void ReadDrivingLicense(SqlDataReader reader)
        {
            drivingLicenses.Add(new DrivingLicense
            {
                type = reader.GetString(reader.GetOrdinal("type")),
                expiration = reader.GetDateTime(reader.GetOrdinal("expiration"))
            });
        }
    }
}
