using System.Text.Json;
using Nexus.Plugins.Contracts;
using Nexus.Plugins.Core;

namespace Nexus.Plugins.Services;

public class WebSocketBridgeService : IDisposable
{
    private readonly PluginHost _pluginHost;
    private readonly List<IWebSocketHandler> _handlers = new();
    private object? _socketIOInstance;
    private Func<string, object, Task<(bool Success, string? ErrorMessage)>>? _emitFunc;
    private string? _currentDeviceId;
    private string? _currentToken;
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private bool _disposed;
    private readonly SemaphoreSlim _handlerLock = new(1, 1);

    public event Action<ConnectionState, string?>? ConnectionStateChanged;
    public event Action<string, JsonElement>? MessageDispatchedToPlugins;
    public Action<string, string, Exception>? ErrorHandler;

    public ConnectionState CurrentState => _connectionState;
    public bool IsConnected => _connectionState == ConnectionState.Connected;
    public int HandlerCount { get; private set; }

    public WebSocketBridgeService(PluginHost pluginHost)
    {
        _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
    }

    public void Initialize<TSocket>(TSocket socketIO,
        string deviceId,
        string token,
        Func<string, object, Task<(bool Success, string? ErrorMessage)>> emitFunc)
    {
        if (socketIO == null) throw new ArgumentNullException(nameof(socketIO));

        _socketIOInstance = socketIO;
        _currentDeviceId = deviceId;
        _currentToken = token;
        _emitFunc = emitFunc;

        System.Diagnostics.Debug.WriteLine($"[WSBridge] 初始化完成, DeviceId={deviceId}");

        RegisterHandlersFromPluginHost();
    }

    public async Task OnMessageReceivedAsync(JsonElement message)
    {
        var eventType = ExtractEventType(message);
        if (string.IsNullOrEmpty(eventType))
            return;

        System.Diagnostics.Debug.WriteLine($"[WSBridge] 收到事件: {eventType}, 分发到 {_handlers.Count} 个处理器");

        await DispatchToHandlersAsync(eventType, message);

        await _pluginHost.EventBus.PublishAsync($"ws:{eventType}", message);

        MessageDispatchedToPlugins?.Invoke(eventType, message);
    }

    public async Task OnConnectedAsync()
    {
        UpdateConnectionState(ConnectionState.Connected);

        foreach (var handler in _handlers.ToList())
        {
            try
            {
                await handler.OnConnectionStateChangedAsync(ConnectionState.Connected);
            }
            catch (Exception ex)
            {
                ReportError(handler.GetType().Name, "OnConnected", ex);
            }
        }

        await _pluginHost.EventBus.PublishAsync("ws:state:connected", DateTime.UtcNow);
    }

    public async Task OnDisconnectedAsync(string? errorMessage = null)
    {
        UpdateConnectionState(ConnectionState.Disconnected, errorMessage);

        foreach (var handler in _handlers.ToList())
        {
            try
            {
                await handler.OnConnectionStateChangedAsync(ConnectionState.Disconnected, errorMessage);
            }
            catch (Exception ex)
            {
                ReportError(handler.GetType().Name, "OnDisconnected", ex);
            }
        }

        await _pluginHost.EventBus.PublishAsync("ws:state:disconnected", DateTime.UtcNow);
    }

    public async Task OnReconnectingAsync()
    {
        UpdateConnectionState(ConnectionState.Reconnecting);

        foreach (var handler in _handlers.ToList())
        {
            try
            {
                await handler.OnConnectionStateChangedAsync(ConnectionState.Reconnecting);
            }
            catch (Exception ex)
            {
                ReportError(handler.GetType().Name, "OnReconnecting", ex);
            }
        }
    }

    public async Task<bool> EmitToServerAsync(string eventName, object data)
    {
        if (_emitFunc == null || !IsConnected)
        {
            System.Diagnostics.Debug.WriteLine($"[WSBridge] 无法发送: 未连接或未初始化");
            return false;
        }

        try
        {
            var (success, error) = await _emitFunc(eventName, data);
            if (!success)
                System.Diagnostics.Debug.WriteLine($"[WSBridge] 发送 {eventName} 失败: {error}");
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WSBridge] 发送 {eventName} 异常: {ex.Message}");
            return false;
        }
    }

    public async Task BroadcastToLocalPluginsAsync(string eventName, object data)
    {
        await _pluginHost.EventBus.PublishAsync($"local:{eventName}", data);
    }

    public void RegisterHandler(IWebSocketHandler handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        _handlerLock.Wait();
        try
        {
            if (!_handlers.Contains(handler))
            {
                _handlers.Add(handler);
                HandlerCount = _handlers.Count;
                System.Diagnostics.Debug.WriteLine($"[WSBridge] 注册处理器: {handler.GetType().Name}, 事件: [{string.Join(", ", handler.SubscribedEvents)}]");
            }
        }
        finally
        {
            _handlerLock.Release();
        }
    }

    public void UnregisterHandler(IWebSocketHandler handler)
    {
        _handlerLock.Wait();
        try
        {
            _handlers.Remove(handler);
            HandlerCount = _handlers.Count;
        }
        finally
        {
            _handlerLock.Release();
        }
    }

    public void RegisterHandlersFromPluginHost()
    {
        var pluginHandlers = _pluginHost.GetAllWebSocketHandlers().ToList();

        foreach (var handler in pluginHandlers)
        {
            RegisterHandler(handler);
        }

        System.Diagnostics.Debug.WriteLine($"[WSBridge] 从 PluginHost 注册了 {pluginHandlers.Count} 个 WS 处理器");
    }

    public WebSocketContext CreateWebSocketContext()
    {
        return new WebSocketContext(
            _currentDeviceId ?? "",
            _currentToken ?? "",
            IsConnected,
            _connectionState,
            _emitFunc != null ? EmitToServerAsync : null,
            BroadcastToLocalPluginsAsync,
            (name, data) => _ = BroadcastToLocalPluginsAsync(name, data)
        );
    }

    private async Task DispatchToHandlersAsync(string eventType, JsonElement message)
    {
        var context = CreateWebSocketContext();
        var dispatchTasks = new List<Task>();

        foreach (var handler in _handlers.ToList())
        {
            bool isInterested = handler.SubscribedEvents.Any(e =>
                e.Equals(eventType, StringComparison.OrdinalIgnoreCase) ||
                e.Equals("*", StringComparison.OrdinalIgnoreCase));

            if (!isInterested)
                continue;

            dispatchTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await handler.HandleMessageAsync(eventType, message, context);
                }
                catch (Exception ex)
                {
                    ReportError(handler.GetType().Name, $"HandleMessage({eventType})", ex);
                }
            }));
        }

        if (dispatchTasks.Count > 0)
        {
            await Task.WhenAll(dispatchTasks);
        }
    }

    private static string ExtractEventType(JsonElement message)
    {
        if (message.ValueKind == JsonValueKind.Object)
        {
            if (message.TryGetProperty("type", out var typeProp))
                return typeProp.GetString() ?? "";

            if (message.TryGetProperty("event", out var eventProp))
                return eventProp.GetString() ?? "";
        }

        return "";
    }

    private void UpdateConnectionState(ConnectionState newState, string? errorMessage = null)
    {
        if (_connectionState == newState && newState != ConnectionState.Connected)
            return;

        var oldState = _connectionState;
        _connectionState = newState;

        System.Diagnostics.Debug.WriteLine($"[WSBridge] 连接状态: {oldState} -> {newState}" +
            (errorMessage != null ? $", 错误: {errorMessage}" : ""));

        ConnectionStateChanged?.Invoke(newState, errorMessage);
    }

    private void ReportError(string handlerName, string operation, Exception ex)
    {
        var msg = $"[WSBridge] 处理器 {handlerName} 在 {operation} 中异常: {ex.Message}";
        System.Diagnostics.Debug.WriteLine(msg);
        ErrorHandler?.Invoke(handlerName, operation, ex);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _handlerLock.Dispose();
        _handlers.Clear();
        _socketIOInstance = null;
        _emitFunc = null;
    }
}
