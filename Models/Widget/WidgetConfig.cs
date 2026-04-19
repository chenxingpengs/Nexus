using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.Models.Widget
{
    public enum LocationMode
    {
        Auto,
        Manual
    }

    public class WidgetConfig : ObservableObject
    {
        private bool _isEnabled = true;
        private double _opacity = 0.6;
        private int _width = 400;
        private int _cardSpacing = 8;
        private List<CardConfig> _cards = new();
        private WeatherLocationConfig? _weatherLocation;
        private LocationMode _locationMode = LocationMode.Auto;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value);
        }

        public int Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public int CardSpacing
        {
            get => _cardSpacing;
            set => SetProperty(ref _cardSpacing, value);
        }

        public List<CardConfig> Cards
        {
            get => _cards;
            set => SetProperty(ref _cards, value);
        }

        public WeatherLocationConfig? WeatherLocation
        {
            get => _weatherLocation;
            set => SetProperty(ref _weatherLocation, value);
        }

        public LocationMode LocationMode
        {
            get => _locationMode;
            set => SetProperty(ref _locationMode, value);
        }

        public static WidgetConfig CreateDefault()
        {
            return new WidgetConfig
            {
                IsEnabled = true,
                Opacity = 0.6,
                Width = 400,
                CardSpacing = 8,
                Cards = new List<CardConfig>
                {
                    new CardConfig { Type = CardType.Shortcut, IsVisible = true, Order = 0 },
                    new CardConfig { Type = CardType.Weather, IsVisible = true, Order = 1 },
                    new CardConfig { Type = CardType.Announcement, IsVisible = true, Order = 2 },
                    new CardConfig { Type = CardType.Attendance, IsVisible = true, Order = 3 }
                },
                WeatherLocation = null
            };
        }
    }

    public class CardConfig : ObservableObject
    {
        private CardType _type;
        private bool _isVisible = true;
        private int _order;

        public CardType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public int Order
        {
            get => _order;
            set => SetProperty(ref _order, value);
        }
    }

    public class WeatherLocationConfig : ObservableObject
    {
        private string _cityId = string.Empty;
        private string _cityName = string.Empty;
        private string _province = string.Empty;

        public string CityId
        {
            get => _cityId;
            set => SetProperty(ref _cityId, value);
        }

        public string CityName
        {
            get => _cityName;
            set => SetProperty(ref _cityName, value);
        }

        public string Province
        {
            get => _province;
            set => SetProperty(ref _province, value);
        }

        public bool IsConfigured => !string.IsNullOrEmpty(CityId);
    }
}
