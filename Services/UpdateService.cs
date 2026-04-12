using Nexus.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Services.Http;

namespace Nexus.Services;

public class UpdateService : HttpService
{
    private readonly string _updateConfigFile;
    private UpdateConfig _updateConfig;

    public static readonly string CurrentVersion = typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public event Action<UpdateStatus, string>? StatusChanged;
    public event Action<UpdateProgress>? ProgressChanged;
    public event Action<UpdateInfo>? UpdateAvailable;

    private UpdateStatus _currentStatus = UpdateStatus.Idle;
    public UpdateStatus CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            if (_currentStatus != value)
            {
                _currentStatus = value;
                StatusChanged?.Invoke(value, _currentStatusMessage);
            }
        }
    }

    private string _currentStatusMessage = "";
    public string CurrentStatusMessage
    {
        get => _currentStatusMessage;
        private set
        {
            _currentStatusMessage = value;
        }
    }

    private UpdateInfo? _currentUpdateInfo;
    public UpdateInfo? CurrentUpdateInfo
    {
        get => _currentUpdateInfo;
        private set => _currentUpdateInfo = value;
    }

    private string? _downloadedFilePath;
    public string? DownloadedFilePath
    {
        get => _downloadedFilePath;
        private set => _downloadedFilePath = value;
    }

    private UpdateProgress _currentProgress = new UpdateProgress();
    public UpdateProgress CurrentProgress => _currentProgress;

    public UpdateConfig UpdateConfig => _updateConfig;

    private void SetStatus(UpdateStatus status, string message)
    {
        _currentStatus = status;
        _currentStatusMessage = message;
        StatusChanged?.Invoke(status, message);
    }

    private void SetProgress(UpdateProgress progress)
    {
        _currentProgress = progress;
        ProgressChanged?.Invoke(progress);
    }

    public UpdateService(ConfigService configService, ToastService? toastService = null) 
        : base(configService, toastService)
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexus"
        );
        _updateConfigFile = Path.Combine(configDir, "update.json");
        _updateConfig = LoadUpdateConfig();
    }

    private string GetMirrorUrl(string originalUrl)
    {
        if (!_updateConfig.UseMirror || string.IsNullOrEmpty(_updateConfig.MirrorUrl))
        {
            return originalUrl;
        }

        var mirrorBase = _updateConfig.MirrorUrl.TrimEnd('/');
        
        if (originalUrl.StartsWith("https://api.github.com/"))
        {
            return $"{mirrorBase}/{originalUrl}";
        }
        
        if (originalUrl.StartsWith("https://github.com/") || 
            originalUrl.StartsWith("https://raw.githubusercontent.com/") ||
            originalUrl.StartsWith("https://objects.githubusercontent.com/"))
        {
            return $"{mirrorBase}/{originalUrl}";
        }
        
        return originalUrl;
    }

    private UpdateConfig LoadUpdateConfig()
    {
        try
        {
            if (File.Exists(_updateConfigFile))
            {
                var json = File.ReadAllText(_updateConfigFile);
                return JsonSerializer.Deserialize<UpdateConfig>(json) ?? new UpdateConfig();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] LoadUpdateConfig error: {ex.Message}");
        }
        return new UpdateConfig();
    }

    private void SaveUpdateConfig()
    {
        try
        {
            var configDir = Path.GetDirectoryName(_updateConfigFile);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir!);
            }

            var json = JsonSerializer.Serialize(_updateConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_updateConfigFile, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] SaveUpdateConfig error: {ex.Message}");
        }
    }

    public bool ShouldCheckForUpdate()
    {
        if (!_updateConfig.AutoCheckOnStartup)
            return false;

        if (_updateConfig.LastCheckTime == null)
            return true;

        var timeSinceLastCheck = DateTime.Now - _updateConfig.LastCheckTime.Value;
        return timeSinceLastCheck.TotalHours >= _updateConfig.CheckIntervalHours;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        SetStatus(UpdateStatus.Checking, "正在检查更新...");

        try
        {
            var url = $"https://api.github.com/repos/{_updateConfig.GitHubOwner}/{_updateConfig.GitHubRepo}/releases/latest";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd($"Nexus/{CurrentVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github.v3+json");
            
            using var response = await HttpClient.SendAsync(request);
            
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
                {
                    var resetTimestamp = long.Parse(resetValues.First());
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetTimestamp).LocalDateTime;
                    var waitTime = resetTime - DateTime.Now;
                    
                    if (waitTime.TotalMinutes > 0)
                    {
                        SetStatus(UpdateStatus.Error, $"GitHub API 速率限制，请 {waitTime.Minutes} 分钟后重试");
                        return null;
                    }
                }
                
                SetStatus(UpdateStatus.Error, "GitHub API 速率限制，请稍后重试");
                return null;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                SetStatus(UpdateStatus.Error, $"检查更新失败: HTTP {(int)response.StatusCode}");
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(content))
            {
                SetStatus(UpdateStatus.Error, "无法获取版本信息");
                return null;
            }

            var trimmedContent = content.TrimStart();
            if (!trimmedContent.StartsWith("{") && !trimmedContent.StartsWith("["))
            {
                Debug.WriteLine($"[UpdateService] GitHub API 返回非 JSON 内容: {content.Substring(0, Math.Min(200, content.Length))}...");
                SetStatus(UpdateStatus.Error, "GitHub API 返回异常，可能触发了速率限制");
                return null;
            }

            var release = JsonSerializer.Deserialize<GitHubRelease>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (release == null)
            {
                SetStatus(UpdateStatus.Error, "无法获取版本信息");
                return null;
            }

            _updateConfig.LastCheckTime = DateTime.Now;
            SaveUpdateConfig();

            var latestVersion = release.TagName.TrimStart('v');
            var currentVersion = CurrentVersion;

            if (CompareVersions(latestVersion, currentVersion) > 0)
            {
                var updateInfo = ParseUpdateInfo(release);
                _currentUpdateInfo = updateInfo;

                if (_updateConfig.SkippedVersion == latestVersion)
                {
                    SetStatus(UpdateStatus.NoUpdate, "已是最新版本");
                    return null;
                }

                SetStatus(UpdateStatus.UpdateAvailable, $"发现新版本 {latestVersion}");
                UpdateAvailable?.Invoke(updateInfo);
                return updateInfo;
            }
            else
            {
                SetStatus(UpdateStatus.NoUpdate, "已是最新版本");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] CheckForUpdateAsync error: {ex.Message}");
            SetStatus(UpdateStatus.Error, $"检查更新失败: {ex.Message}");
            return null;
        }
    }

    private UpdateInfo ParseUpdateInfo(GitHubRelease release)
    {
        var updateInfo = new UpdateInfo
        {
            LatestVersion = release.TagName.TrimStart('v'),
            ReleaseNotes = release.Body,
            ReleaseDate = release.PublishedAt,
            IsPrerelease = release.Prerelease
        };

        foreach (var asset in release.Assets)
        {
            if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                updateInfo.DownloadUrl = asset.BrowserDownloadUrl;
                updateInfo.FileSize = asset.Size;
                updateInfo.FileName = asset.Name;
                break;
            }
        }

        return updateInfo;
    }

    public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        SetStatus(UpdateStatus.Downloading, "正在下载更新...");

        var tempDir = Path.Combine(Path.GetTempPath(), "NexusUpdate");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        var filePath = Path.Combine(tempDir, updateInfo.FileName);

        try
        {
            var downloadUrl = GetMirrorUrl(updateInfo.DownloadUrl);
            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = updateInfo.FileSize > 0 ? updateInfo.FileSize : response.Content.Headers.ContentLength ?? 0;
            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            var startTime = DateTime.Now;
            var lastReportTime = startTime;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                var now = DateTime.Now;
                if ((now - lastReportTime).TotalMilliseconds >= 100)
                {
                    var elapsed = (now - startTime).TotalSeconds;
                    var speed = elapsed > 0 ? totalBytesRead / elapsed : 0;

                    SetProgress(new UpdateProgress
                    {
                        BytesReceived = totalBytesRead,
                        TotalBytes = totalBytes,
                        SpeedBytesPerSecond = speed
                    });

                    lastReportTime = now;
                }
            }

            SetProgress(new UpdateProgress
            {
                BytesReceived = totalBytesRead,
                TotalBytes = totalBytes,
                SpeedBytesPerSecond = 0
            });

            _downloadedFilePath = filePath;
            SetStatus(UpdateStatus.DownloadComplete, "下载完成");
            return filePath;
        }
        catch (OperationCanceledException)
        {
            SetStatus(UpdateStatus.Error, "下载已取消");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] DownloadUpdateAsync error: {ex.Message}");
            SetStatus(UpdateStatus.Error, $"下载失败: {ex.Message}");
            return null;
        }
    }

    public bool InstallUpdate(string filePath)
    {
        SetStatus(UpdateStatus.Installing, "正在安装更新...");

        try
        {
            if (!File.Exists(filePath))
            {
                SetStatus(UpdateStatus.Error, "安装文件不存在");
                return false;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };

            if (filePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                processStartInfo.Arguments = "/quiet /norestart";
            }
            else if (filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processStartInfo.Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
            }

            Process.Start(processStartInfo);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] InstallUpdate error: {ex.Message}");
            SetStatus(UpdateStatus.Error, $"安装失败: {ex.Message}");
            return false;
        }
    }

    public void SkipVersion(string version)
    {
        _updateConfig.SkippedVersion = version;
        SaveUpdateConfig();
    }

    public void ClearSkippedVersion()
    {
        _updateConfig.SkippedVersion = null;
        SaveUpdateConfig();
    }

    public void SetUpdateConfig(string owner, string repo)
    {
        _updateConfig.GitHubOwner = owner;
        _updateConfig.GitHubRepo = repo;
        SaveUpdateConfig();
    }

    public void SetAutoCheck(bool autoCheck)
    {
        _updateConfig.AutoCheckOnStartup = autoCheck;
        SaveUpdateConfig();
    }

    public void SetAutoDownloadAndInstall(bool autoDownload)
    {
        _updateConfig.AutoDownloadAndInstall = autoDownload;
        SaveUpdateConfig();
    }

    public void SetMirrorConfig(bool useMirror, string mirrorUrl)
    {
        _updateConfig.UseMirror = useMirror;
        _updateConfig.MirrorUrl = mirrorUrl;
        SaveUpdateConfig();
    }

    public void ResetStatus()
    {
        _currentStatus = UpdateStatus.Idle;
        _currentStatusMessage = "";
        _currentUpdateInfo = null;
        _downloadedFilePath = null;
        _currentProgress = new UpdateProgress();
        StatusChanged?.Invoke(UpdateStatus.Idle, "");
    }

    public bool InstallDownloadedUpdate()
    {
        if (string.IsNullOrEmpty(_downloadedFilePath) || !File.Exists(_downloadedFilePath))
        {
            return false;
        }
        return InstallUpdate(_downloadedFilePath);
    }

    private int CompareVersions(string version1, string version2)
    {
        version1 = version1.TrimStart('v');
        version2 = version2.TrimStart('v');

        var mainPart1 = version1.Split('-', 2)[0];
        var mainPart2 = version2.Split('-', 2)[0];

        var parts1 = mainPart1.Split('.');
        var parts2 = mainPart2.Split('.');

        var maxLength = Math.Max(parts1.Length, parts2.Length);

        for (int i = 0; i < maxLength; i++)
        {
            var num1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
            var num2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;

            if (num1 != num2)
            {
                return num1.CompareTo(num2);
            }
        }

        var hasPreRelease1 = version1.Contains('-');
        var hasPreRelease2 = version2.Contains('-');

        if (!hasPreRelease1 && hasPreRelease2)
        {
            return 1;
        }
        if (hasPreRelease1 && !hasPreRelease2)
        {
            return -1;
        }
        if (hasPreRelease1 && hasPreRelease2)
        {
            var preRelease1 = version1.Split('-', 2)[1];
            var preRelease2 = version2.Split('-', 2)[1];

            return ComparePreRelease(preRelease1, preRelease2);
        }

        return 0;
    }

    private int ComparePreRelease(string pre1, string pre2)
    {
        var precedence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", 0 },
            { "beta", 1 },
            { "rc", 2 },
            { "preview", 0 }
        };

        static (string type, string num) ParsePreRelease(string pre)
        {
            var match = System.Text.RegularExpressions.Regex.Match(pre, @"^([a-zA-Z]+)\.?(\d*)$");
            if (match.Success)
            {
                return (match.Groups[1].Value.ToLower(), match.Groups[2].Value);
            }
            return (pre.ToLower(), "");
        }

        var (type1, num1) = ParsePreRelease(pre1);
        var (type2, num2) = ParsePreRelease(pre2);

        var priority1 = precedence.TryGetValue(type1, out var p1) ? p1 : int.MaxValue;
        var priority2 = precedence.TryGetValue(type2, out var p2) ? p2 : int.MaxValue;

        if (priority1 != priority2)
        {
            return priority1.CompareTo(priority2);
        }

        var n1 = int.TryParse(num1, out var v1) ? v1 : 0;
        var n2 = int.TryParse(num2, out var v2) ? v2 : 0;

        return n1.CompareTo(n2);
    }

    public static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}


