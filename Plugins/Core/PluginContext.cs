using Nexus.Plugins.Contracts;
using Nexus.Plugins.Contracts.Models;

namespace Nexus.Plugins.Core;

public class PluginContext : IPluginContext
{
    public string PluginId { get; }
    public string ConfigFolder { get; }
    public string DataFolder { get; }
    public string AssetsFolder { get; }
    public PluginManifest Manifest { get; }

    private readonly IServiceProvider _hostServices;
    private readonly List<IWebSocketHandler> _wsHandlers = new();
    private readonly List<IViewProvider> _viewProviders = new();
    private readonly List<IMenuProvider> _menuProviders = new();
    private readonly List<ISettingsProvider> _settingsProviders = new();
    private readonly List<INotificationHandler> _notificationHandlers = new();
    private readonly List<IWidgetProvider> _widgetProviders = new();

    public IReadOnlyList<IWebSocketHandler> WebSocketHandlers => _wsHandlers.AsReadOnly();
    public IReadOnlyList<IViewProvider> ViewProviders => _viewProviders.AsReadOnly();
    public IReadOnlyList<IMenuProvider> MenuProviders => _menuProviders.AsReadOnly();
    public IReadOnlyList<ISettingsProvider> SettingsProviders => _settingsProviders.AsReadOnly();
    public IReadOnlyList<INotificationHandler> NotificationHandlers => _notificationHandlers.AsReadOnly();
    public IReadOnlyList<IWidgetProvider> WidgetProviders => _widgetProviders.AsReadOnly();

    internal PluginContext(
        string pluginId,
        PluginManifest manifest,
        string baseConfigDir,
        IServiceProvider hostServices)
    {
        PluginId = pluginId;
        Manifest = manifest;
        _hostServices = hostServices;

        ConfigFolder = Path.Combine(baseConfigDir, pluginId, "Config");
        DataFolder = Path.Combine(baseConfigDir, pluginId, "Data");
        AssetsFolder = Path.Combine(baseConfigDir, "..", "ExternalPlugins", pluginId, "assets");

        Directory.CreateDirectory(ConfigFolder);
        Directory.CreateDirectory(DataFolder);
    }

    public T? GetService<T>() where T : class => _hostServices.GetService<T>();

    public void RegisterWebSocketHandler(IWebSocketHandler handler)
    {
        if (!_wsHandlers.Contains(handler))
            _wsHandlers.Add(handler);
    }

    public void RegisterViewProvider(IViewProvider provider)
    {
        if (!_viewProviders.Contains(provider))
            _viewProviders.Add(provider);
    }

    public void RegisterMenuProvider(IMenuProvider provider)
    {
        if (!_menuProviders.Contains(provider))
            _menuProviders.Add(provider);
    }

    public void RegisterSettingsProvider(ISettingsProvider provider)
    {
        if (!_settingsProviders.Contains(provider))
            _settingsProviders.Add(provider);
    }

    public void RegisterNotificationHandler(INotificationHandler handler)
    {
        if (!_notificationHandlers.Contains(handler))
            _notificationHandlers.Add(handler);
    }

    public void RegisterWidgetProvider(IWidgetProvider provider)
    {
        if (!_widgetProviders.Contains(provider))
            _widgetProviders.Add(provider);
    }
}
