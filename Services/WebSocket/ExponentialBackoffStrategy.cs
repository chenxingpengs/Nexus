using System;

namespace Nexus.Services.WebSocket;

public class ExponentialBackoffStrategy
{
    private readonly double _baseDelay;
    private readonly double _maxDelay;
    private readonly double _jitter;
    private readonly double _multiplier;
    private readonly Random _random;
    private readonly object _lock = new();

    public int CurrentAttempt { get; private set; }
    public int MaxAttempts { get; set; }
    public bool IsExhausted => CurrentAttempt >= MaxAttempts;
    public bool CanRetry => !IsExhausted;

    public ExponentialBackoffStrategy(
        double baseDelayMs = 1000,
        double maxDelayMs = 60000,
        double jitter = 0.3,
        double multiplier = 2.0,
        int maxAttempts = 10)
    {
        _baseDelay = baseDelayMs;
        _maxDelay = maxDelayMs;
        _jitter = jitter;
        _multiplier = multiplier;
        _random = new Random();
        MaxAttempts = maxAttempts;
        CurrentAttempt = 0;
    }

    public TimeSpan GetNextDelay()
    {
        lock (_lock)
        {
            if (IsExhausted)
            {
                return TimeSpan.Zero;
            }

            var delay = CalculateDelayWithJitter(CurrentAttempt);
            CurrentAttempt++;
            return delay;
        }
    }

    public TimeSpan PeekNextDelay()
    {
        lock (_lock)
        {
            if (IsExhausted)
            {
                return TimeSpan.Zero;
            }

            return CalculateDelayWithJitter(CurrentAttempt);
        }
    }

    private TimeSpan CalculateDelayWithJitter(int attempt)
    {
        var exponentialDelay = _baseDelay * Math.Pow(_multiplier, attempt);
        var cappedDelay = Math.Min(exponentialDelay, _maxDelay);

        var jitterRange = cappedDelay * _jitter;
        var jitterValue = (_random.NextDouble() * 2 - 1) * jitterRange;
        var finalDelay = cappedDelay + jitterValue;

        finalDelay = Math.Max(0, Math.Min(finalDelay, _maxDelay));

        return TimeSpan.FromMilliseconds(finalDelay);
    }

    public void Reset()
    {
        lock (_lock)
        {
            CurrentAttempt = 0;
        }
    }

    public void SetMaxAttempts(int maxAttempts)
    {
        MaxAttempts = Math.Max(1, maxAttempts);
    }

    public int GetRemainingAttempts()
    {
        return Math.Max(0, MaxAttempts - CurrentAttempt);
    }

    public BackoffStats GetStats()
    {
        return new BackoffStats
        {
            CurrentAttempt = CurrentAttempt,
            MaxAttempts = MaxAttempts,
            IsExhausted = IsExhausted,
            RemainingAttempts = GetRemainingAttempts(),
            NextDelayMs = IsExhausted ? 0 : PeekNextDelay().TotalMilliseconds
        };
    }
}

public class BackoffStats
{
    public int CurrentAttempt { get; set; }
    public int MaxAttempts { get; set; }
    public bool IsExhausted { get; set; }
    public int RemainingAttempts { get; set; }
    public double NextDelayMs { get; set; }
}
