using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Nexus.Services;

public class TokenRefreshService : IDisposable
{
    private readonly AuthService _authService;
    private readonly ToastService _toastService;
    private readonly ConfigService _configService;
    private Timer? _checkTimer;
    private bool _isRefreshing;
    private bool _disposed;

    public const int TokenValidDays = 90;
    public const int AutoRefreshThresholdDays = 14;
    public const int CheckIntervalHours = 6;
    public const int MaxRetryAttempts = 3;

    public event Action? TokenRefreshed;
    public event Action? TokenRefreshFailed;
    public event Action? TokenExpired;

    public TokenRefreshService(AuthService authService, ToastService toastService, ConfigService configService)
    {
        _authService = authService;
        _toastService = toastService;
        _configService = configService;
    }

    public void Start()
    {
        if (_checkTimer != null) return;

        System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 启动 Token 刷新服务");

        _checkTimer = new Timer(async _ => await CheckAndRefreshAsync(), null, TimeSpan.Zero, TimeSpan.FromHours(CheckIntervalHours));
    }

    public void Stop()
    {
        _checkTimer?.Dispose();
        _checkTimer = null;
        System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 停止 Token 刷新服务");
    }

    public async Task<bool> CheckAndRefreshAsync()
    {
        if (_isRefreshing || !_configService.Config.IsBound)
        {
            return true;
        }

        _isRefreshing = true;

        try
        {
            var config = _configService.Config;

            if (string.IsNullOrEmpty(config.AccessToken))
            {
                System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 无 Token，跳过检查");
                return true;
            }

            if (_authService.IsTokenExpired())
            {
                System.Diagnostics.Debug.WriteLine("[TokenRefreshService] Token 已过期");
                TokenExpired?.Invoke();
                return false;
            }

            if (_authService.IsTokenExpiringSoon(AutoRefreshThresholdDays))
            {
                System.Diagnostics.Debug.WriteLine("[TokenRefreshService] Token 即将过期，开始刷新");
                return await RefreshTokenWithRetryAsync();
            }

            System.Diagnostics.Debug.WriteLine("[TokenRefreshService] Token 状态正常");
            return true;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public async Task<bool> RefreshTokenWithRetryAsync()
    {
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            System.Diagnostics.Debug.WriteLine($"[TokenRefreshService] 刷新尝试 {attempt}/{MaxRetryAttempts}");

            var (success, errorMessage) = await _authService.RefreshTokenAsync();

            if (success)
            {
                System.Diagnostics.Debug.WriteLine("[TokenRefreshService] Token 刷新成功");
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TokenRefreshed?.Invoke();
                });
                
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[TokenRefreshService] 刷新失败: {errorMessage}");

            if (attempt < MaxRetryAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                System.Diagnostics.Debug.WriteLine($"[TokenRefreshService] 等待 {delay.TotalSeconds} 秒后重试");
                await Task.Delay(delay);
            }
        }

        System.Diagnostics.Debug.WriteLine("[TokenRefreshService] Token 刷新失败，已达最大重试次数");
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TokenRefreshFailed?.Invoke();
        });
        
        return false;
    }

    public async Task<bool> EnsureValidTokenAsync()
    {
        var config = _configService.Config;

        if (!config.IsBound || string.IsNullOrEmpty(config.AccessToken))
        {
            return false;
        }

        if (_authService.IsTokenExpired())
        {
            return await RefreshTokenWithRetryAsync();
        }

        if (_authService.IsTokenExpiringSoon(AutoRefreshThresholdDays))
        {
            _ = RefreshTokenWithRetryAsync();
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        Stop();
        _disposed = true;
    }
}
