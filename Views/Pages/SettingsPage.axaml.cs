using Avalonia.Controls;
using Nexus.Services;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Pages
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage(ConfigService configService, AuthService authService)
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(configService, authService);
        }
    }
}
