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

        public event EventHandler<string>? WarningClosed;

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
            TitleText.Text = notification.Title;
            ContentText.Text = notification.Content;

            var flashColor = notification.FlashColor;
            if (TryParseColor(flashColor, out var color))
            {
                FlashBorder.Background = new SolidColorBrush(color);
                ConfirmButton.Foreground = new SolidColorBrush(color);
            }

            SetWarningType(notification.Type, notification.AlertSubtype, notification.Magnitude, notification.Eta);
            StartFlashing();
        }

        private void SetWarningType(string type, string? alertSubtype, string? magnitude, string? eta)
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

                if (alertSubtype == "early_warning" && !string.IsNullOrEmpty(eta))
                {
                    EtaPanel.IsVisible = true;
                    EtaText.Text = eta;
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
            WarningClosed?.Invoke(this, _notificationId);
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopFlashing();
            base.OnClosed(e);
        }
    }
}
