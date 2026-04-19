using System;

namespace Nexus.Services.WebSocket.Models;

public class QualityReport
{
    public double AverageLatency { get; set; }
    public double Jitter { get; set; }
    public double PacketLoss { get; set; }
    public ConnectionQuality Quality { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int SampleCount { get; set; }

    public int AverageLatencyMs => (int)Math.Round(AverageLatency);
}

public enum ConnectionQuality
{
    Excellent,
    Good,
    Fair,
    Poor,
    Bad
}

public class QualityRecommendation
{
    public bool ShouldReconnect { get; set; }
    public bool ShouldReduceHeartbeat { get; set; }
    public bool ShouldPauseSending { get; set; }
    public bool ShouldUseCompression { get; set; }
    public string Reason { get; set; } = string.Empty;
}
