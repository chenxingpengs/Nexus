using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Widget;
using Nexus.Services;
using Nexus.Services.Widget;

namespace Nexus.ViewModels.Pages
{
    public partial class WidgetSettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly WidgetService _widgetService;
        private readonly CitySearchService _citySearchService;

        [ObservableProperty]
        private bool _isWidgetEnabled;

        [ObservableProperty]
        private double _widgetOpacity;

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

        [ObservableProperty]
        private bool _isAutoLocationMode = true;

        [ObservableProperty]
        private string _autoLocationCity = "";

        public ObservableCollection<CityInfo> SearchResults { get; } = new();
        public ObservableCollection<CardConfigViewModel> CardConfigs { get; } = new();

        public string OpacityPercent => $"{(int)(WidgetOpacity * 100)}%";

        public bool IsManualMode => !IsAutoLocationMode;

        public WidgetSettingsViewModel(ConfigService configService, WidgetService widgetService)
        {
            _configService = configService;
            _widgetService = widgetService;
            _citySearchService = widgetService.GetCitySearchService();

            LoadSettings();
            
            CardConfigs.CollectionChanged += OnCardConfigsCollectionChanged;
        }

        private void OnCardConfigsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                SaveCardOrder();
            }
        }

        private void SaveCardOrder()
        {
            for (int i = 0; i < CardConfigs.Count; i++)
            {
                CardConfigs[i].Order = i;
            }
            _configService.SaveConfig();
            _widgetService.RefreshWindowPosition();
        }

        public void MoveCard(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || newIndex < 0 || oldIndex >= CardConfigs.Count || newIndex >= CardConfigs.Count)
                return;
            
            CardConfigs.Move(oldIndex, newIndex);
        }

        [RelayCommand]
        private void MoveCardUp(CardConfigViewModel? card)
        {
            if (card == null)
                return;
            
            var index = CardConfigs.IndexOf(card);
            if (index > 0)
            {
                MoveCard(index, index - 1);
            }
        }

        [RelayCommand]
        private void MoveCardDown(CardConfigViewModel? card)
        {
            if (card == null)
                return;
            
            var index = CardConfigs.IndexOf(card);
            if (index >= 0 && index < CardConfigs.Count - 1)
            {
                MoveCard(index, index + 1);
            }
        }

        private void LoadSettings()
        {
            var config = _configService.GetWidgetConfig();
            IsWidgetEnabled = config.IsEnabled;
            WidgetOpacity = config.Opacity;
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

            CardConfigs.Clear();
            var orderedCards = config.Cards.OrderBy(c => c.Order).ToList();
            foreach (var card in orderedCards)
            {
                CardConfigs.Add(new CardConfigViewModel(card, OnCardVisibilityChanged));
            }
        }

        private void OnCardVisibilityChanged()
        {
            _configService.SaveConfig();
            _widgetService.RefreshWindowPosition();
        }

        partial void OnIsWidgetEnabledChanged(bool value)
        {
            _widgetService.SetWidgetEnabled(value);
        }

        partial void OnWidgetOpacityChanged(double value)
        {
            _widgetService.SetWidgetOpacity(value);
            OnPropertyChanged(nameof(OpacityPercent));
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
            System.Diagnostics.Debug.WriteLine($"[SearchCity] 开始搜索: {CitySearchText}");
            
            if (string.IsNullOrWhiteSpace(CitySearchText))
            {
                HasSearchResults = false;
                SearchResults.Clear();
                return;
            }

            IsSearching = true;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SearchCity] _citySearchService: {_citySearchService != null}");
                var results = await _citySearchService.SearchCitiesAsync(CitySearchText);
                System.Diagnostics.Debug.WriteLine($"[SearchCity] 搜索结果数量: {results.Count}");
                
                SearchResults.Clear();
                foreach (var city in results)
                {
                    SearchResults.Add(city);
                }
                HasSearchResults = SearchResults.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchCity] 错误: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
            }
        }

        partial void OnSelectedCityChanged(CityInfo? value)
        {
            if (value != null)
            {
                _ = SetWeatherLocationAsync(value);
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

    public class CardConfigViewModel : ObservableObject
    {
        private readonly CardConfig _config;
        private readonly Action? _onVisibilityChanged;

        public CardConfigViewModel(CardConfig config, Action? onVisibilityChanged = null)
        {
            _config = config;
            _onVisibilityChanged = onVisibilityChanged;
        }

        public CardType CardType => _config.Type;

        public string CardName => _config.Type switch
        {
            CardType.Shortcut => "快捷栏",
            CardType.Weather => "天气卡片",
            CardType.Announcement => "公告卡片",
            CardType.Attendance => "考勤统计卡片",
            _ => "未知卡片"
        };

        public string CardIcon => _config.Type switch
        {
            CardType.Shortcut => "快捷",
            CardType.Weather => "天气",
            CardType.Announcement => "公告",
            CardType.Attendance => "考勤",
            _ => "卡片"
        };

        public bool IsVisible
        {
            get => _config.IsVisible;
            set
            {
                if (_config.IsVisible != value)
                {
                    _config.IsVisible = value;
                    OnPropertyChanged();
                    _onVisibilityChanged?.Invoke();
                }
            }
        }

        public int Order
        {
            get => _config.Order;
            set
            {
                if (_config.Order != value)
                {
                    _config.Order = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
