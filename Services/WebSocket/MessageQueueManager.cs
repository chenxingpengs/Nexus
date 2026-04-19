using Nexus.Services.WebSocket.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services.WebSocket;

public class MessageQueueManager : IDisposable
{
    private readonly ConcurrentPriorityQueue<QueuedMessage> _messageQueue;
    private readonly int _maxQueueSize;
    private readonly TimeSpan _messageTtl;
    private readonly Timer? _cleanupTimer;
    private readonly object _lock = new();
    private bool _isDisposed;
    private int _expiredCount;

    public event EventHandler<QueuedMessage>? MessageEnqueued;
    public event EventHandler<QueuedMessage>? MessageDequeued;
    public event EventHandler<int>? MessagesExpired;

    public MessageQueueManager(int maxQueueSize = 1000, TimeSpan? messageTtl = null)
    {
        _maxQueueSize = maxQueueSize;
        _messageTtl = messageTtl ?? TimeSpan.FromMinutes(5);
        _messageQueue = new ConcurrentPriorityQueue<QueuedMessage>();
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public bool Enqueue(string eventType, object? data, int priority = 0, bool requiresAck = false, long sequenceNumber = 0)
    {
        if (_isDisposed) return false;

        lock (_lock)
        {
            if (_messageQueue.Count >= _maxQueueSize)
            {
                var items = _messageQueue.ToList();
                var lowestPriority = items.MinBy(x => x.Priority);
                if (lowestPriority != null && lowestPriority.Priority < priority)
                {
                    _messageQueue.TryDequeue(out _);
                }
                else
                {
                    return false;
                }
            }

            var message = new QueuedMessage
            {
                Id = Guid.NewGuid().ToString(),
                EventType = eventType,
                Data = data,
                Priority = priority,
                CreatedAt = DateTime.UtcNow,
                RequiresAck = requiresAck,
                SequenceNumber = sequenceNumber,
                Ttl = _messageTtl
            };

            _messageQueue.Enqueue(message);
            MessageEnqueued?.Invoke(this, message);
            return true;
        }
    }

    public QueuedMessage? Dequeue()
    {
        if (_isDisposed) return null;

        if (_messageQueue.TryDequeue(out var message))
        {
            if (message != null && message.IsExpired)
            {
                _expiredCount++;
                return Dequeue();
            }
            if (message != null)
            {
                MessageDequeued?.Invoke(this, message);
            }
            return message;
        }
        return null;
    }

    public QueuedMessage? Peek()
    {
        if (_isDisposed) return null;

        if (_messageQueue.TryPeek(out var message))
        {
            return message;
        }
        return null;
    }

    public void CleanupExpired(object? state)
    {
        if (_isDisposed) return;

        var expiredMessages = new List<QueuedMessage>();
        var tempQueue = new ConcurrentPriorityQueue<QueuedMessage>();

        while (_messageQueue.TryDequeue(out var message))
        {
            if (message != null && message.IsExpired)
            {
                expiredMessages.Add(message);
            }
            else if (message != null)
            {
                tempQueue.Enqueue(message);
            }
        }

        while (tempQueue.TryDequeue(out var message))
        {
            if (message != null)
            {
                _messageQueue.Enqueue(message);
            }
        }

        if (expiredMessages.Count > 0)
        {
            Interlocked.Add(ref _expiredCount, expiredMessages.Count);
            MessagesExpired?.Invoke(this, expiredMessages.Count);
        }
    }

    public QueueStatus GetStatus()
    {
        var messages = _messageQueue.ToList();
        return new QueueStatus
        {
            Count = messages.Count,
            MaxSize = _maxQueueSize,
            HighPriorityCount = messages.Count(x => x.Priority > 0),
            ExpiredCount = _expiredCount,
            OldestMessageTime = messages.Count > 0 ? messages.Min(x => x.CreatedAt) : null
        };
    }

    public int Count => _messageQueue.Count;

    public bool IsEmpty => _messageQueue.IsEmpty;

    public void Clear()
    {
        while (_messageQueue.TryDequeue(out _)) { }
    }

    public async Task PersistToFileAsync(string filePath)
    {
        if (_isDisposed) return;

        var messages = _messageQueue.ToList();
        var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task LoadFromFileAsync(string filePath)
    {
        if (_isDisposed || !File.Exists(filePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var messages = JsonSerializer.Deserialize<List<QueuedMessage>>(json);

            if (messages != null)
            {
                foreach (var message in messages.Where(m => !m.IsExpired))
                {
                    _messageQueue.Enqueue(message);
                }
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cleanupTimer?.Dispose();
        Clear();
    }
}

public class ConcurrentPriorityQueue<T> where T : IComparable<T>
{
    private readonly ConcurrentQueue<T> _queue = new();
    private readonly object _lock = new();

    public void Enqueue(T item)
    {
        lock (_lock)
        {
            var items = _queue.ToList();
            items.Add(item);
            items.Sort();
            _queue.Clear();
            foreach (var i in items)
            {
                _queue.Enqueue(i);
            }
        }
    }

    public bool TryDequeue(out T? item)
    {
        return _queue.TryDequeue(out item);
    }

    public bool TryPeek(out T? item)
    {
        return _queue.TryPeek(out item);
    }

    public int Count => _queue.Count;

    public bool IsEmpty => _queue.IsEmpty;

    public List<T> ToList()
    {
        return _queue.ToList();
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
