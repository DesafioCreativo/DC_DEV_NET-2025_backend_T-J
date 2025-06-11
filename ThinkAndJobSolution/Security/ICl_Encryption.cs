namespace ThinkAndJobSolution.Security
{
    public interface ICl_Encryption
    {
        string RijndaelEncryptString(string plainText);
        string RijndaelDecryptString(string cipherText);
    }
}
