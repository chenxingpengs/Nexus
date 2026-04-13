using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services.Meeting
{
    public class DecryptionService : IDisposable
    {
        private readonly byte[] _key;
        private AesGcm? _aesGcm;
        private bool _disposed;

        public DecryptionService(string base64Key)
        {
            if (string.IsNullOrEmpty(base64Key))
                throw new ArgumentNullException(nameof(base64Key));

            _key = Convert.FromBase64String(base64Key);
            
            if (_key.Length != 32)
                throw new ArgumentException("密钥长度必须为32字节 (AES-256)", nameof(base64Key));

            _aesGcm = new AesGcm(_key);
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            if (_disposed || _aesGcm == null)
                throw new ObjectDisposedException(nameof(DecryptionService));

            if (encryptedData == null || encryptedData.Length < 28)
                throw new ArgumentException("加密数据无效", nameof(encryptedData));

            var nonce = new byte[12];
            Buffer.BlockCopy(encryptedData, 0, nonce, 0, 12);

            var ciphertext = new byte[encryptedData.Length - 12];
            Buffer.BlockCopy(encryptedData, 12, ciphertext, 0, ciphertext.Length);

            var plaintext = new byte[ciphertext.Length - 16];
            var tag = new byte[16];
            
            Buffer.BlockCopy(ciphertext, ciphertext.Length - 16, tag, 0, 16);
            
            var actualCiphertext = new byte[ciphertext.Length - 16];
            Buffer.BlockCopy(ciphertext, 0, actualCiphertext, 0, actualCiphertext.Length);

            _aesGcm.Decrypt(nonce, actualCiphertext, tag, plaintext);

            return plaintext;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _aesGcm?.Dispose();
                _aesGcm = null;
                _disposed = true;
            }
        }
    }
}
