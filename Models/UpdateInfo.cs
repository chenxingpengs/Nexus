using System;
using System.Text.Json.Serialization;

namespace Nexus.Models
{
    public enum UpdateStatus
    {
        Idle,
        Checking,
        UpdateAvailable,
        NoUpdate,
        Downloading,
        DownloadComplete,
        Installing,
        Error
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = string.Empty;
    }

    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool IsPrerelease { get; set; }
    }

    public class UpdateProgress
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public double ProgressPercentage => TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100 : 0;
        public double SpeedBytesPerSecond { get; set; }
    }

    public class UpdateConfig
    {
        public string GitHubOwner { get; set; } = "chenxingpengs";
        public string GitHubRepo { get; set; } = "Nexus";
        public bool AutoCheckOnStartup { get; set; } = true;
        public bool AutoDownloadAndInstall { get; set; } = true;
        public int CheckIntervalHours { get; set; } = 4;
        public DateTime? LastCheckTime { get; set; }
        public string? SkippedVersion { get; set; }
        public bool UseMirror { get; set; } = true;
        public string MirrorUrl { get; set; } = "https://gh-proxy.com";
    }
}
