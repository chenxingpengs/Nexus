using System.Text.Json;

namespace Nexus.Plugins.Contracts;

public interface IWebSocketHandler
{
    string[] SubscribedEvents { get; }
    Task HandleMessageAsync(string eventName, JsonElement data, WebSocketContext context);
    Task OnConnectionStateChangedAsync(ConnectionState state, string? errorMessage = null);
}

public enum ConnectionState
{
    Connected,
    Disconnected,
    Reconnecting,
    Error
}
