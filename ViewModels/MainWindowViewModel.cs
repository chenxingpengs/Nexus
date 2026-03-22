using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Schedule;
using Nexus.Services;
using Nexus.Services.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nexus.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        public event Action? BindSuccessAndReady;
        public event Action<int, string>? RequestOpenScheduleSetup;


        #region 属性

        private Avalonia.Media.Imaging.Bitmap? _qrCodeImage;
        public Avalonia.Media.Imaging.Bitmap? QrCodeImage
        {
            get => _qrCodeImage;
            set => SetProperty(ref _qrCodeImage, value);
        }

        private string _token = string.Empty;
        public string Token
        {
            get => _token;
            set => SetProperty(ref _token, value);
        }

        private string _deviceId = GetDeviceUniqueId();
        public string DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        private string _deviceName = Environment.MachineName;
        public string DeviceName
        {
            get => _deviceName;
            set => SetProperty(ref _deviceName, value);
        }

        private string _deviceType = "classroom_terminal";
        public string DeviceType
        {
            get => _deviceType;
            set => SetProperty(ref _deviceType, value);
        }

        private string? _appVersion;
        public string? AppVersion
        {
            get => _appVersion;
            set => SetProperty(ref _appVersion, value);
        }

        private string _statusMessage = "正在获取绑定二维码...";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private BindState _bindState = BindState.Loading;
        public BindState BindState
        {
            get => _bindState;
            set
            {
                if (SetProperty(ref _bindState, value))
                {
                    OnPropertyChanged(nameof(IsShowQrCode));
                    OnPropertyChanged(nameof(IsShowLoading));
                    OnPropertyChanged(nameof(IsShowVerifySuccess));
                    OnPropertyChanged(nameof(IsShowBindSuccess));
                    OnPropertyChanged(nameof(IsShowError));
                    OnPropertyChanged(nameof(IsShowScheduleIncomplete));
                }
            }
        }

        public bool IsShowQrCode => BindState == BindState.ShowQrCode;
        public bool IsShowLoading => BindState == BindState.Loading;
        public bool IsShowVerifySuccess => BindState == BindState.VerifySuccess;
        public bool IsShowBindSuccess => BindState == BindState.BindSuccess || BindState == BindState.ScheduleIncomplete;
        public bool IsShowError => BindState == BindState.Error;
        public bool IsShowScheduleIncomplete => BindState == BindState.ScheduleIncomplete;

        private string _verifyUserName = string.Empty;
        public string VerifyUserName
        {
            get => _verifyUserName;
            set => SetProperty(ref _verifyUserName, value);
        }

        private string _bindClassName = string.Empty;
        public string BindClassName
        {
            get => _bindClassName;
            set => SetProperty(ref _bindClassName, value);
        }

        private int _bindClassId;
        public int BindClassId
        {
            get => _bindClassId;
            set => SetProperty(ref _bindClassId, value);
        }

        private List<MissingSlotModel> _missingSlots = new();
        public List<MissingSlotModel> MissingSlots
        {
            get => _missingSlots;
            set => SetProperty(ref _missingSlots, value);
        }

        private int _missingSlotsCount;
        public int MissingSlotsCount
        {
            get => _missingSlotsCount;
            set => SetProperty(ref _missingSlotsCount, value);
        }

        private int _totalSlotsCount;
        public int TotalSlotsCount
        {
            get => _totalSlotsCount;
            set => SetProperty(ref _totalSlotsCount, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _isWebSocketConnected = false;
        public bool IsWebSocketConnected
        {
            get => _isWebSocketConnected;
            set => SetProperty(ref _isWebSocketConnected, value);
        }

        private Services.ConnectionStatus _connectionStatus = Services.ConnectionStatus.Disconnected;
        public Services.ConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (SetProperty(ref _connectionStatus, value))
                {
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(ConnectionStatusColor));
                    OnPropertyChanged(nameof(ConnectionStatusIcon));
                    OnPropertyChanged(nameof(IsConnectionGood));
                    OnPropertyChanged(nameof(IsConnectionPoor));
                }
            }
        }

        public string ConnectionStatusText
        {
            get
            {
                return ConnectionStatus switch
                {
                    Services.ConnectionStatus.Disconnected => "未连接",
                    Services.ConnectionStatus.Connecting => "连接中...",
                    Services.ConnectionStatus.Connected => _connectionLatency > 0 ? $"已连接 ({_connectionLatency}ms)" : "已连接",
                    Services.ConnectionStatus.Reconnecting => $"重连中 (第{_reconnectCount}次)",
                    Services.ConnectionStatus.Error => "连接错误",
                    _ => "未知状态"
                };
            }
        }

        public string ConnectionStatusColor
        {
            get
            {
                return ConnectionStatus switch
                {
                    Services.ConnectionStatus.Disconnected => "#F44336",
                    Services.ConnectionStatus.Connecting => "#FF9800",
                    Services.ConnectionStatus.Connected => _connectionLatency switch
                    {
                        < 100 => "#4CAF50",
                        < 300 => "#8BC34A",
                        < 500 => "#FFC107",
                        _ => "#FF9800"
                    },
                    Services.ConnectionStatus.Reconnecting => "#FF9800",
                    Services.ConnectionStatus.Error => "#F44336",
                    _ => "#999999"
                };
            }
        }

        public string ConnectionStatusIcon
        {
            get
            {
                return ConnectionStatus switch
                {
                    Services.ConnectionStatus.Disconnected => "Disconnected",
                    Services.ConnectionStatus.Connecting => "Connecting",
                    Services.ConnectionStatus.Connected => _connectionLatency switch
                    {
                        < 100 => "Excellent",
                        < 300 => "Good",
                        < 500 => "Fair",
                        _ => "Poor"
                    },
                    Services.ConnectionStatus.Reconnecting => "Reconnecting",
                    Services.ConnectionStatus.Error => "Error",
                    _ => "Unknown"
                };
            }
        }

        private int _connectionLatency = 0;
        public int ConnectionLatency
        {
            get => _connectionLatency;
            set
            {
                if (SetProperty(ref _connectionLatency, value))
                {
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(ConnectionStatusColor));
                    OnPropertyChanged(nameof(ConnectionStatusIcon));
                    OnPropertyChanged(nameof(IsConnectionGood));
                    OnPropertyChanged(nameof(IsConnectionPoor));
                    OnPropertyChanged(nameof(LatencyQualityText));
                }
            }
        }

        public bool IsConnectionGood => ConnectionStatus == Services.ConnectionStatus.Connected && _connectionLatency < 300;
        public bool IsConnectionPoor => ConnectionStatus == Services.ConnectionStatus.Connected && _connectionLatency >= 500;

        public string LatencyQualityText
        {
            get
            {
                if (ConnectionStatus != Services.ConnectionStatus.Connected) return "";
                return _connectionLatency switch
                {
                    < 100 => "优秀",
                    < 300 => "良好",
                    < 500 => "一般",
                    _ => "较差"
                };
            }
        }

        private int _reconnectCount = 0;
        public int ReconnectCount
        {
            get => _reconnectCount;
            set => SetProperty(ref _reconnectCount, value);
        }

        private DateTime? _connectedAt;
        public DateTime? ConnectedAt
        {
            get => _connectedAt;
            set => SetProperty(ref _connectedAt, value);
        }

        public string ConnectedDurationText
        {
            get
            {
                if (!_connectedAt.HasValue || ConnectionStatus != Services.ConnectionStatus.Connected)
                    return "";
                var duration = DateTime.Now - _connectedAt.Value;
                if (duration.TotalMinutes < 1)
                    return $"{(int)duration.TotalSeconds}秒";
                else if (duration.TotalHours < 1)
                    return $"{(int)duration.TotalMinutes}分钟";
                else
                    return $"{(int)duration.TotalHours}小时{(int)duration.Minutes}分钟";
            }
        }

        #endregion

        #region 命令

        public ICommand RefreshTokenCommand { get; }
        public ICommand RetryCommand { get; }
        public ICommand OpenScheduleSetupCommand { get; }
        public ICommand ContinueWithoutScheduleCommand { get; }

        #endregion

        #region 私有字段

        private readonly HttpService _httpService;
        private readonly QRCodeService _qrCodeService;
        private readonly SocketIOService _socketIOService;
        private readonly ConfigService _configService;
        private readonly ScheduleService _scheduleService;
        private readonly ToastService _toastService;
        private bool _isDisposed;

        #endregion

        #region 构造函数

        private static string GetDeviceUniqueId()
        {
            var (deviceId, _, _) = DeviceIdentifier.GetDeviceInfo();
            return deviceId;
        }

        private static (string DeviceId, string? MacAddress, string? IpAddress) GetFullDeviceInfo()
        {
            return DeviceIdentifier.GetDeviceInfo();
        }

        public MainWindowViewModel(ConfigService configService, ToastService toastService, ScheduleService scheduleService)
        {
            _configService = configService;
            _toastService = toastService;
            _scheduleService = scheduleService;
            _httpService = new HttpService(configService, toastService);
            _qrCodeService = new QRCodeService();
            _socketIOService = new SocketIOService(_configService.Config.ServerUrl);

            _socketIOService.MessageReceived += OnSocketIOMessageReceived;
            _socketIOService.ErrorOccurred += OnSocketIOErrorOccurred;
            _socketIOService.Connected += OnSocketIOConnected;
            _socketIOService.Disconnected += OnSocketIODisconnected;
            _socketIOService.Reconnecting += OnSocketIOReconnecting;
            _socketIOService.Reconnected += OnSocketIOReconnected;
            _socketIOService.ConnectionInfoChanged += OnConnectionInfoChanged;
            _socketIOService.LatencyUpdated += OnLatencyUpdated;

            RefreshTokenCommand = new AsyncRelayCommand(RefreshToken);
            RetryCommand = new AsyncRelayCommand(RefreshToken);
            OpenScheduleSetupCommand = new RelayCommand(OnOpenScheduleSetup);
            ContinueWithoutScheduleCommand = new RelayCommand(OnContinueWithoutSchedule);

            if (!string.IsNullOrEmpty(_configService.Config.DeviceId))
            {
                DeviceId = _configService.Config.DeviceId;
            }
            else
            {
                _configService.SetDeviceInfo(DeviceId, DeviceName);
            }

            if (!string.IsNullOrEmpty(_configService.Config.DeviceType))
            {
                DeviceType = _configService.Config.DeviceType;
            }

            AppVersion = _configService.Config.AppVersion ?? GetAppVersion();

            _ = RefreshToken();
        }

        private string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        }

        #endregion

        #region Socket.IO 事件处理

        private void OnConnectionInfoChanged(object? sender, Services.ConnectionInfo info)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ConnectionStatus = info.Status;
                ConnectionLatency = info.LatencyMs;
                ReconnectCount = info.ReconnectCount;
                ConnectedAt = info.ConnectedAt;

                if (!string.IsNullOrEmpty(info.LastError))
                {
                    StatusMessage = info.LastError;
                }
            });
        }

        private void OnLatencyUpdated(object? sender, int latency)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ConnectionLatency = latency;
            });
        }

        private void OnSocketIOConnected(object? sender, EventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsWebSocketConnected = true;
                StatusMessage = "WebSocket 连接成功，等待扫码...";
            });
        }

        private void OnSocketIODisconnected(object? sender, EventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsWebSocketConnected = false;
                StatusMessage = "WebSocket 连接已断开";
            });
        }

        private void OnSocketIOReconnecting(object? sender, string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = message;
                System.Diagnostics.Debug.WriteLine($"[CampusLink] Socket.IO 重连: {message}");
            });
        }

        private void OnSocketIOReconnected(object? sender, EventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsWebSocketConnected = true;
                StatusMessage = "Socket.IO 重连成功，等待扫码...";
                System.Diagnostics.Debug.WriteLine("[CampusLink] Socket.IO 重连成功");
            });
        }

        private void OnSocketIOErrorOccurred(object? sender, string errorMessage)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusMessage = errorMessage;
                System.Diagnostics.Debug.WriteLine($"Socket.IO error: {errorMessage}");
            });
        }

        private async void OnSocketIOMessageReceived(object? sender, JsonElement message)
        {
            try
            {
                var msgType = message.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString() ?? ""
                    : "";
                var msg = message.TryGetProperty("msg", out var msgElement)
                    ? msgElement.GetString() ?? ""
                    : "";

                string? dataJson = null;
                if (message.TryGetProperty("data", out var dataElement))
                {
                    dataJson = dataElement.GetRawText();
                }

                System.Diagnostics.Debug.WriteLine($"[Nexus] 收到消息: type={msgType}, msg={msg}, dataJson={(dataJson != null ? "有数据" : "无数据")}");

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    JsonElement? data = null;
                    if (dataJson != null)
                    {
                        try
                        {
                            var doc = JsonDocument.Parse(dataJson);
                            data = doc.RootElement.Clone();
                            doc.Dispose();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Nexus] 解析 data 失败: {ex.Message}");
                        }
                    }

                    switch (msgType)
                    {
                        case "connect_response":
                            var code = message.TryGetProperty("code", out var codeElement)
                                ? codeElement.GetInt32()
                                : 0;
                            if (code == 200)
                            {
                                StatusMessage = "连接成功，等待扫码...";
                            }
                            else
                            {
                                StatusMessage = $"连接失败：{msg}";
                            }
                            break;

                        case "verify_success":
                            System.Diagnostics.Debug.WriteLine($"[Nexus] 准备调用 HandleVerifySuccess");
                            await HandleVerifySuccess(msg, data);
                            break;

                        case "verify_failed":
                            await HandleVerifyFailed(msg);
                            break;

                        case "bind_success":
                            await HandleBindSuccess(msg, data);
                            break;

                        case "bind_failed":
                            await HandleBindFailed(msg);
                            break;

                        case "error":
                            StatusMessage = $"服务器错误：{msg}";
                            break;

                        default:
                            System.Diagnostics.Debug.WriteLine($"Unknown message type: {msgType}");
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = $"处理消息失败：{ex.Message}";
                });
                System.Diagnostics.Debug.WriteLine($"Process message error: {ex}");
            }
        }

        private async Task HandleVerifySuccess(string message, JsonElement? data)
        {
            System.Diagnostics.Debug.WriteLine($"[Nexus] HandleVerifySuccess 开始执行");
            System.Diagnostics.Debug.WriteLine($"[Nexus] 当前 BindState: {BindState}");

            _bindState = BindState.VerifySuccess;
            OnPropertyChanged(nameof(BindState));
            OnPropertyChanged(nameof(IsShowQrCode));
            OnPropertyChanged(nameof(IsShowLoading));
            OnPropertyChanged(nameof(IsShowVerifySuccess));
            OnPropertyChanged(nameof(IsShowBindSuccess));
            OnPropertyChanged(nameof(IsShowError));

            StatusMessage = message;

            System.Diagnostics.Debug.WriteLine($"[Nexus] 设置后 BindState: {BindState}");
            System.Diagnostics.Debug.WriteLine($"[Nexus] IsShowVerifySuccess: {IsShowVerifySuccess}");

            if (data.HasValue && data.Value.TryGetProperty("user_info", out var userInfo))
            {
                VerifyUserName = userInfo.TryGetProperty("name", out var name)
                    ? name.GetString() ?? ""
                    : "";
                System.Diagnostics.Debug.WriteLine($"[Nexus] VerifyUserName: {VerifyUserName}");
            }

            System.Diagnostics.Debug.WriteLine($"[Nexus] HandleVerifySuccess 完成");
        }

        private async Task HandleVerifyFailed(string message)
        {
            BindState = BindState.Error;
            ErrorMessage = message;
            StatusMessage = message;

            await Task.Delay(3000);
            await RefreshToken();
        }

        private async Task HandleBindSuccess(string message, JsonElement? data)
        {
            System.Diagnostics.Debug.WriteLine($"[Nexus] HandleBindSuccess 被调用");
            System.Diagnostics.Debug.WriteLine($"[Nexus] data: {data}");

            if (data.HasValue)
            {
                var dataJson = data.Value.GetRawText();
                System.Diagnostics.Debug.WriteLine($"[Nexus] data raw: {dataJson}");

                var classId = data.Value.TryGetProperty("class_id", out var classIdElement)
                    ? classIdElement.GetInt32()
                    : 0;

                var className = data.Value.TryGetProperty("class_name", out var classNameElement)
                    ? classNameElement.GetString() ?? ""
                    : "";

                var accessToken = data.Value.TryGetProperty("access_token", out var tokenElement)
                    ? tokenElement.GetString() ?? ""
                    : "";

                var tokenExpiresAt = data.Value.TryGetProperty("token_expires_at", out var expiresElement)
                    ? expiresElement.GetString()
                    : null;

                System.Diagnostics.Debug.WriteLine($"[Nexus] classId: {classId}, className: {className}");
                System.Diagnostics.Debug.WriteLine($"[Nexus] accessToken: {(string.IsNullOrEmpty(accessToken) ? "空" : accessToken.Substring(0, Math.Min(20, accessToken.Length)) + "...")}");
                System.Diagnostics.Debug.WriteLine($"[Nexus] tokenExpiresAt: {tokenExpiresAt}");

                if (string.IsNullOrEmpty(accessToken))
                {
                    BindState = BindState.Error;
                    ErrorMessage = "绑定失败：服务器未返回访问令牌，请联系管理员检查后端配置";
                    StatusMessage = ErrorMessage;
                    System.Diagnostics.Debug.WriteLine($"[Nexus] 绑定失败：accessToken为空");
                    return;
                }

                if (classId <= 0)
                {
                    BindState = BindState.Error;
                    ErrorMessage = "绑定失败：班级信息无效";
                    StatusMessage = ErrorMessage;
                    System.Diagnostics.Debug.WriteLine($"[Nexus] 绑定失败：classId无效");
                    return;
                }

                BindClassName = className;
                BindClassId = classId;

                DateTime? expiresAt = null;
                if (!string.IsNullOrEmpty(tokenExpiresAt) && DateTime.TryParse(tokenExpiresAt, out var parsedDate))
                {
                    expiresAt = parsedDate;
                }

                _configService.UpdateBindInfo(classId, BindClassName, accessToken, expiresAt);
                System.Diagnostics.Debug.WriteLine($"[Nexus] 绑定信息已保存，classId: {classId}, className: {BindClassName}");
                System.Diagnostics.Debug.WriteLine($"[Nexus] ConfigService.Config.AccessToken: {(string.IsNullOrEmpty(_configService.Config.AccessToken) ? "空" : _configService.Config.AccessToken.Substring(0, Math.Min(20, _configService.Config.AccessToken.Length)) + "...")}");

                var savedConfig = _configService.Config;
                System.Diagnostics.Debug.WriteLine($"[Nexus] 验证保存: AccessToken={(!string.IsNullOrEmpty(savedConfig.AccessToken) ? "已保存" : "未保存")}, BindInfo={(savedConfig.BindInfo != null ? savedConfig.BindInfo.ClassName : "null")}");

                StatusMessage = "正在检查排班配置...";
                System.Diagnostics.Debug.WriteLine($"[Nexus] 开始检查排班配置, classId={classId}");
                
                ScheduleCompletenessModel? completeness = null;
                try
                {
                    completeness = await _scheduleService.CheckCompletenessAsync(classId);
                    System.Diagnostics.Debug.WriteLine($"[Nexus] 排班检查完成: completeness={(completeness != null ? $"IsComplete={completeness.IsComplete}, MissingSlots={completeness.MissingSlots?.Count ?? 0}" : "null")}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Nexus] 排班检查异常: {ex.Message}");
                }

                if (completeness == null || !completeness.IsComplete)
                {
                    System.Diagnostics.Debug.WriteLine($"[Nexus] 设置 ScheduleIncomplete 状态");
                    BindState = BindState.ScheduleIncomplete;
                    StatusMessage = "排班配置不完整";
                    MissingSlots = completeness?.MissingSlots ?? new List<MissingSlotModel>();
                    MissingSlotsCount = MissingSlots.Count;
                    TotalSlotsCount = completeness?.FixedTimeSlots?.Count * 5 ?? 0;
                    System.Diagnostics.Debug.WriteLine($"[Nexus] 排班不完整: 缺失 {MissingSlotsCount} 个时段, 共 {TotalSlotsCount} 个时段");
                }
                else
                {
                    BindState = BindState.BindSuccess;
                    StatusMessage = message;
                    await Task.Delay(1500);
                    BindSuccessAndReady?.Invoke();
                }
            }
            else
            {
                BindState = BindState.Error;
                ErrorMessage = "绑定失败：服务器返回数据无效";
                StatusMessage = ErrorMessage;
                System.Diagnostics.Debug.WriteLine($"[Nexus] data 为空");
                return;
            }
        }

        private void OnOpenScheduleSetup()
        {
            RequestOpenScheduleSetup?.Invoke(BindClassId, BindClassName);
        }

        private void OnContinueWithoutSchedule()
        {
            BindSuccessAndReady?.Invoke();
        }

        private async Task HandleBindFailed(string message)
        {
            BindState = BindState.Error;
            ErrorMessage = message;
            StatusMessage = message;

            await Task.Delay(3000);
            await RefreshToken();
        }

        #endregion

        #region 公共方法

        public async Task RefreshToken()
        {
            BindState = BindState.Loading;
            StatusMessage = "正在获取绑定二维码...";
            ConnectionStatus = Services.ConnectionStatus.Disconnected;
            ConnectionLatency = 0;
            ReconnectCount = 0;
            ConnectedAt = null;

            await _socketIOService.DisconnectAsync();

            var (deviceId, macAddress, ipAddress) = DeviceIdentifier.GetDeviceInfo();
            DeviceId = deviceId;

            var endpoint = $"/desktop/bind/token?device_id={Uri.EscapeDataString(DeviceId)}&device_name={Uri.EscapeDataString(DeviceName)}&device_type={Uri.EscapeDataString(DeviceType)}&app_version={Uri.EscapeDataString(AppVersion ?? "")}";

            if (!string.IsNullOrEmpty(macAddress))
            {
                endpoint += $"&mac_address={Uri.EscapeDataString(macAddress)}";
            }
            if (!string.IsNullOrEmpty(ipAddress))
            {
                endpoint += $"&ip_address={Uri.EscapeDataString(ipAddress)}";
            }

            System.Diagnostics.Debug.WriteLine($"[CampusLink] DeviceId: {DeviceId}");
            System.Diagnostics.Debug.WriteLine($"[CampusLink] DeviceName: {DeviceName}");
            System.Diagnostics.Debug.WriteLine($"[CampusLink] DeviceType: {DeviceType}");
            System.Diagnostics.Debug.WriteLine($"[CampusLink] AppVersion: {AppVersion}");

            var response = await _httpService.GetAsync<BindTokenData>(endpoint, new RequestOptions
            {
                OperationName = "获取绑定Token",
                MaxRetries = 3,
                RetryDelayMs = 2000,
                RequireAuth = false,
                ShowErrorToast = false
            });

            if (response == null || !response.IsSuccess || response.Data == null || string.IsNullOrEmpty(response.Data.Token))
            {
                var errorMsg = response?.Msg ?? "获取绑定Token失败";
                System.Diagnostics.Debug.WriteLine($"[CampusLink] 请求失败: {errorMsg}");
                BindState = BindState.Error;
                ErrorMessage = errorMsg;
                StatusMessage = errorMsg;
                _toastService.ShowError(errorMsg);
                return;
            }

            Token = response.Data.Token;
            StatusMessage = "获取 token 成功，正在生成二维码...";

            var (qrSuccess, _, qrError) = await GenerateQrCodeAsync(Token);

            if (!qrSuccess)
            {
                BindState = BindState.Error;
                ErrorMessage = qrError ?? "生成二维码失败";
                StatusMessage = qrError ?? "生成二维码失败";
                return;
            }

            ConnectionStatus = Services.ConnectionStatus.Connecting;
            StatusMessage = "正在连接 WebSocket...";
            var (wsSuccess, wsError) = await _socketIOService.ConnectAsync(Token, DeviceId, DeviceType, maxRetries: 3);

            if (!wsSuccess)
            {
                BindState = BindState.Error;
                ErrorMessage = wsError ?? "连接失败";
                StatusMessage = wsError ?? "连接失败";
                ConnectionStatus = Services.ConnectionStatus.Error;
                return;
            }

            BindState = BindState.ShowQrCode;
            StatusMessage = "请使用微信小程序扫码绑定";
        }

        private async Task<(bool Success, string? Base64, string? ErrorMessage)> GenerateQrCodeAsync(string token)
        {
            try
            {
                var qrUrl = $"campuslink://bind?token={token}";
                System.Diagnostics.Debug.WriteLine($"[CampusLink] 二维码内容: {qrUrl}");

                var result = _qrCodeService.GenerateQRCodeBase64(qrUrl);

                if (!result.Success || string.IsNullOrEmpty(result.Base64))
                {
                    return (false, null, result.ErrorMessage ?? "生成二维码失败");
                }

                var imageBytes = Convert.FromBase64String(result.Base64);
                using var stream = new MemoryStream(imageBytes);
                QrCodeImage = new Avalonia.Media.Imaging.Bitmap(stream);

                return (true, result.Base64, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed) return;

            _httpService.Dispose();
            _qrCodeService.Dispose();
            _socketIOService.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    public enum BindState
    {
        Loading,
        ShowQrCode,
        VerifySuccess,
        BindSuccess,
        ScheduleIncomplete,
        Error
    }

    public class BindTokenData
    {
        [System.Text.Json.Serialization.JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}
