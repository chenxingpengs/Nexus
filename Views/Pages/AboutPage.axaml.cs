using Avalonia.Controls;
using Nexus.Services;
using Nexus.ViewModels.Pages;
using System;

namespace Nexus.Views.Pages
{
    public partial class AboutPage : UserControl
    {
        public event Action? RequestLogout;

        public AboutPage()
        {
            InitializeComponent();
        }

        public AboutPage(ConfigService configService, AuthService authService) : this()
        {
            var viewModel = new AboutViewModel(configService, authService);
            DataContext = viewModel;

            viewModel.RequestLogout += () =>
            {
                RequestLogout?.Invoke();
            };
        }
    }
}
