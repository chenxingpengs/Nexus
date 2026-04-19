using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Nexus.Models;

namespace Nexus.Views
{
    public partial class DisasterWarningWindow : Window
    {
        private string _notificationId = string.Empty;
        private bool _isFlashing;
        private DispatcherTimer? _flashTimer;
        private DispatcherTimer? _countdownTimer;
        private int _remainingSeconds;
        private string _currentAlertSubtype = string.Empty;
        private string _currentType = string.Empty;
        private string? _magnitude;
        
        public event EventHandler<string>? WarningClosed;
        public event EventHandler<string>? CountdownFinished;

        public DisasterWarningWindow()
        {
            InitializeComponent();
            
            var screen = Screens.Primary;
            if (screen != null)
            {
                Width = screen.WorkingArea.Width;
                Height = screen.WorkingArea.Height;
                Position = new PixelPoint(screen.WorkingArea.X, screen.WorkingArea.Y);
            }
        }

        public void ShowWarning(Notification notification)
        {
            _notificationId = notification.Id;
            _currentType = notification.Type?.ToLower() ?? string.Empty;
            _currentAlertSubtype = notification.AlertSubtype?.ToLower() ?? string.Empty;
            _magnitude = notification.Magnitude;
            
            TitleText.Text = notification.Title;
            ContentText.Text = notification.Content;

            var flashColor = notification.FlashColor;
            if (TryParseColor(flashColor, out var color))
            {
                FlashBorder.Background = new SolidColorBrush(color);
                ConfirmButton.Foreground = new SolidColorBrush(color);
            }

            SetWarningType(_currentType, _currentAlertSubtype, _magnitude, notification.EtaSeconds);
            
            if (_currentType == "earthquake_warning" && 
                _currentAlertSubtype == "early_warning" && 
                notification.EtaSeconds.HasValue && 
                notification.EtaSeconds.Value > 0)
            {
                StartCountdown(notification.EtaSeconds.Value);
            }
            
            StartFlashing();
        }

        private void StartCountdown(int seconds)
        {
            _remainingSeconds = seconds;
            UpdateCountdownDisplay();
            
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            
            _countdownTimer.Tick += (s, e) =>
            {
                _remainingSeconds--;
                
                if (_remainingSeconds <= 0)
                {
                    StopCountdown();
                    OnCountdownFinished();
                }
                else
                {
                    UpdateCountdownDisplay();
                }
            };
            
            _countdownTimer.Start();
        }

        private void UpdateCountdownDisplay()
        {
            EtaText.Text = $"{_remainingSeconds}秒";
            
            if (_remainingSeconds <= 10)
            {
                EtaText.Foreground = new SolidColorBrush(Colors.Red);
            }
            else if (_remainingSeconds <= 30)
            {
                EtaText.Foreground = new SolidColorBrush(Colors.Orange);
            }
        }

        private void StopCountdown()
        {
            _countdownTimer?.Stop();
            _countdownTimer = null;
        }

        private void OnCountdownFinished()
        {
            _currentAlertSubtype = "arrival";
            
            Dispatcher.UIThread.Post(() =>
            {
                WarningTypeText.Text = "🌍 地震到达报 🌍";
                InstructionText.Text = "地震波已到达！请保持冷静，寻找掩护！";
                EtaPanel.IsVisible = false;
            });
            
            CountdownFinished?.Invoke(this, _notificationId);
        }

        private void SetWarningType(string type, string? alertSubtype, string? magnitude, int? etaSeconds)
        {
            WarningTypeText.Text = type?.ToLower() switch
            {
                "fire_alarm" => "🔥 火灾警报 🔥",
                "air_raid_alert" => GetAirRaidAlertTitle(alertSubtype),
                "earthquake_warning" => GetEarthquakeWarningTitle(alertSubtype),
                _ => "⚠️ 紧急警报 ⚠️"
            };

            InstructionText.Text = type?.ToLower() switch
            {
                "fire_alarm" => "请立即撤离！不要乘坐电梯！",
                "air_raid_alert" => GetAirRaidAlertInstruction(alertSubtype),
                "earthquake_warning" => "请立即寻找掩体！远离高大建筑物！",
                _ => "请立即采取紧急避险措施！"
            };

            ExtraInfoPanel.IsVisible = false;
            EtaPanel.IsVisible = false;

            if (type?.ToLower() == "earthquake_warning")
            {
                if (!string.IsNullOrEmpty(magnitude))
                {
                    ExtraInfoPanel.IsVisible = true;
                    MagnitudeText.Text = magnitude;
                }

                if (alertSubtype == "early_warning" && etaSeconds.HasValue && etaSeconds.Value > 0)
                {
                    EtaPanel.IsVisible = true;
                    _remainingSeconds = etaSeconds.Value;
                    UpdateCountdownDisplay();
                }
            }
        }

        private string GetAirRaidAlertTitle(string? alertSubtype)
        {
            return alertSubtype?.ToLower() switch
            {
                "pre_warning" => "⚠️ 防空预先警报 ⚠️",
                "air_raid" => "⚠️ 防空空袭警报 ⚠️",
                "all_clear" => "✅ 防空解除警报 ✅",
                _ => "⚠️ 防空警报 ⚠️"
            };
        }

        private string GetAirRaidAlertInstruction(string? alertSubtype)
        {
            return alertSubtype?.ToLower() switch
            {
                "pre_warning" => "空袭即将来临，请立即做好防护准备！",
                "air_raid" => "空袭正在进行，请立即进入防空掩体！",
                "all_clear" => "空袭威胁已解除，可以恢复正常活动。",
                _ => "请立即进入防空掩体！远离窗户！"
            };
        }

        private string GetEarthquakeWarningTitle(string? alertSubtype)
        {
            return alertSubtype?.ToLower() switch
            {
                "early_warning" => "🌍 地震预警 🌍",
                "arrival" => "🌍 地震到达报 🌍",
                _ => "🌍 地震预警 🌍"
            };
        }

        private void StartFlashing()
        {
            _isFlashing = true;
            _flashTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.5)
            };
            
            _flashTimer.Tick += (s, e) =>
            {
                if (_isFlashing)
                {
                    var currentOpacity = FlashBorder.Opacity;
                    FlashBorder.Opacity = currentOpacity < 0.9 ? 0.95 : 0.7;
                }
            };
            
            _flashTimer.Start();
        }

        private void StopFlashing()
        {
            _isFlashing = false;
            _flashTimer?.Stop();
            _flashTimer = null;
        }

        private bool TryParseColor(string colorString, out Color color)
        {
            color = Colors.Red;

            try
            {
                if (colorString.StartsWith("#"))
                {
                    color = Color.Parse(colorString);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private void ConfirmButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            StopFlashing();
            StopCountdown();
            WarningClosed?.Invoke(this, _notificationId);
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopFlashing();
            StopCountdown();
            base.OnClosed(e);
        }
    }
}
