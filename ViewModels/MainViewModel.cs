using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Nexus.Models;
using Nexus.Services;
using Nexus.Views.Pages;

namespace Nexus.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly AuthService _authService;
        private readonly UpdateService _updateService;
        private DispatcherTimer? _updateCheckTimer;

        private object? _currentPage;
        public object? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private int _selectedNavigationItem;
        public int SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value))
                {
                    NavigateToPage(value);
                }
            }
        }

        private string _deviceStatus = "已连接";
        public string DeviceStatus
        {
            get => _deviceStatus;
            set => SetProperty(ref _deviceStatus, value);
        }

        private string _className = "";
        public string ClassName
        {
            get => _className;
            set => SetProperty(ref _className, value);
        }

        private string _deviceId = "";
        public string DeviceIdDisplay
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        public ICommand UnbindCommand { get; }

        public event Action? RequestLogout;

        public MainViewModel() : this(new ConfigService(), new AuthService(new ConfigService()), new UpdateService(new ConfigService()))
        {
        }

        public MainViewModel(ConfigService configService, AuthService authService, UpdateService updateService)
        {
            _configService = configService;
            _authService = authService;
            _updateService = updateService;

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Label = "概览", IconSymbol = Symbol.Home, Tag = "Dashboard" },
                new NavigationItem { Label = "设置", IconSymbol = Symbol.Setting, Tag = "Settings" },
                new NavigationItem { Label = "系统更新", IconSymbol = Symbol.Sync, Tag = "Update" },
                new NavigationItem { Label = "关于", IconSymbol = Symbol.Help, Tag = "About" }
            };

            UnbindCommand = new RelayCommand(OnUnbind);

            LoadBindInfo();
            _selectedNavigationItem = 0;
            NavigateToPage(0);
            
            StartUpdateCheck();
        }

        private void LoadBindInfo()
        {
            var config = _configService.Config;
            
            if (config.BindInfo != null)
            {
                ClassName = config.BindInfo.ClassName;
            }
            
            DeviceIdDisplay = config.DeviceId;
        }

        private void NavigateToPage(int index)
        {
            CurrentPage = index switch
            {
                0 => new DashboardPage(),
                1 => new SettingsPage(_configService, _authService),
                2 => new UpdatePage(_updateService),
                3 => new AboutPage(),
                _ => new DashboardPage()
            };
        }

        private void StartUpdateCheck()
        {
            if (_updateService.ShouldCheckForUpdate())
            {
                _ = CheckForUpdateAsync();
            }

            _updateCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(_updateService.UpdateConfig.CheckIntervalHours)
            };
            _updateCheckTimer.Tick += async (s, e) =>
            {
                await CheckForUpdateAsync();
            };
            _updateCheckTimer.Start();
        }

        private async Task CheckForUpdateAsync()
        {
            var updateInfo = await _updateService.CheckForUpdateAsync();
            if (updateInfo != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SelectedNavigationItem = 2;
                });
            }
        }

        private void OnUnbind()
        {
            _configService.ClearBindInfo();
            RequestLogout?.Invoke();
        }
    }

    public class NavigationItem
    {
        public string Label { get; set; } = "";
        public Symbol IconSymbol { get; set; }
        public string Tag { get; set; } = "";
    }
}
