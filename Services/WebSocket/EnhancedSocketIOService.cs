using Nexus.Services.WebSocket.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services.WebSocket;

public class EnhancedSocketIOService : IDisposable
{
    private readonly string _baseUrl;
    private readonly WebSocketConfig _config;
    private SocketIOClient.SocketIO? _socket;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _lock = new();
    private bool _isDisposed;
    private DateTime? _connectedAt;
    private string? _currentServerUrl;
    private int _reconnectAttempts;

    private readonly MessageQueueManager _messageQueue;
    private readonly AckManager _ackManager;
    private readonly SmartHeartbeatManager _heartbeatManager;
    private readonly ExponentialBackoffStrategy _reconnectStrategy;
    private readonly StateRecoveryManager _stateRecovery;
    private readonly SequenceNumberManager _sequenceManager;
    private readonly ConnectionQualityMonitor _qualityMonitor;
    private readonly FlowController _flowController;

    public event EventHandler<JsonElement>? MessageReceived;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<string>? Reconnecting;
    public event EventHandler? Reconnected;
    public event EventHandler<ConnectionInfo>? ConnectionInfoChanged;
    public event EventHandler<int>? LatencyUpdated;
    public event EventHandler<QualityReport>? QualityChanged;
    public event EventHandler<RecoveryProgressEventArgs>? RecoveryProgressChanged;

    public bool IsConnected => _socket?.Connected ?? false;
    public int CurrentLatency => _qualityMonitor.GetReport().AverageLatencyMs;
    public ConnectionInfo CurrentConnectionInfo => GetConnectionInfo();
    public WebSocketConfig Config => _config;

    public EnhancedSocketIOService(string baseUrl, WebSocketConfig? config = null)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _config = config ?? new WebSocketConfig();

        _messageQueue = new MessageQueueManager(_config.MaxQueueSize, _config.MessageTtl);
        _ackManager = new AckManager(_config.MaxAckRetries, _config.AckTimeout);
        _heartbeatManager = new SmartHeartbeatManager(
            _config.HeartbeatMinIntervalMs,
            _config.HeartbeatMaxIntervalMs,
            _config.HeartbeatDefaultIntervalMs);
        _reconnectStrategy = new ExponentialBackoffStrategy(
            _config.ReconnectBaseDelayMs,
            _config.ReconnectMaxDelayMs,
            _config.ReconnectJitter,
            2.0,
            _config.MaxReconnectAttempts);
        _stateRecovery = new StateRecoveryManager();
        _sequenceManager = new SequenceNumberManager(_config.SequenceWindowSize);
        _qualityMonitor = new ConnectionQualityMonitor(_config.QualitySampleSize);
        _flowController = new FlowController(_config.MaxConcurrentSends, _config.MinSendIntervalMs);

        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        _heartbeatManager.HeartbeatRequired += async (s, e) =>
        {
            if (IsConnected)
            {
                var startTime = DateTime.UtcNow;
                await SendPingAsync();
                var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _heartbeatManager.RecordLatency(latency);
                _qualityMonitor.RecordLatency(latency);
            }
        };

        _heartbeatManager.QualityChanged += (s, e) =>
        {
            Debug.WriteLine($"[EnhancedSocketIO] 质量变化: {e.OldQuality} -> {e.NewQuality}");
        };

        _ackManager.AckTimeout += (s, e) =>
        {
            Debug.WriteLine($"[EnhancedSocketIO] ACK超时: {e.MessageId}, 事件: {e.EventType}");
            if (e.Data != null)
            {
                _messageQueue.Enqueue(e.EventType, e.Data, priority: 5, requiresAck: true);
            }
        };

        _ackManager.AckRetry += (s, e) =>
        {
            Debug.WriteLine($"[EnhancedSocketIO] ACK重试: {e.MessageId}, 次数: {e.RetryCount}");
        };

        _stateRecovery.ProgressChanged += (s, e) =>
        {
            RecoveryProgressChanged?.Invoke(this, e);
        };

        _qualityMonitor.QualityReportGenerated += (s, e) =>
        {
            QualityChanged?.Invoke(this, e.Report);
        };
    }

    public void RegisterRecoveryAction(string key, Func<Task> action, int priority = 0)
    {
        _stateRecovery.RegisterAction(key, action, priority);
    }

    public void UnregisterRecoveryAction(string key)
    {
        _stateRecovery.UnregisterAction(key);
    }

    private ConnectionInfo GetConnectionInfo()
    {
        var qualityReport = _qualityMonitor.GetReport();
        return new ConnectionInfo
        {
            Status = GetStatusFromState(),
            StatusText = GetStatusText(),
            LatencyMs = (int)qualityReport.AverageLatency,
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
        var qualityReport = _qualityMonitor.GetReport();
        return GetStatusFromState() switch
        {
            ConnectionStatus.Connected => $"已连接 ({qualityReport.AverageLatency:F0}ms, {qualityReport.Quality})",
            ConnectionStatus.Connecting => "连接中...",
            ConnectionStatus.Reconnecting => $"重连中 (第{_reconnectAttempts}次)",
            ConnectionStatus.Error => "连接错误",
            _ => "未连接"
        };
    }

    private void UpdateConnectionInfo(ConnectionStatus status, string statusText, string? error = null)
    {
        var qualityReport = _qualityMonitor.GetReport();
        var info = new ConnectionInfo
        {
            Status = status,
            StatusText = statusText,
            LatencyMs = (int)qualityReport.AverageLatency,
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
                _reconnectAttempts = 0;
                _reconnectStrategy.Reset();

                string wsScheme = _baseUrl.StartsWith("https") ? "wss" : "ws";
                string wsBaseUrl = _baseUrl.Replace("http://", $"{wsScheme}://").Replace("https://", $"{wsScheme}://");

                _currentServerUrl = $"{wsBaseUrl}/desktop/bind";

                Debug.WriteLine($"[EnhancedSocketIO] 连接到: {_currentServerUrl}");

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
                    ReconnectionDelay = (int)_reconnectStrategy.GetNextDelay().TotalMilliseconds,
                    ReconnectionDelayMax = (int)_config.ReconnectMaxDelayMs,
                    ConnectionTimeout = _config.ConnectionTimeout,
                };

                _socket = new SocketIOClient.SocketIO($"{wsBaseUrl}/desktop/bind", options);

                SetupSocketEvents();

                await _socket.ConnectAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts++;
                Debug.WriteLine($"[EnhancedSocketIO] 连接失败 (尝试 {attempts}/{maxRetries + 1}): {ex.Message}");

                if (attempts <= maxRetries)
                {
                    await Task.Delay(1000 * attempts);
                }
            }
        }

        return (false, lastException?.Message ?? "EnhancedSocketIO连接失败");
    }

    private void SetupSocketEvents()
    {
        if (_socket == null) return;

        _socket.OnConnected += async (sender, e) =>
        {
            Debug.WriteLine("[EnhancedSocketIO] 已连接");
            _reconnectAttempts = 0;
            _connectedAt = DateTime.Now;
            _heartbeatManager.Start();
            _qualityMonitor.Reset();
            UpdateConnectionInfo(ConnectionStatus.Connected, "WebSocket连接成功");
            Connected?.Invoke(this, EventArgs.Empty);

            await ProcessQueuedMessages();
            await _stateRecovery.ExecuteRecovery();
        };

        _socket.OnDisconnected += (sender, e) =>
        {
            Debug.WriteLine("[EnhancedSocketIO] 已断开");
            _heartbeatManager.Stop();
            _connectedAt = null;
            UpdateConnectionInfo(ConnectionStatus.Disconnected, "WebSocket连接已断开");
            Disconnected?.Invoke(this, EventArgs.Empty);

            _reconnectAttempts++;
            var delay = _reconnectStrategy.GetNextDelay();
            var msg = $"正在重连... (尝试 {_reconnectAttempts}, 等待 {delay.TotalSeconds:F1}s)";
            UpdateConnectionInfo(ConnectionStatus.Reconnecting, msg);
            Reconnecting?.Invoke(this, msg);
        };

        _socket.On("connect_response", response => HandleMessage("connect_response", response));
        _socket.On("bind_notification", response => HandleMessage("bind_notification", response));
        _socket.On("power_control", response => HandleMessage("power_control", response));

        _socket.On("pong", response =>
        {
            Debug.WriteLine($"[EnhancedSocketIO] 收到 pong");
            _heartbeatManager.RecordSuccess();
            _qualityMonitor.RecordResult(true);
        });

        _socket.On("ack", response =>
        {
            try
            {
                var json = response.GetValue().ToString();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message_id", out var idElement))
                {
                    var messageId = idElement.GetString();
                    if (messageId != null)
                    {
                        _ackManager.ConfirmAck(messageId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EnhancedSocketIO] 处理ACK失败: {ex.Message}");
            }
        });

        _socket.OnError += (sender, e) =>
        {
            Debug.WriteLine($"[EnhancedSocketIO] 错误: {e}");
            _heartbeatManager.RecordFailure();
            _qualityMonitor.RecordResult(false);
            UpdateConnectionInfo(ConnectionStatus.Error, $"连接错误: {e}", e);
            ErrorOccurred?.Invoke(this, e);
        };

        _socket.OnReconnected += (sender, e) =>
        {
            Debug.WriteLine("[EnhancedSocketIO] 重连成功");
            _reconnectAttempts = 0;
            _reconnectStrategy.Reset();
            _connectedAt = DateTime.Now;
            _heartbeatManager.Start();
            UpdateConnectionInfo(ConnectionStatus.Connected, "WebSocket重连成功");
            Reconnected?.Invoke(this, EventArgs.Empty);
        };
    }

    private void HandleMessage(string eventType, SocketIOClient.SocketIOResponse response)
    {
        try
        {
            var json = response.GetValue().ToString();
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var doc = JsonDocument.Parse(bytes);
            var element = doc.RootElement.Clone();
            doc.Dispose();

            if (element.TryGetProperty("seq", out var seqElement))
            {
                var seq = seqElement.GetInt64();
                if (!_sequenceManager.ValidateReceiveSequence(seq))
                {
                    Debug.WriteLine($"[EnhancedSocketIO] 重复消息, seq: {seq}");
                    return;
                }
            }

            if (element.TryGetProperty("_message_id", out var msgIdElement))
            {
                var messageId = msgIdElement.GetString();
                if (messageId != null)
                {
                    _ = SendAckAsync(messageId);
                }
            }

            MessageReceived?.Invoke(this, element);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EnhancedSocketIO] 解析消息失败: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"解析消息失败: {ex.Message}");
        }
    }

    private async Task SendAckAsync(string messageId)
    {
        await SendAsync("ack", new { message_id = messageId }, new SendOptions { Priority = 10 });
    }

    private async Task ProcessQueuedMessages()
    {
        while (!_messageQueue.IsEmpty && IsConnected)
        {
            var message = _messageQueue.Dequeue();
            if (message == null) break;

            if (message.IsExpired)
            {
                Debug.WriteLine($"[EnhancedSocketIO] 消息已过期: {message.Id}");
                continue;
            }

            var result = await SendAsync(message.EventType, message.Data, new SendOptions
            {
                Priority = message.Priority,
                RequiresAck = message.RequiresAck
            });

            if (!result.Success)
            {
                message.RetryCount++;
                if (message.RetryCount < _config.MaxAckRetries)
                {
                    _messageQueue.Enqueue(message.EventType, message.Data, message.Priority, message.RequiresAck, message.SequenceNumber);
                }
            }
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendAsync(
        string eventType,
        object? data,
        SendOptions? options = null)
    {
        if (!IsConnected || _socket == null)
        {
            if (options?.PersistOffline ?? true)
            {
                var sequence = _sequenceManager.GetNextSendSequence();
                _messageQueue.Enqueue(eventType, data, options?.Priority ?? 0, options?.RequiresAck ?? false, sequence);
            }
            return (false, "未连接，消息已加入队列");
        }

        await _flowController.WaitSendPermission();

        try
        {
            var sequence = _sequenceManager.GetNextSendSequence();

            var message = new
            {
                type = eventType,
                data,
                seq = sequence,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await _socket.EmitAsync(eventType, message);

            if (options?.RequiresAck ?? false)
            {
                _ackManager.RegisterAck(eventType, data, sequence);
            }

            _qualityMonitor.RecordResult(true);

            return (true, null);
        }
        catch (Exception ex)
        {
            _qualityMonitor.RecordResult(false);
            Debug.WriteLine($"[EnhancedSocketIO] 发送失败: {ex.Message}");

            if (options?.PersistOffline ?? true)
            {
                _messageQueue.Enqueue(eventType, data, options?.Priority ?? 0, options?.RequiresAck ?? false);
            }

            return (false, ex.Message);
        }
        finally
        {
            _flowController.ReleaseSendPermission();
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendWithAckAsync(
        string eventType,
        object? data,
        TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        var actualTimeout = timeout ?? _config.AckTimeout;

        var options = new SendOptions
        {
            RequiresAck = true,
            Timeout = actualTimeout
        };

        EventHandler<AckConfirmedEventArgs>? handler = null;
        handler = (s, e) =>
        {
            if (e.EventType == eventType)
            {
                _ackManager.AckConfirmed -= handler;
                tcs.TrySetResult(true);
            }
        };

        _ackManager.AckConfirmed += handler;

        var result = await SendAsync(eventType, data, options);
        if (!result.Success)
        {
            _ackManager.AckConfirmed -= handler;
            return result;
        }

        var completed = await Task.WhenAny(
            tcs.Task,
            Task.Delay(actualTimeout)
        );

        _ackManager.AckConfirmed -= handler;

        if (completed != tcs.Task)
        {
            return (false, "ACK timeout");
        }

        return (true, null);
    }

    public async Task EnqueueMessageAsync(string eventType, object? data, int priority = 0)
    {
        var sequence = _sequenceManager.GetNextSendSequence();
        _messageQueue.Enqueue(eventType, data, priority, false, sequence);

        if (IsConnected)
        {
            await ProcessQueuedMessages();
        }
    }

    public async Task SendPingAsync()
    {
        var startTime = DateTime.UtcNow;
        var result = await SendAsync("ping", new { time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, new SendOptions { Priority = 10 });

        if (result.Success)
        {
            var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _qualityMonitor.RecordLatency(latency);
            LatencyUpdated?.Invoke(this, latency);
        }
        else
        {
            Debug.WriteLine($"[EnhancedSocketIO] 发送心跳失败: {result.ErrorMessage}");
        }
    }

    public QualityReport GetQualityReport()
    {
        return _qualityMonitor.GetReport();
    }

    public QueueStatus GetQueueStatus()
    {
        return _messageQueue.GetStatus();
    }

    public SequenceStats GetSequenceStats()
    {
        return _sequenceManager.GetStats();
    }

    public HeartbeatStats GetHeartbeatStats()
    {
        return _heartbeatManager.GetStats();
    }

    public FlowStats GetFlowStats()
    {
        return _flowController.GetStats();
    }

    public async Task DisconnectAsync()
    {
        try
        {
            lock (_lock)
            {
                _heartbeatManager.Stop();
            }

            if (_socket?.Connected == true)
            {
                await _socket.DisconnectAsync();
            }
            _socket?.Dispose();
            _socket = null;
            _connectedAt = null;
            _reconnectAttempts = 0;
            _reconnectStrategy.Reset();
            UpdateConnectionInfo(ConnectionStatus.Disconnected, "已断开连接");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EnhancedSocketIO] 断开连接错误: {ex.Message}");
        }
    }

    public async Task SaveStateAsync(string path)
    {
        if (_config.EnablePersistence)
        {
            await _messageQueue.PersistToFileAsync(System.IO.Path.Combine(path, "message_queue.json"));
        }
    }

    public async Task LoadStateAsync(string path)
    {
        if (_config.EnablePersistence)
        {
            await _messageQueue.LoadFromFileAsync(System.IO.Path.Combine(path, "message_queue.json"));
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _ = DisconnectAsync();
        _heartbeatManager.Dispose();
        _messageQueue.Dispose();
        _ackManager.Dispose();
        _qualityMonitor.Dispose();
        _flowController.Dispose();
        _cancellationTokenSource?.Dispose();

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

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
