using Microsoft.Extensions.DependencyInjection;

namespace Nexus.Plugins.Contracts;

public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }
    string[] Dependencies { get; }

    void Initialize(IPluginContext context, IServiceCollection services);
    void OnStartup(IServiceProvider serviceProvider);
    void OnShutdown();
    string ConfigFolder { get; }
}
