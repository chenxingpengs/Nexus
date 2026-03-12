using System;

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
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    public class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        public long Size { get; set; }
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
        public string GitHubOwner { get; set; } = "your-github-username";
        public string GitHubRepo { get; set; } = "Nexus";
        public bool AutoCheckOnStartup { get; set; } = true;
        public int CheckIntervalHours { get; set; } = 4;
        public DateTime? LastCheckTime { get; set; }
        public string? SkippedVersion { get; set; }
    }
}
