using System.Text.Json.Serialization;

namespace Nexus.Plugins.Contracts.Models;

public class PluginManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("entryAssembly")]
    public string EntryAssembly { get; set; } = "";

    [JsonPropertyName("entryClass")]
    public string EntryClass { get; set; } = "";

    [JsonPropertyName("dependencies")]
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("capabilities")]
    public PluginCapabilities? Capabilities { get; set; }

    [JsonPropertyName("minHostVersion")]
    public string? MinHostVersion { get; set; }

    [JsonPropertyName("configSchema")]
    public string? ConfigSchema { get; set; }
}

public class PluginCapabilities
{
    [JsonPropertyName("websocketEvents")]
    public string[]? WebsocketEvents { get; set; }

    [JsonPropertyName("views")]
    public List<ManifestViewInfo>? Views { get; set; }

    [JsonPropertyName("widgets")]
    public List<ManifestWidgetInfo>? Widgets { get; set; }

    [JsonPropertyName("menuItems")]
    public List<ManifestMenuItemInfo>? MenuItems { get; set; }
}

public class ManifestViewInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "MainContent";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

public class ManifestWidgetInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}

public class ManifestMenuItemInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}
