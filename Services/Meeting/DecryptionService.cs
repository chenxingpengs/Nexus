using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services.Meeting
{
    public class EncryptedFrameHeader
    {
        public const int HeaderSize = 20;
        public static readonly byte[] MagicBytes = { (byte)'E', (byte)'V', (byte)'F', (byte)'1' };
        
        public byte[] Magic { get; private set; } = new byte[4];
        public byte Version { get; private set; }
        public byte Flags { get; private set; }
        public uint Sequence { get; private set; }
        public ulong Timestamp { get; private set; }
        public uint PayloadSize { get; private set; }

        public static EncryptedFrameHeader? Parse(byte[] data, int offset = 0)
        {
            if (data == null || data.Length < offset + HeaderSize)
                return null;

            var header = new EncryptedFrameHeader();
            Buffer.BlockCopy(data, offset, header.Magic, 0, 4);
            
            if (!IsValidMagic(header.Magic))
                return null;

            header.Version = data[offset + 4];
            header.Flags = data[offset + 5];
            header.Sequence = BitConverter.ToUInt32(data, offset + 6);
            header.Timestamp = BitConverter.ToUInt64(data, offset + 10);
            header.PayloadSize = BitConverter.ToUInt32(data, offset + 18);

            return header;
        }

        public static bool IsValidMagic(byte[] magic)
        {
            return magic != null && magic.Length == 4 &&
                   magic[0] == MagicBytes[0] &&
                   magic[1] == MagicBytes[1] &&
                   magic[2] == MagicBytes[2] &&
                   magic[3] == MagicBytes[3];
        }

        public bool IsValid()
        {
            return IsValidMagic(Magic) && Version == 1;
        }

        public bool IsTimestampValid(int maxAgeMs = 5000)
        {
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Timestamp > now)
            {
                return (Timestamp - now) < 1000;
            }
            return (now - Timestamp) < (ulong)maxAgeMs;
        }
    }

    public class DecryptionService : IDisposable
    {
        private readonly byte[] _key;
        private AesGcm? _aesGcm;
        private bool _disposed;
        private const int TagSize = 16;
        private const int NonceSize = 12;
        private uint _lastSequence;
        private bool _firstFrame = true;

        public event EventHandler<FrameDecryptedEventArgs>? FrameDecrypted;
        
        public DecryptionService(string base64Key)
        {
            if (string.IsNullOrEmpty(base64Key))
                throw new ArgumentNullException(nameof(base64Key));

            _key = Convert.FromBase64String(base64Key);
            
            if (_key.Length != 32)
                throw new ArgumentException("密钥长度必须为32字节 (AES-256)", nameof(base64Key));

            _aesGcm = new AesGcm(_key, TagSize);
            
            Debug.WriteLine($"[DecryptionService] 初始化成功，密钥长度: {_key.Length} 字节");
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            if (_disposed || _aesGcm == null)
                throw new ObjectDisposedException(nameof(DecryptionService));

            if (encryptedData == null || encryptedData.Length < NonceSize + TagSize)
                throw new ArgumentException("加密数据无效", nameof(encryptedData));

            var header = EncryptedFrameHeader.Parse(encryptedData);
            
            if (header != null && header.IsValid())
            {
                return DecryptNewFormat(encryptedData, header);
            }
            
            return DecryptLegacyFormat(encryptedData);
        }

        private byte[] DecryptNewFormat(byte[] data, EncryptedFrameHeader header)
        {
            if (!_firstFrame && header.Sequence <= _lastSequence)
            {
                Debug.WriteLine($"[DecryptionService] 检测到重放或乱序帧: seq={header.Sequence}, lastSeq={_lastSequence}");
            }

            if (!header.IsTimestampValid())
            {
                Debug.WriteLine($"[DecryptionService] 帧时间戳无效: ts={header.Timestamp}");
            }

            int payloadOffset = EncryptedFrameHeader.HeaderSize;
            int payloadLength = data.Length - payloadOffset;

            if (payloadLength < NonceSize + TagSize)
            {
                throw new ArgumentException("加密帧负载无效", nameof(data));
            }

            var nonce = new byte[NonceSize];
            Buffer.BlockCopy(data, payloadOffset, nonce, 0, NonceSize);

            int ciphertextLength = payloadLength - NonceSize;
            var ciphertextWithTag = new byte[ciphertextLength];
            Buffer.BlockCopy(data, payloadOffset + NonceSize, ciphertextWithTag, 0, ciphertextLength);

            var ciphertext = new byte[ciphertextLength - TagSize];
            var tag = new byte[TagSize];
            
            Buffer.BlockCopy(ciphertextWithTag, 0, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(ciphertextWithTag, ciphertext.Length, tag, 0, TagSize);

            var plaintext = new byte[ciphertext.Length];

            _aesGcm!.Decrypt(nonce, ciphertext, tag, plaintext);

            _lastSequence = header.Sequence;
            _firstFrame = false;

            Debug.WriteLine($"[DecryptionService] 新格式解密成功: seq={header.Sequence}, 输出大小={plaintext.Length}");

            FrameDecrypted?.Invoke(this, new FrameDecryptedEventArgs
            {
                Sequence = header.Sequence,
                Timestamp = header.Timestamp,
                DataLength = plaintext.Length
            });

            return plaintext;
        }

        private byte[] DecryptLegacyFormat(byte[] data)
        {
            var nonce = new byte[NonceSize];
            Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);

            var ciphertextWithTag = new byte[data.Length - NonceSize];
            Buffer.BlockCopy(data, NonceSize, ciphertextWithTag, 0, ciphertextWithTag.Length);

            var ciphertext = new byte[ciphertextWithTag.Length - TagSize];
            var tag = new byte[TagSize];
            
            Buffer.BlockCopy(ciphertextWithTag, 0, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(ciphertextWithTag, ciphertext.Length, tag, 0, TagSize);

            var plaintext = new byte[ciphertext.Length];

            _aesGcm!.Decrypt(nonce, ciphertext, tag, plaintext);

            Debug.WriteLine($"[DecryptionService] 旧格式解密成功: 输出大小={plaintext.Length}");

            return plaintext;
        }

        public static string DeriveKey(string baseKey, string context)
        {
            var keyBytes = Convert.FromBase64String(baseKey);
            using var sha256 = SHA256.Create();
            
            var contextBytes = System.Text.Encoding.UTF8.GetBytes(context);
            var combined = new byte[keyBytes.Length + contextBytes.Length];
            Buffer.BlockCopy(keyBytes, 0, combined, 0, keyBytes.Length);
            Buffer.BlockCopy(contextBytes, 0, combined, keyBytes.Length, contextBytes.Length);
            
            var derived = sha256.ComputeHash(combined);
            return Convert.ToBase64String(derived);
        }

        public void ResetSequence()
        {
            _lastSequence = 0;
            _firstFrame = true;
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

    public class FrameDecryptedEventArgs : EventArgs
    {
        public uint Sequence { get; set; }
        public ulong Timestamp { get; set; }
        public int DataLength { get; set; }
    }
}
