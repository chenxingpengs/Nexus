using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Models;

namespace Nexus.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ConfigService _configService;
        private readonly string _updateConfigFile;
        private UpdateConfig _updateConfig;

        public static readonly string CurrentVersion = "1.0.0";

        public event Action<UpdateStatus, string>? StatusChanged;
        public event Action<UpdateProgress>? ProgressChanged;
        public event Action<UpdateInfo>? UpdateAvailable;

        public UpdateConfig UpdateConfig => _updateConfig;

        public UpdateService(ConfigService configService)
        {
            _configService = configService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Nexus/{CurrentVersion}");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Nexus"
            );
            _updateConfigFile = Path.Combine(configDir, "update.json");
            _updateConfig = LoadUpdateConfig();
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
            StatusChanged?.Invoke(UpdateStatus.Checking, "正在检查更新...");

            try
            {
                var url = $"https://api.github.com/repos/{_updateConfig.GitHubOwner}/{_updateConfig.GitHubRepo}/releases/latest";
                var response = await _httpClient.GetStringAsync(url);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (release == null)
                {
                    StatusChanged?.Invoke(UpdateStatus.Error, "无法获取版本信息");
                    return null;
                }

                _updateConfig.LastCheckTime = DateTime.Now;
                SaveUpdateConfig();

                var latestVersion = release.TagName.TrimStart('v');
                var currentVersion = CurrentVersion;

                if (CompareVersions(latestVersion, currentVersion) > 0)
                {
                    var updateInfo = ParseUpdateInfo(release);
                    
                    if (_updateConfig.SkippedVersion == latestVersion)
                    {
                        StatusChanged?.Invoke(UpdateStatus.NoUpdate, "已是最新版本");
                        return null;
                    }

                    StatusChanged?.Invoke(UpdateStatus.UpdateAvailable, $"发现新版本 {latestVersion}");
                    UpdateAvailable?.Invoke(updateInfo);
                    return updateInfo;
                }
                else
                {
                    StatusChanged?.Invoke(UpdateStatus.NoUpdate, "已是最新版本");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] CheckForUpdateAsync error: {ex.Message}");
                StatusChanged?.Invoke(UpdateStatus.Error, $"检查更新失败: {ex.Message}");
                return null;
            }
        }

        private UpdateInfo ParseUpdateInfo(GitHubRelease release)
        {
            var updateInfo = new UpdateInfo
            {
                LatestVersion = release.TagName.TrimStart('v'),
                ReleaseNotes = release.Body,
                ReleaseDate = release.PublishedAt
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
            StatusChanged?.Invoke(UpdateStatus.Downloading, "正在下载更新...");

            var tempDir = Path.Combine(Path.GetTempPath(), "NexusUpdate");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var filePath = Path.Combine(tempDir, updateInfo.FileName);

            try
            {
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

                        ProgressChanged?.Invoke(new UpdateProgress
                        {
                            BytesReceived = totalBytesRead,
                            TotalBytes = totalBytes,
                            SpeedBytesPerSecond = speed
                        });

                        lastReportTime = now;
                    }
                }

                ProgressChanged?.Invoke(new UpdateProgress
                {
                    BytesReceived = totalBytesRead,
                    TotalBytes = totalBytes,
                    SpeedBytesPerSecond = 0
                });

                StatusChanged?.Invoke(UpdateStatus.DownloadComplete, "下载完成");
                return filePath;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(UpdateStatus.Error, "下载已取消");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] DownloadUpdateAsync error: {ex.Message}");
                StatusChanged?.Invoke(UpdateStatus.Error, $"下载失败: {ex.Message}");
                return null;
            }
        }

        public bool InstallUpdate(string filePath)
        {
            StatusChanged?.Invoke(UpdateStatus.Installing, "正在安装更新...");

            try
            {
                if (!File.Exists(filePath))
                {
                    StatusChanged?.Invoke(UpdateStatus.Error, "安装文件不存在");
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
                StatusChanged?.Invoke(UpdateStatus.Error, $"安装失败: {ex.Message}");
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

        private int CompareVersions(string version1, string version2)
        {
            var parts1 = version1.Split('.');
            var parts2 = version2.Split('.');

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

            return 0;
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
}
