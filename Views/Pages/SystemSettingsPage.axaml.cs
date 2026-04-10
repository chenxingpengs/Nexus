using Avalonia.Controls;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Pages
{
    public partial class SystemSettingsPage : UserControl
    {
        public SystemSettingsPage()
        {
            InitializeComponent();
        }

        public SystemSettingsPage(SystemSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
