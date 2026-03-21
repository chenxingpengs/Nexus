using Avalonia.Controls;
using Avalonia.Input;
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

        private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is WidgetSettingsViewModel vm)
            {
                vm.SearchCityCommand.Execute(null);
            }
        }
    }
}
