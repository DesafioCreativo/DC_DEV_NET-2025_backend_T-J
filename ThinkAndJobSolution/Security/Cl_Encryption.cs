using System.Security.Cryptography;
using System.Text;

namespace ThinkAndJobSolution.Security
{
    public class Cl_Encryption : ICl_Encryption
    {
        private static string EncryptionKey = "SB@2022@DEVSB";
        private const string initVector = "dev@sharebusines";
        private const int keysize = 256;
        //--
        public Cl_Encryption(IConfiguration configuration)
        {
            //EncryptionKey = RijndaelDecryptString(configuration["AppSettings:EncryptionKey"]);
        }
        //--
        public string RijndaelDecryptString(string cipherText)
        {
            string passPhrase = EncryptionKey;
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);           

            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            var symmetricKey = Aes.Create();
            symmetricKey.Mode = CipherMode.CBC;

            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
            using (var memoryStream = new MemoryStream(cipherTextBytes))
            {
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        cryptoStream.CopyTo(resultStream);  // Copiamos todo el contenido del stream
                        byte[] plainTextBytes = resultStream.ToArray();
                        return Encoding.UTF8.GetString(plainTextBytes);
                    }
                }
            }           
        }

        public string RijndaelEncryptString(string plainText)
        {
            string passPhrase = EncryptionKey;
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            var symmetricKey = Aes.Create();

            symmetricKey.Mode = CipherMode.CBC;

            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);

            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] cipherTextBytes = memoryStream.ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(cipherTextBytes);
        }
    }
}
