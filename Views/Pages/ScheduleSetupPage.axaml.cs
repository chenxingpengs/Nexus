using Avalonia.Controls;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Pages
{
    public partial class ScheduleSetupPage : UserControl
    {
        public ScheduleSetupPage()
        {
            InitializeComponent();
        }

        public ScheduleSetupPage(ScheduleSetupViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
