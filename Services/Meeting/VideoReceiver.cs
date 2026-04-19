using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<long, FrameAssembler> _frameAssemblers = new();
        private long _frameIdCounter;
        private const int HeaderSize = 10;
        private int _consecutiveDecryptErrors;
        private DateTime _lastErrorLogTime = DateTime.MinValue;
        private const int MaxConsecutiveErrors = 10;
        private static readonly TimeSpan ErrorLogInterval = TimeSpan.FromSeconds(5);
        private uint _lastReceivedSequence;
        private int _frameCount;

        public event EventHandler<byte[]>? FrameReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? Started;
        public event EventHandler? Stopped;
        public event EventHandler<FrameStatisticsEventArgs>? StatisticsUpdated;

        public bool IsReceiving => _isReceiving;
        public int FrameCount => _frameCount;

        public async Task StartReceivingAsync(int port, string encryptionKey)
        {
            if (_isReceiving)
            {
                Debug.WriteLine("[VideoReceiver] 已经在接收中");
                return;
            }

            try
            {
                Debug.WriteLine($"[VideoReceiver] 接收密钥: 长度={encryptionKey?.Length ?? 0}, 前10字符={(encryptionKey?.Length > 10 ? encryptionKey.Substring(0, 10) + "..." : encryptionKey ?? "null")}");
                
                _decryptionService = new DecryptionService(encryptionKey);
                _udpClient = new UdpClient(port);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.ReceiveBufferSize = 1024 * 1024 * 4;

                _cancellationTokenSource = new CancellationTokenSource();
                _isReceiving = true;
                _consecutiveDecryptErrors = 0;
                _frameIdCounter = 0;
                _frameCount = 0;
                _lastReceivedSequence = 0;

                Started?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine($"[VideoReceiver] 开始接收加密视频流，端口: {port}");

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
                        ProcessPacket(result.Buffer);
                    }
                    catch (Exception ex)
                    {
                        LogError($"[VideoReceiver] 处理帧失败: {ex.Message}");
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

        private void ProcessPacket(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            if (EncryptedFrameHeader.IsValidMagic(data))
            {
                ProcessEncryptedFrame(data);
                return;
            }

            if (data.Length < HeaderSize)
            {
                Debug.WriteLine($"[VideoReceiver] 小数据包: 大小={data.Length}");
                TryDecryptAndDispatch(data);
                return;
            }

            int totalSize = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
            int totalChunks = (data[4] << 8) | data[5];
            int chunkIndex = (data[8] << 8) | data[9];

            if (totalSize == 0 || totalChunks <= 1 || totalSize > 10 * 1024 * 1024 || chunkIndex < 0 || chunkIndex >= totalChunks)
            {
                Debug.WriteLine($"[VideoReceiver] 无效分片头或单帧: totalSize={totalSize}, totalChunks={totalChunks}, chunkIndex={chunkIndex}, 数据大小={data.Length}");
                TryDecryptAndDispatch(data);
                return;
            }

            if (chunkIndex == 0)
            {
                Debug.WriteLine($"[VideoReceiver] 收到首分片: totalSize={totalSize}, totalChunks={totalChunks}, packet大小={data.Length}");
            }

            long frameId = GetOrCreateFrameId(totalSize, totalChunks, chunkIndex);
            
            var chunkData = new byte[data.Length - HeaderSize];
            Buffer.BlockCopy(data, HeaderSize, chunkData, 0, chunkData.Length);

            var assembler = _frameAssemblers.GetOrAdd(frameId, _ => new FrameAssembler(totalSize, totalChunks));
            assembler.AddChunk(chunkIndex, chunkData);

            if (assembler.IsComplete)
            {
                _frameAssemblers.TryRemove(frameId, out _);
                var completeData = assembler.Assemble();
                Debug.WriteLine($"[VideoReceiver] 分片重组完成: frameId={frameId}, 总大小={completeData.Length}, 预期大小={assembler.TotalSize}, 收到分片数={assembler.ReceivedCount}/{assembler.TotalChunks}");
                TryDecryptAndDispatch(completeData);
            }

            CleanupOldAssemblers();
        }

        private void ProcessEncryptedFrame(byte[] data)
        {
            var header = EncryptedFrameHeader.Parse(data);
            if (header == null || !header.IsValid())
            {
                Debug.WriteLine($"[VideoReceiver] 无效的加密帧头");
                TryDecryptAndDispatch(data);
                return;
            }

            if (header.Sequence <= _lastReceivedSequence && _lastReceivedSequence - header.Sequence < 1000000)
            {
                Debug.WriteLine($"[VideoReceiver] 检测到重放帧: seq={header.Sequence}, lastSeq={_lastReceivedSequence}");
            }

            _lastReceivedSequence = header.Sequence;
            TryDecryptAndDispatch(data);
        }

        private long GetOrCreateFrameId(int totalSize, int totalChunks, int chunkIndex)
        {
            if (chunkIndex == 0)
            {
                return Interlocked.Increment(ref _frameIdCounter);
            }

            foreach (var kvp in _frameAssemblers)
            {
                if (kvp.Value.TotalSize == totalSize && kvp.Value.TotalChunks == totalChunks && !kvp.Value.HasChunk(chunkIndex))
                {
                    return kvp.Key;
                }
            }

            return Interlocked.Increment(ref _frameIdCounter);
        }

        private void CleanupOldAssemblers()
        {
            if (_frameAssemblers.Count > 10)
            {
                var keysToRemove = new System.Collections.Generic.List<long>();
                foreach (var kvp in _frameAssemblers)
                {
                    if (DateTime.Now - kvp.Value.CreatedAt > TimeSpan.FromSeconds(5))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _frameAssemblers.TryRemove(key, out _);
                }
            }
        }

        private void TryDecryptAndDispatch(byte[] data)
        {
            if (data == null || data.Length < 28)
                return;

            try
            {
                if (_consecutiveDecryptErrors == 0)
                {
                    var noncePreview = BitConverter.ToString(data, 0, Math.Min(4, data.Length)).Replace("-", "");
                    Debug.WriteLine($"[VideoReceiver] 尝试解密: 数据大小={data.Length}, 前4字节={noncePreview}");
                }
                
                var decryptedFrame = _decryptionService?.Decrypt(data);
                if (decryptedFrame != null && decryptedFrame.Length > 0)
                {
                    _consecutiveDecryptErrors = 0;
                    _frameCount++;
                    
                    Debug.WriteLine($"[VideoReceiver] 解密成功: 输出大小={decryptedFrame.Length}, 累计帧数={_frameCount}");
                    
                    StatisticsUpdated?.Invoke(this, new FrameStatisticsEventArgs
                    {
                        TotalFrames = _frameCount,
                        LastFrameSize = decryptedFrame.Length
                    });
                    
                    FrameReceived?.Invoke(this, decryptedFrame);
                }
                else
                {
                    Debug.WriteLine($"[VideoReceiver] 解密返回空数据");
                }
            }
            catch (CryptographicException ex)
            {
                _consecutiveDecryptErrors++;
                
                if (_consecutiveDecryptErrors == 1)
                {
                    Debug.WriteLine($"[VideoReceiver] 首次解密失败 - 数据大小: {data.Length}, 错误: {ex.Message}");
                }
                
                if (_consecutiveDecryptErrors <= MaxConsecutiveErrors)
                {
                    LogError($"[VideoReceiver] 解密失败 (连续错误: {_consecutiveDecryptErrors})");
                }
                else if (_consecutiveDecryptErrors == MaxConsecutiveErrors + 1)
                {
                    LogError($"[VideoReceiver] 解密错误过多，已停止输出详细日志");
                }

                if (_consecutiveDecryptErrors > 100)
                {
                    ErrorOccurred?.Invoke(this, "视频解密持续失败，请检查会议密钥是否正确");
                    _consecutiveDecryptErrors = 0;
                }
            }
            catch (Exception ex)
            {
                LogError($"[VideoReceiver] 解密异常: {ex.Message}");
            }
        }

        private void LogError(string message)
        {
            var now = DateTime.Now;
            if (now - _lastErrorLogTime > ErrorLogInterval)
            {
                Debug.WriteLine(message);
                _lastErrorLogTime = now;
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
                _frameAssemblers.Clear();

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

        private class FrameAssembler
        {
            private readonly byte[][] _chunks;
            private readonly bool[] _received;
            private int _receivedCount;

            public int TotalChunks { get; }
            public int TotalSize { get; }
            public int ReceivedCount => _receivedCount;
            public bool IsComplete => _receivedCount == TotalChunks;
            public DateTime CreatedAt { get; } = DateTime.Now;

            public FrameAssembler(int totalSize, int totalChunks)
            {
                TotalSize = totalSize;
                TotalChunks = totalChunks;
                _chunks = new byte[totalChunks][];
                _received = new bool[totalChunks];
                _receivedCount = 0;
            }

            public bool HasChunk(int index)
            {
                if (index < 0 || index >= TotalChunks)
                    return true;
                return _received[index];
            }

            public void AddChunk(int index, byte[] data)
            {
                if (index < 0 || index >= TotalChunks)
                    return;

                if (!_received[index])
                {
                    _chunks[index] = data;
                    _received[index] = true;
                    _receivedCount++;
                }
            }

            public byte[] Assemble()
            {
                var result = new byte[TotalSize];
                int offset = 0;

                foreach (var chunk in _chunks)
                {
                    if (chunk == null)
                        continue;

                    int copyLength = Math.Min(chunk.Length, result.Length - offset);
                    if (copyLength > 0)
                    {
                        Buffer.BlockCopy(chunk, 0, result, offset, copyLength);
                        offset += copyLength;
                    }
                }

                return result;
            }
        }
    }

    public class FrameStatisticsEventArgs : EventArgs
    {
        public int TotalFrames { get; set; }
        public int LastFrameSize { get; set; }
    }
}
