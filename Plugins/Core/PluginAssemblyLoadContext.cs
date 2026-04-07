using System.Reflection;
using System.Runtime.Loader;
using Nexus.Plugins.Contracts.Models;

namespace Nexus.Plugins.Core;

internal class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _pluginPath;
    private readonly AssemblyDependencyResolver _resolver;
    private readonly HashSet<string> _sharedAssemblies;

    public PluginAssemblyLoadContext(string pluginPath, bool isCollectible, HashSet<string>? sharedAssemblies = null)
        : base(isCollectible: isCollectible)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _sharedAssemblies = sharedAssemblies ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Nexus",
            "Avalonia",
            "Avalonia.Controls",
            "Avalonia.Themes.Fluent",
            "FluentAvalonia",
            "CommunityToolkit.Mvvm",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
            "System.Text.Json"
        };
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? "";

        if (_sharedAssemblies.Contains(name))
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            try
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginALC] 加载程序集 {name} 失败: {ex.Message}");
            }
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }
        return IntPtr.Zero;
    }
}
