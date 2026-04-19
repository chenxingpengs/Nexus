using CommunityToolkit.Mvvm.Input;
using Nexus.Models;
using Nexus.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nexus.ViewModels.Pages
{
    public class UpdateViewModel : ViewModelBase
    {
        private readonly UpdateService _updateService;
        private CancellationTokenSource? _downloadCts;

        private UpdateStatus _status = UpdateStatus.Idle;
        public UpdateStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private UpdateInfo? _availableUpdate;
        public UpdateInfo? AvailableUpdate
        {
            get => _availableUpdate;
            set => SetProperty(ref _availableUpdate, value);
        }

        private double _downloadProgress;
        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        private string _downloadSpeed = "";
        public string DownloadSpeed
        {
            get => _downloadSpeed;
            set => SetProperty(ref _downloadSpeed, value);
        }

        private string _downloadedSize = "";
        public string DownloadedSize
        {
            get => _downloadedSize;
            set => SetProperty(ref _downloadedSize, value);
        }

        private bool _autoCheckUpdate = true;
        public bool AutoCheckUpdate
        {
            get => _autoCheckUpdate;
            set
            {
                if (SetProperty(ref _autoCheckUpdate, value))
                {
                    _updateService.SetAutoCheck(value);
                }
            }
        }

        private bool _autoDownloadAndInstall = true;
        public bool AutoDownloadAndInstall
        {
            get => _autoDownloadAndInstall;
            set
            {
                if (SetProperty(ref _autoDownloadAndInstall, value))
                {
                    _updateService.SetAutoDownloadAndInstall(value);
                }
            }
        }

        private string _githubOwner = "";
        public string GitHubOwner
        {
            get => _githubOwner;
            set => SetProperty(ref _githubOwner, value);
        }

        private string _githubRepo = "";
        public string GitHubRepo
        {
            get => _githubRepo;
            set => SetProperty(ref _githubRepo, value);
        }

        public string CurrentVersion => UpdateService.CurrentVersion;
        public string LatestVersion => AvailableUpdate?.LatestVersion ?? "-";
        public string ReleaseNotes => AvailableUpdate?.ReleaseNotes ?? "";
        public string FileSize => AvailableUpdate != null ? UpdateService.FormatFileSize(AvailableUpdate.FileSize) : "-";
        public string ReleaseDate => AvailableUpdate?.ReleaseDate.ToString("yyyy-MM-dd") ?? "-";

        public bool IsIdle => Status == UpdateStatus.Idle;
        public bool IsChecking => Status == UpdateStatus.Checking;
        public bool IsUpdateAvailable => Status == UpdateStatus.UpdateAvailable && AvailableUpdate != null;
        public bool IsNoUpdate => Status == UpdateStatus.NoUpdate;
        public bool IsDownloading => Status == UpdateStatus.Downloading;
        public bool IsDownloadComplete => Status == UpdateStatus.DownloadComplete;
        public bool IsInstalling => Status == UpdateStatus.Installing;
        public bool HasError => Status == UpdateStatus.Error;

        public ICommand CheckUpdateCommand { get; }
        public ICommand DownloadUpdateCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand InstallUpdateCommand { get; }
        public ICommand SkipVersionCommand { get; }
        public ICommand SaveConfigCommand { get; }

        public UpdateViewModel(UpdateService updateService)
        {
            _updateService = updateService;

            _autoCheckUpdate = _updateService.UpdateConfig.AutoCheckOnStartup;
            _autoDownloadAndInstall = _updateService.UpdateConfig.AutoDownloadAndInstall;
            _githubOwner = _updateService.UpdateConfig.GitHubOwner;
            _githubRepo = _updateService.UpdateConfig.GitHubRepo;

            _status = _updateService.CurrentStatus;
            _statusMessage = _updateService.CurrentStatusMessage;
            _availableUpdate = _updateService.CurrentUpdateInfo;
            _downloadedFilePath = _updateService.DownloadedFilePath;

            if (_updateService.CurrentProgress != null)
            {
                _downloadProgress = _updateService.CurrentProgress.ProgressPercentage;
                _downloadSpeed = FormatSpeed(_updateService.CurrentProgress.SpeedBytesPerSecond);
                _downloadedSize = $"{UpdateService.FormatFileSize(_updateService.CurrentProgress.BytesReceived)} / {UpdateService.FormatFileSize(_updateService.CurrentProgress.TotalBytes)}";
            }

            CheckUpdateCommand = new AsyncRelayCommand(CheckUpdateAsync, () => !IsChecking && !IsDownloading);
            DownloadUpdateCommand = new AsyncRelayCommand(DownloadUpdateAsync, () => IsUpdateAvailable);
            CancelDownloadCommand = new RelayCommand(CancelDownload, () => IsDownloading);
            InstallUpdateCommand = new AsyncRelayCommand(InstallUpdateAsync, () => IsDownloadComplete);
            SkipVersionCommand = new RelayCommand(SkipVersion, () => IsUpdateAvailable);
            SaveConfigCommand = new RelayCommand(SaveConfig);

            _updateService.StatusChanged += OnStatusChanged;
            _updateService.ProgressChanged += OnProgressChanged;
        }

        private void OnStatusChanged(UpdateStatus status, string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Status = status;
                StatusMessage = message;

                if (status == UpdateStatus.UpdateAvailable && _updateService.CurrentUpdateInfo != null)
                {
                    AvailableUpdate = _updateService.CurrentUpdateInfo;
                }

                if (status == UpdateStatus.DownloadComplete)
                {
                    _downloadedFilePath = _updateService.DownloadedFilePath;
                }

                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsChecking));
                OnPropertyChanged(nameof(IsUpdateAvailable));
                OnPropertyChanged(nameof(IsNoUpdate));
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(IsDownloadComplete));
                OnPropertyChanged(nameof(IsInstalling));
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(LatestVersion));
                OnPropertyChanged(nameof(ReleaseNotes));
                OnPropertyChanged(nameof(FileSize));
                OnPropertyChanged(nameof(ReleaseDate));

                ((AsyncRelayCommand)CheckUpdateCommand).NotifyCanExecuteChanged();
                ((AsyncRelayCommand)DownloadUpdateCommand).NotifyCanExecuteChanged();
                ((RelayCommand)CancelDownloadCommand).NotifyCanExecuteChanged();
                ((AsyncRelayCommand)InstallUpdateCommand).NotifyCanExecuteChanged();
                ((RelayCommand)SkipVersionCommand).NotifyCanExecuteChanged();
            });
        }

        private void OnProgressChanged(UpdateProgress progress)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DownloadProgress = progress.ProgressPercentage;
                DownloadSpeed = FormatSpeed(progress.SpeedBytesPerSecond);
                DownloadedSize = $"{UpdateService.FormatFileSize(progress.BytesReceived)} / {UpdateService.FormatFileSize(progress.TotalBytes)}";
            });
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F0} B/s";
            else if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            else
                return $"{bytesPerSecond / 1024 / 1024:F1} MB/s";
        }

        private async Task CheckUpdateAsync()
        {
            AvailableUpdate = await _updateService.CheckForUpdateAsync();
        }

        private async Task DownloadUpdateAsync()
        {
            if (AvailableUpdate == null) return;

            _downloadCts = new CancellationTokenSource();
            var filePath = await _updateService.DownloadUpdateAsync(AvailableUpdate, _downloadCts.Token);

            if (!string.IsNullOrEmpty(filePath))
            {
                _downloadedFilePath = filePath;
            }
        }

        private string? _downloadedFilePath;

        private async Task InstallUpdateAsync()
        {
            if (string.IsNullOrEmpty(_downloadedFilePath))
            {
                _downloadedFilePath = _updateService.DownloadedFilePath;
            }
            
            if (string.IsNullOrEmpty(_downloadedFilePath))
            {
                Status = UpdateStatus.Error;
                StatusMessage = "安装文件路径无效，请重新下载";
                OnPropertyChanged(nameof(HasError));
                return;
            }

            Status = UpdateStatus.Installing;
            StatusMessage = "正在启动安装程序...";
            OnPropertyChanged(nameof(IsInstalling));
            OnPropertyChanged(nameof(IsDownloadComplete));

            var success = await Task.Run(() =>
            {
                return _updateService.InstallUpdate(_downloadedFilePath);
            });

            if (!success)
            {
                Status = UpdateStatus.Error;
                StatusMessage = "安装程序启动失败，请重试";
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(IsInstalling));
            }
        }

        private void CancelDownload()
        {
            _downloadCts?.Cancel();
        }

        private void SkipVersion()
        {
            if (AvailableUpdate != null)
            {
                _updateService.SkipVersion(AvailableUpdate.LatestVersion);
                Status = UpdateStatus.NoUpdate;
                StatusMessage = "已跳过此版本";
                AvailableUpdate = null;
                OnPropertyChanged(nameof(IsUpdateAvailable));
                OnPropertyChanged(nameof(IsNoUpdate));
            }
        }

        private void SaveConfig()
        {
            _updateService.SetUpdateConfig(GitHubOwner, GitHubRepo);
            StatusMessage = "配置已保存";
        }

        public void OnNavigatedTo()
        {
            if (Status == UpdateStatus.Idle && _updateService.ShouldCheckForUpdate())
            {
                _ = CheckUpdateAsync();
            }
        }
    }
}
