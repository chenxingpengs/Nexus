using Avalonia.Controls;
using Nexus.Services;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Pages
{
    public partial class UpdatePage : UserControl
    {
        public UpdatePage()
        {
            InitializeComponent();
        }

        public UpdatePage(UpdateService updateService) : this()
        {
            DataContext = new UpdateViewModel(updateService);
        }
    }
}
