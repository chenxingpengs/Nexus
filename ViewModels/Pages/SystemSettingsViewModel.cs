using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Services;
using System;
using System.Linq;
using System.Windows.Input;

namespace Nexus.ViewModels.Pages
{
    public partial class SystemSettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;

        public string MacAddress => FormatMacAddress(_configService.Config.MacAddress ?? "未检测到");

        [ObservableProperty]
        private bool _autoStart;

        public ICommand UnbindCommand { get; }

        public event Action? RequestLogout;

        public SystemSettingsViewModel(ConfigService configService)
        {
            _configService = configService;
            _autoStart = CheckAutoStart();
            UnbindCommand = new RelayCommand(OnUnbind);
        }

        private string FormatMacAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac == "未检测到")
                return "未检测到";

            var cleanMac = mac.Replace(":", "").Replace("-", "").Replace(".", "");
            if (cleanMac.Length != 12)
                return mac;

            return string.Join(":", Enumerable.Range(0, 6)
                .Select(i => cleanMac.Substring(i * 2, 2).ToUpper()));
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
            catch { }
            return false;
        }

        partial void OnAutoStartChanged(bool value)
        {
            SetAutoStart(value);
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
                            key?.SetValue("Nexus", "\"" + exePath + "\"");
                        }
                    }
                    else
                    {
                        key?.DeleteValue("Nexus", false);
                    }
                }
            }
            catch { }
        }

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }
    }
}
