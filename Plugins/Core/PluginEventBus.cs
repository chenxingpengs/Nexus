using System.Collections.Concurrent;
using Nexus.Plugins.Contracts;

namespace Nexus.Plugins.Core;

public interface IPluginEventBus
{
    Task PublishAsync<T>(string eventName, T data);
    IDisposable Subscribe<T>(string eventName, Func<T, Task> handler);
    void Unsubscribe(string eventName, IDisposable subscription);
}

public class PluginEventBus : IPluginEventBus, IDisposable
{
    private readonly ConcurrentDictionary<string, List<SubscriptionItem>> _subscribers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public async Task PublishAsync<T>(string eventName, T data)
    {
        if (_disposed) return;

        List<SubscriptionItem> handlers;
        await _lock.WaitAsync();
        try
        {
            handlers = _subscribers.GetValueOrDefault(eventName)?.ToList() ?? new List<SubscriptionItem>();
        }
        finally
        {
            _lock.Release();
        }

        var tasks = handlers.Select(async item =>
        {
            try
            {
                await item.Handler.Invoke(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginEventBus] 事件 {eventName} 处理器异常: {ex.Message}");
            }
        });

        await Task.WhenAll(tasks);
    }

    public IDisposable Subscribe<T>(string eventName, Func<T, Task> handler)
    {
        var item = new SubscriptionItem(
            eventName,
            async obj => await handler((T)obj),
            this);

        _lock.Wait();
        try
        {
            if (!_subscribers.ContainsKey(eventName))
                _subscribers[eventName] = new List<SubscriptionItem>();
            _subscribers[eventName].Add(item);
        }
        finally
        {
            _lock.Release();
        }

        return item;
    }

    public void Unsubscribe(string eventName, IDisposable subscription)
    {
        _lock.Wait();
        try
        {
            if (_subscribers.TryGetValue(eventName, out var list))
            {
                list.RemoveAll(s => s == subscription);
                if (list.Count == 0)
                    _subscribers.Remove(eventName, out _);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Wait();
        try
        {
            _subscribers.Clear();
        }
        finally
        {
            _lock.Release();
        }
        _lock.Dispose();
    }

    private class SubscriptionItem : IDisposable
    {
        public string EventName { get; }
        public Func<object, Task> Handler { get; }
        private readonly PluginEventBus _bus;

        public SubscriptionItem(string eventName, Func<object, Task> handler, PluginEventBus bus)
        {
            EventName = eventName;
            Handler = handler;
            _bus = bus;
        }

        public void Dispose() => _bus.Unsubscribe(EventName, this);
    }
}
