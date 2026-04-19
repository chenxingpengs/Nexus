using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Plugins.Contracts;
using Nexus.Plugins.Contracts.Models;

namespace Nexus.Plugins.Core;

public record LoadedPlugin(
    IPlugin Instance,
    PluginManifest Manifest,
    PluginContext Context,
    AssemblyLoadContext LoadContext,
    Assembly Assembly,
    DateTime LoadedAt,
    PluginLoadState State = PluginLoadState.Loaded,
    string? ErrorMessage = null
);

public class PluginHost : IDisposable
{
    private readonly string _pluginDirectory;
    private readonly string _configBaseDir;
    private readonly IServiceProvider _hostServices;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new();
    private readonly PluginEventBus _eventBus;
    private IServiceProvider? _applicationServices;
    private bool _isInitialized;
    private bool _disposed;

    public IReadOnlyDictionary<string, LoadedPlugin> LoadedPlugins => _loadedPlugins;
    public IPluginEventBus EventBus => _eventBus;
    public string PluginDirectory => _pluginDirectory;

    public event Action<IPlugin>? PluginLoaded;
    public event Action<string>? PluginUnloaded;
    public Action<string, string>? PluginErrorLog;

    public PluginHost(
        string pluginDirectory,
        string configBaseDir,
        IServiceProvider hostServices)
    {
        _pluginDirectory = pluginDirectory;
        _configBaseDir = configBaseDir;
        _hostServices = hostServices;
        _eventBus = new PluginEventBus();

        Directory.CreateDirectory(_pluginDirectory);
        Directory.CreateDirectory(_configBaseDir);
    }

    public async Task InitializeAsync(IServiceCollection appServices)
    {
        if (_isInitialized) return;

        var pluginDirs = ManifestParser.FindPluginDirectories(_pluginDirectory);
        System.Diagnostics.Debug.WriteLine($"[PluginHost] 发现 {pluginDirs.Count()} 个插件目录");

        foreach (var pluginDir in pluginDirs)
        {
            try
            {
                await LoadPluginFromDirectoryAsync(pluginDir);
            }
            catch (Exception ex)
            {
                var pluginId = Path.GetFileName(pluginDir);
                PluginErrorLog?.Invoke(pluginId, $"加载失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PluginHost] 加载插件 {pluginId} 失败: {ex}");
            }
        }

        _applicationServices = appServices.BuildServiceProvider();
        _isInitialized = true;

        foreach (var kvp in _loadedPlugins.ToList())
        {
            try
            {
                await StartPluginAsync(kvp.Value);
            }
            catch (Exception ex)
            {
                PluginErrorLog?.Invoke(kvp.Key, $"启动失败: {ex.Message}");
                _loadedPlugins[kvp.Key] = kvp.Value with { State = PluginLoadState.Error, ErrorMessage = ex.Message };
            }
        }

        System.Diagnostics.Debug.WriteLine($"[PluginHost] 初始化完成，成功加载 {_loadedPlugins.Count} 个插件");
    }

    public async Task<LoadedPlugin?> LoadPluginFromDirectoryAsync(string pluginDirectory)
    {
        var manifest = await ManifestParser.TryParsePluginDirectoryAsync(pluginDirectory);
        if (manifest == null)
            return null;

        var dllPath = Path.Combine(pluginDirectory, manifest.EntryAssembly);
        if (!File.Exists(dllPath))
        {
            var altDlls = Directory.GetFiles(pluginDirectory, "*.dll")
                .Where(f => !f.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (altDlls.Length == 0)
                throw new FileNotFoundException($"未找到插件程序集: {manifest.EntryAssembly}");

            dllPath = altDlls[0];
        }

        var context = new PluginAssemblyLoadContext(dllPath, isCollectible: true);
        Assembly assembly;
        try
        {
            assembly = context.LoadFromAssemblyPath(dllPath);
        }
        catch (Exception ex)
        {
            context.Unload();
            throw new InvalidOperationException($"加载程序集失败: {dllPath}", ex);
        }

        var pluginType = FindPluginType(assembly);
        if (pluginType == null)
        {
            context.Unload();
            throw new InvalidOperationException($"未在程序集中找到实现 IPlugin 的类型: {dllPath}");
        }

        IPlugin? pluginInstance;
        try
        {
            pluginInstance = (IPlugin?)Activator.CreateInstance(pluginType, true);
        }
        catch (Exception ex)
        {
            context.Unload();
            throw new InvalidOperationException($"创建插件实例失败: {pluginType.FullName}", ex);
        }

        if (pluginInstance == null)
        {
            context.Unload();
            throw new InvalidOperationException("插件实例创建结果为 null");
        }

        var pluginContext = new PluginContext(manifest.Id, manifest, _configBaseDir, _hostServices);
        var loadedPlugin = new LoadedPlugin(
            pluginInstance,
            manifest,
            pluginContext,
            context,
            assembly,
            DateTime.Now,
            PluginLoadState.Loaded
        );

        try
        {
            var tempServices = new ServiceCollection();
            pluginInstance.Initialize(pluginContext, tempServices);
        }
        catch (Exception ex)
        {
            context.Unload();
            throw new InvalidOperationException($"插件 Initialize 阶段异常: {ex.Message}", ex);
        }

        _loadedPlugins[manifest.Id] = loadedPlugin;
        PluginLoaded?.Invoke(pluginInstance);

        System.Diagnostics.Debug.WriteLine($"[PluginHost] 插件 {manifest.Id} ({manifest.Name}) 加载成功");
        return loadedPlugin;
    }

    public async Task StartPluginAsync(LoadedPlugin plugin)
    {
        if (_applicationServices == null)
            throw new InvalidOperationException("应用服务容器尚未构建，请先调用 InitializeAsync");

        if (plugin.State != PluginLoadState.Loaded && plugin.State != PluginLoadState.Error)
            return;

        _loadedPlugins[plugin.Instance.Id] = plugin with { State = PluginLoadState.Starting };

        try
        {
            plugin.Instance.OnStartup(_applicationServices);
            _loadedPlugins[plugin.Instance.Id] = plugin with { State = PluginLoadState.Started };
            System.Diagnostics.Debug.WriteLine($"[PluginHost] 插件 {plugin.Instance.Id} 启动完成");
        }
        catch (Exception ex)
        {
            _loadedPlugins[plugin.Instance.Id] = plugin with { State = PluginLoadState.Error, ErrorMessage = ex.Message };
            throw;
        }
    }

    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
            return false;

        try
        {
            plugin.Instance.OnShutdown();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginHost] 插件 {pluginId} OnShutdown 异常: {ex.Message}");
        }

        try
        {
            plugin.LoadContext.Unload();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginHost] 卸载 ALC 失败: {ex.Message}");
        }

        _loadedPlugins.Remove(pluginId);
        PluginUnloaded?.Invoke(pluginId);

        System.Diagnostics.Debug.WriteLine($"[PluginHost] 插件 {pluginId} 已卸载");
        return true;
    }

    public LoadedPlugin? GetPlugin(string pluginId) =>
        _loadedPlugins.GetValueOrDefault(pluginId);

    public IEnumerable<LoadedPlugin> GetAllPlugins() => _loadedPlugins.Values;

    public IEnumerable<IWebSocketHandler> GetAllWebSocketHandlers() =>
        _loadedPlugins.Values
            .SelectMany(p => p.Context.WebSocketHandlers);

    public IEnumerable<IViewProvider> GetAllViewProviders() =>
        _loadedPlugins.Values
            .SelectMany(p => p.Context.ViewProviders);

    public IEnumerable<IMenuProvider> GetAllMenuProviders() =>
        _loadedPlugins.Values
            .SelectMany(p => p.Context.MenuProviders);

    public IEnumerable<ISettingsProvider> GetAllSettingsProviders() =>
        _loadedPlugins.Values
            .SelectMany(p => p.Context.SettingsProviders);

    public IEnumerable<INotificationHandler> GetAllNotificationHandlers() =>
        _loadedPlugins.Values
            .SelectMany(p => p.Context.NotificationHandlers);

    public IEnumerable<IWidgetProvider> GetAllWidgetProviders() =>
        _loadedPlugins.Values
            .SelectMany(p => p.Context.WidgetProviders);

    private static Type? FindPluginType(Assembly assembly)
    {
        return assembly.GetTypes()
            .FirstOrDefault(t =>
                typeof(IPlugin).IsAssignableFrom(t) &&
                !t.IsAbstract &&
                !t.IsInterface &&
                t.IsClass);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var pluginId in _loadedPlugins.Keys.ToList())
        {
            try
            {
                UnloadPluginAsync(pluginId).GetAwaiter().GetResult();
            }
            catch { }
        }

        _eventBus.Dispose();
        (_applicationServices as IDisposable)?.Dispose();
    }
}
