using Avalonia.Controls;
using Nexus.Services;
using Nexus.ViewModels.Pages;
using System;

namespace Nexus.Views.Pages
{
    public partial class SettingsPage : UserControl
    {
        private SettingsViewModel? _viewModel;

        public event Action<int, string>? RequestOpenScheduleConfig;

        public SettingsPage()
        {
            InitializeComponent();
        }

        public SettingsPage(ConfigService configService, AuthService authService, ScheduleService scheduleService) : this()
        {
            _viewModel = new SettingsViewModel(configService, authService, scheduleService);
            DataContext = _viewModel;

            _viewModel.RequestOpenScheduleConfig += (classId, className) =>
            {
                RequestOpenScheduleConfig?.Invoke(classId, className);
            };

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
