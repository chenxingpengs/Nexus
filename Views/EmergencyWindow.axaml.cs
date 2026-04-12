using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Nexus.Models;

namespace Nexus.Views
{
    public partial class EmergencyWindow : Window
    {
        private string _notificationId = string.Empty;
        private bool _isFlashing;
        private DispatcherTimer? _flashTimer;

        public event EventHandler<string>? EmergencyClosed;

        public EmergencyWindow()
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

        public void ShowEmergency(Notification notification)
        {
            _notificationId = notification.Id;
            TitleText.Text = notification.Title;
            ContentText.Text = notification.Content;

            var flashColor = notification.FlashColor;
            if (TryParseColor(flashColor, out var color))
            {
                FlashBorder.Background = new SolidColorBrush(color);
            }

            StartFlashing();
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
            EmergencyClosed?.Invoke(this, _notificationId);
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopFlashing();
            base.OnClosed(e);
        }
    }
}
