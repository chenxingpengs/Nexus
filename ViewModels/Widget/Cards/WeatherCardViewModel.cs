using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Nexus.Models.Widget;

namespace Nexus.ViewModels.Widget.Cards
{
    public partial class WeatherCardViewModel : ObservableObject
    {
        private readonly WeatherCardModel _model;

        public WeatherCardViewModel(WeatherCardModel model)
        {
            _model = model;
        }

        public bool IsNotConfigured => _model.IsNotConfigured;
        public string Message => _model.Message;
        public string City => _model.City;
        public string Weather => _model.Weather;
        public double Temperature => _model.Temperature;
        public int? Humidity => _model.Humidity;
        public string WindDirection => _model.WindDirection;
        public string AirQuality => _model.AirQuality;

        public string TemperatureDisplay => $"{_model.Temperature:F0}°C";
        
        public string TemperatureRange
        {
            get
            {
                if (_model.TempMin.HasValue && _model.TempMax.HasValue)
                {
                    return $"{_model.TempMin:F0}°C / {_model.TempMax:F0}°C";
                }
                return "";
            }
        }

        public string WindSpeedDisplay
        {
            get
            {
                if (_model.WindSpeed.HasValue)
                {
                    return $"{_model.WindSpeed:F0}级";
                }
                return "";
            }
        }

        public Symbol WeatherIcon => GetWeatherIcon(_model.Icon);

        private Symbol GetWeatherIcon(string? icon)
        {
            return icon?.ToLower() switch
            {
                "sunny" or "晴" => Symbol.Clear,
                "cloudy" or "多云" => Symbol.Cloud,
                "overcast" or "阴" => Symbol.Cloud,
                "rain" or "雨" => Symbol.Download,
                "snow" or "雪" => Symbol.Folder,
                "fog" or "雾" => Symbol.Cloud,
                _ => Symbol.Globe
            };
        }

        public void UpdateModel(WeatherCardModel model)
        {
            _model.IsNotConfigured = model.IsNotConfigured;
            _model.Message = model.Message;
            _model.City = model.City;
            _model.Weather = model.Weather;
            _model.Icon = model.Icon;
            _model.Temperature = model.Temperature;
            _model.TempMin = model.TempMin;
            _model.TempMax = model.TempMax;
            _model.Humidity = model.Humidity;
            _model.WindDirection = model.WindDirection;
            _model.WindSpeed = model.WindSpeed;
            _model.AirQuality = model.AirQuality;
            
            OnPropertyChanged(nameof(IsNotConfigured));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(City));
            OnPropertyChanged(nameof(Weather));
            OnPropertyChanged(nameof(Temperature));
            OnPropertyChanged(nameof(TemperatureDisplay));
            OnPropertyChanged(nameof(TemperatureRange));
            OnPropertyChanged(nameof(Humidity));
            OnPropertyChanged(nameof(WindDirection));
            OnPropertyChanged(nameof(WindSpeedDisplay));
            OnPropertyChanged(nameof(AirQuality));
            OnPropertyChanged(nameof(WeatherIcon));
        }
    }
}
