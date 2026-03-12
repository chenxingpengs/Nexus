using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models;
using Nexus.Services;

namespace Nexus.ViewModels.Pages
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;

        public string DeviceId => _configService.Config.DeviceId;
        public string DeviceName => _configService.Config.DeviceName;
        public string ClassName => _configService.Config.BindInfo?.ClassName ?? "未绑定";
        public string ServerUrl => _configService.Config.ServerUrl;

        private bool _autoStart;
        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                if (SetProperty(ref _autoStart, value))
                {
                    SetAutoStart(value);
                }
            }
        }

        public ICommand UnbindCommand { get; }

        public event Action? RequestLogout;

        public SettingsViewModel(ConfigService configService, AuthService authService)
        {
            _configService = configService;
            _authService = authService;
            
            _autoStart = CheckAutoStart();
            UnbindCommand = new RelayCommand(OnUnbind);
        }

        private bool CheckAutoStart()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", false);
                    var value = key?.GetValue("Nexus");
                    return value != null;
                }
            }
            catch
            {
            }
            return false;
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    
                    if (enable)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key?.SetValue("Nexus", $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        key?.DeleteValue("Nexus", false);
                    }
                }
            }
            catch
            {
            }
        }

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }
    }
}
