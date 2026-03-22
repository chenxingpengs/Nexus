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
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nexus.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;
        private readonly UpdateService _updateService;
        private readonly ScheduleService _scheduleService;
        private SocketIOService? _socketIOService;
        private DispatcherTimer? _updateCheckTimer;
        private readonly PowerControlService _powerControlService;
        private readonly WolService _wolService;
        private NotificationService? _notificationService;
        private WidgetService? _widgetService;

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

        private bool _notificationVisible;
        public bool NotificationVisible
        {
            get => _notificationVisible;
            set => SetProperty(ref _notificationVisible, value);
        }

        private string _notificationTitle = "";
        public string NotificationTitle
        {
            get => _notificationTitle;
            set => SetProperty(ref _notificationTitle, value);
        }

        private string _notificationContent = "";
        public string NotificationContent
        {
            get => _notificationContent;
            set => SetProperty(ref _notificationContent, value);
        }

        private string _notificationBackgroundColor = "#409EFF";
        public string NotificationBackgroundColor
        {
            get => _notificationBackgroundColor;
            set => SetProperty(ref _notificationBackgroundColor, value);
        }

        public string NotificationDisplayText => string.IsNullOrEmpty(NotificationTitle) 
            ? NotificationContent 
            : $"{NotificationTitle}：{NotificationContent}";

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        public ICommand UnbindCommand { get; }
        public ICommand CloseNotificationCommand { get; }

        public event Action? RequestLogout;

        public MainViewModel(ConfigService configService, AuthService authService, UpdateService updateService, PowerControlService powerControlService, WolService wolService, WidgetService widgetService, ScheduleService scheduleService)
        {
            _configService = configService;
            _authService = authService;
            _updateService = updateService;
            _powerControlService = powerControlService;
            _wolService = wolService;
            _widgetService = widgetService;
            _scheduleService = scheduleService;

            _powerControlService.PowerControlExecuted += OnPowerControlExecuted;

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Label = "考勤配置", IconSymbol = Symbol.Calendar, Tag = "Schedule" },
                new NavigationItem { Label = "小组件设置", IconSymbol = Symbol.Setting, Tag = "WidgetSettings" },
                new NavigationItem { Label = "更新", IconSymbol = Symbol.Sync, Tag = "Update" },
                new NavigationItem { Label = "关于", IconSymbol = Symbol.Help, Tag = "About" }
            };

            UnbindCommand = new RelayCommand(OnUnbind);
            CloseNotificationCommand = new RelayCommand(CloseNotification);

            LoadBindInfo();
            _selectedNavigationItem = 0;
            NavigateToPage(0);

            InitializeSocketIO();
            StartUpdateCheck();
        }

        private async void InitializeSocketIO()
        {
            var config = _configService.Config;
            if (config.IsBound && !string.IsNullOrEmpty(config.AccessToken))
            {
                _socketIOService = new SocketIOService(config.ServerUrl);
                _socketIOService.MessageReceived += OnSocketMessageReceived;
                _socketIOService.NotificationReceived += OnNotificationReceived;
                
                _notificationService = new NotificationService(_socketIOService);

                var deviceId = config.DeviceId;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    await _socketIOService.ConnectAsync(config.AccessToken, deviceId, "classroom_terminal");
                }
            }
        }

        private void OnSocketMessageReceived(object? sender, JsonElement message)
        {
            try
            {
                var type = message.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : "";

                if (type == "power_control")
                {
                    var action = message.TryGetProperty("action", out var actionElement)
                        ? actionElement.GetString()
                        : "";

                    HandlePowerControlMessage(action);
                }
                else if (type == "wol_request")
                {
                    HandleWolRequestMessage(message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 解析消息失败: {ex.Message}");
            }
        }

        private async void HandleWolRequestMessage(JsonElement message)
        {
            try
            {
                var targetMac = message.TryGetProperty("target_mac", out var macElement)
                    ? macElement.GetString()
                    : "";

                var broadcastIp = message.TryGetProperty("broadcast_ip", out var ipElement)
                    ? ipElement.GetString()
                    : null;

                var requestId = message.TryGetProperty("request_id", out var idElement)
                    ? idElement.GetString()
                    : "";

                var targetDeviceId = message.TryGetProperty("target_device_id", out var deviceElement)
                    ? deviceElement.GetString()
                    : "";

                if (string.IsNullOrEmpty(targetMac))
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] WOL请求缺少目标MAC地址");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到WOL代理请求: MAC={targetMac}, IP={broadcastIp}, RequestId={requestId}");

                var result = await Task.Run(() => _wolService.SendWolPacket(targetMac, broadcastIp));

                if (_socketIOService != null && !string.IsNullOrEmpty(requestId))
                {
                    var response = new
                    {
                        type = "wol_response",
                        request_id = requestId,
                        target_device_id = targetDeviceId,
                        success = result.Success,
                        message = result.Message
                    };
                    await _socketIOService.SendAsync("wol_response", response);
                }

                System.Diagnostics.Debug.WriteLine($"[MainViewModel] WOL代理执行完成: {result.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 处理WOL请求失败: {ex.Message}");
            }
        }

        private void HandlePowerControlMessage(string? action)
        {
            var powerAction = PowerControlService.ParseActionFromString(action);
            if (powerAction == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 无效的电源操作: {action}");
                return;
            }

            Dispatcher.UIThread.Post(async () =>
            {
                var actionText = powerAction == PowerAction.Shutdown ? "关机" : "重启";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到远程{actionText}指令");

                await Task.Run(() => _powerControlService.ExecutePowerControl(powerAction.Value));
            });
        }

        private void OnPowerControlExecuted(object? sender, PowerControlResult result)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var actionText = result.Action == PowerAction.Shutdown ? "关机" : "重启";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 电源控制执行完成: {actionText}, 成功={result.Success}");
            });
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
                    var schedulePage = new SettingsPage(_configService, _authService, _scheduleService);
                    schedulePage.RequestOpenScheduleConfig += OnRequestOpenScheduleConfig;
                    CurrentPage = schedulePage;
                    break;
                case 1:
                    CurrentPage = new WidgetSettingsPage(new WidgetSettingsViewModel(_configService, _widgetService!));
                    break;
                case 2:
                    CurrentPage = new UpdatePage(_updateService);
                    break;
                case 3:
                    var aboutPage = new AboutPage(_configService, _authService);
                    aboutPage.RequestLogout += () => RequestLogout?.Invoke();
                    CurrentPage = aboutPage;
                    break;
                default:
                    var defaultSchedulePage = new SettingsPage(_configService, _authService, _scheduleService);
                    defaultSchedulePage.RequestOpenScheduleConfig += OnRequestOpenScheduleConfig;
                    CurrentPage = defaultSchedulePage;
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
                Dispatcher.UIThread.Post(() =>
                {
                    SelectedNavigationItem = 2;
                });
            }
        }

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }

        private void OnNotificationReceived(object? sender, Models.Notification notification)
        {
            if (notification.IsExpired)
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 收到过期通知，忽略。");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到通知: {notification.Title} - {notification.Content}");
            
            _notificationService?.EnqueueNotification(notification);
        }

        private void CloseNotification()
        {
            NotificationVisible = false;
            _notificationService?.CloseCurrent();
        }
    }

    public class NavigationItem
    {
        public string Label { get; set; } = "";
        public Symbol IconSymbol { get; set; }
        public string Tag { get; set; } = "";
    }
}
