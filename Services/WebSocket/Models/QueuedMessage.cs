using System;

namespace Nexus.Services.WebSocket.Models;

public class QueuedMessage : IComparable<QueuedMessage>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public bool RequiresAck { get; set; }
    public long SequenceNumber { get; set; }
    public TimeSpan Ttl { get; set; }

    public bool IsExpired => Ttl != TimeSpan.Zero && DateTime.UtcNow - CreatedAt > Ttl;

    public int CompareTo(QueuedMessage? other)
    {
        if (other == null) return 1;
        int priorityCompare = other.Priority.CompareTo(Priority);
        if (priorityCompare != 0) return priorityCompare;
        return CreatedAt.CompareTo(other.CreatedAt);
    }
}
