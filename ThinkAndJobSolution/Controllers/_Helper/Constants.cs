using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers._Helper
{
    public class Constants
    {
        public struct Pais
        {
            public string nombre { get; set; }
            public string iso2 { get; set; }
            public string iso3 { get; set; }
            public int codigo { get; set; }
            public bool schengen { get; set; }
        };
        public struct Provincia
        {
            public string nombre;
            public List<string> prefijos;
            public List<Localidad> localidades;
        }
        public struct Localidad
        {
            public string nombre;
            public List<string> cps;
        }

        public static bool checkPaisIso3Exists(string iso3, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            bool result = true;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(HelperMethods.CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "SELECT COUNT(*) FROM const_paises WHERE iso3 = @ISO3";

                    command.Parameters.AddWithValue("@ISO3", iso3);

                    result = (int)command.ExecuteScalar() > 0;
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception) { }
            return result;
        }

        public static Pais? getPaisByIso3(string iso3, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            Pais? result = null;
            if (iso3 == null) return result;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(HelperMethods.CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "SELECT * FROM const_paises WHERE iso3 = @ISO3";

                    command.Parameters.AddWithValue("@ISO3", iso3);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = new Pais()
                            {
                                nombre = reader.GetString(reader.GetOrdinal("nombre")),
                                iso2 = reader.GetString(reader.GetOrdinal("iso2")),
                                iso3 = reader.GetString(reader.GetOrdinal("iso3")),
                                codigo = reader.GetInt32(reader.GetOrdinal("codigo")),
                                schengen = reader.GetBoolean(reader.GetOrdinal("schengen"))
                            };
                        }
                    }
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception) { }
            return result;
        }

        public static Pais? getPaisByName(string name, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            Pais? result = null;
            if (name == null) return result;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(HelperMethods.CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "SELECT * FROM const_paises WHERE nombre = @NAME";

                    command.Parameters.AddWithValue("@NAME", name);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = new Pais()
                            {
                                nombre = reader.GetString(reader.GetOrdinal("nombre")),
                                iso2 = reader.GetString(reader.GetOrdinal("iso2")),
                                iso3 = reader.GetString(reader.GetOrdinal("iso3")),
                                codigo = reader.GetInt32(reader.GetOrdinal("codigo")),
                                schengen = reader.GetBoolean(reader.GetOrdinal("schengen"))
                            };
                        }
                    }
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception) { }
            return result;
        }

        public static bool searchCP(string cp, out Provincia provincia, out Localidad localidad, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            bool result = false;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(HelperMethods.CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText =
                        "SELECT L.nombre as lName, P.nombre as pName\n" +
                        "FROM const_localidades_cps LC\n" +
                        "INNER JOIN const_localidades L ON(LC.localidadRef = L.ref)\n" +
                        "INNER JOIN const_provincias P ON(L.provinciaRef = P.ref)\n" +
                        "WHERE LC.cp = @CP";

                    command.Parameters.AddWithValue("@CP", cp);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            provincia = new Provincia()
                            {
                                nombre = reader.GetString(reader.GetOrdinal("pName"))
                            };
                            localidad = new Localidad()
                            {
                                nombre = reader.GetString(reader.GetOrdinal("lName"))
                            };
                            result = true;
                        }
                        else
                        {
                            provincia = new Provincia();
                            localidad = new Localidad();
                        }
                    }
                }
                if (lastConn == null) conn.Close();
            }
            catch (Exception)
            {
                provincia = new Provincia();
                localidad = new Localidad();
            }
            return result;
        }


        public static string validateProvincia(string provincia, SqlConnection lastConn = null, SqlTransaction transaction = null, string backupCP = null)
        {
            string result = null;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(HelperMethods.CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "SELECT nombre FROM const_provincias WHERE nombre = @NOMBRE";
                    command.Parameters.AddWithValue("@NOMBRE", provincia);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = reader.GetString(reader.GetOrdinal("nombre"));
                        }
                    }
                }

                if (result == null && backupCP != null)
                {
                    if (searchCP(backupCP, out Provincia p, out _, conn, transaction))
                    {
                        result = p.nombre;
                    }
                }

                if (lastConn == null) conn.Close();
            }
            catch (Exception)
            {
                result = null;
            }
            return result;
        }

        public static string validateLocalidad(string localidad, SqlConnection lastConn = null, SqlTransaction transaction = null, string backupCP = null)
        {
            string result = null;
            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(HelperMethods.CONNECTION_STRING);
                if (lastConn == null) conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }

                    command.CommandText = "SELECT nombre FROM const_localidades WHERE nombre = @NOMBRE";
                    command.Parameters.AddWithValue("@NOMBRE", localidad);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = reader.GetString(reader.GetOrdinal("nombre"));
                        }
                    }
                }

                if (result == null && backupCP != null)
                {
                    if (searchCP(backupCP, out _, out Localidad l, conn, transaction))
                    {
                        result = l.nombre;
                    }
                }

                if (lastConn == null) conn.Close();
            }
            catch (Exception)
            {
                result = null;
            }
            return result;
        }


        public static FestivoType[] getFestivos(int ano, string provincia, string localidad, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            FestivoType[] festivos = null;

            try
            {
                SqlConnection conn = lastConn ?? new SqlConnection(HelperMethods.CONNECTION_STRING);
                if (lastConn == null) conn.Open();
                string festivosString = null;

                //Intentar obtener los festivos de nivel 3
                if (localidad != null)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }

                        command.CommandText =
                            "SELECT F.festivos " +
                            "FROM const_festivos F " +
                            "INNER JOIN const_localidades L ON(F.localidadRef = L.ref) " +
                            "WHERE F.ano = @ANO AND L.nombre = @LOCALIDAD AND F.nivel = 3";
                        command.Parameters.AddWithValue("@ANO", ano);
                        command.Parameters.AddWithValue("@LOCALIDAD", localidad);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                festivosString = reader.GetString(reader.GetOrdinal("festivos"));
                            }
                        }
                    }
                }

                //Si no hay de nivel 3, buscar de nivel 2
                if (festivosString == null && provincia != null)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }

                        command.CommandText =
                            "SELECT F.festivos " +
                            "FROM const_festivos F " +
                            "INNER JOIN const_provincias P ON(F.provinciaRef = P.ref) " +
                            "WHERE F.ano = @ANO AND P.nombre = @PROVINCIA AND F.nivel = 2";
                        command.Parameters.AddWithValue("@ANO", ano);
                        command.Parameters.AddWithValue("@PROVINCIA", provincia);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                festivosString = reader.GetString(reader.GetOrdinal("festivos"));
                            }
                        }
                    }
                }

                //Si no hay de nivel 2, buscar de nivel 1
                if (festivosString == null)
                {
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        if (transaction != null)
                        {
                            command.Connection = conn;
                            command.Transaction = transaction;
                        }

                        command.CommandText =
                            "SELECT F.festivos " +
                            "FROM const_festivos F " +
                            "WHERE F.ano = @ANO AND F.nivel = 1";
                        command.Parameters.AddWithValue("@ANO", ano);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                festivosString = reader.GetString(reader.GetOrdinal("festivos"));
                            }
                        }
                    }
                }

                if (festivosString != null)
                {
                    festivos = festivosString.Select(d => festivoTypeConverter[d]).ToArray();
                }

                if (lastConn == null) conn.Close();
            }
            catch (Exception)
            {
                festivos = null;
            }

            if (festivos == null)
            {
                festivos = new FestivoType[DateTime.IsLeapYear(ano) ? 366 : 365];
                Array.Fill(festivos, FestivoType.NADA);
            }

            return festivos;
        }

        public static readonly Dictionary<char, FestivoType> festivoTypeConverter = new() { { '0', FestivoType.NADA }, { '1', FestivoType.NACIONAL }, { '2', FestivoType.AUTONOMICA }, { '3', FestivoType.LOCAL } };
        public enum FestivoType
        {
            NADA, NACIONAL, AUTONOMICA, LOCAL
        }


        public static readonly HashSet<string> SCHENGEN_COUNTRIES = new()
        {
            "DEU", "AUT", "BEL", "BGR", "HRV", "DNK", "SVK", "SVN", "ESP", "EST", "FIN", "FRA", "GRC", "HUN", "ISL", "ITA", "LVA", "LIE", "LTU", "LUX", "MLT", "NOR", "NLD", "POL", "PRT", "CZE", "ROU", "SWE", "CHE"
        };

        public static readonly Dictionary<string, Dictionary<string, string>> DEFAULT_SYSCONFIG = new()
        {
            { "anviz-apikey", new(){ {"thinkandjob", "nËJ6zgEjCb*jHú5FFMAÑ2khK"} } },
            { "anviz-endpoint", new(){ {"thinkandjob", "http://anviz.thinkandjob.es" } } },
            { "anvizint-apikey", new(){ {"thinkandjob", "nËJ6zgEjCb*jHú5FFMAÑ2khK"} } },
            { "anvizint-endpoint", new(){ {"thinkandjob", "http://anvizinternal.thinkandjob.es" } } },
            { "clausulas-contratos", new(){ { "thinkandjob", "[]" }, { "imaginefreedom", "[]" } } },
            { "cookies", new(){ {"thinkandjob", "<p class=\"MsoNormal\" style=\"background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt;color:#222222\">Este sitio web,  usa Cookies para mejorar y optimizar la experiencia del usuario. A continuación  encontrarás información detallada sobre qué son las “Cookies”, qué tipología  utiliza este sitio web, cómo puedes desactivarlas en tu navegador y cómo  bloquear específicamente la instalación de Cookies de terceros.<o:p></o:p></span></p>    <p class=\"MsoNormal\" style=\"background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><b><span style=\"font-size:11.0pt;color:black;mso-color-alt:  windowtext\">¿Qué son las Cookies y cómo las utilizan los sitios web del CLIENTE?</span></b><b><span style=\"font-size:11.0pt\"><o:p></o:p></span></b></p>    <p class=\"MsoNormal\" style=\"background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt;color:#222222\">Las Cookies son  archivos que el sitio web o la aplicación que utilizas instala en tu navegador  o en tu dispositivo (Smartphone, tableta……) durante tu recorrido por las  páginas o por la aplicación, y sirven para almacenar información sobre tu  visita. Como la mayoría de los sitios en internet, los portales web del&nbsp;</span><b><span style=\"font-size:11.0pt;color:black;mso-color-alt:windowtext\">CLIENTE </span></b><span style=\"font-size:11.0pt;color:#222222\">&nbsp;utilizan Cookies para:<o:p></o:p></span></p>    <ul type=\"disc\">   <li class=\"MsoNormal\" style=\"color: rgb(34, 34, 34); background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt\">Asegurar que las páginas web pueden funcionar       correctamente<o:p></o:p></span></li>   <li class=\"MsoNormal\" style=\"color: rgb(34, 34, 34); background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt\">Almacenar sus preferencias, como el idioma que       has seleccionado o el tamaño de letra.<o:p></o:p></span></li>   <li class=\"MsoNormal\" style=\"color: rgb(34, 34, 34); background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt\">Conocer tu experiencia de navegación.<o:p></o:p></span></li>   <li class=\"MsoNormal\" style=\"color: rgb(34, 34, 34); background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt\">Recopilar información estadística anónima, como       qué páginas has visto o cuánto tiempo has estado en nuestros medios.<o:p></o:p></span></li>  </ul>    <p class=\"MsoNormal\" style=\"background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt;color:#222222\">El uso de  Cookies nos permite optimizar su navegación, adaptando la información y los  servicios ofrecidos a tus intereses, para proporcionarte una mejor experiencia  siempre que nos visites. Los sitios web del <b>CLIENTE</b>&nbsp;utilizan Cookies para funcionar, adaptar y facilitar  al máximo la navegación del Usuario.<o:p></o:p></span></p>    <p class=\"MsoNormal\" style=\"background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt;color:#222222\">Las Cookies se  asocian únicamente a un usuario anónimo y su ordenador/dispositivo y no  proporcionan referencias que permitan conocer datos personales. En todo momento  podrás acceder a la configuración de tu navegador para modificar y/o bloquear  la instalación de las Cookies enviadas por los sitios web del <b>CLIENTE</b>, sin que ello impida al acceso  a los contenidos. Sin embargo, la calidad del funcionamiento de los Servicios  puede verse afectada.<o:p></o:p></span></p>    <p class=\"MsoNormal\" style=\"background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:11.0pt;color:#222222\">Los Usuarios que  completen el proceso de registro o hayan iniciado sesión con sus datos de  acceso podrán acceder a servicios personalizados y adaptados a sus preferencias  según la información personal suministrada en el momento del registro y la  almacenada en la Cookie de su navegador.<o:p></o:p></span></p>    <p class=\"MsoNormal\"><span style=\"font-size:11.0pt\">&nbsp;</span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <p class=\"MsoListParagraph\" style=\"text-indent:-18.0pt;mso-list:l1 level1 lfo1\"><!--[if !supportLists]--><span style=\"font-family:Symbol;mso-fareast-font-family:Symbol;mso-bidi-font-family:  Symbol\">·<span style=\"font-variant-numeric: normal; font-variant-east-asian: normal; font-stretch: normal; font-size: 7pt; line-height: normal; font-family: &quot;Times New Roman&quot;;\">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;  </span></span><!--[endif]--><span style=\"font-family:&quot;Times New Roman&quot;,serif\">La  cookies de autenticación son por defecto cookies temporales (por sesiones) no  persistentes, sin embargo, los usuarios pueden hacerla persistente marcando la  casilla \"Remember login\" en el control de acceso. Para usuarios  registrados.<o:p></o:p></span></p>    <p class=\"MsoNormal\"><span style=\"font-size:11.0pt\">&nbsp;</span></p>    <p class=\"MsoListParagraph\" style=\"text-indent:-18.0pt;mso-list:l1 level1 lfo1\"><!--[if !supportLists]--><span style=\"font-family:Symbol;mso-fareast-font-family:Symbol;mso-bidi-font-family:  Symbol\">·<span style=\"font-variant-numeric: normal; font-variant-east-asian: normal; font-stretch: normal; font-size: 7pt; line-height: normal; font-family: &quot;Times New Roman&quot;;\">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;  </span></span><!--[endif]--><span style=\"font-family:&quot;Times New Roman&quot;,serif\">Las  cookies de sesión, que se producen al loguearse tienen una duración de 30  minutos desde último período de actividad. Para usuarios registrados.<o:p></o:p></span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <p class=\"MsoListParagraph\" style=\"text-indent:-18.0pt;mso-list:l1 level1 lfo1\"><!--[if !supportLists]--><span style=\"font-family:Symbol;mso-fareast-font-family:Symbol;mso-bidi-font-family:  Symbol\">·<span style=\"font-variant-numeric: normal; font-variant-east-asian: normal; font-stretch: normal; font-size: 7pt; line-height: normal; font-family: &quot;Times New Roman&quot;;\">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;  </span></span><!--[endif]--><span style=\"font-family:&quot;Times New Roman&quot;,serif\">Se  crea una cookie llamada \"lenguaje\" para almacenar el lenguaje - en  una instalación monolingües esto es simplemente el idioma por defecto del navegador,  pero si el sitio es compatible con varios idiomas, entonces puede ser diferente  según el idioma seleccionado. Para todo tipos de usuarios.<o:p></o:p></span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">TITULAR&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; NOMBRE DE LA COOKIE&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; FUNCIÓN<o:p></o:p></span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <p class=\"MsoListParagraph\"><span style=\"font-family:&quot;Times New Roman&quot;,serif\">&nbsp;</span></p>    <ul type=\"disc\">   <li class=\"MsoNormal\" style=\"background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><b><span style=\"font-size:11.0pt;       color:black;mso-color-alt:windowtext\">¿Cómo puedo configurar mis Cookies?</span></b><b><span style=\"font-size:11.0pt\"><o:p></o:p></span></b></li>   <li class=\"MsoNormal\" style=\"color: rgb(34, 34, 34); background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:       11.0pt\">Al navegar y continuar en nuestro Sitio Web estará consintiendo el       uso de las Cookies en las condiciones contenidas en la presente Política       de </span><span style=\"font-size:11.0pt;color:black;mso-color-alt:windowtext\">Cookies.&nbsp;<b>CLIENTE</b></span><span style=\"font-size:11.0pt\">&nbsp;proporciona acceso a esta Política de       Cookies en el momento del registro con el objetivo de que el usuario esté       informado, y sin perjuicio de que éste pueda ejercer su derecho a       bloquear, eliminar y rechazar el uso de Cookies en todo momento.<o:p></o:p></span></li>   <li class=\"MsoNormal\" style=\"color: rgb(34, 34, 34); background-image: initial; background-position: initial; background-size: initial; background-repeat: initial; background-attachment: initial; background-origin: initial; background-clip: initial;\"><span style=\"font-size:       11.0pt\">En cualquier caso le informamos de que, dado que las Cookies no       son necesarias para el uso de nuestro Sitio Web, puede bloquearlas o       deshabilitarlas activando la configuración de su navegador, que le permite       rechazar la instalación de todas las cookies o de algunas de ellas. La       práctica mayoría de los navegadores permiten advertir de la presencia de       Cookies o rechazarlas automáticamente. Si las rechaza podrá seguir usando       nuestro Sitio Web, aunque el uso de algunos de sus servicios podrá ser       limitado y por tanto su experiencia en nuestro Sitio Web menos       satisfactoria.<o:p></o:p></span></li>  </ul>    <p class=\"MsoNormal\" style=\"mso-margin-top-alt:auto;mso-margin-bottom-alt:auto;  text-align:justify;mso-outline-level:3\"><b><span style=\"font-size:11.0pt;  color:black\">¿Cómo puedo bloquear o permitir las cookies?<o:p></o:p></span></b></p>    <p class=\"MsoNormal\" style=\"mso-margin-top-alt:auto;mso-margin-bottom-alt:auto;  text-align:justify\"><a name=\"configuracion\"></a><span style=\"font-family:&quot;Verdana&quot;,sans-serif;  color:black\">El usuario podrá -en cualquier momento- elegir qué cookies quiere  que funcionen en este sitio web mediante:<o:p></o:p></span></p>    <ul type=\"disc\">   <li class=\"MsoNormal\" style=\"color:black;mso-margin-top-alt:auto;mso-margin-bottom-alt:       auto;text-align:justify;mso-list:l1 level1 lfo1\"><span style=\"font-family:       &quot;Verdana&quot;,sans-serif\">la configuración del&nbsp;<b>navegador</b>; por       ejemplo:<o:p></o:p></span></li>   <ul type=\"circle\">    <li class=\"MsoNormal\" style=\"color:black;mso-margin-top-alt:auto;mso-margin-bottom-alt:        auto;text-align:justify;mso-list:l1 level2 lfo1\"><b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:EN-US\">Chrome</span></b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:        EN-US\">, desde&nbsp;<a href=\"http://support.google.com/chrome/bin/answer.py?hl=es&amp;answer=95647\" target=\"_blank\"><span style=\"color:blue\">http://support.google.com/chrome/bin/answer.py?hl=es&amp;answer=95647</span></a><o:p></o:p></span></li>    <li class=\"MsoNormal\" style=\"color:black;mso-margin-top-alt:auto;mso-margin-bottom-alt:        auto;text-align:justify;mso-list:l1 level2 lfo1\"><b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:EN-US\">Explorer</span></b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:        EN-US\">, desde&nbsp;<a href=\"http://windows.microsoft.com/es-es/windows7/how-to-manage-cookies-in-internet-explorer-9\" target=\"_blank\"><span style=\"color:blue\">http://windows.microsoft.com/es-es/windows7/how-to-manage-cookies-in-internet-explorer-9</span></a><o:p></o:p></span></li>    <li class=\"MsoNormal\" style=\"color:black;mso-margin-top-alt:auto;mso-margin-bottom-alt:        auto;text-align:justify;mso-list:l1 level2 lfo1\"><b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:EN-US\">Firefox</span></b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:        EN-US\">, desde&nbsp;<a href=\"http://support.mozilla.org/es/kb/habilitar-y-deshabilitar-cookies-que-los-sitios-we\" target=\"_blank\"><span style=\"color:blue\">http://support.mozilla.org/es/kb/habilitar-y-deshabilitar-cookies-que-los-sitios-we</span></a><o:p></o:p></span></li>    <li class=\"MsoNormal\" style=\"color:black;mso-margin-top-alt:auto;mso-margin-bottom-alt:        auto;text-align:justify;mso-list:l1 level2 lfo1\"><b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:EN-US\">Safari</span></b><span lang=\"EN-US\" style=\"font-family:&quot;Verdana&quot;,sans-serif;mso-ansi-language:        EN-US\">, desde&nbsp;<a href=\"http://support.apple.com/kb/ph5042\" target=\"_blank\"><span style=\"color:blue\">http://support.apple.com/kb/ph5042</span></a><o:p></o:p></span></li>   </ul>   <li class=\"MsoNormal\" style=\"color:black;mso-margin-top-alt:auto;mso-margin-bottom-alt:       auto;text-align:justify;mso-list:l1 level1 lfo1\"><span style=\"font-family:       &quot;Verdana&quot;,sans-serif\">Existen herramientas de terceros, disponibles on       line, que permiten a los usuarios detectar las cookies en cada sitio web       que visita y gestionar su desactivación.<o:p></o:p></span></li>  </ul>" } } },
            { "tarifa-max", new(){ { "thinkandjob", "200" }, { "imaginefreedom", "55" } } },
            { "tarifa-min", new(){ { "thinkandjob", "195" }, { "imaginefreedom", "47" } } },
            { "terms", new(){ {"thinkandjob", "<div style=\"mso-element:para-border-div;border:none;border-bottom:solid windowtext 1.5pt;  padding:0cm 0cm 1.0pt 0cm\"><p class=\"MsoNormal\" align=\"center\" style=\"margin-bottom:0cm;text-align:center;  line-height:normal;border:none;mso-border-bottom-alt:solid windowtext 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><img src=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAosAAACeCAYAAABabkAWAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsIAAA7CARUoSoAAABjdSURBVHhe7d0/zBTFH8fx05YCtNBEA5gYoHvAQixMgESlAxqxUrDQDmzQ7oFCLeFXqJ0Y/0YTeRo1Vhij0phHGylMfKRArbRR7Gh8fvs5Z8O5zOx9Z2f2z92+X8nE5/Du9m53Zva7szPfu2OzMAEAAAA87nT/BQAAAG5DsAgAAIAggkUAAAAEESwCAAAgiGARAAAAQQSLAAAACCJYBAAAQBDBIgAAAIIIFgEAABBEsAgAAIAggkUAAAAEESwCAAAgiGARAAAAQQSLAAAACCJYBAAAQBDBIgAAAIIIFgEAABBEsAgAAIAggkUAAAAEESwCAAAgiGARAAAAQQSLAAAACCJYBAAAQBDBIgAAAIIIFgEAABBEsAgAAIAggkUAAAAEESwCAAAgiGARAAAAQQSLAAAACCJYBAAAQBDBIgAAAIIIFgEAABBEsAgAAIAggkUAAAAEESwCAAAgiGARAAAAQQSLAAAACCJYBAAAQBDBIgAAAIIIFgEAABBEsAgAAIAggkUAAAAEESwCAAAgiGARAAAAQQSLAAAACCJYBAAAQBDBIgAAAIIIFgEAABBEsAgAAIAggkUAAAAEESwCAAAgiGARAAAAQQSLAAAACCJYBAAAQNAdmwX3NwL++OOPydWrV92j5fL444+7v275+eefJ7/88ot7VG9lZWVyzz33uEfNWfbx1q1bJw8//LB7dMt33303uXHjhns0HDt37pzs2rXLPbrlww8/dH+F6bhU92tMPQxtu4kvvvjC/RXmq0e5+T5HqE4sCktby3ks29JlH7kI+yOkWodz9Z9DZO2Xu2rDfZzXloqCRdhdunRp89SpU5s7duxQkL3wxWd1ddX7XF85evSoe1Way5cve99/thw4cMA9+7/0777n9120H318z62Wa9euuWffYtlHZdm7d697VTrf+1dLbr///vvmhQsXpvVr27Zt3m3OFn3f06dPb66trbl3WAyWthaqR0MSUzdzFNWJEydOTLc7VOvr65tnz56d1k3fd5gtRQA8/T6LVn/rxPTLXRzHgwcPerftK0OuV33hNnSkJ598cvL6669Pr1CKCjW9AhmzTz/91DTyhG798MMPkyLYco8Wh+rSsWPHJvfee+/kzJkz0/r1119/uf8bpu+rdqn2edddd01Onjw5HUnAclKdeO+99yZPPPHE5NChQ4PpgzTC+sILL0zr4P79+yevvPLKtG7Oo/OJvo/q7x133DFtA2PqV5977jn3VzvUF3799dfuEZogWEygW2/qCN58883pUPpYtd3Ql8327dvdX+169dVXFyZg0olRJ32d/BUgpigDid27d0+DRp3AsbwUBKjenDt3zv1L91THtP09e/ZML1osFzh11Ab0ncZy0aNg+ezZs+5RXjo26guRhmAxAwVLGxsbox1lbLOhozmdsF588UX3aJjKkRidGNu48lfQqBP4xYsX3b9gWWkUT8FV11S3VMe0/dQgsaq86FEbWXZvvPFGK4Gx+sDcx2WMCBYz0WRYjTI+88wz7l/GZZFGscZkyNMENAH+8OHD05GYNulE8fzzzzPKOAIKrk6fPu0etU9BnOpW28GI2si+ffuWuv6W7TQn9X3vv/++e4QUBIuZqbM6cuSIezQuuRs68tDI99BOMmWgaJnPlYvaprZJwLjcNEK1trbmHrVDdUjzCtu+0JmltqJ5kGo7y0p3F3IeO6ZI5UOw2ALdlhjjLWk1dG73DY+mCXR5UpunDBT7uDWkE+4333zjHmFZ6cK1rYsCva/qb+rc2ibKhTDLLNex09Qo7S/kQbDYAt2SbvvKdqheeuklRm4GSNMEhjAi0WegKFqMphWnWG6qX21cIJWBYpcj4rM0zWlIF35t0LHT/M8UmhLFopa8RpeUW5Uodg7DgQMHGiWC1ZWNtcJqNfXevXvdo+74FhXEfG6fU6dORXdomluiRQ51dBx8n1dzlJomBLaMMmm7Tahj990G2bFjx+S3335zj/yuXbs2efDBB92jf1n2UZ2DBw9OvvrqK/fITqk85rF0IzrR6jZa06t9XxuJGSVUoDi021KWtra6upp08tR+L9uHkiSXmRtyJlK31M0cfZyCNGsCfvXZ169fd4/y0LzXlJG9al8S833Un+QaVdR5sNoOcyc7V3+TMoq/vr7eOFm3MiukLJhTWrwufmhgoShYHBMl29TXblKUOPXixYvuneZTYuGig/S+V7UogelQxCTlDpXYpKaW49LGPvJtp1py2759u3c7syU1KXeoFAGTezc73/tUi4USbPteW1dWVlY2z58/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\" style=\"background-color: transparent; width: 100%;\"></p><p class=\"MsoNormal\" align=\"center\" style=\"margin-bottom:0cm;text-align:center;  line-height:normal;border:none;mso-border-bottom-alt:solid windowtext 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:18.0pt;mso-bidi-font-family:&quot;Open Sans&quot;\">Aviso  legal<o:p></o:p></span></b></p>    <p class=\"MsoNormal\" align=\"center\" style=\"margin-bottom:0cm;text-align:center;  line-height:normal;border:none;mso-border-bottom-alt:solid windowtext 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:12.0pt;mso-bidi-font-family:&quot;Open Sans&quot;\">&nbsp;</span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">El presente aviso legal (en adelante, el \"Aviso Legal\")  regula el uso del servicio del portal de Internet www.thinkandjob.com (en  adelante, el \"Web\") de ROYAL HECROVA, S.L. con domicilio social en C/  BAZA, 9, H, 18220, ALBOLOTE, GRANADA con CIF B42810572. <o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;;color:#049CC1\">Legislación<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">Con carácter general las relaciones entre <a name=\"_Hlk512360002\">ROYAL  HECROVA, S.L. </a>con los Usuarios de sus servicios telemáticos, presentes en  la web, se encuentran sometidas a la legislación y jurisdicción españolas.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">Las partes renuncian expresamente al fuero que les pudiera  corresponder y someten expresamente a los Juzgados y Tribunales de JUN para  resolver cualquier controversia que pueda surgir en la interpretación o  ejecución de las presentes condiciones contractuales.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Contenido y uso<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">El Usuario queda informado, y acepta, que el acceso a la presente  web no supone, en modo alguno, el inicio de una relación comercial con ROYAL  HECROVA, S.L..<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">El titular del web no se identifica con las opiniones vertidas en  el mismo por sus colaboradores. La Empresa se reserva el derecho de efectuar  sin previo aviso las modificaciones que considere oportunas en su Web, pudiendo  cambiar, suprimir o añadir tanto los contenidos y servicios que se presten a  través de la misma como la forma en la que éstos aparezcan presentados o  localizados en sus servidores.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Propiedad intelectual e industrial<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">Los derechos de propiedad intelectual del contenido de las páginas  web, su diseño gráfico y códigos son titularidad de ROYAL HECROVA, S.L. y, por  tanto, queda prohibida su reproducción, distribución, comunicación pública,  transformación o cualquier otra actividad que se pueda realizar con los  contenidos de sus páginas web ni aun citando las fuentes, salvo consentimiento  por escrito de ROYAL HECROVA, S.L.. Todos los nombres comerciales, marcas o  signos distintos de cualquier clase contenidos en las páginas web de la Empresa  son propiedad de sus dueños y están protegidos por ley.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Enlaces (Links)<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:10.0pt;font-family:  &quot;Open Sans&quot;,sans-serif;color:#767171;mso-themecolor:background2;mso-themeshade:  128\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">La presencia de enlaces (links) en las páginas web de ROYAL  HECROVA, S.L. tiene finalidad meramente informativa y en ningún caso supone  sugerencia, invitación o recomendación sobre los mismos.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Confidencialidad y Protección de Datos<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">A efecto de lo previsto en RGPD de 27 de abril de 2016, ROYAL  HECROVA, S.L. informa al Usuario de la existencia de un tratamiento  automatizado de datos de carácter personal creado por y para ROYAL HECROVA,  S.L. y bajo su responsabilidad, con la finalidad de realizar el mantenimiento y  gestión de la relación con el Usuario, así como las labores de información. En  el momento de la aceptación de las presentes condiciones generales, ROYAL  HECROVA, S.L. precisará del Usuario la recogida de unos datos imprescindibles  para la prestación de sus servicios.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Registro de ficheros y formularios<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">La cumplimentación del formulario de registro es obligatoria para  acceder y disfrutar de determinados servicios ofrecidos en la web. El no  facilitar los datos personales solicitados o el no aceptar la presente política  de protección de datos supone la imposibilidad de suscribirse, registrarse o  participar en cualquiera de las promociones en las que se soliciten datos  carácter personal.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">A efecto de lo previsto en RGPD de 27 de abril de 2016, le  informamos que los datos personales que se obtengan como consecuencia de su  registro como Usuario, serán incorporados a un fichero titularidad de ROYAL  HECROVA, S.L. con C.I.F B42810572 y domicilio en C/ PEDRO DE MENA, 7, JUN,  18213, GRANADA, teniendo implementadas las medidas de seguridad establecidas en  el Real Decreto 1720/2007, de 11 de junio.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Exactitud y veracidad de los datos facilitados<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">El Usuario es el único responsable de la veracidad y corrección de  los datos incluidos, exonerándose ROYAL HECROVA, S.L. de cualquier  responsabilidad al respecto. Los usuarios garantizan y responden, en cualquier  caso, de la exactitud, vigencia y autenticidad de los datos personales  facilitados, y se comprometen a mantenerlos debidamente actualizados. El  usuario acepta proporcionar información completa y correcta en el formulario de  registro o suscripción. ROYAL HECROVA, S.L. no responde de la veracidad de las  informaciones que no sean de elaboración propia y de las que se indique otra  fuente, por lo que tampoco asume responsabilidad alguna en cuanto a hipotéticos  perjuicios que pudieran originarse por el uso de dicha información. &nbsp;ROYAL HECROVA, S.L. se reserva el derecho a  actualizar, modificar o eliminar la información contenida en sus páginas web  pudiendo incluso limitar o no permitir el acceso a dicha información. Se  exonera a ROYAL HECROVA, S.L. de responsabilidad ante cualquier daño o  perjuicio que pudiera sufrir el Usuario como consecuencia de errores, defectos  u omisiones, en la información facilitada por ROYAL HECROVA, S.L. siempre que  proceda de fuentes ajenas a ROYAL HECROVA, S.L..<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Cookies<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">El sitio www.thinkandjob.com no utiliza cookies, considerando tales  ficheros físicos de información alojados en el propio terminal del usuario y  sirven para facilitar la navegación del usuario por el portal. De todas formas,  el usuario tiene la posibilidad de configurar el navegador de tal modo que  impida la instalación de estos archivos.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Finalidades<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">Las finalidades de [NOMBRE_EMPRESA] son el mantenimiento y gestión  de la relación con el Usuario, así como las labores de información.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Menores de edad<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">En el supuesto que algunos de nuestros servicios vayan dirigidos  específicamente a menores de edad, ROYAL HECROVA, S.L. solicitará la  conformidad de los padres o tutores para la recogida de los datos personales o,  en su caso, para el tratamiento automatizado de los datos.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Cesión de datos a terceros<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">ROYAL HECROVA, S.L. no realizará cesión de datos de los usuarios a  terceros.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-family:&quot;Open Sans&quot;;  color:#049CC1\">Ejercicio de derechos de acceso, rectificación, cancelación y  oposición<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">Podrá dirigir sus comunicaciones y ejercitar los derechos de  acceso, rectificación, supresión, limitación, portabilidad y oposición en la  dirección de Internet a www.thinkandjob.com o bien por correo ordinario dirigido  a ROYAL HECROVA, S.L., Ref. RGPD, en C/ PEDRO DE MENA, 7, JUN, 18213, GRANADA.  Para ejercer dichos derechos es necesario que usted acredite su personalidad  frente a ROYAL HECROVA, S.L. mediante el envío de fotocopia de Documento  Nacional de Identidad o cualquier otro medio válido en Derecho. No obstante, la  modificación o rectificación de sus datos de registro se podrá realizar en el  propio Site identificándose, previamente, con su usuario y contraseña.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;;color:#049CC1\">Medidas de seguridad<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">ROYAL HECROVA, S.L. &nbsp;&nbsp;ha  adoptado los niveles de seguridad de protección de los Datos Personales  legalmente requeridos, y procuran instalar aquellos otros medios y medidas  técnicas adicionales a su alcance para evitar la pérdida, mal uso, alteración, acceso  no autorizado y robo de los Datos Personales facilitados a ROYAL HECROVA, S.L. no  será responsable de posibles daños o perjuicios que se pudieran derivar de  interferencias, omisiones, interrupciones, virus informáticos, averías  telefónicas o desconexiones en el funcionamiento operativo de este sistema  electrónico, motivadas por causas ajenas a ROYAL HECROVA, S.L.; de retrasos o  bloqueos en el uso del presente sistema electrónico causados por deficiencias o  sobrecargas de líneas telefónicas o sobrecargas en el Centro de Procesos de  Datos, en el sistema de Internet o en otros sistemas electrónicos, así como de  daños que puedan ser causados por terceras personas mediante intromisiones  ilegítimas fuera del control de ROYAL HECROVA, S.L.. Ello, no obstante, el  Usuario debe ser consciente de que las medidas de seguridad en Internet no son  inexpugnables.<o:p></o:p></span></p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><div style=\"mso-element:para-border-div;border:none;border-bottom:solid #049CC1 1.5pt;  padding:0cm 0cm 1.0pt 0cm\">    <p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal;border:none;mso-border-bottom-alt:solid #049CC1 1.5pt;  padding:0cm;mso-padding-alt:0cm 0cm 1.0pt 0cm\"><b><span style=\"font-size:14.0pt;mso-bidi-font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;;color:#049CC1\">Aceptación y consentimiento<o:p></o:p></span></b></p>    </div><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">&nbsp;</span></p><p class=\"MsoNormal\" align=\"center\">      </p><p class=\"MsoNormal\" style=\"margin-bottom:0cm;text-align:justify;text-justify:  inter-ideograph;line-height:normal\"><span style=\"font-size:12.0pt;mso-bidi-font-family:  &quot;Open Sans&quot;\">El usuario declara haber sido informado de las condiciones sobre  protección de datos personales, aceptando y consintiendo el tratamiento  automatizado de los mismos por parte de ROYAL HECROVA, S.L., en la forma y para  las finalidades indicadas en la presente Política de Protección de Datos  Personales. Ciertos servicios prestados en el Portal pueden contener  condiciones particulares con previsiones específicas en materia de protección  de Datos Personales.<o:p></o:p></span></p>" } } },
            { "woffice-apikey", new(){ {"thinkandjob", "nËJ6zgEjCb*jHú5FFMAÑ2khK" } } },
            { "woffice-endpoint", new(){ {"thinkandjob", "http://woffice.thinkandjob.es" } } },
            { "face-extractor-cd", new(){ {"thinkandjob", "15"} } },
            { "bases-cotizacion", new(){ {"thinkandjob", @"{""sm"":1166.7,""cm"":4139.4,""cct"":4.7,""cce"":23.6,""dt"":1.6,""de"":6.7,""fe"":0.2,""fot"":0.1,""foe"":0.6,""aee"":2.75,""pnc"":1.55,""het"":4.7,""hee"":23.6,""bie"":2,""pcmun"":27.53,""iva"":21}" } } }
        };
    }
}
