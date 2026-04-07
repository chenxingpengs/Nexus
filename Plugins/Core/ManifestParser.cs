using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexus.Plugins.Contracts.Models;

namespace Nexus.Plugins.Core;

public static class ManifestParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<PluginManifest> ParseFromFileAsync(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"插件清单文件不存在: {manifestPath}");

        var json = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8);
        return ParseFromString(json, Path.GetDirectoryName(manifestPath)!);
    }

    public static PluginManifest ParseFromString(string json, string? basePath = null)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions)
            ?? throw new InvalidOperationException("无法解析插件清单");

        if (string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidOperationException("插件清单缺少 id 字段");

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            var dirName = basePath != null ? Path.GetFileName(basePath.TrimEnd(Path.DirectorySeparatorChar)) : "";
            manifest.EntryAssembly = $"{manifest.Id}.dll";
        }

        return manifest;
    }

    public static async Task<PluginManifest?> TryParsePluginDirectoryAsync(string pluginDir)
    {
        var manifestPath = Path.Combine(pluginDir, "manifest.json");
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            return await ParseFromFileAsync(manifestPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static IEnumerable<string> FindPluginDirectories(string pluginsRootDir)
    {
        if (!Directory.Exists(pluginsRootDir))
            return Array.Empty<string>();

        return Directory.GetDirectories(pluginsRootDir)
            .Where(d =>
            {
                var dirName = Path.GetFileName(d);
                if (dirName.StartsWith("_") || dirName.StartsWith("."))
                    return false;
                if (dirName.Equals("__pycache__", StringComparison.OrdinalIgnoreCase))
                    return false;
                return File.Exists(Path.Combine(d, "manifest.json"));
            });
    }
}
