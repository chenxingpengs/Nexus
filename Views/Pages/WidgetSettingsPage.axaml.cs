using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Pages
{
    public partial class WidgetSettingsPage : UserControl
    {
        public WidgetSettingsPage()
        {
            InitializeComponent();
        }

        public WidgetSettingsPage(WidgetSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnCardPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is CardConfigViewModel card)
            {
                if (DataContext is WidgetSettingsViewModel viewModel)
                {
                    viewModel.SelectedCard = card;
                    UpdateCardSelectionVisual();
                }
            }
        }

        private void UpdateCardSelectionVisual()
        {
            if (DataContext is not WidgetSettingsViewModel viewModel) return;

            var allBorders = this.GetVisualDescendants().OfType<Border>();
            foreach (var border in allBorders)
            {
                if (border.DataContext is CardConfigViewModel card)
                {
                    var isSelected = viewModel.SelectedCard == card;
                    border.BorderBrush = isSelected
                        ? Avalonia.Media.Brushes.DodgerBlue
                        : Avalonia.Media.Brushes.Transparent;
                    border.Background = isSelected
                        ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xE3, 0xF2, 0xFD))
                        : Avalonia.Media.Brushes.White;
                }
            }
        }

        private void OnAddCardClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not WidgetSettingsViewModel viewModel) return;

            var availableCards = viewModel.AvailableCards
                .Where(c => !c.IsVisible)
                .ToList();

            if (availableCards.Count == 0) return;

            var menuFlyout = new MenuFlyout();

            foreach (var card in availableCards)
            {
                var menuItem = new MenuItem
                {
                    Header = card.CardName,
                    Tag = card
                };
                menuItem.Click += OnAddMenuItemClick;
                menuFlyout.Items.Add(menuItem);
            }

            if (sender is Button button)
            {
                menuFlyout.ShowAt(button);
            }
        }

        private void OnAddMenuItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is CardConfigViewModel card)
            {
                if (DataContext is WidgetSettingsViewModel viewModel)
                {
                    viewModel.AddCard(card);
                }
            }
        }
    }
}
