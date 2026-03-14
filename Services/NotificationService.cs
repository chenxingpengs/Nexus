using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;
using Nexus.Models;
using Nexus.Views;

namespace Nexus.Services
{
    public class NotificationService : IDisposable
    {
        private readonly ConcurrentQueue<Models.Notification> _notificationQueue = new();
        private Models.Notification? _currentNotification;
        private bool _isDisplaying;
        private readonly SocketIOService _socketIOService;
        private NotificationWindow? _currentWindow;
        private readonly object _lock = new();

        public event EventHandler<Models.Notification>? NotificationReceived;
        public event Action? NotificationClosed;
        public event EventHandler<Models.Notification>? NotificationExpired;

        public Models.Notification? CurrentNotification => _currentNotification;
        public int QueueCount => _notificationQueue.Count;
        public bool IsDisplaying => _isDisplaying;

        public NotificationService(SocketIOService socketIOService)
        {
            _socketIOService = socketIOService;
        }

        public void EnqueueNotification(Models.Notification notification)
        {
            if (notification.IsExpired)
            {
                Debug.WriteLine($"[NotificationService] 通知已过期，跳过: {notification.Id}");
                NotificationExpired?.Invoke(this, notification);
                return;
            }

            lock (_lock)
            {
                if (notification.NotificationPriority == Models.NotificationPriority.Urgent)
                {
                    while (_notificationQueue.TryDequeue(out _)) { }
                    _notificationQueue.Enqueue(notification);
                    Debug.WriteLine($"[NotificationService] 紧急通知入队，清空队列: {notification.Title}");
                }
                else
                {
                    _notificationQueue.Enqueue(notification);
                    Debug.WriteLine($"[NotificationService] 通知入队: {notification.Title}, 队列长度: {_notificationQueue.Count}");
                }

                if (!_isDisplaying)
                {
                    ShowNext();
                }
            }
        }

        private void ShowNext()
        {
            lock (_lock)
            {
                if (_notificationQueue.TryDequeue(out var notification))
                {
                    _currentNotification = notification;
                    _isDisplaying = true;
                    
                    Debug.WriteLine($"[NotificationService] 显示通知: {notification.Title}");
                    NotificationReceived?.Invoke(this, notification);
                    
                    ShowNotificationWindow(notification);
                    
                    _ = SendAckAsync(notification.Id);
                }
                else
                {
                    _currentNotification = null;
                    _isDisplaying = false;
                    Debug.WriteLine($"[NotificationService] 队列为空，停止显示");
                }
            }
        }

        private void ShowNotificationWindow(Models.Notification notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _currentWindow?.Close();
                    
                    _currentWindow = new NotificationWindow();
                    _currentWindow.NotificationClosed += OnWindowClosed;
                    _currentWindow.ShowNotification(notification);
                    
                    Debug.WriteLine($"[NotificationService] 通知窗口已显示: {notification.Title}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NotificationService] 显示通知窗口失败: {ex.Message}");
                }
            });
        }

        private void OnWindowClosed(object? sender, string notificationId)
        {
            Debug.WriteLine($"[NotificationService] 通知窗口已关闭: {notificationId}");
            _currentWindow = null;
            CloseCurrent();
        }

        public void CloseCurrent()
        {
            lock (_lock)
            {
                if (_currentNotification != null)
                {
                    Debug.WriteLine($"[NotificationService] 关闭通知: {_currentNotification.Title}");
                    NotificationClosed?.Invoke();
                    _currentNotification = null;
                }
                
                _isDisplaying = false;
                
                Dispatcher.UIThread.Post(() =>
                {
                    _currentWindow?.Close();
                    _currentWindow = null;
                    ShowNext();
                });
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                while (_notificationQueue.TryDequeue(out _)) { }
                _currentNotification = null;
                _isDisplaying = false;
                Debug.WriteLine($"[NotificationService] 清空所有通知");
            }
            
            Dispatcher.UIThread.Post(() =>
            {
                _currentWindow?.Close();
                _currentWindow = null;
            });
        }

        private async Task SendAckAsync(string notificationId)
        {
            try
            {
                if (_socketIOService.IsConnected)
                {
                    await _socketIOService.SendAsync("notification:ack", new { notification_id = notificationId });
                    Debug.WriteLine($"[NotificationService] 发送ACK: {notificationId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] 发送ACK失败: {ex.Message}");
            }
        }

        public async Task SendReadAsync(string notificationId)
        {
            try
            {
                if (_socketIOService.IsConnected)
                {
                    await _socketIOService.SendAsync("notification:read", new { notification_id = notificationId });
                    Debug.WriteLine($"[NotificationService] 发送已读: {notificationId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] 发送已读失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            ClearAll();
        }
    }
}
