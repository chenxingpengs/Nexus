using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Widget;
using Nexus.Services;
using Nexus.Services.Widget;

namespace Nexus.ViewModels.Widget.Settings
{
    public partial class WeatherCardSettingsViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly WidgetService _widgetService;
        private readonly CitySearchService _citySearchService;

        [ObservableProperty]
        private bool _isAutoLocationMode = true;

        [ObservableProperty]
        private string _autoLocationCity = "";

        [ObservableProperty]
        private string _citySearchText = "";

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _hasSearchResults;

        [ObservableProperty]
        private CityInfo? _selectedCity;

        [ObservableProperty]
        private string _currentCityName = "";

        [ObservableProperty]
        private bool _hasWeatherLocation;

        public ObservableCollection<CityInfo> SearchResults { get; } = new();

        public bool IsManualMode => !IsAutoLocationMode;

        public WeatherCardSettingsViewModel(ConfigService configService, WidgetService widgetService)
        {
            _configService = configService;
            _widgetService = widgetService;
            _citySearchService = widgetService.GetCitySearchService();

            LoadWeatherSettings();
        }

        public void LoadWeatherSettings()
        {
            var config = _configService.GetWidgetConfig();
            IsAutoLocationMode = config.LocationMode == LocationMode.Auto;

            if (config.WeatherLocation != null && config.WeatherLocation.IsConfigured)
            {
                CurrentCityName = config.WeatherLocation.CityName;
                HasWeatherLocation = true;
            }
            else
            {
                HasWeatherLocation = false;
            }

            if (IsAutoLocationMode)
            {
                _ = RefreshAutoLocationAsync();
            }
        }

        partial void OnIsAutoLocationModeChanged(bool value)
        {
            var mode = value ? LocationMode.Auto : LocationMode.Manual;
            _configService.UpdateLocationMode(mode);
            OnPropertyChanged(nameof(IsManualMode));

            if (value)
            {
                _ = RefreshAutoLocationAsync();
            }
        }

        partial void OnSelectedCityChanged(CityInfo? value)
        {
            if (value != null)
            {
                _ = SetWeatherLocationAsync(value);
            }
        }

        private async Task RefreshAutoLocationAsync()
        {
            try
            {
                var location = await _widgetService.GetAutoLocationAsync();
                if (!string.IsNullOrEmpty(location))
                {
                    AutoLocationCity = location;
                }
            }
            catch
            {
                AutoLocationCity = "定位中...";
            }
        }

        [RelayCommand]
        private async Task SearchCityAsync()
        {
            if (string.IsNullOrWhiteSpace(CitySearchText))
            {
                HasSearchResults = false;
                SearchResults.Clear();
                return;
            }

            IsSearching = true;
            try
            {
                var results = await _citySearchService.SearchCitiesAsync(CitySearchText);

                SearchResults.Clear();
                foreach (var city in results)
                {
                    SearchResults.Add(city);
                }
                HasSearchResults = SearchResults.Count > 0;
            }
            catch
            {
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task SetWeatherLocationAsync(CityInfo city)
        {
            await _widgetService.SetWeatherLocationAsync(city.CityId, city.Name, city.Province);
            CurrentCityName = city.Name;
            HasWeatherLocation = true;
            HasSearchResults = false;
            CitySearchText = "";
            SearchResults.Clear();
        }

        [RelayCommand]
        private void ClearLocation()
        {
            _widgetService.ClearWeatherLocation();
            HasWeatherLocation = false;
            CurrentCityName = "";
        }
    }
}
