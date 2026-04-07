namespace Nexus.Models;

public record RemotePluginInfo(
    string Id,
    string Name,
    string Version,
    string Description,
    string Author,
    string? Icon,
    bool Installed,
    bool Enabled,
    string EntryType,
    string? GithubRepo,
    string? DownloadUrl,
    string? HtmlUrl,
    string? LatestVersion,
    bool? HasUpdate,
    string? PublishedAt
)
{
    public string InstallButtonText =>
        Installed ? (HasUpdate == true ? "更新" : "已安装") : "安装";

    public string InstallButtonColor => this switch
    {
        { Installed: false } => "#1976D2",
        { HasUpdate: true } => "#FF9800",
        _ => "#9E9E9E"
    };

    public bool HasNoDownloadUrl => string.IsNullOrEmpty(DownloadUrl);
}

public record MarketPluginItem(
    string PluginId,
    string Name,
    string Version,
    string Description,
    string Author,
    string DownloadUrl,
    string HtmlUrl,
    string PublishedAt
);
