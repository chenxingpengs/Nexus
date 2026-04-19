using Avalonia.Controls;
using Nexus.ViewModels.Pages;

namespace Nexus.Views
{
    public partial class ScheduleSetupWindow : Window
    {
        public ScheduleSetupWindow()
        {
            InitializeComponent();
        }

        public ScheduleSetupWindow(ScheduleSetupViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
