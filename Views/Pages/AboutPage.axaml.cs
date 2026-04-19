using Avalonia.Controls;
using Nexus.Services;
using Nexus.ViewModels.Pages;
using System;
using System.Threading.Tasks;

namespace Nexus.Views.Pages
{
    public partial class AboutPage : UserControl
    {
        private AboutViewModel? _viewModel;
        private PasswordService? _passwordService;

        public event Action? RequestLogout;

        public AboutPage()
        {
            InitializeComponent();
        }

        public AboutPage(ConfigService configService, AuthService authService, PasswordService passwordService) : this()
        {
            _passwordService = passwordService;
            _viewModel = new AboutViewModel(configService, authService, passwordService);
            DataContext = _viewModel;

            _viewModel.RequestLogout += () =>
            {
                RequestLogout?.Invoke();
            };

            _viewModel.RequestPasswordVerification += async (callback) =>
            {
                await HandlePasswordVerificationAsync(callback);
            };
        }

        private async Task HandlePasswordVerificationAsync(Action<bool> callback)
        {
            if (_passwordService == null)
            {
                callback(false);
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window parentWindow)
            {
                var (success, password) = await PasswordDialog.ShowDialogAsync(parentWindow, "解绑确认 - 请输入密码");

                if (success && !string.IsNullOrEmpty(password))
                {
                    if (_passwordService.VerifyPassword(password))
                    {
                        callback(true);
                    }
                    else
                    {
                        var toastService = new ToastService();
                        toastService.ShowError("密码错误，无法解绑设备");
                        callback(false);
                    }
                }
                else
                {
                    callback(false);
                }
            }
            else
            {
                callback(false);
            }
        }
    }
}
