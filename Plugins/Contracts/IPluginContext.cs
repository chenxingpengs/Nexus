using Microsoft.Extensions.DependencyInjection;
using Nexus.Plugins.Contracts.Models;

namespace Nexus.Plugins.Contracts;

public interface IPluginContext
{
    string PluginId { get; }
    string ConfigFolder { get; }
    string DataFolder { get; }
    string AssetsFolder { get; }
    PluginManifest Manifest { get; }
    T? GetService<T>() where T : class;
    void RegisterWebSocketHandler(IWebSocketHandler handler);
    void RegisterViewProvider(IViewProvider provider);
    void RegisterMenuProvider(IMenuProvider provider);
    void RegisterSettingsProvider(ISettingsProvider provider);
    void RegisterNotificationHandler(INotificationHandler handler);
    void RegisterWidgetProvider(IWidgetProvider provider);
}
