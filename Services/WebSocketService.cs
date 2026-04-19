using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services
{
    /// <summary>
    /// WebSocket 服务 - 提供稳定的 WebSocket 连接和自动重连
    /// </summary>
    public class WebSocketService : IDisposable
    {
        private ClientWebSocket _webSocket;
        private readonly string _baseUrl;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler Connected;
        public event EventHandler Disconnected;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public WebSocketService(string baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        /// <summary>
        /// 连接到 WebSocket 服务器
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ConnectAsync(
            string token,
            string deviceId,
            int maxRetries = 3)
        {
            int attempts = 0;
            Exception? lastException = null;

            while (attempts <= maxRetries)
            {
                try
                {
                    if (_webSocket?.State == WebSocketState.Open)
                    {
                        await DisconnectAsync();
                    }

                    _cancellationTokenSource = new CancellationTokenSource();
                    _webSocket = new ClientWebSocket();

                    string wsScheme = _baseUrl.StartsWith("https") ? "wss" : "ws";
                    string wsBaseUrl = _baseUrl.Replace("http://", $"{wsScheme}://").Replace("https://", $"{wsScheme}://");
                    string wsUrl = $"{wsBaseUrl}/desktop/bind?token={Uri.EscapeDataString(token)}&device_id={Uri.EscapeDataString(deviceId)}";

                    System.Diagnostics.Debug.WriteLine($"[WebSocket] 连接 URL: {wsUrl}");

                    await _webSocket.ConnectAsync(new Uri(wsUrl), _cancellationTokenSource.Token);

                    Connected?.Invoke(this, EventArgs.Empty);

                    _ = ReceiveMessagesAsync();
                    
                    return (true, null);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    System.Diagnostics.Debug.WriteLine($"[WebSocket] 连接失败 (尝试 {attempts}/{maxRetries + 1}): {ex.Message}");
                    
                    if (attempts <= maxRetries)
                    {
                        await Task.Delay(1000 * attempts);
                    }
                }
            }

            return (false, lastException?.Message ?? "WebSocket连接失败");
        }

        /// <summary>
        /// 断开 WebSocket 连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "User disconnected",
                        CancellationToken.None);
                }

                _webSocket?.Dispose();
                _webSocket = null;

                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebSocket disconnect error: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> SendAsync(string message)
        {
            if (!IsConnected)
            {
                return (false, "WebSocket 未连接");
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                var buffer = new ArraySegment<byte>(bytes);
                await _webSocket.SendAsync(
                    buffer,
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource.Token);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"发送消息失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 接收消息循环
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            try
            {
                var buffer = new byte[1024 * 4];

                while (_webSocket?.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        MessageReceived?.Invoke(this, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            string.Empty,
                            CancellationToken.None);
                        Disconnected?.Invoke(this, EventArgs.Empty);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不处理
            }
            catch (WebSocketException ex)
            {
                ErrorOccurred?.Invoke(this, $"WebSocket 错误：{ex.Message}");
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"接收消息失败：{ex.Message}");
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 解析 WebSocket 消息
        /// </summary>
        public static (string Type, string Message, JsonElement? Data) ParseMessage(string message)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;

                string msgType = root.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString() ?? ""
                    : "";

                string msg = root.TryGetProperty("msg", out var msgElement)
                    ? msgElement.GetString() ?? ""
                    : "";

                JsonElement? data = root.TryGetProperty("data", out var dataElement)
                    ? dataElement
                    : null;

                return (msgType, msg, data);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Parse message error: {ex.Message}");
                return ("error", "消息格式错误", null);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _ = DisconnectAsync();
            _cancellationTokenSource?.Dispose();
            _webSocket?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
