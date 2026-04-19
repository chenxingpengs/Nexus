using Avalonia.Controls;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Pages
{
    public partial class MeetingPage : UserControl
    {
        public MeetingPage()
        {
            InitializeComponent();
        }

        public MeetingPage(MeetingPageViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
