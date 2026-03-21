using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Nexus.Models.Widget;
using Nexus.Services.Widget;

namespace Nexus.Views.Widget
{
    public partial class DesktopWidgetWindow : Window
    {
        private readonly WidgetService _widgetService;
        private bool _isClosing;
        private DispatcherTimer? _positionTimer;
        private double _lastHeight;
        private const double RightOffset = 110;
        private const double BottomOffset = 200;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        private const uint SPI_GETWORKAREA = 0x0030;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public DesktopWidgetWindow(WidgetService widgetService)
        {
            InitializeComponent();
            _widgetService = widgetService;
            
            Loaded += OnWindowLoaded;
            
            ShowInTaskbar = false;
        }

        private void OnWindowLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            PositionWindowInRightBottomCorner();
            ApplyTransparency();
            LoadCardData();
            
            StartPositionMonitor();
        }

        private void StartPositionMonitor()
        {
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _positionTimer.Tick += PositionTimer_Tick;
            _positionTimer.Start();
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            var currentHeight = Bounds.Height;
            if (currentHeight > 0 && Math.Abs(currentHeight - _lastHeight) > 0.5)
            {
                _lastHeight = currentHeight;
                RepositionWindow();
            }
        }

        private void LoadCardData()
        {
            ApplyCardVisibility();
            ApplyCardOrder();
            
            if (_widgetService.ShortcutData != null && ShortcutCard != null)
            {
                ShortcutCard.DataContext = _widgetService.ShortcutData;
                ShortcutCard.SetShortcutService(_widgetService.GetShortcutService());
            }
            if (_widgetService.WeatherData != null && WeatherCard != null)
            {
                WeatherCard.DataContext = _widgetService.WeatherData;
            }
            if (_widgetService.AnnouncementData != null && AnnouncementCard != null)
            {
                AnnouncementCard.DataContext = _widgetService.AnnouncementData;
            }
            if (_widgetService.AttendanceData != null && AttendanceCard != null)
            {
                AttendanceCard.DataContext = _widgetService.AttendanceData;
            }
        }

        private void ApplyCardVisibility()
        {
            var config = _widgetService.GetConfig();
            
            foreach (var cardConfig in config.Cards)
            {
                switch (cardConfig.Type)
                {
                    case CardType.Shortcut:
                        if (ShortcutCard != null)
                            ShortcutCard.IsVisible = cardConfig.IsVisible;
                        break;
                    case CardType.Weather:
                        if (WeatherCard != null)
                            WeatherCard.IsVisible = cardConfig.IsVisible;
                        break;
                    case CardType.Announcement:
                        if (AnnouncementCard != null)
                            AnnouncementCard.IsVisible = cardConfig.IsVisible;
                        break;
                    case CardType.Attendance:
                        if (AttendanceCard != null)
                            AttendanceCard.IsVisible = cardConfig.IsVisible;
                        break;
                }
            }
        }

        private void ApplyCardOrder()
        {
            var config = _widgetService.GetConfig();
            var orderedCards = config.Cards.OrderBy(c => c.Order).ToList();
            
            for (int i = 0; i < orderedCards.Count; i++)
            {
                var cardConfig = orderedCards[i];
                Control? card = cardConfig.Type switch
                {
                    CardType.Shortcut => ShortcutCard,
                    CardType.Weather => WeatherCard,
                    CardType.Announcement => AnnouncementCard,
                    CardType.Attendance => AttendanceCard,
                    _ => null
                };
                
                if (card != null && CardsContainer != null)
                {
                    var currentIndex = CardsContainer.Children.IndexOf(card);
                    if (currentIndex != i && currentIndex >= 0)
                    {
                        CardsContainer.Children.Remove(card);
                        CardsContainer.Children.Insert(i, card);
                    }
                }
            }
        }

        private RECT GetWorkingArea()
        {
            var rect = new RECT();
            SystemParametersInfo(SPI_GETWORKAREA, 0, ref rect, 0);
            return rect;
        }

        private void PositionWindowInRightBottomCorner()
        {
            var workingArea = GetWorkingArea();
            
            WindowState = WindowState.Normal;
            
            UpdateLayout();
            
            Width = 400;
            
            var windowHeight = Bounds.Height > 0 ? Bounds.Height : 400;
            _lastHeight = windowHeight;
            
            var x = workingArea.Right - Width - RightOffset;
            var y = workingArea.Bottom - windowHeight - BottomOffset;
            
            if (x < workingArea.Left + 10) x = workingArea.Left + 10;
            if (y < workingArea.Top + 10) y = workingArea.Top + 10;
            
            Position = new PixelPoint((int)x, (int)y);
        }

        private void RepositionWindow()
        {
            var workingArea = GetWorkingArea();
            var windowWidth = Bounds.Width;
            var windowHeight = Bounds.Height;
            
            if (windowWidth <= 0 || windowHeight <= 0) return;
            
            var x = workingArea.Right - windowWidth - RightOffset;
            var y = workingArea.Bottom - windowHeight - BottomOffset;
            
            if (x < workingArea.Left + 10) x = workingArea.Left + 10;
            if (y < workingArea.Top + 10) y = workingArea.Top + 10;
            
            var newX = (int)x;
            var newY = (int)y;
            
            if (Position.X != newX || Position.Y != newY)
            {
                Position = new PixelPoint(newX, newY);
            }
        }

        private void ApplyTransparency()
        {
            var config = _widgetService.GetConfig();
            if (MainBorder != null)
            {
                MainBorder.Opacity = config.Opacity;
            }
        }

        public void UpdateOpacity(double opacity)
        {
            if (MainBorder != null)
            {
                MainBorder.Opacity = opacity;
            }
        }

        public void UpdateWeatherData(WeatherCardModel data)
        {
            if (WeatherCard != null)
            {
                WeatherCard.DataContext = data;
            }
        }

        public void RefreshShortcutData(ShortcutCardModel data)
        {
            if (ShortcutCard != null)
            {
                ShortcutCard.DataContext = data;
            }
        }

        public void UpdateAttendanceData(AttendanceCardModel data)
        {
            if (AttendanceCard != null)
            {
                AttendanceCard.DataContext = data;
            }
        }

        public void SetAttendanceCardVisibility(bool visible)
        {
            if (AttendanceCard != null)
            {
                AttendanceCard.IsVisible = visible;
            }
        }

        public void RefreshPosition()
        {
            ApplyCardVisibility();
            ApplyCardOrder();
            _lastHeight = 0;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!_isClosing)
            {
                _positionTimer?.Stop();
                e.Cancel = true;
                Hide();
            }
            base.OnClosing(e);
        }

        public void ForceClose()
        {
            _isClosing = true;
            _positionTimer?.Stop();
            Close();
        }
    }
}
