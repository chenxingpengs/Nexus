using Nexus.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Nexus.Services
{
    public class ConfigService
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexus"
        );
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "Nexusc]Config.json");
        private static readonly byte[] Entropy = { 0x4E, 0x65, 0x78, 0x75, 0x73, 0x41, 0x70, 0x70 };

        public AppConfig Config { get; private set; }

        public ConfigService()
        {
            Config = LoadConfig();
        }

        public AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return new AppConfig();
                }

                var json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config != null && !string.IsNullOrEmpty(config.AccessToken))
                {
                    config.AccessToken = DecryptToken(config.AccessToken);
                }

                var result = config ?? new AppConfig();
                result.ServerUrl = new AppConfig().ServerUrl;

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadConfig error: {ex.Message}");
                return new AppConfig();
            }
        }

        public void SaveConfig()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                var configToSave = new AppConfig
                {
                    DeviceId = Config.DeviceId,
                    DeviceName = Config.DeviceName,
                    DeviceType = Config.DeviceType,
                    AppVersion = Config.AppVersion,
                    AccessToken = !string.IsNullOrEmpty(Config.AccessToken)
                        ? EncryptToken(Config.AccessToken)
                        : null,
                    TokenExpiresAt = Config.TokenExpiresAt,
                    BindInfo = Config.BindInfo,
                    ServerUrl = Config.ServerUrl
                };

                var json = JsonSerializer.Serialize(configToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveConfig error: {ex.Message}");
                throw;
            }
        }

        public void ClearConfig()
        {
            try
            {
                Config = new AppConfig();

                if (File.Exists(ConfigFile))
                {
                    File.Delete(ConfigFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearConfig error: {ex.Message}");
            }
        }

        public void UpdateBindInfo(int classId, string className, string accessToken, DateTime? expiresAt = null)
        {
            Config.BindInfo = new BindInfo
            {
                ClassId = classId,
                ClassName = className,
                BindTime = DateTime.Now
            };
            Config.AccessToken = accessToken;
            Config.TokenExpiresAt = expiresAt ?? DateTime.Now.AddDays(30);
            SaveConfig();
        }

        public void ClearBindInfo()
        {
            Config.BindInfo = null;
            Config.AccessToken = null;
            Config.TokenExpiresAt = null;
            SaveConfig();
        }

        public void SetDeviceInfo(string deviceId, string deviceName)
        {
            Config.DeviceId = deviceId;
            Config.DeviceName = deviceName;
            SaveConfig();
        }

        private string EncryptToken(string plainToken)
        {
            if (!OperatingSystem.IsWindows())
            {
                return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainToken));
            }

            var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainToken);
            var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        private string DecryptToken(string encryptedToken)
        {
            if (!OperatingSystem.IsWindows())
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encryptedToken));
            }

            var encryptedBytes = Convert.FromBase64String(encryptedToken);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
    }
}
