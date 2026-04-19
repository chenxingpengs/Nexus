using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
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
        private Window? _currentWindow;
        private readonly SoundService _soundService;
        private readonly VolumeControlService _volumeControlService;
        private readonly object _lock = new();

        public event EventHandler<Models.Notification>? NotificationReceived;
        public event Action? NotificationClosed;
        public event EventHandler<Models.Notification>? NotificationExpired;
        public event EventHandler<PageCall>? PageCallReceived;

        public Models.Notification? CurrentNotification => _currentNotification;
        public int QueueCount => _notificationQueue.Count;
        public bool IsDisplaying => _isDisplaying;

        public NotificationService(SocketIOService socketIOService)
        {
            _socketIOService = socketIOService;
            _soundService = new SoundService();
            _volumeControlService = new VolumeControlService();
        }

        public void EnqueueNotification(Models.Notification notification)
        {
            if (notification.IsExpired)
            {
                Debug.WriteLine($"[NotificationService] 通知已过期，跳过: {notification.Id}");
                NotificationExpired?.Invoke(this, notification);
                return;
            }

            bool shouldShowNext;
            bool isEmergencyBroadcast = notification.NotificationType == Models.NotificationType.FireAlarm ||
                                        notification.NotificationType == Models.NotificationType.AirRaidAlert ||
                                        notification.NotificationType == Models.NotificationType.EarthquakeWarning ||
                                        notification.NotificationType == Models.NotificationType.Emergency;

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

                if (isEmergencyBroadcast && _isDisplaying)
                {
                    Debug.WriteLine($"[NotificationService] 应急广播，立即替换当前通知: {notification.Title}");
                    Dispatcher.UIThread.Post(() =>
                    {
                        _currentWindow?.Close();
                    });
                    _isDisplaying = false;
                    _currentNotification = null;
                }

                shouldShowNext = !_isDisplaying;
            }

            if (shouldShowNext)
            {
                ShowNext();
            }
        }

        private void ShowNext()
        {
            Models.Notification? notificationToDisplay = null;

            lock (_lock)
            {
                if (_notificationQueue.TryDequeue(out var notification))
                {
                    _currentNotification = notification;
                    _isDisplaying = true;
                    notificationToDisplay = notification;
                    
                    Debug.WriteLine($"[NotificationService] 显示通知: {notification.Title}");
                }
                else
                {
                    _currentNotification = null;
                    _isDisplaying = false;
                    Debug.WriteLine($"[NotificationService] 队列为空，停止显示");
                    return;
                }
            }

            NotificationReceived?.Invoke(this, notificationToDisplay);
            ShowNotificationWindow(notificationToDisplay);
            _ = SendAckAsync(notificationToDisplay.Id);
        }

        private void ShowNotificationWindow(Models.Notification notification)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _currentWindow?.Close();
                    
                    var notificationType = notification.NotificationType;
                    
                    switch (notificationType)
                    {
                        case Models.NotificationType.Alert:
                            ShowAlertWindow(notification);
                            break;
                            
                        case Models.NotificationType.Emergency:
                            ShowEmergencyWindow(notification);
                            break;
                            
                        case Models.NotificationType.FireAlarm:
                            _volumeControlService.MaximizeVolume();
                            ShowDisasterWarningWindow(notification);
                            PlayFireAlarmSound();
                            break;
                            
                        case Models.NotificationType.AirRaidAlert:
                            _volumeControlService.MaximizeVolume();
                            ShowDisasterWarningWindow(notification);
                            PlayAirRaidAlertSound(notification.AlertSubtype);
                            break;
                            
                        case Models.NotificationType.EarthquakeWarning:
                            _volumeControlService.MaximizeVolume();
                            ShowDisasterWarningWindow(notification);
                            if (notification.AlertSubtype?.ToLower() == "arrival")
                            {
                                PlayAirRaidAlertSound("air_raid");
                            }
                            else
                            {
                                PlayEarthquakeWarningSound();
                            }
                            break;
                            
                        case Models.NotificationType.System:
                            ShowSystemNotification(notification);
                            break;
                            
                        case Models.NotificationType.Banner:
                        default:
                            ShowBannerWindow(notification);
                            break;
                    }
                    
                    Debug.WriteLine($"[NotificationService] 通知窗口已显示: {notification.Title}, 类型: {notificationType}");
                    
                    var speakConfig = notification.Display?.Speak;
                    if (speakConfig == null || speakConfig.SpeakEnabled)
                    {
                        var speakText = string.IsNullOrEmpty(notification.Title) 
                            ? notification.Content 
                            : $"{notification.Title}。{notification.Content}";
                        var voice = speakConfig?.SpeakVoice ?? "xiaoxiao";
                        var rate = speakConfig?.SpeakRate ?? 1;
                        
                        bool needRestoreVolume = false;
                        if (notificationType != Models.NotificationType.FireAlarm &&
                            notificationType != Models.NotificationType.AirRaidAlert &&
                            notificationType != Models.NotificationType.EarthquakeWarning)
                        {
                            _volumeControlService.MaximizeVolume();
                            needRestoreVolume = true;
                        }
                        
                        TTS.Speak(speakText, voice: voice, rate: rate);
                        
                        if (needRestoreVolume)
                        {
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(500);
                                _volumeControlService.RestoreVolume();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NotificationService] 显示通知窗口失败: {ex.Message}");
                }
            });
        }
        
        private void PlayFireAlarmSound()
        {
            _soundService.PlaySound("fire_alarm.mp3", loop: true, volume: 1.0f);
        }
        
        private void PlayAirRaidAlertSound(string? alertSubtype = null)
        {
            var soundFile = alertSubtype?.ToLower() switch
            {
                "pre_warning" => "air_raid_pre_warning.mp3",
                "air_raid" => "air_raid_attack.mp3",
                "all_clear" => "air_raid_all_clear.mp3",
                _ => "air_raid_alert.mp3"
            };
            _soundService.PlaySound(soundFile, loop: true, volume: 1.0f);
        }
        
        private void PlayEarthquakeWarningSound()
        {
            _soundService.PlaySound("earthquake_warning.mp3", loop: true, volume: 1.0f);
        }
        
        private void ShowBannerWindow(Models.Notification notification)
        {
            var window = new NotificationWindow();
            window.NotificationClosed += OnWindowClosed;
            window.ShowNotification(notification);
            _currentWindow = window;
        }
        
        private void ShowAlertWindow(Models.Notification notification)
        {
            var window = new Window
            {
                Title = notification.Title,
                Width = 450,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false
            };
            
            var alertControl = new AlertWindow();
            alertControl.AlertClosed += (s, id) =>
            {
                window.Close();
                OnWindowClosed(s, id);
            };
            alertControl.ShowAlert(notification);
            
            window.Content = alertControl;
            window.Show();
            _currentWindow = window;
        }
        
 private void ShowEmergencyWindow(Models.Notification notification)
        {
            var window = new EmergencyWindow();
            window.EmergencyClosed += (s, id) =>
            {
                OnWindowClosed(s, id);
            };
            window.ShowEmergency(notification);
            window.Show();
            _currentWindow = window;
        }
        
        private void ShowDisasterWarningWindow(Models.Notification notification)
        {
            var window = new DisasterWarningWindow();
            window.WarningClosed += (s, id) =>
            {
                OnWindowClosed(s, id);
            };
            window.CountdownFinished += (s, id) =>
            {
                OnEarthquakeCountdownFinished();
            };
            window.ShowWarning(notification);
            window.Show();
            window.Activate();
            window.Topmost = true;
            window.Focus();
            _currentWindow = window;
        }
        
        private void OnEarthquakeCountdownFinished()
        {
            Debug.WriteLine($"[NotificationService] 地震预警倒计时结束，切换为空袭警报声音");
            Dispatcher.UIThread.Post(() =>
            {
                _soundService.StopPlayback();
                _soundService.PlaySound("air_raid_attack.mp3", loop: true, volume: 1.0f);
            });
        }
        
        private void ShowSystemNotification(Models.Notification notification)
        {
            var toastService = new ToastService();
            toastService.ShowToast(notification.Title, notification.Content);
            
            var duration = notification.Display?.Duration ?? 5;
            if (duration > 0)
            {
                Task.Delay(TimeSpan.FromSeconds(duration)).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => CloseCurrent());
                });
            }
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
                    _soundService.StopPlayback();
                    _volumeControlService.RestoreVolume();
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
                    await _socketIOService.SendAsync("notification_ack", new { notification_id = notificationId });
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
                    await _socketIOService.SendAsync("notification_read", new { notification_id = notificationId });
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
            _soundService.Dispose();
        }

        public void ShowPageCall(PageCall pageCall)
        {
            Debug.WriteLine($"[NotificationService] 显示寻人窗口: {pageCall.StudentName}");
            
            PageCallReceived?.Invoke(this, pageCall);
            
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var window = new PageCallWindow(_socketIOService, _volumeControlService);
                    window.PageCallClosed += (s, id) =>
                    {
                        Debug.WriteLine($"[NotificationService] 寻人窗口已关闭: {id}");
                    };
                    window.ShowPageCall(pageCall);
                    window.Show();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NotificationService] 显示寻人窗口失败: {ex.Message}");
                }
            });
        }

        public void HandlePageCallPush(JsonElement data)
        {
            try
            {
                var pageCall = JsonSerializer.Deserialize<PageCall>(data.ToString(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (pageCall != null)
                {
                    Debug.WriteLine($"[NotificationService] 收到寻人推送: {pageCall.StudentName}");
                    ShowPageCall(pageCall);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] 解析寻人数据失败: {ex.Message}");
            }
        }
    }
}
