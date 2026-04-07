using Microsoft.Extensions.DependencyInjection;
using Nexus.Plugins.Contracts;
using Nexus.Plugins.Contracts.Models;
using Nexus.Plugins.Core;

namespace Nexus.Plugins.Services;

public class PluginService : IDisposable
{
    private readonly PluginHost _pluginHost;
    private readonly WebSocketBridgeService? _wsBridge;
    private bool _isInitialized;

    public PluginHost Host => _pluginHost;
    public WebSocketBridgeService? WSBridge => _wsBridge;

    public event Action<PluginMetadata>? PluginLoaded;
    public event Action<string>? PluginUnloaded;
    public event Action<ConnectionState, string?>? WSConnectionStateChanged;

    public PluginService(PluginHost pluginHost, WebSocketBridgeService? wsBridge = null)
    {
        _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
        _wsBridge = wsBridge;

        _pluginHost.PluginLoaded += OnPluginLoadedInternal;
        _pluginHost.PluginUnloaded += (id) => PluginUnloaded?.Invoke(id);
        _pluginHost.PluginErrorLog += (id, msg) =>
            System.Diagnostics.Debug.WriteLine($"[PluginService] 插件 {id} 错误: {msg}");

        if (_wsBridge != null)
        {
            _wsBridge.ConnectionStateChanged += (state, err) => WSConnectionStateChanged?.Invoke(state, err);
            _wsBridge.ErrorHandler += (handler, op, ex) =>
                System.Diagnostics.Debug.WriteLine($"[PluginService] WS处理器 {handler} 在 {op} 异常: {ex.Message}");
        }
    }

    public async Task InitializeAsync(IServiceCollection appServices)
    {
        if (_isInitialized) return;

        await _pluginHost.InitializeAsync(appServices);

        _isInitialized = true;

        foreach (var plugin in _pluginHost.GetAllPlugins())
        {
            var meta = new PluginMetadata(
                plugin.Manifest.Id,
                plugin.Manifest.Name,
                plugin.Manifest.Version,
                plugin.Manifest.Description,
                plugin.Manifest.Author,
                plugin.Assembly.Location,
                plugin.State,
                plugin.LoadedAt
            );
            PluginLoaded?.Invoke(meta);
        }

        System.Diagnostics.Debug.WriteLine($"[PluginService] 初始化完成，{_pluginHost.LoadedPlugins.Count} 个插件就绪");
    }

    public async Task<bool> InstallPluginAsync(string pluginPackagePath)
    {
        var targetDir = Path.Combine(_pluginHost.PluginDirectory, Path.GetFileNameWithoutExtension(pluginPackagePath));

        if (Directory.Exists(targetDir))
            throw new InvalidOperationException($"插件目录已存在: {targetDir}");

        Directory.CreateDirectory(targetDir);

        try
        {
            ZipFile.ExtractToDirectory(pluginPackagePath, targetDir);
            var loaded = await _pluginHost.LoadPluginFromDirectoryAsync(targetDir);

            if (loaded != null && _isInitialized)
            {
                await _pluginHost.StartPluginAsync(loaded);
                _wsBridge?.RegisterHandlersFromPluginHost();
            }

            return loaded != null;
        }
        catch
        {
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);
            throw;
        }
    }

    public async Task<bool> UninstallPluginAsync(string pluginId)
    {
        return await _pluginHost.UnloadPluginAsync(pluginId);
    }

    public IEnumerable<PluginMetadata> GetAllPluginMetadata()
    {
        return _pluginHost.GetAllPlugins().Select(p => new PluginMetadata(
            p.Manifest.Id,
            p.Manifest.Name,
            p.Manifest.Version,
            p.Manifest.Description,
            p.Manifest.Author,
            p.Assembly.Location,
            p.State,
            p.LoadedAt,
            p.ErrorMessage
        ));
    }

    public LoadedPlugin? GetPlugin(string pluginId) => _pluginHost.GetPlugin(pluginId);

    private void OnPluginLoadedInternal(IPlugin plugin)
    {
        var loaded = _pluginHost.GetPlugin(plugin.Id);
        if (loaded != null)
        {
            PluginLoaded?.Invoke(new PluginMetadata(
                loaded.Manifest.Id,
                loaded.Manifest.Name,
                loaded.Manifest.Version,
                loaded.Manifest.Description,
                loaded.Manifest.Author,
                loaded.Assembly.Location,
                loaded.State,
                loaded.LoadedAt
            ));
        }
    }

    public void Dispose()
    {
        _pluginHost.Dispose();
        _wsBridge?.Dispose();
    }
}
