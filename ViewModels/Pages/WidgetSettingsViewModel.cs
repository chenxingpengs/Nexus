using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Widget;
using Nexus.Services;
using Nexus.Services.Widget;
using Nexus.ViewModels.Widget.Settings;
using Nexus.Views.Widget.Settings;

namespace Nexus.ViewModels.Pages
{
    public partial class WidgetSettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly WidgetService _widgetService;

        [ObservableProperty]
        private bool _isWidgetEnabled;

        [ObservableProperty]
        private double _widgetOpacity;

        [ObservableProperty]
        private CardConfigViewModel? _selectedCard;

        private readonly Dictionary<CardType, ICardSettingsProvider> _settingsProviders = new();

        public ObservableCollection<CardConfigViewModel> AvailableCards { get; } = new();
        public ObservableCollection<CardConfigViewModel> EnabledCards { get; } = new();

        public string OpacityPercent => $"{(int)(WidgetOpacity * 100)}%";

        public bool HasEnabledCards => EnabledCards.Count > 0;
        public bool HasAvailableCards => AvailableCards.Count > 0;

        public Control? CurrentSettingsPanel
        {
            get
            {
                if (SelectedCard == null) return null;
                if (_settingsProviders.TryGetValue(SelectedCard.CardType, out var provider))
                {
                    return provider.CreateSettingsPanel();
                }
                return null;
            }
        }

        public string CurrentSettingsTitle
        {
            get
            {
                if (SelectedCard == null) return "";
                return SelectedCard.CardType switch
                {
                    CardType.Weather => "天气卡片配置",
                    CardType.Shortcut => "快捷栏配置",
                    CardType.Announcement => "公告卡片配置",
                    CardType.Attendance => "考勤统计配置",
                    _ => "卡片配置"
                };
            }
        }

        public bool HasSelectedCard => SelectedCard != null;

        public WidgetSettingsViewModel(ConfigService configService, WidgetService widgetService)
        {
            _configService = configService;
            _widgetService = widgetService;

            RegisterSettingsProviders();
            LoadSettings();

            EnabledCards.CollectionChanged += OnEnabledCardsCollectionChanged;
        }

        private void RegisterSettingsProviders()
        {
            var weatherProvider = new WeatherCardSettingsProvider(_configService, _widgetService);
            _settingsProviders[CardType.Weather] = weatherProvider;
            _settingsProviders[CardType.Shortcut] = new ShortcutCardSettingsProvider();
            _settingsProviders[CardType.Announcement] = new AnnouncementCardSettingsProvider();
            _settingsProviders[CardType.Attendance] = new AttendanceCardSettingsProvider();
        }

        private void OnEnabledCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                SaveCardOrder();
            }
            OnPropertyChanged(nameof(HasEnabledCards));
            OnPropertyChanged(nameof(HasAvailableCards));
        }

        partial void OnSelectedCardChanged(CardConfigViewModel? value)
        {
            OnPropertyChanged(nameof(CurrentSettingsPanel));
            OnPropertyChanged(nameof(CurrentSettingsTitle));
            OnPropertyChanged(nameof(HasSelectedCard));
        }

        public void MoveCard(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || newIndex < 0 || oldIndex >= EnabledCards.Count || newIndex >= EnabledCards.Count)
                return;

            EnabledCards.Move(oldIndex, newIndex);
            SaveCardOrder();
        }

        public void AddCard(CardConfigViewModel card)
        {
            if (card == null || EnabledCards.Contains(card)) return;

            EnabledCards.Add(card);
            card.IsVisible = true;
            SaveCardOrder();
        }

        public void RemoveCard(CardConfigViewModel card)
        {
            if (card == null || !EnabledCards.Contains(card)) return;

            if (SelectedCard == card)
            {
                SelectedCard = null;
            }

            EnabledCards.Remove(card);
            card.IsVisible = false;
            SaveCardOrder();
        }

        [RelayCommand]
        private void RemoveSelectedCard()
        {
            if (SelectedCard != null)
            {
                RemoveCard(SelectedCard);
            }
        }

        private void SaveCardOrder()
        {
            var config = _configService.GetWidgetConfig();
            config.Cards.Clear();

            var enabledTypes = new HashSet<CardType>();

            for (int i = 0; i < EnabledCards.Count; i++)
            {
                var card = EnabledCards[i];
                card.Order = i;
                card.IsVisible = true;
                enabledTypes.Add(card.CardType);
                config.Cards.Add(new CardConfig
                {
                    Type = card.CardType,
                    IsVisible = true,
                    Order = i
                });
            }

            foreach (var card in AvailableCards)
            {
                if (!enabledTypes.Contains(card.CardType))
                {
                    card.IsVisible = false;
                    config.Cards.Add(new CardConfig
                    {
                        Type = card.CardType,
                        IsVisible = false,
                        Order = 999
                    });
                }
            }

            _configService.SaveConfig();
            _widgetService.RefreshWindowPosition();
        }

        private void LoadSettings()
        {
            try
            {
                var config = _configService.GetWidgetConfig();
                IsWidgetEnabled = config.IsEnabled;
                WidgetOpacity = config.Opacity;

                AvailableCards.Clear();
                EnabledCards.Clear();

                var allCardTypes = new[] { CardType.Shortcut, CardType.Weather, CardType.Announcement, CardType.Attendance };

                var cards = config.Cards ?? new List<CardConfig>();
                var configDict = cards.ToDictionary(c => c.Type);
                var cardViewModelDict = new Dictionary<CardType, CardConfigViewModel>();

                foreach (var cardType in allCardTypes)
                {
                    CardConfigViewModel viewModel;
                    if (configDict.TryGetValue(cardType, out var cardConfig))
                    {
                        viewModel = new CardConfigViewModel(cardConfig, OnCardVisibilityChanged);
                    }
                    else
                    {
                        var newConfig = new CardConfig { Type = cardType, IsVisible = false, Order = 999 };
                        viewModel = new CardConfigViewModel(newConfig, OnCardVisibilityChanged);
                    }
                    cardViewModelDict[cardType] = viewModel;
                }

                foreach (var cardType in allCardTypes)
                {
                    var viewModel = cardViewModelDict[cardType];
                    AvailableCards.Add(viewModel);

                    if (viewModel.IsVisible)
                    {
                        EnabledCards.Add(viewModel);
                    }
                }

                var orderedEnabled = EnabledCards.OrderBy(c => c.Order).ToList();
                EnabledCards.Clear();
                foreach (var card in orderedEnabled)
                {
                    EnabledCards.Add(card);
                }

                OnPropertyChanged(nameof(HasEnabledCards));
                OnPropertyChanged(nameof(HasAvailableCards));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WidgetSettingsViewModel] LoadSettings error: {ex.Message}");
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

        public string CardDescription => _config.Type switch
        {
            CardType.Shortcut => "U盘、高拍仪等快捷入口",
            CardType.Weather => "显示当前天气和空气质量",
            CardType.Announcement => "显示学校公告通知",
            CardType.Attendance => "显示班级考勤统计",
            _ => ""
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
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(BorderColor));
                    _onVisibilityChanged?.Invoke();
                }
            }
        }

        public string StatusText => IsVisible ? "已启用" : "未启用";

        public string StatusColor => IsVisible ? "#4CAF50" : "#9E9E9E";

        public string BorderColor => IsVisible ? "#4CAF50" : "#E0E0E0";

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

    internal class WeatherCardSettingsProvider : ICardSettingsProvider
    {
        private readonly ConfigService _configService;
        private readonly WidgetService _widgetService;

        public CardType CardType => CardType.Weather;
        public string SettingsTitle => "天气卡片配置";

        public WeatherCardSettingsProvider(ConfigService configService, WidgetService widgetService)
        {
            _configService = configService;
            _widgetService = widgetService;
        }

        public Control CreateSettingsPanel()
        {
            var viewModel = new WeatherCardSettingsViewModel(_configService, _widgetService);
            return new WeatherCardSettingsPanel(viewModel);
        }
    }

    internal class ShortcutCardSettingsProvider : ICardSettingsProvider
    {
        public CardType CardType => CardType.Shortcut;
        public string SettingsTitle => "快捷栏配置";

        public Control CreateSettingsPanel()
        {
            return new ShortcutCardSettingsPanel();
        }
    }

    internal class AnnouncementCardSettingsProvider : ICardSettingsProvider
    {
        public CardType CardType => CardType.Announcement;
        public string SettingsTitle => "公告卡片配置";

        public Control CreateSettingsPanel()
        {
            return new AnnouncementCardSettingsPanel();
        }
    }

    internal class AttendanceCardSettingsProvider : ICardSettingsProvider
    {
        public CardType CardType => CardType.Attendance;
        public string SettingsTitle => "考勤统计配置";

        public Control CreateSettingsPanel()
        {
            return new AttendanceCardSettingsPanel();
        }
    }
}
