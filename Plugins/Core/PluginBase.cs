using Microsoft.Extensions.DependencyInjection;
using Nexus.Plugins.Contracts;

namespace Nexus.Plugins.Core;

public abstract class PluginBase : IPlugin, IWebSocketHandler
{
    private IPluginContext? _context;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Version { get; }
    public virtual string Description => "";
    public virtual string Author => "";
    public virtual string[] Dependencies => Array.Empty<string>();

    public string ConfigFolder => _context?.ConfigFolder ?? "";
    public string DataFolder => _context?.DataFolder ?? "";
    public string AssetsFolder => _context?.AssetsFolder ?? "";

    public virtual string[] SubscribedEvents => Array.Empty<string>();

    public virtual void Initialize(IPluginContext context, IServiceCollection services)
    {
        _context = context;
    }

    public virtual void OnStartup(IServiceProvider serviceProvider) { }

    public virtual void OnShutdown() { }

    public virtual Task HandleMessageAsync(string eventName, System.Text.Json.JsonElement data, WebSocketContext context)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnConnectionStateChangedAsync(ConnectionState state, string? errorMessage = null)
    {
        return Task.CompletedTask;
    }

    protected T? GetService<T>() where T : class => _context?.GetService<T>();
}
