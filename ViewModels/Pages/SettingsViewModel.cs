using CommunityToolkit.Mvvm.Input;
using Nexus.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.ViewModels.Pages
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;
        private readonly ScheduleService _scheduleService;

        public string DeviceId => _configService.Config.DeviceId;
        public string DeviceName => _configService.Config.DeviceName;
        public string ClassName => _configService.Config.BindInfo?.ClassName ?? "未绑定";
        public string ServerUrl => _configService.Config.ServerUrl;
        public int ClassId => _configService.Config.BindInfo?.ClassId ?? 0;
        public bool IsBound => _configService.Config.IsBound;

        [ObservableProperty]
        private bool _autoStart;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenScheduleConfigCommand))]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasMissingSlots;

        [ObservableProperty]
        private bool _scheduleComplete;

        [ObservableProperty]
        private int _configuredCount;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public ICommand UnbindCommand { get; }

        public event Action? RequestLogout;
        public event Action<int, string>? RequestOpenScheduleConfig;

        public SettingsViewModel(ConfigService configService, AuthService authService, ScheduleService scheduleService)
        {
            _configService = configService;
            _authService = authService;
            _scheduleService = scheduleService;

            _autoStart = CheckAutoStart();
            UnbindCommand = new RelayCommand(OnUnbind);
            
            // 初始化时通知命令重新评估
            OpenScheduleConfigCommand.NotifyCanExecuteChanged();
        }

        public async Task InitializeAsync()
        {
            if (IsBound && ClassId > 0)
            {
                await LoadScheduleCompletenessAsync();
            }
        }

        private async Task LoadScheduleCompletenessAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                var completeness = await _scheduleService.CheckCompletenessAsync(ClassId);
                
                if (completeness != null)
                {
                    HasMissingSlots = !completeness.IsComplete && completeness.MissingSlots.Count > 0;
                    ScheduleComplete = completeness.IsComplete;
                    TotalCount = completeness.FixedTimeSlots.Count * 5;
                    ConfiguredCount = TotalCount - completeness.MissingSlots.Count;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "获取排班配置失败，请检查网络连接";
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = "加载失败: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanOpenScheduleConfig()
        {
            return IsBound && ClassId > 0 && !IsLoading;
        }

        [RelayCommand(CanExecute = nameof(CanOpenScheduleConfig))]
        private void OpenScheduleConfig()
        {
            if (IsBound && ClassId > 0)
            {
                RequestOpenScheduleConfig?.Invoke(ClassId, ClassName);
            }
        }

        private bool CheckAutoStart()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", false);
                    var value = key?.GetValue("Nexus");
                    return value != null;
                }
            }
            catch { }
            return false;
        }

        partial void OnAutoStartChanged(bool value)
        {
            SetAutoStart(value);
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true);

                    if (enable)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key?.SetValue("Nexus", "\"" + exePath + "\"");
                        }
                    }
                    else
                    {
                        key?.DeleteValue("Nexus", false);
                    }
                }
            }
            catch { }
        }

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }
    }
}
