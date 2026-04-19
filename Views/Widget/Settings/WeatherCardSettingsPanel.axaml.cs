using Avalonia.Controls;
using Avalonia.Input;
using Nexus.ViewModels.Widget.Settings;

namespace Nexus.Views.Widget.Settings
{
    public partial class WeatherCardSettingsPanel : UserControl
    {
        public WeatherCardSettingsPanel()
        {
            InitializeComponent();
        }

        public WeatherCardSettingsPanel(WeatherCardSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is WeatherCardSettingsViewModel vm)
            {
                vm.SearchCityCommand.Execute(null);
            }
        }
    }
}
