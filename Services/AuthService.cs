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

    public async Task<(bool Success, string? ErrorMessage)> RefreshTokenAsync()
    {
        var config = ConfigService.Config;
        
        if (string.IsNullOrEmpty(config.AccessToken))
        {
            return (false, "无有效 Token");
        }

        System.Diagnostics.Debug.WriteLine($"[AuthService] 开始刷新 Token");

        var response = await PostAsync<RefreshTokenData>("/desktop/device/token/refresh", null, new RequestOptions
        {
            OperationName = "刷新 Token",
            RequireAuth = true
        });

        if (response == null)
        {
            return (false, "刷新失败：无响应");
        }

        if (!response.IsSuccess)
        {
            return (false, response.Msg ?? "刷新失败");
        }

        if (response.Data == null || string.IsNullOrEmpty(response.Data.AccessToken))
        {
            return (false, "服务器未返回新 Token");
        }

        ConfigService.Config.AccessToken = response.Data.AccessToken;
        ConfigService.Config.TokenExpiresAt = response.Data.ExpiresAt;
        ConfigService.SaveConfig();

        System.Diagnostics.Debug.WriteLine($"[AuthService] Token 刷新成功，新过期时间: {response.Data.ExpiresAt}");
        
        return (true, null);
    }

    public async Task<(bool Success, AuthorizationRequestData? Data, string? ErrorMessage, bool AlreadyBound)> CreateBindRequestAsync(string? deviceName = null, string? requestNote = null)
    {
        var config = ConfigService.Config;

        if (string.IsNullOrEmpty(config.DeviceId))
        {
            return (false, null, "设备ID不存在", false);
        }

        System.Diagnostics.Debug.WriteLine($"[AuthService] 发起授权绑定请求");

        var body = new
        {
            device_id = config.DeviceId,
            device_name = deviceName ?? config.DeviceName ?? Environment.MachineName,
            device_type = config.DeviceType,
            mac_address = config.MacAddress,
            ip_address = config.IpAddress,
            request_note = requestNote
        };

        var response = await PostAsync<AuthorizationRequestData>("/desktop/bind/request", body, new RequestOptions
        {
            OperationName = "发起授权绑定",
            RequireAuth = false
        });

        if (response == null)
        {
            return (false, null, "请求失败：无响应", false);
        }

        if (!response.IsSuccess)
        {
            if (response.Code == 400)
            {
                return (false, null, response.Msg ?? "设备已绑定", true);
            }
            return (false, null, response.Msg ?? "请求失败", false);
        }

        System.Diagnostics.Debug.WriteLine($"[AuthService] 授权请求已创建: {response.Data?.RequestId}");
        
        return (true, response.Data, null, false);
    }

    public async Task<(bool Success, AuthorizationStatusData? Data, string? ErrorMessage)> CheckAuthorizationStatusAsync(string requestId)
    {
        var response = await GetAsync<AuthorizationStatusData>($"/desktop/bind/request/{requestId}/status", new RequestOptions
        {
            OperationName = "查询授权状态",
            RequireAuth = false
        });

        if (response == null)
        {
            return (false, null, "查询失败：无响应");
        }

        if (!response.IsSuccess)
        {
            return (false, null, response.Msg ?? "查询失败");
        }

        if (response.Data?.Status == "authorized" && !string.IsNullOrEmpty(response.Data.AccessToken))
        {
            ConfigService.UpdateBindInfo(
                response.Data.ClassId,
                response.Data.ClassName ?? "",
                response.Data.AccessToken,
                response.Data.TokenExpiresAt
            );
        }

        return (true, response.Data, null);
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

public class RefreshTokenData
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}

public class AuthorizationRequestData
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
    
    [JsonPropertyName("expires_in_seconds")]
    public int ExpiresInSeconds { get; set; }
}

public class AuthorizationStatusData
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }
    
    [JsonPropertyName("class_id")]
    public int ClassId { get; set; }
    
    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }
    
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }
    
    [JsonPropertyName("reject_reason")]
    public string? RejectReason { get; set; }
}

#endregion
