using System;
using System.Security.Cryptography;
using System.Text;

namespace SyZero.Web.Common
{
    /// <summary> 
    /// 加密
    /// </summary> 
    public class AESEncrypt : IEncrypt
    {
        private static readonly byte[] Keys = { 0x41, 0x72, 0x65, 0x79, 0x6F, 0x75, 0x6D, 0x79, 0x53, 0x6E, 0x6F, 0x77, 0x6D, 0x61, 0x6E, 0x3F };

        public string Encrypt(string encryptString, string encryptKey)
        {
            if (encryptString == null)
            {
                throw new ArgumentNullException(nameof(encryptString));
            }

            using var rijndaelProvider = new RijndaelManaged();
            rijndaelProvider.Key = CreateKey(encryptKey);
            rijndaelProvider.IV = Keys;
            using var rijndaelEncrypt = rijndaelProvider.CreateEncryptor();

            byte[] inputData = Encoding.UTF8.GetBytes(encryptString);
            byte[] encryptedData = rijndaelEncrypt.TransformFinalBlock(inputData, 0, inputData.Length);

            return Convert.ToBase64String(encryptedData);
        }

        public string Decrypt(string decryptString, string decryptKey)
        {
            try
            {
                using var rijndaelProvider = new RijndaelManaged();
                rijndaelProvider.Key = CreateKey(decryptKey);
                rijndaelProvider.IV = Keys;
                using var rijndaelDecrypt = rijndaelProvider.CreateDecryptor();

                byte[] inputData = Convert.FromBase64String(decryptString);
                byte[] decryptedData = rijndaelDecrypt.TransformFinalBlock(inputData, 0, inputData.Length);

                return Encoding.UTF8.GetString(decryptedData);
            }
            catch
            {
                return "";
            }
        }

        private static byte[] CreateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            var sourceBytes = Encoding.UTF8.GetBytes(key);
            var keyBytes = new byte[32];
            var copyLength = Math.Min(sourceBytes.Length, keyBytes.Length);
            Array.Copy(sourceBytes, keyBytes, copyLength);
            for (var i = copyLength; i < keyBytes.Length; i++)
            {
                keyBytes[i] = (byte)' ';
            }

            return keyBytes;
        }
    }
}
