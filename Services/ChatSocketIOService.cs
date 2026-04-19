using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Models.Chat;

namespace Nexus.Services
{
    public class ChatConnectionInfo
    {
        public bool IsConnected { get; set; }
        public string StatusText { get; set; } = "";
        public DateTime? ConnectedAt { get; set; }
        public string? LastError { get; set; }
    }

    public class ChatSocketIOService : IDisposable, IAsyncDisposable
    {
        private readonly string _baseUrl;
        private SocketIOClient.SocketIO? _socket;
        private CancellationTokenSource? _cancellationTokenSource;
        private System.Timers.Timer? _heartbeatTimer;
        private readonly object _lock = new object();
        private bool _isDisposed;
        private DateTime? _connectedAt;

        public event EventHandler<ChatMessage>? MessageReceived;
        public event EventHandler<TypingEventData>? TypingStarted;
        public event EventHandler<TypingEventData>? TypingStopped;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<ChatConnectionInfo>? ConnectionStateChanged;

        public bool IsConnected => _socket?.Connected ?? false;
        public ChatConnectionInfo CurrentConnectionInfo => new ChatConnectionInfo
        {
            IsConnected = IsConnected,
            StatusText = IsConnected ? "已连接" : "未连接",
            ConnectedAt = _connectedAt,
            LastError = null
        };

        public ChatSocketIOService(string baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        public async Task<(bool Success, string? ErrorMessage)> ConnectAsync(
            string token,
            string deviceId,
            int maxRetries = 5)
        {
            int attempts = 0;
            Exception? lastException = null;

            while (attempts <= maxRetries)
            {
                try
                {
                    if (_socket?.Connected == true)
                    {
                        await DisconnectAsync();
                    }

                    _cancellationTokenSource = new CancellationTokenSource();

                    string wsScheme = _baseUrl.StartsWith("https") ? "wss" : "ws";
                    string wsBaseUrl = _baseUrl.Replace("http://", $"{wsScheme}://").Replace("https://", $"{wsScheme}://");

                    Debug.WriteLine($"[ChatSocketIO] 连接到 /chat 命名空间");
                    Debug.WriteLine($"[ChatSocketIO] Token: {token.Substring(0, Math.Min(20, token.Length))}...");
                    Debug.WriteLine($"[ChatSocketIO] DeviceId: {deviceId}");

                    UpdateConnectionState(false, "正在连接...");

                    var options = new SocketIOClient.SocketIOOptions
                    {
                        Path = "/socket.io",
                        EIO = 4,
                        Query = new Dictionary<string, string>
                        {
                            { "token", token },
                            { "device_id", deviceId }
                        },
                        Reconnection = true,
                        ReconnectionAttempts = maxRetries,
                        ReconnectionDelay = 1000,
                        ReconnectionDelayMax = 30000,
                        ConnectionTimeout = TimeSpan.FromSeconds(15),
                    };

                    _socket = new SocketIOClient.SocketIO($"{wsBaseUrl}/chat", options);

                    SetupEventHandlers();

                    await _socket.ConnectAsync();

                    _connectedAt = DateTime.Now;
                    UpdateConnectionState(true, "已连接");
                    StartHeartbeat();

                    Debug.WriteLine("[ChatSocketIO] 连接成功");
                    return (true, null);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    Debug.WriteLine($"[ChatSocketIO] 连接失败 (尝试 {attempts}/{maxRetries + 1}): {ex.Message}");

                    if (attempts <= maxRetries)
                    {
                        await Task.Delay(1000 * attempts);
                    }
                }
            }

            UpdateConnectionState(false, "连接失败", lastException?.Message);
            return (false, lastException?.Message ?? "连接失败");
        }

        private void SetupEventHandlers()
        {
            if (_socket == null) return;

            _socket.OnConnected += (sender, e) =>
            {
                Debug.WriteLine("[ChatSocketIO] 已连接到 /chat 命名空间");
                _connectedAt = DateTime.Now;
                UpdateConnectionState(true, "已连接");
                Connected?.Invoke(this, EventArgs.Empty);
            };

            _socket.OnDisconnected += (sender, e) =>
            {
                Debug.WriteLine("[ChatSocketIO] 已断开连接");
                _connectedAt = null;
                StopHeartbeat();
                UpdateConnectionState(false, "已断开连接");
                Disconnected?.Invoke(this, EventArgs.Empty);
            };

            _socket.On("connected", response =>
            {
                Debug.WriteLine($"[ChatSocketIO] 收到 connected 确认: {response}");
            });

            _socket.On("chat:message", response =>
            {
                try
                {
                    var json = response.GetValue().ToString();
                    Debug.WriteLine($"[ChatSocketIO] 收到消息原始JSON: {json}");
                    
                    var message = JsonSerializer.Deserialize<ChatMessage>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (message != null)
                    {
                        Debug.WriteLine($"[ChatSocketIO] 解析后: Id={message.Id}, SenderDeviceId={message.SenderDeviceId}, SenderName={message.SenderName}, IsMine={message.IsMine}");
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatSocketIO] 解析消息失败: {ex.Message}");
                    ErrorOccurred?.Invoke(this, $"解析消息失败: {ex.Message}");
                }
            });

            _socket.On("chat:typing", response =>
            {
                try
                {
                    var json = response.GetValue().ToString();
                    var data = JsonSerializer.Deserialize<TypingEventData>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (data != null)
                    {
                        TypingStarted?.Invoke(this, data);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatSocketIO] 解析typing失败: {ex.Message}");
                }
            });

            _socket.On("chat:stop_typing", response =>
            {
                try
                {
                    var json = response.GetValue().ToString();
                    var data = JsonSerializer.Deserialize<TypingEventData>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (data != null)
                    {
                        TypingStopped?.Invoke(this, data);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatSocketIO] 解析stop_typing失败: {ex.Message}");
                }
            });

            _socket.On("error", response =>
            {
                try
                {
                    var json = response.GetValue().ToString();
                    Debug.WriteLine($"[ChatSocketIO] 收到错误: {json}");
                    ErrorOccurred?.Invoke(this, json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatSocketIO] 解析错误失败: {ex.Message}");
                }
            });

            _socket.OnError += (sender, e) =>
            {
                Debug.WriteLine($"[ChatSocketIO] Socket错误: {e}");
                ErrorOccurred?.Invoke(this, e);
                UpdateConnectionState(false, "连接错误", e);
            };
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
                UpdateConnectionState(false, "已断开连接");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatSocketIO] 断开连接错误: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> JoinConversationAsync(string conversationId)
        {
            if (!IsConnected || _socket == null)
            {
                return (false, "未连接到聊天服务器");
            }

            try
            {
                await _socket.EmitAsync("join_conversation", new { conversation_id = conversationId });
                Debug.WriteLine($"[ChatSocketIO] 加入会话: {conversationId}");
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatSocketIO] 加入会话失败: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> LeaveConversationAsync(string conversationId)
        {
            if (!IsConnected || _socket == null)
            {
                return (false, "未连接到聊天服务器");
            }

            try
            {
                await _socket.EmitAsync("leave_conversation", new { conversation_id = conversationId });
                Debug.WriteLine($"[ChatSocketIO] 离开会话: {conversationId}");
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatSocketIO] 离开会话失败: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendMessageAsync(
            string conversationId,
            string content,
            string type = "text",
            int? replyToId = null,
            Dictionary<string, object>? extraData = null)
        {
            if (!IsConnected || _socket == null)
            {
                return (false, "未连接到聊天服务器");
            }

            try
            {
                var data = new Dictionary<string, object>
                {
                    { "conversation_id", conversationId },
                    { "content", content },
                    { "type", type }
                };

                if (replyToId.HasValue)
                {
                    data["reply_to_id"] = replyToId.Value;
                }

                if (extraData != null)
                {
                    data["extra_data"] = extraData;
                }

                await _socket.EmitAsync("send_message", data);
                Debug.WriteLine($"[ChatSocketIO] 发送消息: {conversationId}, {content.Substring(0, Math.Min(50, content.Length))}...");
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatSocketIO] 发送消息失败: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task SendTypingStartAsync(string conversationId)
        {
            if (!IsConnected || _socket == null) return;

            try
            {
                await _socket.EmitAsync("typing_start", new { conversation_id = conversationId });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatSocketIO] 发送typing_start失败: {ex.Message}");
            }
        }

        public async Task SendTypingStopAsync(string conversationId)
        {
            if (!IsConnected || _socket == null) return;

            try
            {
                await _socket.EmitAsync("typing_stop", new { conversation_id = conversationId });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatSocketIO] 发送typing_stop失败: {ex.Message}");
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
                    if (IsConnected && _socket != null)
                    {
                        try
                        {
                            await _socket.EmitAsync("ping", new { time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ChatSocketIO] 心跳发送失败: {ex.Message}");
                        }
                    }
                };
                _heartbeatTimer.Start();
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
                }
            }
        }

        private void UpdateConnectionState(bool isConnected, string statusText, string? error = null)
        {
            var info = new ChatConnectionInfo
            {
                IsConnected = isConnected,
                StatusText = statusText,
                ConnectedAt = _connectedAt,
                LastError = error
            };
            ConnectionStateChanged?.Invoke(this, info);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            StopHeartbeat();
            _cancellationTokenSource?.Dispose();
            _socket?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            Dispose();
        }
    }

    public class TypingEventData
    {
        public string ConversationId { get; set; } = "";
        public string? DeviceId { get; set; }
        public int? UserId { get; set; }
        public string? SenderName { get; set; }
    }
}
