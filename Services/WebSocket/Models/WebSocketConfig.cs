using System;

namespace Nexus.Services.WebSocket.Models;

public class WebSocketConfig
{
    public int ConnectionTimeoutMs { get; set; } = 15000;
    public int MaxReconnectAttempts { get; set; } = 10;

    public int HeartbeatMinIntervalMs { get; set; } = 10000;
    public int HeartbeatMaxIntervalMs { get; set; } = 60000;
    public int HeartbeatDefaultIntervalMs { get; set; } = 30000;

    public int MaxQueueSize { get; set; } = 1000;
    public int MessageTtlSeconds { get; set; } = 300;

    public int AckTimeoutMs { get; set; } = 10000;
    public int MaxAckRetries { get; set; } = 3;

    public int MaxConcurrentSends { get; set; } = 5;
    public int MinSendIntervalMs { get; set; } = 50;

    public double ReconnectBaseDelayMs { get; set; } = 1000;
    public double ReconnectMaxDelayMs { get; set; } = 60000;
    public double ReconnectJitter { get; set; } = 0.3;

    public bool EnableCompression { get; set; } = true;
    public int CompressionThreshold { get; set; } = 1024;

    public bool EnablePersistence { get; set; } = true;
    public string PersistencePath { get; set; } = "websocket_cache";

    public int QualitySampleSize { get; set; } = 10;
    public int SequenceWindowSize { get; set; } = 100;

    public TimeSpan MessageTtl => TimeSpan.FromSeconds(MessageTtlSeconds);
    public TimeSpan AckTimeout => TimeSpan.FromMilliseconds(AckTimeoutMs);
    public TimeSpan ConnectionTimeout => TimeSpan.FromMilliseconds(ConnectionTimeoutMs);
    public TimeSpan MinSendInterval => TimeSpan.FromMilliseconds(MinSendIntervalMs);
}
