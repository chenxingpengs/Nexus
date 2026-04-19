using System.Text.Json;

namespace Nexus.Plugins.Contracts;

public class WebSocketContext
{
    private readonly Func<string, object, Task<bool>>? _emitFunc;
    private readonly Func<string, object, Task>? _broadcastFunc;
    private readonly Action<string, object>? _publishLocalFunc;

    public string DeviceId { get; }
    public string Token { get; }
    public bool IsConnected { get; }
    public ConnectionState ConnectionState { get; }

    internal WebSocketContext(
        string deviceId,
        string token,
        bool isConnected,
        ConnectionState connectionState,
        Func<string, object, Task<bool>>? emitFunc = null,
        Func<string, object, Task>? broadcastFunc = null,
        Action<string, object>? publishLocalFunc = null)
    {
        DeviceId = deviceId;
        Token = token;
        IsConnected = isConnected;
        ConnectionState = connectionState;
        _emitFunc = emitFunc;
        _broadcastFunc = broadcastFunc;
        _publishLocalFunc = publishLocalFunc;
    }

    public Task<bool> EmitAsync(string eventName, object data)
    {
        if (_emitFunc == null)
            return Task.FromResult(false);
        return _emitFunc(eventName, data);
    }

    public Task BroadcastToPluginsAsync(string eventName, object data)
    {
        if (_broadcastFunc == null)
            return Task.CompletedTask;
        return _broadcastFunc(eventName, data);
    }

    public void PublishLocal(string eventName, object data)
    {
        _publishLocalFunc?.Invoke(eventName, data);
    }

    public static WebSocketContext Disconnected(string deviceId = "", string token = "")
    {
        return new WebSocketContext(deviceId, token, false, ConnectionState.Disconnected);
    }
}
