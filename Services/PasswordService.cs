using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nexus.Services
{
    public class PasswordService
    {
        private static readonly string PasswordFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexus",
            "security.dat"
        );
        private static readonly byte[] Entropy = { 0x4E, 0x65, 0x78, 0x75, 0x73, 0x53, 0x65, 0x63 };

        private string? _cachedPasswordHash;

        public bool HasPassword
        {
            get
            {
                if (_cachedPasswordHash != null) return true;
                return File.Exists(PasswordFile);
            }
        }

        public bool IsPasswordSet => HasPassword;

        public bool SetPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            try
            {
                var hash = HashPassword(password);
                _cachedPasswordHash = hash;

                var directory = Path.GetDirectoryName(PasswordFile);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var encryptedHash = ProtectData(hash);
                File.WriteAllBytes(PasswordFile, encryptedHash);
                
                File.SetAttributes(PasswordFile, FileAttributes.Hidden | FileAttributes.System);
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PasswordService] SetPassword error: {ex.Message}");
                return false;
            }
        }

        public bool VerifyPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            try
            {
                var storedHash = GetStoredPasswordHash();
                if (storedHash == null)
                {
                    return false;
                }

                var inputHash = HashPassword(password);
                return storedHash == inputHash;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PasswordService] VerifyPassword error: {ex.Message}");
                return false;
            }
        }

        public bool ChangePassword(string oldPassword, string newPassword)
        {
            if (!VerifyPassword(oldPassword))
            {
                return false;
            }

            return SetPassword(newPassword);
        }

        public bool RemovePassword(string password)
        {
            if (!VerifyPassword(password))
            {
                return false;
            }

            try
            {
                if (File.Exists(PasswordFile))
                {
                    File.Delete(PasswordFile);
                }
                _cachedPasswordHash = null;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PasswordService] RemovePassword error: {ex.Message}");
                return false;
            }
        }

        private string? GetStoredPasswordHash()
        {
            if (_cachedPasswordHash != null)
            {
                return _cachedPasswordHash;
            }

            if (!File.Exists(PasswordFile))
            {
                return null;
            }

            try
            {
                var encryptedHash = File.ReadAllBytes(PasswordFile);
                _cachedPasswordHash = UnprotectData(encryptedHash);
                return _cachedPasswordHash;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PasswordService] GetStoredPasswordHash error: {ex.Message}");
                return null;
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + "NexusSalt2024");
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private static byte[] ProtectData(string data)
        {
            if (!OperatingSystem.IsWindows())
            {
                return Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(data)));
            }

            var bytes = Encoding.UTF8.GetBytes(data);
            return ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        }

        private static string UnprotectData(byte[] protectedData)
        {
            if (!OperatingSystem.IsWindows())
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(protectedData)));
            }

            var bytes = ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }

        public void InitializeDefaultPassword()
        {
            if (!HasPassword)
            {
                SetPassword("zhhqzx");
                System.Diagnostics.Debug.WriteLine("[PasswordService] 默认密码已初始化");
            }
        }
    }
}
