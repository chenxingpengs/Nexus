using CommunityToolkit.Mvvm.Input;
using Nexus.Services;
using System;
using System.Reflection;
using System.Windows.Input;

namespace Nexus.ViewModels.Pages
{
    public class AboutViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;

        public string VersionText { get; }
        public string DeviceName { get; }
        public string DeviceId { get; }
        public bool IsBound { get; }

        public ICommand UnbindCommand { get; }

        public event Action? RequestLogout;

        public AboutViewModel(ConfigService configService, AuthService authService)
        {
            _configService = configService;
            _authService = authService;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText = version != null ? $"版本 {version.Major}.{version.Minor}.{version.Build}" : "版本 1.0.0";

            var config = _configService.Config;
            DeviceName = config.DeviceName ?? "未命名设备";
            DeviceId = config.DeviceId ?? "";
            IsBound = config.IsBound;

            UnbindCommand = new RelayCommand(OnUnbind);
        }

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }
    }
}
