using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Nexus.Models;
using Nexus.Services;
using Nexus.Services.Widget;
using Nexus.ViewModels.Pages;
using Nexus.Views;
using Nexus.Views.Pages;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nexus.ViewModels
{
    public class SettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;
        private readonly UpdateService _updateService;
        private readonly ScheduleService _scheduleService;
        private readonly ToastService _toastService;
        private readonly PasswordService _passwordService;
        private DispatcherTimer? _updateCheckTimer;
        private readonly WidgetService? _widgetService;

        private object? _currentPage;
        public object? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private int _selectedNavigationItem;
        public int SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value))
                {
                    NavigateToPage(value);
                }
            }
        }

        private string _deviceStatus = "已连接";
        public string DeviceStatus
        {
            get => _deviceStatus;
            set => SetProperty(ref _deviceStatus, value);
        }

        private string _className = "";
        public string ClassName
        {
            get => _className;
            set => SetProperty(ref _className, value);
        }

        private string _deviceId = "";
        public string DeviceIdDisplay
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        public ObservableCollection<SettingsNavigationItem> NavigationItems { get; }

        public ICommand UnbindCommand { get; }

        public event Action? RequestLogout;

        public SettingsViewModel(ConfigService configService, AuthService authService, UpdateService updateService, WidgetService widgetService, ScheduleService scheduleService, ToastService toastService, PasswordService passwordService)
        {
            _configService = configService;
            _authService = authService;
            _updateService = updateService;
            _widgetService = widgetService;
            _scheduleService = scheduleService;
            _toastService = toastService;
            _passwordService = passwordService;

            NavigationItems = new ObservableCollection<SettingsNavigationItem>
            {
                new SettingsNavigationItem { Label = "系统设置", IconSymbol = Symbol.Setting, Tag = "SystemSettings" },
                new SettingsNavigationItem { Label = "考勤配置", IconSymbol = Symbol.Calendar, Tag = "Schedule" },
                new SettingsNavigationItem { Label = "小组件设置", IconSymbol = Symbol.Setting, Tag = "WidgetSettings" },
                new SettingsNavigationItem { Label = "更新", IconSymbol = Symbol.Sync, Tag = "Update" },
                new SettingsNavigationItem { Label = "插件管理", IconSymbol = Symbol.Add, Tag = "PluginManage" },
                new SettingsNavigationItem { Label = "关于", IconSymbol = Symbol.Help, Tag = "About" }
            };

            UnbindCommand = new RelayCommand(OnUnbind);

            LoadBindInfo();
            _selectedNavigationItem = 0;
            NavigateToPage(0);

            StartUpdateCheck();
        }

        private void LoadBindInfo()
        {
            var config = _configService.Config;

            if (config.BindInfo != null)
            {
                ClassName = config.BindInfo.ClassName;
            }

            DeviceIdDisplay = config.DeviceId;
        }

        private void NavigateToPage(int index)
        {
            switch (index)
            {
                case 0:
                    CurrentPage = new SystemSettingsPage(new ViewModels.Pages.SystemSettingsViewModel(_configService));
                    break;
                case 1:
                    var schedulePage = new SettingsPage(_configService, _authService, _scheduleService);
                    schedulePage.RequestOpenScheduleConfig += OnRequestOpenScheduleConfig;
                    CurrentPage = schedulePage;
                    break;
                case 2:
                    CurrentPage = new WidgetSettingsPage(new WidgetSettingsViewModel(_configService, _widgetService!));
                    break;
                case 3:
                    CurrentPage = new UpdatePage(_updateService);
                    break;
                case 4:
                    var pluginManagePage = new PluginManagePage
                    {
                        DataContext = new PluginManageViewModel(_configService)
                    };
                    CurrentPage = pluginManagePage;
                    break;
                case 5:
                    var aboutPage = new AboutPage(_configService, _authService, _passwordService);
                    aboutPage.RequestLogout += () => RequestLogout?.Invoke();
                    CurrentPage = aboutPage;
                    break;
                default:
                    CurrentPage = new SystemSettingsPage(new ViewModels.Pages.SystemSettingsViewModel(_configService));
                    break;
            }
        }

        private void OnRequestOpenScheduleConfig(int classId, string className)
        {
            OpenScheduleConfigWindow(classId, className);
        }

        private void OpenScheduleConfigWindow(int classId, string className)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var viewModel = new ScheduleSetupViewModel(_scheduleService, _configService);
                var window = new ScheduleSetupWindow
                {
                    DataContext = viewModel
                };

                viewModel.SetupCompleted += () =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        window.Close();
                    });
                };

                viewModel.RequestSkip += () =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        window.Close();
                    });
                };

                window.Show();
                await viewModel.InitializeAsync(classId, className);
            });
        }

        private void StartUpdateCheck()
        {
            if (_updateService.ShouldCheckForUpdate())
            {
                _ = CheckForUpdateAsync();
            }

            _updateCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(_updateService.UpdateConfig.CheckIntervalHours)
            };
            _updateCheckTimer.Tick += async (s, e) =>
            {
                await CheckForUpdateAsync();
            };
            _updateCheckTimer.Start();
        }

        private async Task CheckForUpdateAsync()
        {
            var updateInfo = await _updateService.CheckForUpdateAsync();
            if (updateInfo != null)
            {
                if (_updateService.UpdateConfig.AutoDownloadAndInstall)
                {
                    _toastService.ShowInfo($"发现新版本 {updateInfo.LatestVersion}，正在后台下载...");
                    
                    var filePath = await _updateService.DownloadUpdateAsync(updateInfo);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        _toastService.ShowSuccess($"新版本 {updateInfo.LatestVersion} 下载完成，即将自动安装...", "更新就绪");
                        
                        await Task.Delay(2000);
                        _updateService.InstallUpdate(filePath);
                    }
                    else
                    {
                        _toastService.ShowError("更新下载失败，请稍后重试");
                    }
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        SelectedNavigationItem = 3;
                        _toastService.ShowInfo($"发现新版本 {updateInfo.LatestVersion}，请前往更新页面查看");
                    });
                }
            }
        }

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }

        public void Dispose()
        {
            if (_updateCheckTimer != null)
            {
                _updateCheckTimer.Stop();
                _updateCheckTimer = null;
            }

            _widgetService?.Stop();
        }
    }

    public class SettingsNavigationItem
    {
        public string Label { get; set; } = "";
        public Symbol IconSymbol { get; set; }
        public string Tag { get; set; } = "";
    }
}
