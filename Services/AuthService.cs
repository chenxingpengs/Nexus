using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nexus.Services.Http;

namespace Nexus.Services;

public class AuthService : HttpService
{
    public event Action<string>? StatusChanged;
    public event Action<bool>? AuthStateChanged;

    public AuthService(ConfigService configService, ToastService? toastService = null) 
        : base(configService, toastService)
    {
    }

    private string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
    }

    public async Task<(bool Success, string? ErrorMessage)> VerifyDeviceAsync()
    {
        var config = ConfigService.Config;

        System.Diagnostics.Debug.WriteLine($"[AuthService] VerifyDeviceAsync 开始");
        System.Diagnostics.Debug.WriteLine($"[AuthService] DeviceId={config.DeviceId}");
        System.Diagnostics.Debug.WriteLine($"[AuthService] DeviceType={config.DeviceType}");
        System.Diagnostics.Debug.WriteLine($"[AuthService] ServerUrl={config.ServerUrl}");

        if (string.IsNullOrEmpty(config.DeviceId))
        {
            System.Diagnostics.Debug.WriteLine("[AuthService] 设备ID不存在");
            return (false, "设备ID不存在");
        }

        StatusChanged?.Invoke("正在验证设备...");

        var appVersion = config.AppVersion ?? GetAppVersion();
        var endpoint = $"/desktop/device/verify?device_id={Uri.EscapeDataString(config.DeviceId)}&device_type={Uri.EscapeDataString(config.DeviceType)}&app_version={Uri.EscapeDataString(appVersion)}";

        var response = await GetAsync<VerifyData>(endpoint, new RequestOptions
        {
            OperationName = "设备验证",
            MaxRetries = 3,
            RetryDelayMs = 2000,
            RequireAuth = false
        });

        if (response == null)
        {
            return (false, "验证失败：无响应");
        }

        if (!response.IsSuccess)
        {
            return (false, response.Msg ?? "验证失败");
        }

        if (response.Data == null || !response.Data.Bound)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] 设备未绑定: Data={(response.Data != null ? "存在" : "null")}, Bound={response.Data?.Bound}");
            return (false, "设备绑定信息失效");
        }

        System.Diagnostics.Debug.WriteLine($"[AuthService] 准备更新绑定信息: ClassId={response.Data.ClassId}, ClassName={response.Data.ClassName}");

        ConfigService.UpdateBindInfo(
            response.Data.ClassId,
            response.Data.ClassName ?? "",
            response.Data.AccessToken ?? config.AccessToken ?? "",
            response.Data.TokenExpiresAt
        );

        if (!string.IsNullOrEmpty(response.Data.DeviceType))
        {
            ConfigService.Config.DeviceType = response.Data.DeviceType;
        }

        StatusChanged?.Invoke("设备验证成功");
        AuthStateChanged?.Invoke(true);

        System.Diagnostics.Debug.WriteLine("[AuthService] 验证成功");
        return (true, null);
    }

    public async Task<(bool Success, string? Token, string? ErrorMessage)> GetAccessTokenAsync()
    {
        var config = ConfigService.Config;

        if (string.IsNullOrEmpty(config.DeviceId))
        {
            return (false, null, "设备ID不存在");
        }

        StatusChanged?.Invoke("正在获取访问令牌...");

        var appVersion = config.AppVersion ?? GetAppVersion();
        var body = new
        {
            device_id = config.DeviceId,
            device_type = config.DeviceType,
            app_version = appVersion
        };

        var response = await PostAsync<AuthData>("/desktop/device/auth", body, new RequestOptions
        {
            OperationName = "获取访问令牌",
            RequireAuth = false
        });

        if (response == null)
        {
            return (false, null, "获取令牌失败：无响应");
        }

        if (!response.IsSuccess)
        {
            return (false, null, response.Msg ?? "获取令牌失败");
        }

        if (response.Data == null || string.IsNullOrEmpty(response.Data.Token))
        {
            return (false, null, "服务器未返回令牌");
        }

        ConfigService.Config.AccessToken = response.Data.Token;
        ConfigService.Config.TokenExpiresAt = response.Data.ExpiresAt;
        ConfigService.SaveConfig();

        StatusChanged?.Invoke("获取令牌成功");

        return (true, response.Data.Token, null);
    }

    public void Logout()
    {
        ConfigService.ClearBindInfo();
        AuthStateChanged?.Invoke(false);
    }

    public bool IsTokenExpired()
    {
        var config = ConfigService.Config;
        return config.TokenExpiresAt.HasValue && config.TokenExpiresAt.Value < DateTime.Now;
    }

    public bool IsTokenExpiringSoon(int daysThreshold = 7)
    {
        var config = ConfigService.Config;
        if (!config.TokenExpiresAt.HasValue) return true;
        return config.TokenExpiresAt.Value < DateTime.Now.AddDays(daysThreshold);
    }
}

#region Response Models

public class VerifyData
{
    [JsonPropertyName("bound")]
    public bool Bound { get; set; }
    
    [JsonPropertyName("device_type")]
    public string? DeviceType { get; set; }
    
    [JsonPropertyName("class_id")]
    public int ClassId { get; set; }
    
    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }
    
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }
}

public class AuthData
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}

#endregion
