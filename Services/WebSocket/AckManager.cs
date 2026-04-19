using Nexus.Services.WebSocket.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Nexus.Services.WebSocket;

public class AckManager : IDisposable
{
    private readonly ConcurrentDictionary<string, PendingAck> _pendingAcks;
    private readonly int _maxRetries;
    private readonly TimeSpan _ackTimeout;
    private readonly Timer? _timeoutChecker;
    private readonly object _lock = new();
    private bool _isDisposed;

    public event EventHandler<AckTimeoutEventArgs>? AckTimeout;
    public event EventHandler<AckConfirmedEventArgs>? AckConfirmed;
    public event EventHandler<AckRetryEventArgs>? AckRetry;

    public AckManager(int maxRetries = 3, TimeSpan? ackTimeout = null)
    {
        _maxRetries = maxRetries;
        _ackTimeout = ackTimeout ?? TimeSpan.FromSeconds(10);
        _pendingAcks = new ConcurrentDictionary<string, PendingAck>();
        _timeoutChecker = new Timer(CheckTimeouts, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public string RegisterAck(string eventType, object? data, long sequenceNumber = 0)
    {
        if (_isDisposed) return string.Empty;

        var messageId = Guid.NewGuid().ToString();
        var pendingAck = new PendingAck
        {
            MessageId = messageId,
            EventType = eventType,
            Data = data,
            SentAt = DateTime.UtcNow,
            RetryCount = 0,
            Status = AckStatus.Pending,
            SequenceNumber = sequenceNumber,
            Timeout = _ackTimeout
        };

        _pendingAcks[messageId] = pendingAck;
        return messageId;
    }

    public bool ConfirmAck(string messageId)
    {
        if (_isDisposed) return false;

        if (_pendingAcks.TryRemove(messageId, out var ack))
        {
            ack.Status = AckStatus.Acknowledged;
            AckConfirmed?.Invoke(this, new AckConfirmedEventArgs
            {
                MessageId = messageId,
                EventType = ack.EventType,
                Data = ack.Data
            });
            return true;
        }
        return false;
    }

    public void CheckTimeouts(object? state)
    {
        if (_isDisposed) return;

        var timeoutMessages = _pendingAcks.Values
            .Where(a => a.IsTimeout && a.Status == AckStatus.Pending)
            .ToList();

        foreach (var ack in timeoutMessages)
        {
            if (ack.CanRetry)
            {
                ack.RetryCount++;
                ack.SentAt = DateTime.UtcNow;
                ack.Status = AckStatus.Pending;

                AckRetry?.Invoke(this, new AckRetryEventArgs
                {
                    MessageId = ack.MessageId,
                    EventType = ack.EventType,
                    Data = ack.Data,
                    RetryCount = ack.RetryCount
                });
            }
            else
            {
                ack.Status = AckStatus.Failed;
                _pendingAcks.TryRemove(ack.MessageId, out _);

                AckTimeout?.Invoke(this, new AckTimeoutEventArgs
                {
                    MessageId = ack.MessageId,
                    EventType = ack.EventType,
                    Data = ack.Data,
                    RetryCount = ack.RetryCount
                });
            }
        }
    }

    public IEnumerable<PendingAck> GetTimeoutMessages()
    {
        return _pendingAcks.Values
            .Where(a => a.IsTimeout && a.Status == AckStatus.Pending)
            .ToList();
    }

    public IEnumerable<PendingAck> GetPendingAcks()
    {
        return _pendingAcks.Values.ToList();
    }

    public PendingAck? GetAck(string messageId)
    {
        _pendingAcks.TryGetValue(messageId, out var ack);
        return ack;
    }

    public int PendingCount => _pendingAcks.Count;

    public void Clear()
    {
        _pendingAcks.Clear();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _timeoutChecker?.Dispose();
        Clear();
    }
}

public class AckTimeoutEventArgs : EventArgs
{
    public string MessageId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
    public int RetryCount { get; set; }
}

public class AckConfirmedEventArgs : EventArgs
{
    public string MessageId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class AckRetryEventArgs : EventArgs
{
    public string MessageId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
    public int RetryCount { get; set; }
}
