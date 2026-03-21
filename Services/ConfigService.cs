using Nexus.Models;
using Nexus.Models.Widget;
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
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "NexusConfig.json");
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
                Console.WriteLine($"[ConfigService] LoadConfig 开始, 配置文件路径: {ConfigFile}");
                
                if (!File.Exists(ConfigFile))
                {
                    Console.WriteLine("[ConfigService] 配置文件不存在，返回默认配置");
                    return new AppConfig();
                }

                var json = File.ReadAllText(ConfigFile);
                Console.WriteLine($"[ConfigService] 配置文件内容: {json}");
                
                var config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config != null && !string.IsNullOrEmpty(config.AccessToken))
                {
                    config.AccessToken = DecryptToken(config.AccessToken);
                }

                var result = config ?? new AppConfig();
                Console.WriteLine($"[ConfigService] 加载后 ServerUrl: {result.ServerUrl}");
                Console.WriteLine($"[ConfigService] 加载后 BindInfo: {(result.BindInfo != null ? $"ClassId={result.BindInfo.ClassId}, ClassName={result.BindInfo.ClassName}" : "null")}");
                Console.WriteLine($"[ConfigService] 加载后 IsBound: {result.IsBound}");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadConfig error: {ex.Message}");
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
                    ServerUrl = Config.ServerUrl,
                    WidgetConfig = Config.WidgetConfig
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
            Console.WriteLine($"[ConfigService] UpdateBindInfo 被调用: classId={classId}, className={className}, accessToken={(string.IsNullOrEmpty(accessToken) ? "空" : "存在")}, expiresAt={expiresAt}");
            
            Config.BindInfo = new BindInfo
            {
                ClassId = classId,
                ClassName = className,
                BindTime = DateTime.Now
            };
            Config.AccessToken = accessToken;
            Config.TokenExpiresAt = expiresAt ?? DateTime.Now.AddDays(30);
            
            Console.WriteLine($"[ConfigService] BindInfo 已设置: ClassId={Config.BindInfo.ClassId}, ClassName={Config.BindInfo.ClassName}");
            
            SaveConfig();
            
            Console.WriteLine($"[ConfigService] SaveConfig 已调用");
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

        public WidgetConfig GetWidgetConfig()
        {
            if (Config.WidgetConfig == null)
            {
                Config.WidgetConfig = WidgetConfig.CreateDefault();
                SaveConfig();
            }
            return Config.WidgetConfig;
        }

        public void UpdateWidgetConfig(WidgetConfig config)
        {
            Config.WidgetConfig = config;
            SaveConfig();
        }

        public void UpdateWidgetEnabled(bool isEnabled)
        {
            GetWidgetConfig().IsEnabled = isEnabled;
            SaveConfig();
        }

        public void UpdateWidgetOpacity(double opacity)
        {
            GetWidgetConfig().Opacity = opacity;
            SaveConfig();
        }

        public void UpdateWeatherLocation(string cityId, string cityName, string province)
        {
            var widgetConfig = GetWidgetConfig();
            widgetConfig.WeatherLocation = new WeatherLocationConfig
            {
                CityId = cityId,
                CityName = cityName,
                Province = province
            };
            SaveConfig();
        }

        public void ClearWeatherLocation()
        {
            GetWidgetConfig().WeatherLocation = null;
            SaveConfig();
        }

        public void UpdateLocationMode(LocationMode mode)
        {
            GetWidgetConfig().LocationMode = mode;
            SaveConfig();
        }

        public LocationMode GetLocationMode()
        {
            return GetWidgetConfig().LocationMode;
        }

        public int GetClassId()
        {
            return Config.BindInfo?.ClassId ?? 0;
        }

        public string? GetServerUrl()
        {
            return Config.ServerUrl;
        }

        public string? GetAccessToken()
        {
            return Config.AccessToken;
        }
    }
}
