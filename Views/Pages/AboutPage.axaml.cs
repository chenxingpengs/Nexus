using Avalonia.Controls;
using Nexus.Services;

namespace Nexus.Views.Pages
{
    public partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
            VersionText.Text = $"版本 {UpdateService.CurrentVersion}";
        }
    }
}
