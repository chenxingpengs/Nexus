using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nexus.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ConfigService _configService;

        public event Action<string>? StatusChanged;
        public event Action<bool>? AuthStateChanged;

        public AuthService(ConfigService configService)
        {
            _configService = configService;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        private string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        }

        public async Task<(bool Success, string? ErrorMessage)> VerifyDeviceAsync()
        {
            var config = _configService.Config;

            System.Diagnostics.Debug.WriteLine($"[AuthService] VerifyDeviceAsync 开始");
            System.Diagnostics.Debug.WriteLine($"[AuthService] DeviceId={config.DeviceId}");
            System.Diagnostics.Debug.WriteLine($"[AuthService] DeviceType={config.DeviceType}");
            System.Diagnostics.Debug.WriteLine($"[AuthService] ServerUrl={config.ServerUrl}");

            if (string.IsNullOrEmpty(config.DeviceId))
            {
                System.Diagnostics.Debug.WriteLine("[AuthService] 设备ID不存在");
                return (false, "设备ID不存在");
            }

            try
            {
                StatusChanged?.Invoke("正在验证设备...");

                var appVersion = config.AppVersion ?? GetAppVersion();
                var url = $"{config.ServerUrl}/desktop/device/verify?device_id={Uri.EscapeDataString(config.DeviceId)}&device_type={Uri.EscapeDataString(config.DeviceType)}&app_version={Uri.EscapeDataString(appVersion)}";
                System.Diagnostics.Debug.WriteLine($"[AuthService] 请求URL: {url}");

                var response = await _httpClient.GetAsync(url);
                System.Diagnostics.Debug.WriteLine($"[AuthService] 响应状态码: {response.StatusCode}");

                var content = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[AuthService] 响应内容: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"服务器错误: {response.StatusCode}");
                }

                var result = JsonSerializer.Deserialize<VerifyResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Console.WriteLine($"[AuthService] 反序列化结果: Code={result?.Code}, Data={(result?.Data != null ? $"Bound={result.Data.Bound}, ClassId={result.Data.ClassId}, ClassName={result.Data.ClassName}" : "null")}");

                if (result == null || result.Code != 200)
                {
                    return (false, result?.Msg ?? "验证失败");
                }

                if (result.Data == null || !result.Data.Bound)
                {
                    Console.WriteLine($"[AuthService] 设备未绑定: Data={(result.Data != null ? "存在" : "null")}, Bound={result.Data?.Bound}");
                    return (false, "设备绑定信息失效");
                }

                Console.WriteLine($"[AuthService] 准备更新绑定信息: ClassId={result.Data.ClassId}, ClassName={result.Data.ClassName}, AccessToken={(string.IsNullOrEmpty(result.Data.AccessToken) ? "空" : "存在")}");

                _configService.UpdateBindInfo(
                    result.Data.ClassId,
                    result.Data.ClassName ?? "",
                    result.Data.AccessToken ?? config.AccessToken ?? "",
                    result.Data.TokenExpiresAt
                );

                Console.WriteLine($"[AuthService] 绑定信息已更新，当前配置: BindInfo={(_configService.Config.BindInfo != null ? $"ClassId={_configService.Config.BindInfo.ClassId}" : "null")}");

                if (!string.IsNullOrEmpty(result.Data.DeviceType))
                {
                    _configService.Config.DeviceType = result.Data.DeviceType;
                }

                StatusChanged?.Invoke("设备验证成功");
                AuthStateChanged?.Invoke(true);

                System.Diagnostics.Debug.WriteLine("[AuthService] 验证成功");
                return (true, null);
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] HTTP错误: {ex.Message}");
                return (false, $"网络错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] 异常: {ex.Message}");
                return (false, $"验证失败: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? Token, string? ErrorMessage)> GetAccessTokenAsync()
        {
            var config = _configService.Config;

            if (string.IsNullOrEmpty(config.DeviceId))
            {
                return (false, null, "设备ID不存在");
            }

            try
            {
                StatusChanged?.Invoke("正在获取访问令牌...");

                var appVersion = config.AppVersion ?? GetAppVersion();
                var url = $"{config.ServerUrl}/desktop/device/auth";
                var body = new
                {
                    device_id = config.DeviceId,
                    device_type = config.DeviceType,
                    app_version = appVersion
                };
                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, $"服务器错误: {response.StatusCode}");
                }

                var result = JsonSerializer.Deserialize<AuthResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null || result.Code != 200)
                {
                    return (false, null, result?.Msg ?? "获取令牌失败");
                }

                if (result.Data == null || string.IsNullOrEmpty(result.Data.Token))
                {
                    return (false, null, "服务器未返回令牌");
                }

                _configService.Config.AccessToken = result.Data.Token;
                _configService.Config.TokenExpiresAt = result.Data.ExpiresAt;
                _configService.SaveConfig();

                StatusChanged?.Invoke("获取令牌成功");

                return (true, result.Data.Token, null);
            }
            catch (HttpRequestException ex)
            {
                return (false, null, $"网络错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"获取令牌失败: {ex.Message}");
            }
        }

        public void Logout()
        {
            _configService.ClearBindInfo();
            AuthStateChanged?.Invoke(false);
        }

        public bool IsTokenExpired()
        {
            var config = _configService.Config;
            return config.TokenExpiresAt.HasValue && config.TokenExpiresAt.Value < DateTime.Now;
        }

        public bool IsTokenExpiringSoon(int daysThreshold = 7)
        {
            var config = _configService.Config;
            if (!config.TokenExpiresAt.HasValue) return true;
            return config.TokenExpiresAt.Value < DateTime.Now.AddDays(daysThreshold);
        }
    }

    #region Response Models

    public class VerifyResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("code")]
        public int Code { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("msg")]
        public string Msg { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public VerifyData? Data { get; set; }
    }

    public class VerifyData
    {
        [System.Text.Json.Serialization.JsonPropertyName("bound")]
        public bool Bound { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("device_type")]
        public string? DeviceType { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("class_id")]
        public int ClassId { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("class_name")]
        public string? ClassName { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("token_expires_at")]
        public DateTime? TokenExpiresAt { get; set; }
    }

    public class AuthResponse
    {
        public int Code { get; set; }
        public string Msg { get; set; } = string.Empty;
        public AuthData? Data { get; set; }
    }

    public class AuthData
    {
        public string Token { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    #endregion
}
