using System;
using static System.Environment;

namespace ThinkAndJobSolution.Controllers._Helper
{
    public class InstallationConstants
    {
        public static readonly string SECRET_KEY_XOR = "/Q!{KQ[#Q9Zk^rkH-Z,X6Df2";
        public static readonly string PDF_TO_TEXT = OSVersion.Platform == PlatformID.Win32NT ? "./Programs/pdftotext.exe" : "./Programs/pdftotext";
        public static readonly string NIF_REPRESENTANTE = "47021446Q";

        //CONECCIONES LOCALES
        public static readonly string CONNECTION_STRING = "Data Source=3.148.234.194;Initial Catalog=thinkandjob_db;User ID=sa;Password=hS4f9gXKqf12CdHHHGwFqHnhy4IanXBW;TrustServerCertificate=True"; //TODO: Cambiar        
        /*public static readonly string CONNECTION_STRING = "Data Source=DESKTOP-CM5I9HA\\MSSQLSERVER01;Initial Catalog=thinkandjob_db;Integrated Security=True;Encrypt=False";*/ //TODO: Cambiar        

        public static readonly string SUGGESTIONS_AND_COMPLAINTS_EMAIL = "buzon@rentingjob.es";
        public static readonly string CARTERO_CONNECTION_STRING = "Data Source=DESKTOP-GRJJF16\\MSSQLSERVER01;Initial Catalog=RJCartero;Integrated Security=True;TrustServerCertificate=True"; //TODO: Cambiar  LOG PARA ENVIAR CORREOS DESPUES
        public static readonly string ANVIZ_CONNECTION_STRING = "Data Source=DESKTOP-GRJJF16\\MSSQLSERVER01;Initial Catalog=RJCarteroAnviz;Integrated Security=True;TrustServerCertificate=True"; //TODO: Cambiar SISTEMA BIOMETRICO EN ESPAÑA, TODOS LOS SERVICIOS ANVIZ NO SE VAN A UTILIZAR -- ESTO PARA SUDAMERICA NO FUNCIONA
        public static readonly string LOGS_CL_CONNECTION_STRING = "Data Source=DESKTOP-GRJJF16\\MSSQLSERVER01;Initial Catalog=RJLogsCL;Integrated Security=True;TrustServerCertificate=True"; //TODO: Cambiar BASE DE DATOS DE LOGS- POR VERIFICAR SI LO SIGUEN USANDO

        //CONECCIONES ESCRITORIO REMOTO ERNESTO
        //public static readonly string CONNECTION_STRING = "Data Source=DESKTOP-250F0K5;Initial Catalog=thinkandjob_db;Integrated Security=True;TrustServerCertificate=True"; //TODO: Cambiar        
        //public static readonly string SUGGESTIONS_AND_COMPLAINTS_EMAIL = "buzon@rentingjob.es";
        //public static readonly string CARTERO_CONNECTION_STRING = "Data Source=DESKTOP-250F0K5;Initial Catalog=RJCartero;Integrated Security=True;TrustServerCertificate=True"; //TODO: Cambiar  LOG PARA ENVIAR CORREOS DESPUES
        //public static readonly string ANVIZ_CONNECTION_STRING = "Data Source=DESKTOP-250F0K5;Initial Catalog=RJCarteroAnviz;Integrated Security=True;TrustServerCertificate=True"; //TODO: Cambiar SISTEMA BIOMETRICO EN ESPAÑA, TODOS LOS SERVICIOS ANVIZ NO SE VAN A UTILIZAR -- ESTO PARA SUDAMERICA NO FUNCIONA
        //public static readonly string LOGS_CL_CONNECTION_STRING = "Data Source=DESKTOP-250F0K5;Initial Catalog=RJLogsCL;Integrated Security=True;TrustServerCertificate=True"; //TODO: Cambiar BASE DE DATOS DE LOGS- POR VERIFICAR SI LO SIGUEN USANDO


        //public static readonly string PUBLIC_URL = "https://rentingjob.local:44313";
        public static readonly string PUBLIC_URL = "https://jobtojob.local:44313";

        public static readonly string WIN_PYTHON_OSFFICE = "\"C:\\Program Files (x86)\\OpenOffice 4\\program\\python.exe\"";
        public static readonly string WIN_UNICONV = "\"C:\\Program Files(x86)\\OpenOffice 4\\program\\unoconv-0.8.2\\unoconv-0.8.2\\unoconv\"";
        public static readonly string UNIX_UNICONV = "/usr/bin/unoconv";

        public static readonly string BACKUP_DEFAULT_DIR = @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\";
        public static readonly string BACKUP_TARGET_DIR = @"C:\Users\israe\Downloads"; //TODO: Cambiar

        public static readonly string FOLDER_SPLITER = OSVersion.Platform == PlatformID.Win32NT ? "\\" : "/";
        public static readonly bool WINPLATFORM = OSVersion.Platform == PlatformID.Win32NT;



        /*
         #elif Test

            public static readonly string CONNECTION_STRING = "Server=b0ve.com;Initial Catalog=RentingJobDB;User Id=sa;Password=arancelesAlContrabandoIlegal*;";

            public static readonly string PUBLIC_URL = "https://rentingjob.b0ve.com";

            public static readonly string EMAIL_FROM = "b0ve@b0ve.com";
            public static readonly string EMAIL_FROM_PASS = "3AS9Lvw5E9N5aQT";
            public static readonly string EMAIL_FROM_NAME = "B0vE";
            public static readonly string EMAIL_REPLY = "borja@b0ve.com";
            public static readonly string EMAIL_REPLY_NAME = "B0vE";
            public static readonly string EMAIL_SMTP_SERVER = "mail.noip.com";
            public static readonly int EMAIL_SMTP_PORT = 587;

            public static readonly string BACKUP_DEFAULT_DIR = "/var/opt/mssql/data/";
            public static readonly string BACKUP_TARGET_DIR = "/home/b0ve/bdbackups/";

    #elif Prod

            public static readonly string CONNECTION_STRING = "Server=localhost;Initial Catalog=RentingJobDB;User Id=sa;Password=^G/GB49pn-qmCw~A;";

            public static readonly string PUBLIC_URL = "https://rentingjob.es";

            public static readonly string EMAIL_FROM = "";
            public static readonly string EMAIL_FROM_PASS = "";
            public static readonly string EMAIL_FROM_NAME = "";
            public static readonly string EMAIL_REPLY = "";
            public static readonly string EMAIL_REPLY_NAME = "";
            public static readonly string EMAIL_SMTP_SERVER = "";
            public static readonly int EMAIL_SMTP_PORT = 587;

            public static readonly string BACKUP_DEFAULT_DIR = "/var/opt/mssql/data/";
            public static readonly string BACKUP_TARGET_DIR = "/root/backups/";

    #endif
         */

    }
}
