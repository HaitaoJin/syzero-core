using System;
using System.Security.Cryptography;
using System.Text;

namespace SyZero.Web.Common
{
    public class SyEncode : ISyEncode
    {
        public string Decrypt(string Text, string sKey, EncryptType typecs)
        {
            var encrypt = CreateEncryptor(typecs);
            return encrypt.Decrypt(Text, sKey);
        }

        public string DecryptForWeChatApplet(string encryptedData, string sessionKey, string iv)
        {
            var decryptBytes = Convert.FromBase64String(encryptedData);
            var keyBytes = Convert.FromBase64String(sessionKey);
            var ivBytes = Convert.FromBase64String(iv);
            var outputBytes = DecryptByAesBytes(decryptBytes, keyBytes, ivBytes);
            return Encoding.UTF8.GetString(outputBytes);
        }

        public string Encrypt(string Text, string sKey, EncryptType typecs)
        {
            var encrypt = CreateEncryptor(typecs);
            return encrypt.Encrypt(Text, sKey);
        }

        public string Get16MD5One(string Text)
        {
            MD5Encrypt encrypt = new MD5Encrypt();
            return encrypt.Get16MD5One(Text);
        }

        public string Get32MD5One(string Text)
        {
            MD5Encrypt encrypt = new MD5Encrypt();
            return encrypt.Get32MD5One(Text);
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="decryptedBytes">待解密的字节数组</param>
        /// <param name="keyBytes">解密密钥字节数组</param>
        /// <param name="ivBytes">IV初始化向量字节数组</param>
        /// <param name="cipher">运算模式</param>
        /// <param name="padding">填充模式</param>
        /// <returns></returns>
        private static byte[] DecryptByAesBytes(byte[] decryptedBytes, byte[] keyBytes, byte[] ivBytes,
            CipherMode cipher = CipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            if (decryptedBytes == null || decryptedBytes.Length <= 0)
                throw new ArgumentNullException(nameof(decryptedBytes));
            if (keyBytes == null || keyBytes.Length <= 0)
                throw new ArgumentNullException(nameof(keyBytes));
            if (ivBytes == null || ivBytes.Length <= 0)
                throw new ArgumentNullException(nameof(ivBytes));

            var aes = new AesCryptoServiceProvider
            {
                Key = keyBytes,
                IV = ivBytes,
                Mode = cipher,
                Padding = padding
            };
            var outputBytes = aes.CreateDecryptor().TransformFinalBlock(decryptedBytes, 0, decryptedBytes.Length);
            return outputBytes;
        }

        private static IEncrypt CreateEncryptor(EncryptType typecs)
        {
            return typecs switch
            {
                EncryptType.DES => new DESEncrypt(),
                EncryptType.AES => new AESEncrypt(),
                _ => throw new ArgumentOutOfRangeException(nameof(typecs), typecs, "Unsupported encryption type.")
            };
        }
    }
}
