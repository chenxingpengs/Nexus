using System;

namespace Nexus.Services.WebSocket.Models;

public class PendingAck
{
    public string MessageId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public AckStatus Status { get; set; } = AckStatus.Pending;
    public long SequenceNumber { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsTimeout => DateTime.UtcNow - SentAt > Timeout;
    public bool CanRetry => RetryCount < 3 && Status != AckStatus.Failed;
}

public enum AckStatus
{
    Pending,
    Acknowledged,
    Timeout,
    Failed
}
