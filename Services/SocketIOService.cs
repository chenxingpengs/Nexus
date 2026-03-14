using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services
{
    public class ConnectionInfo
    {
        public ConnectionStatus Status { get; set; }
        public string StatusText { get; set; } = "";
        public int LatencyMs { get; set; }
        public DateTime? ConnectedAt { get; set; }
        public int ReconnectCount { get; set; }
        public string? LastError { get; set; }
        public string ServerUrl { get; set; } = "";
        public TimeSpan? ConnectedDuration => ConnectedAt.HasValue ? DateTime.Now - ConnectedAt.Value : null;
    }

    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }

    public class SocketIOService : IDisposable
    {
        private readonly string _baseUrl;
        private SocketIOClient.SocketIO? _socket;
        private CancellationTokenSource? _cancellationTokenSource;
        private System.Timers.Timer? _heartbeatTimer;
        private readonly object _lock = new object();
        private bool _isDisposed;
        private int _reconnectAttempts;
        private DateTime? _lastPingTime;
        private int _currentLatency;
        private DateTime? _connectedAt;
        private string? _currentServerUrl;

        public event EventHandler<JsonElement>? MessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler<string>? Reconnecting;
        public event EventHandler? Reconnected;
        public event EventHandler<ConnectionInfo>? ConnectionInfoChanged;
        public event EventHandler<int>? LatencyUpdated;

        public bool IsConnected => _socket?.Connected ?? false;
        public int CurrentLatency => _currentLatency;
        public ConnectionInfo CurrentConnectionInfo => GetConnectionInfo();

        public SocketIOService(string baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            UpdateConnectionInfo(ConnectionStatus.Disconnected, "未连接");
        }

        private ConnectionInfo GetConnectionInfo()
        {
            return new ConnectionInfo
            {
                Status = GetStatusFromState(),
                StatusText = GetStatusText(),
                LatencyMs = _currentLatency,
                ConnectedAt = _connectedAt,
                ReconnectCount = _reconnectAttempts,
                LastError = null,
                ServerUrl = _currentServerUrl ?? ""
            };
        }

        private ConnectionStatus GetStatusFromState()
        {
            if (_socket?.Connected == true) return ConnectionStatus.Connected;
            if (_reconnectAttempts > 0) return ConnectionStatus.Reconnecting;
            return ConnectionStatus.Disconnected;
        }

        private string GetStatusText()
        {
            return GetStatusFromState() switch
            {
                ConnectionStatus.Connected => $"已连接 ({_currentLatency}ms)",
                ConnectionStatus.Connecting => "连接中...",
                ConnectionStatus.Reconnecting => $"重连中 (第{_reconnectAttempts}次)",
                ConnectionStatus.Error => "连接错误",
                _ => "未连接"
            };
        }

        private void UpdateConnectionInfo(ConnectionStatus status, string statusText, string? error = null)
        {
            var info = new ConnectionInfo
            {
                Status = status,
                StatusText = statusText,
                LatencyMs = _currentLatency,
                ConnectedAt = _connectedAt,
                ReconnectCount = _reconnectAttempts,
                LastError = error,
                ServerUrl = _currentServerUrl ?? ""
            };
            ConnectionInfoChanged?.Invoke(this, info);
        }

        public async Task<(bool Success, string? ErrorMessage)> ConnectAsync(
            string token,
            string deviceId,
            string deviceType = "classroom_terminal",
            int maxRetries = 10)
        {
            return await ErrorHandlingService.ExecuteAsync(async () =>
            {
                if (_socket?.Connected == true)
                {
                    await DisconnectAsync();
                }

                _cancellationTokenSource = new CancellationTokenSource();
                _reconnectAttempts = 0;

                string wsScheme = _baseUrl.StartsWith("https") ? "wss" : "ws";
                string wsBaseUrl = _baseUrl.Replace("http://", $"{wsScheme}://").Replace("https://", $"{wsScheme}://");

                _currentServerUrl = $"{wsBaseUrl}/desktop/bind";

                Debug.WriteLine($"[SocketIO] 基础 URL: {wsBaseUrl}");
                Debug.WriteLine($"[SocketIO] 命名空间: /desktop/bind");
                Debug.WriteLine($"[SocketIO] Token: {token.Substring(0, Math.Min(20, token.Length))}...");
                Debug.WriteLine($"[SocketIO] DeviceId: {deviceId}");
                Debug.WriteLine($"[SocketIO] DeviceType: {deviceType}");

                UpdateConnectionInfo(ConnectionStatus.Connecting, "正在建立WebSocket连接...");

                var options = new SocketIOClient.SocketIOOptions
                {
                    Path = "/socket.io",
                    Query = new Dictionary<string, string>
                    {
                        { "token", token },
                        { "device_id", deviceId },
                        { "device_type", deviceType }
                    },
                    Reconnection = true,
                    ReconnectionAttempts = maxRetries,
                    ReconnectionDelay = 1000,
                    ReconnectionDelayMax = 30000,
                    ConnectionTimeout = TimeSpan.FromSeconds(15),
                };

                Debug.WriteLine($"[SocketIO] 连接 URL: {wsBaseUrl}/desktop/bind");
                Debug.WriteLine($"[SocketIO] Path: /socket.io");

                _socket = new SocketIOClient.SocketIO($"{wsBaseUrl}/desktop/bind", options);

                _socket.OnConnected += (sender, e) =>
                {
                    Debug.WriteLine("[SocketIO] 已连接到 /desktop/bind 命名空间");
                    _reconnectAttempts = 0;
                    _connectedAt = DateTime.Now;
                    StartHeartbeat();
                    UpdateConnectionInfo(ConnectionStatus.Connected, "WebSocket连接成功");
                    Connected?.Invoke(this, EventArgs.Empty);
                };

                _socket.OnDisconnected += (sender, e) =>
                {
                    Debug.WriteLine("[SocketIO] 已断开");
                    StopHeartbeat();
                    _connectedAt = null;
                    _currentLatency = 0;
                    UpdateConnectionInfo(ConnectionStatus.Disconnected, "WebSocket连接已断开");
                    Disconnected?.Invoke(this, EventArgs.Empty);

                    _reconnectAttempts++;
                    var msg = $"正在重连... (尝试 {_reconnectAttempts})";
                    UpdateConnectionInfo(ConnectionStatus.Reconnecting, msg);
                    Reconnecting?.Invoke(this, msg);
                };

                _socket.On("connect_response", response =>
                {
                    Debug.WriteLine($"[SocketIO] 收到 connect_response: {response}");
                    try
                    {
                        var json = response.GetValue().ToString();
                        Debug.WriteLine($"[SocketIO] connect_response JSON: {json}");
                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        var doc = JsonDocument.Parse(bytes);
                        var element = doc.RootElement.Clone();
                        doc.Dispose();
                        MessageReceived?.Invoke(this, element);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SocketIO] 解析 connect_response 失败: {ex.Message}");
                        ErrorOccurred?.Invoke(this, $"解析消息失败: {ex.Message}");
                    }
                });

                _socket.On("bind_notification", response =>
                {
                    Debug.WriteLine($"[SocketIO] 收到 bind_notification: {response}");
                    try
                    {
                        var json = response.GetValue().ToString();
                        Debug.WriteLine($"[SocketIO] bind_notification JSON: {json}");
                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        var doc = JsonDocument.Parse(bytes);
                        var element = doc.RootElement.Clone();
                        doc.Dispose();
                        MessageReceived?.Invoke(this, element);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SocketIO] 解析 bind_notification 失败: {ex.Message}");
                        ErrorOccurred?.Invoke(this, $"解析消息失败: {ex.Message}");
                    }
                });

                _socket.On("pong", response =>
                {
                    Debug.WriteLine($"[SocketIO] 收到 pong: {response}");
                    if (_lastPingTime.HasValue)
                    {
                        _currentLatency = (int)(DateTime.Now - _lastPingTime.Value).TotalMilliseconds;
                        LatencyUpdated?.Invoke(this, _currentLatency);
                        UpdateConnectionInfo(ConnectionStatus.Connected, $"已连接 ({_currentLatency}ms)");
                        Debug.WriteLine($"[SocketIO] 延迟: {_currentLatency}ms");
                    }
                });

                _socket.On("power_control", response =>
                {
                    Debug.WriteLine($"[SocketIO] 收到 power_control: {response}");
                    try
                    {
                        var json = response.GetValue().ToString();
                        Debug.WriteLine($"[SocketIO] power_control JSON: {json}");
                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        var doc = JsonDocument.Parse(bytes);
                        var element = doc.RootElement.Clone();
                        doc.Dispose();
                        MessageReceived?.Invoke(this, element);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SocketIO] 解析 power_control 失败: {ex.Message}");
                        ErrorOccurred?.Invoke(this, $"解析消息失败: {ex.Message}");
                    }
                });

                _socket.On("wol_request", response =>
                {
                    Debug.WriteLine($"[SocketIO] 收到 wol_request: {response}");
                    try
                    {
                        var json = response.GetValue().ToString();
                        Debug.WriteLine($"[SocketIO] wol_request JSON: {json}");
                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        var doc = JsonDocument.Parse(bytes);
                        var element = doc.RootElement.Clone();
                        doc.Dispose();
                        MessageReceived?.Invoke(this, element);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SocketIO] 解析 wol_request 失败: {ex.Message}");
                        ErrorOccurred?.Invoke(this, $"解析消息失败: {ex.Message}");
                    }
                });

                _socket.OnError += (sender, e) =>
                {
                    Debug.WriteLine($"[SocketIO] 错误: {e}");
                    UpdateConnectionInfo(ConnectionStatus.Error, $"连接错误: {e}", e);
                    ErrorOccurred?.Invoke(this, e);
                };

                _socket.OnReconnected += (sender, e) =>
                {
                    Debug.WriteLine("[SocketIO] 重连成功");
                    _reconnectAttempts = 0;
                    _connectedAt = DateTime.Now;
                    StartHeartbeat();
                    UpdateConnectionInfo(ConnectionStatus.Connected, "WebSocket重连成功");
                    Reconnected?.Invoke(this, EventArgs.Empty);
                };

                await _socket.ConnectAsync();

            }, "SocketIO连接", 3);
        }

        public async Task DisconnectAsync()
        {
            try
            {
                lock (_lock)
                {
                    StopHeartbeat();
                }

                if (_socket?.Connected == true)
                {
                    await _socket.DisconnectAsync();
                }
                _socket?.Dispose();
                _socket = null;
                _connectedAt = null;
                _currentLatency = 0;
                _reconnectAttempts = 0;
                UpdateConnectionInfo(ConnectionStatus.Disconnected, "已断开连接");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SocketIO] 断开连接错误: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendAsync(string eventName, object data, int maxRetries = 3)
        {
            if (!IsConnected || _socket == null)
            {
                return (false, "SocketIO 未连接");
            }

            int attempts = 0;
            while (attempts < maxRetries)
            {
                try
                {
                    if (_socket == null)
                    {
                        return (false, "SocketIO 连接已断开");
                    }
                    await _socket.EmitAsync(eventName, data);
                    return (true, null);
                }
                catch (Exception ex)
                {
                    attempts++;
                    Debug.WriteLine($"[SocketIO] 发送消息失败 (尝试 {attempts}/{maxRetries}): {ex.Message}");

                    if (attempts < maxRetries)
                    {
                        await Task.Delay(1000 * attempts);
                    }
                }
            }

            return (false, "发送消息失败：达到最大重试次数");
        }

        public async Task SendPingAsync()
        {
            _lastPingTime = DateTime.Now;
            var result = await SendAsync("ping", new { time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            if (!result.Success)
            {
                Debug.WriteLine($"[SocketIO] 发送心跳失败: {result.ErrorMessage}");
            }
        }

        private void StartHeartbeat()
        {
            lock (_lock)
            {
                StopHeartbeat();

                _heartbeatTimer = new System.Timers.Timer(30000);
                _heartbeatTimer.AutoReset = true;
                _heartbeatTimer.Elapsed += async (sender, e) =>
                {
                    if (IsConnected)
                    {
                        await SendPingAsync();
                    }
                };
                _heartbeatTimer.Start();
                Debug.WriteLine("[SocketIO] 心跳定时器已启动 (30秒间隔)");
            }
        }

        private void StopHeartbeat()
        {
            lock (_lock)
            {
                if (_heartbeatTimer != null)
                {
                    _heartbeatTimer.Stop();
                    _heartbeatTimer.Dispose();
                    _heartbeatTimer = null;
                    Debug.WriteLine("[SocketIO] 心跳定时器已停止");
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _ = DisconnectAsync();
            StopHeartbeat();
            _cancellationTokenSource?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
