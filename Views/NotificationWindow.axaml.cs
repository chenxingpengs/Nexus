using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Nexus.Models;

namespace Nexus.Views
{
    public partial class NotificationWindow : Window
    {
        private System.Timers.Timer? _displayTimer;
        private System.Timers.Timer? _scrollTimer;
        private string _notificationId = string.Empty;
        private double _scrollOffset = 0;
        private int _scrollSpeed = 50;
        private string _fullText = string.Empty;
        private double _textWidth = 0;
        private double _canvasWidth = 0;
        private double _gap = 100;

        public event EventHandler<string>? NotificationClosed;

        public NotificationWindow()
        {
            InitializeComponent();
            
            var screen = Screens.Primary;
            if (screen != null)
            {
                Width = screen.WorkingArea.Width;
                Height = 72;
                Position = new PixelPoint(screen.WorkingArea.X, screen.WorkingArea.Y);
            }
        }

        public void ShowNotification(Notification notification)
        {
            _notificationId = notification.Id;
            _fullText = string.IsNullOrEmpty(notification.Title) 
                ? notification.Content 
                : $"{notification.Title}：{notification.Content}";
            
            ScrollText.Text = _fullText;
            
            var backgroundColor = notification.BackgroundColor;
            if (TryParseColor(backgroundColor, out var color))
            {
                MainBorder.Background = new SolidColorBrush(color);
            }

            _scrollSpeed = notification.Display?.ScrollSpeed ?? 50;

            Show();

            StartScrollAnimation();

            var duration = notification.Display?.Duration ?? 10;
            if (duration > 0)
            {
                _displayTimer = new System.Timers.Timer(duration * 1000);
                _displayTimer.AutoReset = false;
                _displayTimer.Elapsed += (s, e) => CloseNotification();
                _displayTimer.Start();
            }
        }

        private void StartScrollAnimation()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _textWidth = ScrollText.DesiredSize.Width;
                _canvasWidth = ScrollCanvas.Bounds.Width;
                
                if (_textWidth <= 0 || _canvasWidth <= 0)
                {
                    _scrollTimer = new System.Timers.Timer(100);
                    _scrollTimer.Elapsed += (s, e) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _textWidth = ScrollText.DesiredSize.Width;
                            _canvasWidth = ScrollCanvas.Bounds.Width;
                            
                            if (_textWidth > 0 && _canvasWidth > 0)
                            {
                                _scrollTimer?.Stop();
                                BeginScrolling();
                            }
                        });
                    };
                    _scrollTimer.Start();
                }
                else
                {
                    BeginScrolling();
                }
            });
        }

        private void BeginScrolling()
        {
            _scrollOffset = _canvasWidth;
            Canvas.SetLeft(ScrollText, _scrollOffset);
            Canvas.SetTop(ScrollText, 20);
            
            _scrollTimer = new System.Timers.Timer(16);
            _scrollTimer.Elapsed += (s, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _scrollOffset -= _scrollSpeed * 0.016;
                    
                    double totalWidth = _textWidth + _gap;
                    
                    if (_scrollOffset < -totalWidth)
                    {
                        _scrollOffset = _canvasWidth;
                    }
                    
                    Canvas.SetLeft(ScrollText, _scrollOffset);
                });
            };
            _scrollTimer.Start();
        }

        private bool TryParseColor(string colorString, out Color color)
        {
            color = Colors.DodgerBlue;
            
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

        private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            CloseNotification();
        }

        private void CloseNotification()
        {
            Dispatcher.UIThread.Post(async () =>
            {
                _displayTimer?.Stop();
                _displayTimer?.Dispose();
                _scrollTimer?.Stop();
                _scrollTimer?.Dispose();
                
                Opacity = 0;
                
                await Task.Delay(300);
                
                NotificationClosed?.Invoke(this, _notificationId);
                Close();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _displayTimer?.Stop();
            _displayTimer?.Dispose();
            _scrollTimer?.Stop();
            _scrollTimer?.Dispose();
            
            base.OnClosed(e);
        }
    }
}
