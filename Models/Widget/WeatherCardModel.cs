using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.Models.Widget
{
    public class WeatherCardModel : WidgetCard
    {
        private bool _isNotConfigured;
        private string _message = string.Empty;
        private string _city = string.Empty;
        private string _weather = string.Empty;
        private string _icon = string.Empty;
        private double _temperature;
        private double? _tempMin;
        private double? _tempMax;
        private int? _humidity;
        private string _windDirection = string.Empty;
        private double? _windSpeed;
        private string _airQuality = string.Empty;
        private double? _feelsLike;
        private double? _visibility;
        private double? _pressure;
        private double? _uvIndex;
        private int? _aqi;
        private int? _aqiLevel;
        private string _aqiPrimary = string.Empty;

        public WeatherCardModel()
        {
            Type = CardType.Weather;
            Title = "天气";
        }

        public bool IsNotConfigured
        {
            get => _isNotConfigured;
            set => SetProperty(ref _isNotConfigured, value);
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }

        public string Weather
        {
            get => _weather;
            set => SetProperty(ref _weather, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        public double? TempMin
        {
            get => _tempMin;
            set => SetProperty(ref _tempMin, value);
        }

        public double? TempMax
        {
            get => _tempMax;
            set => SetProperty(ref _tempMax, value);
        }

        public int? Humidity
        {
            get => _humidity;
            set => SetProperty(ref _humidity, value);
        }

        public string WindDirection
        {
            get => _windDirection;
            set => SetProperty(ref _windDirection, value);
        }

        public double? WindSpeed
        {
            get => _windSpeed;
            set => SetProperty(ref _windSpeed, value);
        }

        public string AirQuality
        {
            get => _airQuality;
            set => SetProperty(ref _airQuality, value);
        }

        public double? FeelsLike
        {
            get => _feelsLike;
            set => SetProperty(ref _feelsLike, value);
        }

        public double? Visibility
        {
            get => _visibility;
            set => SetProperty(ref _visibility, value);
        }

        public double? Pressure
        {
            get => _pressure;
            set => SetProperty(ref _pressure, value);
        }

        public double? UvIndex
        {
            get => _uvIndex;
            set => SetProperty(ref _uvIndex, value);
        }

        public int? Aqi
        {
            get => _aqi;
            set => SetProperty(ref _aqi, value);
        }

        public int? AqiLevel
        {
            get => _aqiLevel;
            set => SetProperty(ref _aqiLevel, value);
        }

        public string AqiPrimary
        {
            get => _aqiPrimary;
            set => SetProperty(ref _aqiPrimary, value);
        }

        public new DateTime? LastUpdated { get; set; }
    }
}
