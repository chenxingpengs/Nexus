using Avalonia.Controls;
using Nexus.Services;
using Nexus.ViewModels.Pages;
using System;

namespace Nexus.Views.Pages
{
    public partial class TimeSlotQuotaPage : UserControl
    {
        private TimeSlotQuotaViewModel? _viewModel;

        public TimeSlotQuotaPage()
        {
            InitializeComponent();
        }

        public TimeSlotQuotaPage(ConfigService configService, AuthService authService) : this()
        {
            _viewModel = new TimeSlotQuotaViewModel(configService, authService);
            DataContext = _viewModel;
            _ = InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }
        }
    }
}
