using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Nexus.ViewModels
{
    public partial class SplashScreenViewModel : ViewModelBase
    {
        private bool _isNavigating = false;
        public bool IsNavigating
        {
            get => _isNavigating;
            set => SetProperty(ref _isNavigating, value);
        }

        private bool _hasError = false;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _statusMessage = "点击开始绑定";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _showCloseButton = false;
        public bool ShowCloseButton
        {
            get => _showCloseButton;
            set => SetProperty(ref _showCloseButton, value);
        }

        private bool _showStartButton = true;
        public bool ShowStartButton
        {
            get => _showStartButton;
            set => SetProperty(ref _showStartButton, value);
        }

        public ICommand StartCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand RetryCommand { get; }

        public event Action? NavigateToMainRequested;
        public event Action? CloseRequested;
        public event Action? RetryRequested;

        public SplashScreenViewModel()
        {
            StartCommand = new RelayCommand(OnStart, CanStart);
            CloseCommand = new RelayCommand(OnClose);
            RetryCommand = new RelayCommand(OnRetry);
        }

        private bool CanStart()
        {
            return !_isNavigating;
        }

        private void OnStart()
        {
            if (_isNavigating) return;
            
            IsNavigating = true;
            HasError = false;
            StatusMessage = "正在加载...";
            NavigateToMainRequested?.Invoke();
        }

        private void OnClose()
        {
            CloseRequested?.Invoke();
        }

        private void OnRetry()
        {
            HasError = false;
            IsNavigating = true;
            StatusMessage = "正在验证...";
            ShowCloseButton = false;
            RetryRequested?.Invoke();
        }

        public void SetLoadingState(string message)
        {
            IsNavigating = true;
            HasError = false;
            StatusMessage = message;
            ShowStartButton = false;
        }

        public void SetErrorState(string errorMessage)
        {
            IsNavigating = false;
            HasError = true;
            ErrorMessage = errorMessage;
            StatusMessage = "连接失败";
            ShowCloseButton = true;
            ShowStartButton = false;
        }

        public void SetSuccessState()
        {
            IsNavigating = false;
            HasError = false;
        }
    }
}
