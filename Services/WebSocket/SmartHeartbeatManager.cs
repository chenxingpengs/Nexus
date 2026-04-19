using Nexus.Services.WebSocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Nexus.Services.WebSocket;

public class SmartHeartbeatManager : IDisposable
{
    private readonly int _minInterval;
    private readonly int _maxInterval;
    private readonly int _defaultInterval;
    private readonly Queue<int> _latencyHistory;
    private readonly int _historySize;
    private Timer? _heartbeatTimer;
    private readonly object _lock = new();
    private bool _isDisposed;
    private DateTime _lastHeartbeatTime;
    private int _successCount;
    private int _failureCount;

    public int CurrentInterval { get; private set; }
    public ConnectionQuality Quality { get; private set; } = ConnectionQuality.Good;

    public event EventHandler<HeartbeatEventArgs>? HeartbeatRequired;
    public event EventHandler<QualityChangedEventArgs>? QualityChanged;

    public SmartHeartbeatManager(
        int minIntervalMs = 10000,
        int maxIntervalMs = 60000,
        int defaultIntervalMs = 30000,
        int historySize = 10)
    {
        _minInterval = minIntervalMs;
        _maxInterval = maxIntervalMs;
        _defaultInterval = defaultIntervalMs;
        _historySize = historySize;
        _latencyHistory = new Queue<int>();
        CurrentInterval = _defaultInterval;
    }

    public void Start()
    {
        if (_isDisposed) return;

        Stop();
        lock (_lock)
        {
            _heartbeatTimer = new Timer(OnHeartbeatTimer, null, CurrentInterval, CurrentInterval);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }
    }

    private void OnHeartbeatTimer(object? state)
    {
        if (_isDisposed) return;

        _lastHeartbeatTime = DateTime.UtcNow;
        HeartbeatRequired?.Invoke(this, new HeartbeatEventArgs
        {
            Timestamp = _lastHeartbeatTime
        });
    }

    public void RecordLatency(int latencyMs)
    {
        if (_isDisposed) return;

        lock (_lock)
        {
            _latencyHistory.Enqueue(latencyMs);
            while (_latencyHistory.Count > _historySize)
            {
                _latencyHistory.Dequeue();
            }

            AdjustInterval(latencyMs);
            EvaluateQuality();
        }
    }

    public void RecordSuccess()
    {
        Interlocked.Increment(ref _successCount);
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _failureCount);
    }

    private void AdjustInterval(int latencyMs)
    {
        var newQuality = EvaluateQualityFromLatency(latencyMs);

        int newInterval = newQuality switch
        {
            ConnectionQuality.Excellent => _maxInterval,
            ConnectionQuality.Good => (_minInterval + _maxInterval) / 2 + 15000,
            ConnectionQuality.Fair => _defaultInterval,
            ConnectionQuality.Poor => (_minInterval + _defaultInterval) / 2,
            ConnectionQuality.Bad => _minInterval,
            _ => _defaultInterval
        };

        if (Math.Abs(newInterval - CurrentInterval) > 5000)
        {
            var oldInterval = CurrentInterval;
            CurrentInterval = newInterval;

            if (_heartbeatTimer != null)
            {
                _heartbeatTimer.Change(CurrentInterval, CurrentInterval);
            }

            QualityChanged?.Invoke(this, new QualityChangedEventArgs
            {
                OldQuality = Quality,
                NewQuality = newQuality,
                OldIntervalMs = oldInterval,
                NewIntervalMs = newInterval
            });
        }
    }

    private void EvaluateQuality()
    {
        if (_latencyHistory.Count == 0) return;

        var avgLatency = _latencyHistory.Average();
        var newQuality = EvaluateQualityFromLatency((int)avgLatency);

        if (newQuality != Quality)
        {
            var oldQuality = Quality;
            Quality = newQuality;

            QualityChanged?.Invoke(this, new QualityChangedEventArgs
            {
                OldQuality = oldQuality,
                NewQuality = newQuality,
                OldIntervalMs = CurrentInterval,
                NewIntervalMs = CurrentInterval
            });
        }
    }

    private ConnectionQuality EvaluateQualityFromLatency(int latencyMs)
    {
        return latencyMs switch
        {
            < 100 => ConnectionQuality.Excellent,
            < 300 => ConnectionQuality.Good,
            < 500 => ConnectionQuality.Fair,
            < 1000 => ConnectionQuality.Poor,
            _ => ConnectionQuality.Bad
        };
    }

    public HeartbeatStats GetStats()
    {
        return new HeartbeatStats
        {
            CurrentIntervalMs = CurrentInterval,
            AverageLatencyMs = _latencyHistory.Count > 0 ? (int)_latencyHistory.Average() : 0,
            Quality = Quality,
            SuccessCount = _successCount,
            FailureCount = _failureCount,
            LastHeartbeatTime = _lastHeartbeatTime
        };
    }

    public void Reset()
    {
        lock (_lock)
        {
            _latencyHistory.Clear();
            CurrentInterval = _defaultInterval;
            Quality = ConnectionQuality.Good;
            _successCount = 0;
            _failureCount = 0;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}

public class HeartbeatEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; }
}

public class QualityChangedEventArgs : EventArgs
{
    public ConnectionQuality OldQuality { get; set; }
    public ConnectionQuality NewQuality { get; set; }
    public int OldIntervalMs { get; set; }
    public int NewIntervalMs { get; set; }
}
