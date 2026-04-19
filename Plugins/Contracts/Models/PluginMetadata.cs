namespace Nexus.Plugins.Contracts.Models;

public record PluginMetadata(
    string Id,
    string Name,
    string Version,
    string Description,
    string Author,
    string AssemblyPath,
    PluginLoadState State = PluginLoadState.Unloaded,
    DateTime? LoadedAt = null,
    string? ErrorMessage = null
)
{
    public string StateDisplayText => State switch
    {
        PluginLoadState.Unloaded => "未加载",
        PluginLoadState.Loading => "加载中...",
        PluginLoadState.Loaded => "已加载",
        PluginLoadState.Starting => "启动中",
        PluginLoadState.Started => "✅ 已启动",
        PluginLoadState.Error => "❌ 错误",
        PluginLoadState.Unloading => "卸载中...",
        _ => State.ToString()
    };
}

public enum PluginLoadState
{
    Unloaded,
    Loading,
    Loaded,
    Starting,
    Started,
    Error,
    Unloading
}
