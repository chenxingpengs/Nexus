using Avalonia.Controls;
using Nexus.ViewModels.Widget;

namespace Nexus.Views.Widget
{
    public partial class AttendanceDetailWindow : Window
    {
        public AttendanceDetailWindow()
        {
            InitializeComponent();
        }

        public AttendanceDetailWindow(AttendanceDetailViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
