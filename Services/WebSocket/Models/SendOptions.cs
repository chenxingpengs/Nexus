using System;

namespace Nexus.Services.WebSocket.Models;

public class SendOptions
{
    public int Priority { get; set; }
    public bool RequiresAck { get; set; }
    public TimeSpan? Timeout { get; set; }
    public bool PersistOffline { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public bool Compress { get; set; }
}

public class FlowStats
{
    public int CurrentQueueSize { get; set; }
    public int MaxQueueSize { get; set; }
    public int AvailableSlots { get; set; }
    public double SendRate { get; set; }
    public int DroppedMessages { get; set; }
}

public class QueueStatus
{
    public int Count { get; set; }
    public int MaxSize { get; set; }
    public int HighPriorityCount { get; set; }
    public int ExpiredCount { get; set; }
    public DateTime? OldestMessageTime { get; set; }
}

public class SequenceStats
{
    public long SentCount { get; set; }
    public long ReceivedCount { get; set; }
    public long DuplicateCount { get; set; }
    public long OutOfOrderCount { get; set; }
}

public class HeartbeatStats
{
    public int CurrentIntervalMs { get; set; }
    public int AverageLatencyMs { get; set; }
    public ConnectionQuality Quality { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastHeartbeatTime { get; set; }
}

public class RecoveryProgress
{
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }
}
