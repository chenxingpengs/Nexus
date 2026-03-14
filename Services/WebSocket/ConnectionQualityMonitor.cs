using Nexus.Services.WebSocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Nexus.Services.WebSocket;

public class ConnectionQualityMonitor : IDisposable
{
    private readonly CircularBuffer<int> _latencyBuffer;
    private readonly CircularBuffer<bool> _successBuffer;
    private readonly int _sampleSize;
    private Timer? _reportTimer;
    private readonly object _lock = new();
    private bool _isDisposed;
    private DateTime _lastReportTime;

    public event EventHandler<QualityReportEventArgs>? QualityReportGenerated;

    public ConnectionQualityMonitor(int sampleSize = 10)
    {
        _sampleSize = sampleSize;
        _latencyBuffer = new CircularBuffer<int>(sampleSize);
        _successBuffer = new CircularBuffer<bool>(sampleSize);
        _reportTimer = new Timer(GenerateReport, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void RecordLatency(int latencyMs)
    {
        if (_isDisposed) return;

        lock (_lock)
        {
            _latencyBuffer.Add(latencyMs);
        }
    }

    public void RecordResult(bool success)
    {
        if (_isDisposed) return;

        lock (_lock)
        {
            _successBuffer.Add(success);
        }
    }

    public QualityReport GetReport()
    {
        lock (_lock)
        {
            var latencies = _latencyBuffer.GetAll().ToList();
            var results = _successBuffer.GetAll().ToList();

            var avgLatency = latencies.Count > 0 ? latencies.Average() : 0;
            var jitter = CalculateJitter(latencies);
            var packetLoss = results.Count > 0 ? (1.0 - results.Count(x => x) / (double)results.Count) * 100 : 0;
            var quality = EvaluateQuality((int)avgLatency, packetLoss);

            return new QualityReport
            {
                AverageLatency = Math.Round(avgLatency, 2),
                Jitter = Math.Round(jitter, 2),
                PacketLoss = Math.Round(packetLoss, 2),
                Quality = quality,
                Description = GetQualityDescription(quality),
                GeneratedAt = DateTime.UtcNow,
                SampleCount = latencies.Count
            };
        }
    }

    public QualityRecommendation GetRecommendation()
    {
        var report = GetReport();

        return new QualityRecommendation
        {
            ShouldReconnect = report.Quality == ConnectionQuality.Bad,
            ShouldReduceHeartbeat = report.Quality == ConnectionQuality.Poor || report.Quality == ConnectionQuality.Bad,
            ShouldPauseSending = report.PacketLoss > 30,
            ShouldUseCompression = report.AverageLatency > 200,
            Reason = GetRecommendationReason(report)
        };
    }

    private double CalculateJitter(List<int> latencies)
    {
        if (latencies.Count < 2) return 0;

        var differences = new List<double>();
        for (int i = 1; i < latencies.Count; i++)
        {
            differences.Add(Math.Abs(latencies[i] - latencies[i - 1]));
        }

        return differences.Average();
    }

    private ConnectionQuality EvaluateQuality(int avgLatency, double packetLoss)
    {
        if (packetLoss > 20 || avgLatency > 1000)
            return ConnectionQuality.Bad;
        if (packetLoss > 10 || avgLatency > 500)
            return ConnectionQuality.Poor;
        if (packetLoss > 5 || avgLatency > 300)
            return ConnectionQuality.Fair;
        if (avgLatency > 100)
            return ConnectionQuality.Good;
        return ConnectionQuality.Excellent;
    }

    private string GetQualityDescription(ConnectionQuality quality)
    {
        return quality switch
        {
            ConnectionQuality.Excellent => "连接质量优秀，延迟极低",
            ConnectionQuality.Good => "连接质量良好，延迟较低",
            ConnectionQuality.Fair => "连接质量一般，存在一定延迟",
            ConnectionQuality.Poor => "连接质量较差，延迟较高",
            ConnectionQuality.Bad => "连接质量很差，建议检查网络",
            _ => "未知连接质量"
        };
    }

    private string GetRecommendationReason(QualityReport report)
    {
        var reasons = new List<string>();

        if (report.PacketLoss > 20)
            reasons.Add($"丢包率过高({report.PacketLoss:F1}%)");
        if (report.AverageLatency > 500)
            reasons.Add($"延迟过高({report.AverageLatency:F0}ms)");
        if (report.Jitter > 100)
            reasons.Add($"抖动过大({report.Jitter:F0}ms)");

        return reasons.Count > 0 ? string.Join("，", reasons) : "连接状态正常";
    }

    private void GenerateReport(object? state)
    {
        if (_isDisposed) return;

        _lastReportTime = DateTime.UtcNow;
        var report = GetReport();

        QualityReportGenerated?.Invoke(this, new QualityReportEventArgs
        {
            Report = report,
            Recommendation = GetRecommendation()
        });
    }

    public void Reset()
    {
        lock (_lock)
        {
            _latencyBuffer.Clear();
            _successBuffer.Clear();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _reportTimer?.Dispose();
    }
}

public class QualityReportEventArgs : EventArgs
{
    public QualityReport Report { get; set; } = new();
    public QualityRecommendation Recommendation { get; set; } = new();
}

public class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
        _head = 0;
        _count = 0;
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
    }

    public IEnumerable<T> GetAll()
    {
        lock (_lock)
        {
            var result = new T[_count];
            for (int i = 0; i < _count; i++)
            {
                var index = (_head - _count + i + _buffer.Length) % _buffer.Length;
                result[i] = _buffer[index];
            }
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _count = 0;
        }
    }

    public int Count => _count;
}
