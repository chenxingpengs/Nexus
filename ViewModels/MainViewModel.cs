using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Nexus.Models;
using Nexus.Models.Chat;
using Nexus.Models.Meeting;
using Nexus.Services;
using Nexus.Services.Meeting;
using Nexus.Services.Widget;
using Nexus.ViewModels.Pages;
using Nexus.Views;
using Nexus.Views.Pages;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nexus.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;
        private readonly UpdateService _updateService;
        private readonly ScheduleService _scheduleService;
        private readonly ToastService _toastService;
        private readonly PasswordService _passwordService;
        private readonly PowerControlService _powerControlService;
        private readonly WolService _wolService;
        private readonly WidgetService? _widgetService;
        private readonly SoundService _soundService;
        private SocketIOService? _socketIOService;
        private NotificationService? _notificationService;
        private MeetingPageViewModel? _meetingPageViewModel;
        private MeetingPage? _meetingPage;
        private Window? _meetingRoomWindow;
        private Window? _settingsWindow;
        private ChatPageViewModel? _chatPageViewModel;
        private ChatPage? _chatPage;
        private ChatSocketIOService? _chatSocketService;
        private ChatHttpService? _chatHttpService;
        private ChatCacheService? _chatCacheService;
        private TrayService? _trayService;

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

        private int _totalUnreadCount;
        public int TotalUnreadCount
        {
            get => _totalUnreadCount;
            set => SetProperty(ref _totalUnreadCount, value);
        }

        public string NotificationDisplayText => string.IsNullOrEmpty(NotificationTitle) 
            ? NotificationContent 
            : $"{NotificationTitle}：{NotificationContent}";

        public ObservableCollection<MainNavigationItem> NavigationItems { get; }

        public ICommand UnbindCommand { get; }
        public ICommand CloseNotificationCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public event Action? RequestLogout;

        public MainViewModel(ConfigService configService, AuthService authService, UpdateService updateService, PowerControlService powerControlService, WolService wolService, WidgetService widgetService, ScheduleService scheduleService, ToastService toastService, PasswordService passwordService, TrayService? trayService = null)
        {
            _configService = configService;
            _authService = authService;
            _updateService = updateService;
            _powerControlService = powerControlService;
            _wolService = wolService;
            _widgetService = widgetService;
            _scheduleService = scheduleService;
            _toastService = toastService;
            _passwordService = passwordService;
            _trayService = trayService;
            _soundService = new SoundService();

            _powerControlService.PowerControlExecuted += OnPowerControlExecuted;

            NavigationItems = new ObservableCollection<MainNavigationItem>
            {
                new MainNavigationItem { Label = "聊天", IconSymbol = Symbol.Contact, Tag = "Chat" },
                new MainNavigationItem { Label = "会议", IconSymbol = Symbol.Video, Tag = "Meeting" }
            };

            UnbindCommand = new RelayCommand(OnUnbind);
            CloseNotificationCommand = new RelayCommand(CloseNotification);
            OpenSettingsCommand = new RelayCommand(OpenSettings);

            LoadBindInfo();
            _selectedNavigationItem = 0;
            NavigateToPage(0);

            InitializeSocketIO();
        }

        public void SetTrayService(TrayService trayService)
        {
            _trayService = trayService;
        }

        public void OnNewMessageReceived(bool isFromOther)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] OnNewMessageReceived 被调用: isFromOther={isFromOther}");
            
            if (!isFromOther) return;

            Debug.WriteLine("[MainViewModel] 收到新消息，播放提示音并开始图标闪烁");
            
            PlayMessageSound();
            StartTrayFlashing();
        }

        public void OnMessagesRead()
        {
            Debug.WriteLine("[MainViewModel] 消息已读，停止图标闪烁");
            StopTrayFlashing();
        }

        private void PlayMessageSound()
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] PlayMessageSound 被调用");
            
            try
            {
                var soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", "message.mp3");
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 音频文件路径: {soundPath}");
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 音频文件存在: {System.IO.File.Exists(soundPath)}");
                
                if (System.IO.File.Exists(soundPath))
                {
                    _soundService.PlaySound("message.mp3", loop: false, volume: 0.8f);
                    Debug.WriteLine("[MainViewModel] 播放消息提示音");
                }
                else
                {
                    Debug.WriteLine("[MainViewModel] 消息提示音文件不存在，使用系统提示音");
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] 播放消息提示音失败: {ex.Message}");
                try
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
                catch
                {
                }
            }
        }

        private void StartTrayFlashing()
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] StartTrayFlashing 被调用, _trayService={_trayService != null}, IsFlashing={_trayService?.IsFlashing}");
            
            if (_trayService != null && !_trayService.IsFlashing)
            {
                _trayService.StartFlashing();
            }
        }

        private void StopTrayFlashing()
        {
            _trayService?.StopFlashing();
        }

        private async void InitializeSocketIO()
        {
            var config = _configService.Config;
            if (config.IsBound && !string.IsNullOrEmpty(config.AccessToken))
            {
                _socketIOService = new SocketIOService(config.ServerUrl);
                _socketIOService.MessageReceived += OnSocketMessageReceived;
                _socketIOService.NotificationReceived += OnNotificationReceived;
                _socketIOService.PageCallReceived += OnPageCallReceived;
                
                _notificationService = new NotificationService(_socketIOService);

                var deviceId = config.DeviceId;
                if (!string.IsNullOrEmpty(deviceId))
                {
                    await _socketIOService.ConnectAsync(config.AccessToken, deviceId, "classroom_terminal");
                }
                
                SetupMeetingEventHandlers();
                
                InitializeChatServices();
            }
        }

        private async void InitializeChatServices()
        {
            var config = _configService.Config;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] InitializeChatServices: IsBound={config.IsBound}, HasToken={!string.IsNullOrEmpty(config.AccessToken)}, HasDeviceId={!string.IsNullOrEmpty(config.DeviceId)}");
            
            if (config.IsBound && !string.IsNullOrEmpty(config.AccessToken) && !string.IsNullOrEmpty(config.DeviceId))
            {
                _chatCacheService = new ChatCacheService();
                System.Diagnostics.Debug.WriteLine("[MainViewModel] ChatCacheService 已创建");
                
                _chatHttpService = new ChatHttpService(_configService, _toastService);
                System.Diagnostics.Debug.WriteLine("[MainViewModel] ChatHttpService 已创建");
                
                _chatSocketService = new ChatSocketIOService(config.ServerUrl);
                
                _chatSocketService.MessageReceived += OnChatMessageReceived;
                _chatSocketService.Connected += (s, e) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("[MainViewModel] 聊天 WebSocket 已连接");
                    });
                };
                
                var result = await _chatSocketService.ConnectAsync(config.AccessToken, config.DeviceId);
                
                if (result.Success)
                {
                    System.Diagnostics.Debug.WriteLine("[MainViewModel] 聊天服务连接成功");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] 聊天服务连接失败: {result.ErrorMessage}");
                }
                
                if (_chatPageViewModel != null && _chatSocketService != null && _chatHttpService != null)
                {
                    _chatPageViewModel.SetChatServices(_chatSocketService, _chatHttpService, _chatCacheService);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainViewModel] 跳过聊天服务初始化：设备未绑定或缺少认证信息");
            }
        }

        private void OnChatMessageReceived(object? sender, ChatMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] OnChatMessageReceived 被调用: ConversationId={message.ConversationId}");
            
            Dispatcher.UIThread.Post(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到聊天消息: ConversationId={message.ConversationId}, Content={message.Content?.Substring(0, Math.Min(50, message.Content?.Length ?? 0))}");
                
                if (_chatCacheService != null)
                {
                    _chatCacheService.AddMessage(message.ConversationId, message);
                    _chatCacheService.UpdateConversationLastMessage(
                        message.ConversationId,
                        message.Content,
                        message.SentAt
                    );
                }
                
                var currentDeviceId = _configService.Config.DeviceId;
                var isFromOther = string.IsNullOrEmpty(message.SenderDeviceId) || 
                                  message.SenderDeviceId != currentDeviceId;
                
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] isFromOther={isFromOther}, SenderDeviceId={message.SenderDeviceId}, CurrentDeviceId={currentDeviceId}");
                
                if (isFromOther)
                {
                    OnNewMessageReceived(true);
                }
                
                if (_chatPageViewModel != null)
                {
                    return;
                }
            });
        }

        private void SetupMeetingEventHandlers()
        {
            if (_socketIOService == null) return;
            
            _socketIOService.On("meeting:invited", response =>
            {
                try
                {
                    var json = response.GetValue().ToString();
                    var invitation = JsonSerializer.Deserialize<MeetingInvitation>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (invitation != null)
                    {
                        Dispatcher.UIThread.Post(() => ShowMeetingInvitation(invitation));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] 处理会议邀请失败: {ex.Message}");
                }
            });
        }

        private void ShowMeetingInvitation(MeetingInvitation invitation)
        {
            var viewModel = new MeetingInvitationViewModel(invitation);
            var window = new MeetingInvitationWindow
            {
                DataContext = viewModel
            };

            viewModel.Accepted += async () =>
            {
                window.Close();
                if (_meetingPageViewModel != null)
                {
                    await _meetingPageViewModel.AcceptInvitationAsync(invitation);
                }
            };

            viewModel.Rejected += async () =>
            {
                window.Close();
                if (_meetingPageViewModel != null)
                {
                    await _meetingPageViewModel.RejectInvitationAsync(invitation);
                }
            };

            window.Show();
        }

        private void OnPageCallReceived(object? sender, JsonElement data)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 收到寻人推送: {data}");
                _notificationService?.HandlePageCallPush(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 处理寻人推送失败: {ex.Message}");
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
                    if (_chatPage == null || _chatPageViewModel == null)
                    {
                        _chatPageViewModel = new ChatPageViewModel(_configService, _toastService);
                        _chatPage = new ChatPage(_chatPageViewModel);
                        
                        _chatPageViewModel.NewMessageReceived += (sender, message) =>
                        {
                            var currentDeviceId = _configService.Config.DeviceId;
                            var isFromOther = string.IsNullOrEmpty(message.SenderDeviceId) || 
                                              message.SenderDeviceId != currentDeviceId;
                            OnNewMessageReceived(isFromOther);
                        };
                        
                        _chatPageViewModel.MessagesRead += (sender, e) =>
                        {
                            OnMessagesRead();
                        };
                        
                        Debug.WriteLine($"[MainViewModel] ChatPage 创建: _chatSocketService={_chatSocketService != null}, _chatHttpService={_chatHttpService != null}, _chatCacheService={_chatCacheService != null}");
                        
                        if (_chatSocketService != null && _chatHttpService != null)
                        {
                            _chatPageViewModel.SetChatServices(_chatSocketService, _chatHttpService, _chatCacheService);
                        }
                        else
                        {
                            Debug.WriteLine("[MainViewModel] 聊天服务未初始化，尝试重新初始化");
                            InitializeChatServices();
                        }
                    }
                    CurrentPage = _chatPage;
                    break;

                case 1:
                    if (_meetingPage == null || _meetingPageViewModel == null)
                    {
                        _meetingPageViewModel = new MeetingPageViewModel(_configService, _toastService);
                        _meetingPage = new MeetingPage(_meetingPageViewModel);
                        
                        if (_socketIOService != null)
                        {
                            _meetingPageViewModel.SetSocketIOService(_socketIOService);
                        }
                        
                        _meetingPageViewModel.RequestShowMeetingRoom += OnRequestShowMeetingRoom;
                    }
                    CurrentPage = _meetingPage;
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] 未知导航索引: {index}，默认跳转到聊天");
                    if (_chatPage == null || _chatPageViewModel == null)
                    {
                        _chatPageViewModel = new ChatPageViewModel(_configService, _toastService);
                        _chatPage = new ChatPage(_chatPageViewModel);
                        
                        _chatPageViewModel.NewMessageReceived += (sender, message) =>
                        {
                            var currentDeviceId = _configService.Config.DeviceId;
                            var isFromOther = string.IsNullOrEmpty(message.SenderDeviceId) || 
                                              message.SenderDeviceId != currentDeviceId;
                            OnNewMessageReceived(isFromOther);
                        };
                        
                        _chatPageViewModel.MessagesRead += (sender, e) =>
                        {
                            OnMessagesRead();
                        };
                        
                        if (_chatSocketService != null && _chatHttpService != null)
                        {
                            _chatPageViewModel.SetChatServices(_chatSocketService, _chatHttpService, _chatCacheService);
                        }
                        else
                        {
                            Debug.WriteLine("[MainViewModel] 聊天服务未初始化，尝试重新初始化");
                            InitializeChatServices();
                        }
                    }
                    CurrentPage = _chatPage;
                    break;
            }
        }

        private void OnRequestShowMeetingRoom(MeetingRoomViewModel viewModel)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_meetingRoomWindow != null)
                {
                    _meetingRoomWindow.Close();
                }

                _meetingRoomWindow = new MeetingRoomWindow
                {
                    DataContext = viewModel
                };

                viewModel.LeaveRequested += () =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _meetingRoomWindow?.Close();
                        _meetingRoomWindow = null;
                    });
                };

                _meetingRoomWindow.Closed += (s, e) =>
                {
                    _meetingRoomWindow = null;
                };

                _meetingRoomWindow.Show();
            });
        }

        private void OpenSettings()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_settingsWindow != null)
                {
                    _settingsWindow.Activate();
                    return;
                }

                var settingsViewModel = new SettingsViewModel(
                    _configService, 
                    _authService, 
                    _updateService, 
                    _widgetService!, 
                    _scheduleService, 
                    _toastService, 
                    _passwordService
                );
                
                _settingsWindow = new SettingsView
                {
                    DataContext = settingsViewModel
                };

                settingsViewModel.RequestLogout += () =>
                {
                    _settingsWindow.Close();
                    RequestLogout?.Invoke();
                };

                _settingsWindow.Closed += (s, e) =>
                {
                    _settingsWindow = null;
                };

                _settingsWindow.Show();
            });
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

        public void Dispose()
        {
            if (_socketIOService != null)
            {
                _socketIOService.MessageReceived -= OnSocketMessageReceived;
                _socketIOService.NotificationReceived -= OnNotificationReceived;
                _socketIOService.Dispose();
                _socketIOService = null;
            }

            _powerControlService.PowerControlExecuted -= OnPowerControlExecuted;

            _notificationService?.Dispose();
            _notificationService = null;

            _widgetService?.Stop();
            
            _meetingPageViewModel?.Dispose();
            _meetingPageViewModel = null;
            
            _chatPageViewModel?.Dispose();
            _chatPageViewModel = null;
            
            _chatSocketService?.Dispose();
            _chatSocketService = null;
            
            _chatHttpService?.Dispose();
            _chatHttpService = null;
            
            _chatCacheService?.Dispose();
            _chatCacheService = null;
            
            _soundService?.Dispose();
        }
    }

    public class MainNavigationItem
    {
        public string Label { get; set; } = "";
        public Symbol IconSymbol { get; set; }
        public string Tag { get; set; } = "";
    }
}
