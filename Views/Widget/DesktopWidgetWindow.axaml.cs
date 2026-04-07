using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Nexus.Models.Widget;
using Nexus.Services.Widget;
using Nexus.ViewModels.Widget.Cards;

namespace Nexus.Views.Widget
{
    public partial class DesktopWidgetWindow : Window
    {
        private readonly WidgetService _widgetService;
        private bool _allowClose;
        private DispatcherTimer? _positionTimer;
        private double _lastHeight;
        private const double RightOffset = 16;
        private const double BottomOffset = 80;

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
            if (_widgetService.AttendanceViewModel != null && AttendanceCard != null)
            {
                AttendanceCard.DataContext = _widgetService.AttendanceViewModel;
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

        private PixelRect? GetWorkingArea()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            return screen?.WorkingArea;
        }

        private void PositionWindowInRightBottomCorner()
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen == null) return;

            WindowState = WindowState.Normal;
            UpdateLayout();

            var workingArea = screen.WorkingArea;
            var scaling = RenderScaling;

            var windowWidth = Bounds.Width > 0 ? Bounds.Width * scaling : Width * scaling;
            var windowHeight = Bounds.Height > 0 ? Bounds.Height * scaling : 400 * scaling;
            _lastHeight = Bounds.Height > 0 ? Bounds.Height : 400;

            var screenWidth = workingArea.Width;
            var screenHeight = workingArea.Height;

            var x = screenWidth - windowWidth - RightOffset;
            var y = screenHeight - windowHeight - BottomOffset;

            x = Math.Max(x, 0);
            y = Math.Max(y, 0);
            x = Math.Min(x, screenWidth - windowWidth);
            y = Math.Min(y, screenHeight - windowHeight);

            Position = new PixelPoint(workingArea.X + (int)x, workingArea.Y + (int)y);
        }

        private PixelPoint CalculateClampedPosition(PixelRect workingArea, double windowWidth, double windowHeight)
        {
            var scaling = RenderScaling;
            var physicalWidth = windowWidth * scaling;
            var physicalHeight = windowHeight * scaling;

            var screenWidth = workingArea.Width;
            var screenHeight = workingArea.Height;

            var x = screenWidth - physicalWidth - RightOffset;
            var y = screenHeight - physicalHeight - BottomOffset;

            x = Math.Max(x, 0);
            y = Math.Max(y, 0);
            x = Math.Min(x, screenWidth - physicalWidth);
            y = Math.Min(y, screenHeight - physicalHeight);

            return new PixelPoint(workingArea.X + (int)x, workingArea.Y + (int)y);
        }

        private void RepositionWindow()
        {
            var workingArea = GetWorkingArea();
            if (workingArea == null) return;

            var windowWidth = Bounds.Width;
            var windowHeight = Bounds.Height;

            if (windowWidth <= 0 || windowHeight <= 0) return;

            var position = CalculateClampedPosition(workingArea.Value, windowWidth, windowHeight);

            if (Position.X != position.X || Position.Y != position.Y)
            {
                Position = position;
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

        public void UpdateAttendanceData(AttendanceCardViewModel viewModel)
        {
            if (AttendanceCard != null)
            {
                AttendanceCard.DataContext = viewModel;
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
            if (!_allowClose)
            {
                e.Cancel = true;
            }
            else
            {
                _positionTimer?.Stop();
                base.OnClosing(e);
            }
        }

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }
    }
}
