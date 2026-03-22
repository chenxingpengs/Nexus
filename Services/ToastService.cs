using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;
using Nexus.Views;

namespace Nexus.Services;

public enum ToastType
{
    Success,
    Warning,
    Error,
    Info
}

public class ToastItem
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ToastType Type { get; set; } = ToastType.Info;
    public int DurationMs { get; set; } = 4000;
    public Action? OnClick { get; set; }
}

public class ToastService : IDisposable
{
    private readonly ConcurrentQueue<ToastItem> _toastQueue = new();
    private ToastWindow? _currentWindow;
    private bool _isDisplaying;
    private readonly object _lock = new();
    private bool _disposed;

    public event Action<ToastItem>? ToastShown;
    public event Action? ToastClosed;

    public void ShowSuccess(string message, string title = "成功", int durationMs = 3000)
    {
        Show(new ToastItem
        {
            Title = title,
            Message = message,
            Type = ToastType.Success,
            DurationMs = durationMs
        });
    }

    public void ShowWarning(string message, string title = "警告", int durationMs = 4000)
    {
        Show(new ToastItem
        {
            Title = title,
            Message = message,
            Type = ToastType.Warning,
            DurationMs = durationMs
        });
    }

    public void ShowError(string message, string title = "错误", int durationMs = 20000)
    {
        Show(new ToastItem
        {
            Title = title,
            Message = message,
            Type = ToastType.Error,
            DurationMs = durationMs
        });
    }

    public void ShowInfo(string message, string title = "提示", int durationMs = 4000)
    {
        Show(new ToastItem
        {
            Title = title,
            Message = message,
            Type = ToastType.Info,
            DurationMs = durationMs
        });
    }

    public void Show(ToastItem toast)
    {
        if (_disposed) return;
        
        _toastQueue.Enqueue(toast);
        Debug.WriteLine($"[ToastService] Toast 入队: {toast.Title} - {toast.Message}");
        
        lock (_lock)
        {
            if (!_isDisplaying)
            {
                _isDisplaying = true;
                _ = ProcessQueueAsync();
            }
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (_toastQueue.TryDequeue(out var toast))
        {
            await ShowToastAsync(toast);
        }

        lock (_lock)
        {
            _isDisplaying = false;
        }
    }

    private async Task ShowToastAsync(ToastItem toast)
    {
        if (_disposed) return;

        TaskCompletionSource<bool>? tcs = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                _currentWindow?.Close();
                
                _currentWindow = new ToastWindow();
                _currentWindow.Closed += (s, e) =>
                {
                    _currentWindow = null;
                    ToastClosed?.Invoke();
                };
                
                _currentWindow.Show(toast);
                ToastShown?.Invoke(toast);
                
                Debug.WriteLine($"[ToastService] 显示 Toast: {toast.Title} - {toast.Message}, 持续 {toast.DurationMs}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ToastService] 显示 Toast 失败: {ex.Message}");
            }
        });

        await Task.Delay(toast.DurationMs);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                if (_currentWindow != null)
                {
                    _currentWindow.Close();
                    _currentWindow = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ToastService] 关闭 Toast 失败: {ex.Message}");
            }
        });
    }

    public void ClearAll()
    {
        while (_toastQueue.TryDequeue(out _)) { }
        
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _currentWindow?.Close();
                _currentWindow = null;
            }
            catch { }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        ClearAll();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}
