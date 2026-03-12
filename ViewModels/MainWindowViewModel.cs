using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Drawing.Imaging;
using Nexus.Services;
using System.Text.Json;

namespace Nexus.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private const string BaseUrl = "https://api.hqzx.me";

        public event Action? BindSuccessAndReady;


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
                }
            }
        }

        public bool IsShowQrCode => BindState == BindState.ShowQrCode;
        public bool IsShowLoading => BindState == BindState.Loading;
        public bool IsShowVerifySuccess => BindState == BindState.VerifySuccess;
        public bool IsShowBindSuccess => BindState == BindState.BindSuccess;
        public bool IsShowError => BindState == BindState.Error;

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

        #endregion

        #region 私有字段

        private readonly HttpClient _httpClient;
        private readonly QRCodeService _qrCodeService;
        private readonly SocketIOService _socketIOService;
        private readonly ConfigService _configService;
        private bool _isDisposed;

        #endregion

        #region 构造函数

        private static string GetDeviceUniqueId()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var machineName = Environment.MachineName;
                    var userName = Environment.UserName;
                    var osVersion = Environment.OSVersion.ToString();
                    var combined = $"{machineName}_{userName}_{osVersion}";
                    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    return "device_" + Convert.ToHexString(hash).Substring(0, 16).ToLower();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDeviceUniqueId error: {ex.Message}");
            }
            return "device_" + Guid.NewGuid().ToString("N").Substring(0, 16);
        }

        public MainWindowViewModel()
        {
            _configService = new ConfigService();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _qrCodeService = new QRCodeService();
            _socketIOService = new SocketIOService(BaseUrl);

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

            // 3秒后自动重试
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

                BindState = BindState.BindSuccess;
                StatusMessage = message;
                BindClassName = className;

                DateTime? expiresAt = null;
                if (!string.IsNullOrEmpty(tokenExpiresAt) && DateTime.TryParse(tokenExpiresAt, out var parsedDate))
                {
                    expiresAt = parsedDate;
                }
                
                _configService.UpdateBindInfo(classId, BindClassName, accessToken, expiresAt);
                System.Diagnostics.Debug.WriteLine($"[Nexus] 绑定信息已保存，classId: {classId}, className: {BindClassName}");
                
                var savedConfig = _configService.Config;
                System.Diagnostics.Debug.WriteLine($"[Nexus] 验证保存: AccessToken={(!string.IsNullOrEmpty(savedConfig.AccessToken) ? "已保存" : "未保存")}, BindInfo={(savedConfig.BindInfo != null ? savedConfig.BindInfo.ClassName : "null")}");
            }
            else
            {
                BindState = BindState.Error;
                ErrorMessage = "绑定失败：服务器返回数据无效";
                StatusMessage = ErrorMessage;
                System.Diagnostics.Debug.WriteLine($"[Nexus] data 为空");
                return;
            }

            await Task.Delay(1500);
            
            BindSuccessAndReady?.Invoke();
        }

        private async Task HandleBindFailed(string message)
        {
            BindState = BindState.Error;
            ErrorMessage = message;
            StatusMessage = message;

            // 3秒后自动重试
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

            var url = $"{BaseUrl}/desktop/bind/token?device_id={Uri.EscapeDataString(DeviceId)}&device_name={Uri.EscapeDataString(DeviceName)}&device_type={Uri.EscapeDataString(DeviceType)}&app_version={Uri.EscapeDataString(AppVersion ?? "")}";
            
            System.Diagnostics.Debug.WriteLine($"[CampusLink] 请求 URL: {url}");
            System.Diagnostics.Debug.WriteLine($"[CampusLink] DeviceId: {DeviceId}");
            System.Diagnostics.Debug.WriteLine($"[CampusLink] DeviceName: {DeviceName}");
            System.Diagnostics.Debug.WriteLine($"[CampusLink] DeviceType: {DeviceType}");
            System.Diagnostics.Debug.WriteLine($"[CampusLink] AppVersion: {AppVersion}");

            var (success, responseData, errorMessage) = await ErrorHandlingService.ExecuteAsync<string?>(
                async () =>
                {
                    System.Diagnostics.Debug.WriteLine($"[CampusLink] 发送 HTTP 请求...");
                    var response = await _httpClient.GetAsync(url);
                    System.Diagnostics.Debug.WriteLine($"[CampusLink] 收到响应: {response.StatusCode}");
                    
                    var content = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[CampusLink] 响应内容长度: {content?.Length ?? 0}");
                    
                    if (!string.IsNullOrEmpty(content) && content.Length > 200)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CampusLink] 响应内容前200字符: {content.Substring(0, 200)}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CampusLink] 响应内容: {content}");
                    }
                    
                    response.EnsureSuccessStatusCode();
                    return content;
                },
                "获取绑定Token",
                maxRetries: 3,
                retryDelayMs: 2000);

            if (!success)
            {
                System.Diagnostics.Debug.WriteLine($"[CampusLink] 请求失败: {errorMessage}");
                BindState = BindState.Error;
                ErrorMessage = errorMessage ?? "未知错误";
                StatusMessage = errorMessage ?? "未知错误";
                return;
            }

            // 解析响应
            var (parseSuccess, token, parseError) = await ParseTokenResponse(responseData);

            if (!parseSuccess)
            {
                BindState = BindState.Error;
                ErrorMessage = parseError ?? "解析失败";
                StatusMessage = parseError ?? "解析失败";
                return;
            }

            Token = token ?? "";
            StatusMessage = "获取 token 成功，正在生成二维码...";

            // 生成二维码
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

        /// <summary>
        /// 解析 Token 响应
        /// </summary>
        private Task<(bool Success, string? Token, string? ErrorMessage)> ParseTokenResponse(string? responseData)
        {
            // 检查响应是否为空
            if (string.IsNullOrWhiteSpace(responseData))
            {
                return Task.FromResult((false, (string?)null, "服务器返回空响应"));
            }

            // 检查响应是否是 HTML 错误页面（以 < 开头）
            if (responseData.TrimStart().StartsWith("<"))
            {
                // 尝试提取 HTML 中的错误信息
                var errorInfo = ExtractErrorFromHtml(responseData);
                return Task.FromResult((false, (string?)null, $"服务器返回错误页面：{errorInfo}"));
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(responseData);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("code", out var codeElement) || codeElement.GetInt32() != 200)
                {
                    var msg = root.TryGetProperty("msg", out var msgElement)
                        ? msgElement.GetString()
                        : "获取 token 失败";
                    return Task.FromResult((false, (string?)null, msg));
                }

                if (root.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        return Task.FromResult((true, token, (string?)null));
                    }
                }

                return Task.FromResult((false, (string?)null, "响应中缺少 token 数据"));
            }
            catch (JsonException ex)
            {
                // 如果响应内容较短，显示在错误信息中帮助调试
                var preview = responseData.Length > 100 ? responseData.Substring(0, 100) + "..." : responseData;
                return Task.FromResult((false, (string?)null, $"解析响应失败：{ex.Message}\n响应内容：{preview}"));
            }
        }

        /// <summary>
        /// 从 HTML 错误页面中提取错误信息
        /// </summary>
        private string ExtractErrorFromHtml(string html)
        {
            try
            {
                // 尝试提取 title 标签内容
                var titleStart = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
                var titleEnd = html.IndexOf("</title>", StringComparison.OrdinalIgnoreCase);
                if (titleStart >= 0 && titleEnd > titleStart)
                {
                    return html.Substring(titleStart + 7, titleEnd - titleStart - 7);
                }

                // 尝试提取 h1 标签内容
                var h1Start = html.IndexOf("<h1>", StringComparison.OrdinalIgnoreCase);
                var h1End = html.IndexOf("</h1>", StringComparison.OrdinalIgnoreCase);
                if (h1Start >= 0 && h1End > h1Start)
                {
                    return html.Substring(h1Start + 4, h1End - h1Start - 4);
                }

                return "未知错误";
            }
            catch
            {
                return "未知错误";
            }
        }

        /// <summary>
        /// 生成二维码
        /// </summary>
        private async Task<(bool Success, string? Base64, string? ErrorMessage)> GenerateQrCodeAsync(string token)
        {
            try
            {
                // 生成小程序码 URL
                var qrUrl = $"campuslink://bind?token={token}";
                System.Diagnostics.Debug.WriteLine($"[CampusLink] 二维码内容: {qrUrl}");

                // 使用 QRCodeService 生成二维码
                var result = _qrCodeService.GenerateQRCodeBase64(qrUrl);

                if (!result.Success || string.IsNullOrEmpty(result.Base64))
                {
                    return (false, null, result.ErrorMessage ?? "生成二维码失败");
                }

                // 转换为 Avalonia Bitmap
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

            _httpClient.Dispose();
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
        Error
    }
}
