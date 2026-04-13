using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services.Meeting
{
    public class VideoReceiver : IDisposable
    {
        private UdpClient? _udpClient;
        private DecryptionService? _decryptionService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;
        private bool _isReceiving;

        public event EventHandler<byte[]>? FrameReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Started;
        public event EventHandler? Stopped;

        public bool IsReceiving => _isReceiving;

        public async Task StartReceivingAsync(int port, string encryptionKey)
        {
            if (_isReceiving)
            {
                Debug.WriteLine("[VideoReceiver] 已经在接收中");
                return;
            }

            try
            {
                _decryptionService = new DecryptionService(encryptionKey);
                _udpClient = new UdpClient(port);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.ReceiveBufferSize = 1024 * 1024 * 4;

                _cancellationTokenSource = new CancellationTokenSource();
                _isReceiving = true;

                Started?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine($"[VideoReceiver] 开始接收视频流，端口: {port}");

                _ = ReceiveLoopAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _isReceiving = false;
                Debug.WriteLine($"[VideoReceiver] 启动失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, $"启动视频接收失败: {ex.Message}");
                throw;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync();
                    
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var decryptedFrame = _decryptionService?.Decrypt(result.Buffer);
                        if (decryptedFrame != null)
                        {
                            FrameReceived?.Invoke(this, decryptedFrame);
                        }
                    }
                    catch (CryptographicException ex)
                    {
                        Debug.WriteLine($"[VideoReceiver] 解密失败: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[VideoReceiver] 处理帧失败: {ex.Message}");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine($"[VideoReceiver] 接收循环异常: {ex.Message}");
                    ErrorOccurred?.Invoke(this, $"接收视频流异常: {ex.Message}");
                }
            }
            finally
            {
                _isReceiving = false;
                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        public void StopReceiving()
        {
            if (!_isReceiving)
                return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _udpClient?.Close();
                _decryptionService?.Dispose();

                _udpClient = null;
                _decryptionService = null;
                _cancellationTokenSource = null;
                _isReceiving = false;

                Debug.WriteLine("[VideoReceiver] 已停止接收");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoReceiver] 停止接收时出错: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopReceiving();
            _cancellationTokenSource?.Dispose();
            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
