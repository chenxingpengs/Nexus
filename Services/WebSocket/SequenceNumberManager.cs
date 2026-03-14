using Nexus.Services.WebSocket.Models;
using System;
using System.Linq;
using System.Threading;

namespace Nexus.Services.WebSocket;

public class SequenceNumberManager
{
    private long _sendSequence;
    private long _expectedReceiveSequence;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, DateTime> _receivedSequences;
    private readonly int _windowSize;
    private readonly TimeSpan _sequenceTtl;
    private readonly object _lock = new();
    private long _duplicateCount;
    private long _outOfOrderCount;

    public SequenceNumberManager(int windowSize = 100, TimeSpan? sequenceTtl = null)
    {
        _windowSize = windowSize;
        _sequenceTtl = sequenceTtl ?? TimeSpan.FromMinutes(5);
        _receivedSequences = new System.Collections.Concurrent.ConcurrentDictionary<long, DateTime>();
        _sendSequence = 0;
        _expectedReceiveSequence = 0;
        _duplicateCount = 0;
        _outOfOrderCount = 0;
    }

    public long GetNextSendSequence()
    {
        lock (_lock)
        {
            return Interlocked.Increment(ref _sendSequence);
        }
    }

    public bool ValidateReceiveSequence(long sequence)
    {
        lock (_lock)
        {
            if (IsDuplicate(sequence))
            {
                Interlocked.Increment(ref _duplicateCount);
                return false;
            }

            if (sequence < _expectedReceiveSequence)
            {
                Interlocked.Increment(ref _outOfOrderCount);
            }
            else if (sequence == _expectedReceiveSequence)
            {
                _expectedReceiveSequence++;
            }
            else
            {
                Interlocked.Increment(ref _outOfOrderCount);
                _expectedReceiveSequence = sequence + 1;
            }

            _receivedSequences[sequence] = DateTime.UtcNow;
            CleanupExpired();

            return true;
        }
    }

    public bool IsDuplicate(long sequence)
    {
        return _receivedSequences.ContainsKey(sequence);
    }

    public void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _receivedSequences
            .Where(kvp => now - kvp.Value > _sequenceTtl)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _receivedSequences.TryRemove(key, out _);
        }
    }

    public SequenceStats GetStats()
    {
        return new SequenceStats
        {
            SentCount = _sendSequence,
            ReceivedCount = _expectedReceiveSequence,
            DuplicateCount = _duplicateCount,
            OutOfOrderCount = _outOfOrderCount
        };
    }

    public void Reset()
    {
        lock (_lock)
        {
            _sendSequence = 0;
            _expectedReceiveSequence = 0;
            _receivedSequences.Clear();
            _duplicateCount = 0;
            _outOfOrderCount = 0;
        }
    }

    public int ReceivedWindowSize => _receivedSequences.Count;
}
