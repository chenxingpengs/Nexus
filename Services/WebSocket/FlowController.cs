using Nexus.Services.WebSocket.Models;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services.WebSocket;

public class FlowController : IDisposable
{
    private readonly int _maxConcurrentSends;
    private readonly int _maxQueueSize;
    private readonly TimeSpan _minSendInterval;
    private readonly SemaphoreSlim _sendSemaphore;
    private DateTime _lastSendTime;
    private Timer? _statsTimer;
    private readonly object _lock = new();
    private bool _isDisposed;
    private int _droppedMessages;
    private int _totalSends;
    private readonly ConcurrentQueue<DateTime> _sendTimes;

    public event EventHandler<FlowControlEventArgs>? FlowControlTriggered;

    public FlowController(int maxConcurrentSends = 5, int minSendIntervalMs = 50, int maxQueueSize = 100)
    {
        _maxConcurrentSends = maxConcurrentSends;
        _maxQueueSize = maxQueueSize;
        _minSendInterval = TimeSpan.FromMilliseconds(minSendIntervalMs);
        _sendSemaphore = new SemaphoreSlim(maxConcurrentSends, maxConcurrentSends);
        _sendTimes = new ConcurrentQueue<DateTime>();
        _statsTimer = new Timer(CalculateStats, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public async Task WaitSendPermission()
    {
        if (_isDisposed) return;

        var now = DateTime.UtcNow;
        var timeSinceLastSend = now - _lastSendTime;

        if (timeSinceLastSend < _minSendInterval)
        {
            var delay = _minSendInterval - timeSinceLastSend;
            await Task.Delay(delay);
        }

        await _sendSemaphore.WaitAsync();

        lock (_lock)
        {
            _lastSendTime = DateTime.UtcNow;
            _sendTimes.Enqueue(_lastSendTime);
            _totalSends++;
        }
    }

    public void ReleaseSendPermission()
    {
        if (_isDisposed) return;

        _sendSemaphore.Release();
    }

    public bool CanSend()
    {
        if (_isDisposed) return false;

        return _sendSemaphore.CurrentCount > 0;
    }

    public int GetAvailableSlots()
    {
        return _sendSemaphore.CurrentCount;
    }

    public void RecordDropped()
    {
        Interlocked.Increment(ref _droppedMessages);

        FlowControlTriggered?.Invoke(this, new FlowControlEventArgs
        {
            Reason = "队列已满，消息被丢弃",
            DroppedCount = _droppedMessages
        });
    }

    private void CalculateStats(object? state)
    {
        if (_isDisposed) return;

        var now = DateTime.UtcNow;
        var oneSecondAgo = now.AddSeconds(-1);

        while (_sendTimes.TryDequeue(out var sendTime))
        {
            if (sendTime > oneSecondAgo)
            {
                _sendTimes.Enqueue(sendTime);
                break;
            }
        }
    }

    public FlowStats GetStats()
    {
        var now = DateTime.UtcNow;
        var oneSecondAgo = now.AddSeconds(-1);
        var recentSends = 0;

        foreach (var sendTime in _sendTimes)
        {
            if (sendTime > oneSecondAgo)
                recentSends++;
        }

        return new FlowStats
        {
            CurrentQueueSize = _maxConcurrentSends - _sendSemaphore.CurrentCount,
            MaxQueueSize = _maxConcurrentSends,
            AvailableSlots = _sendSemaphore.CurrentCount,
            SendRate = recentSends,
            DroppedMessages = _droppedMessages
        };
    }

    public void AdjustLimits(int maxConcurrent, int maxQueue)
    {
        // Note: SemaphoreSlim doesn't support dynamic adjustment
        // This is a placeholder for future implementation
    }

    public void Reset()
    {
        lock (_lock)
        {
            while (_sendTimes.TryDequeue(out _)) { }
            _droppedMessages = 0;
            _totalSends = 0;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _statsTimer?.Dispose();
        _sendSemaphore.Dispose();
    }
}

public class FlowControlEventArgs : EventArgs
{
    public string Reason { get; set; } = string.Empty;
    public int DroppedCount { get; set; }
}
