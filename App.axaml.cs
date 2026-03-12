using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Nexus.Services;
using Nexus.ViewModels;
using Nexus.Views;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nexus
{
    public partial class App : Application
    {
        public static readonly string Version = UpdateService.CurrentVersion;
        
        private ConfigService? _configService;
        private AuthService? _authService;
        private UpdateService? _updateService;
        private TrayService? _trayService;
        private SplashScreenViewModel? _splashViewModel;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine($"[Nexus] 应用启动 - 版本 {Version}");
            System.Diagnostics.Debug.WriteLine("========================================");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();
                
                _configService = new ConfigService();
                _authService = new AuthService(_configService);
                _updateService = new UpdateService(_configService);
                
                System.Diagnostics.Debug.WriteLine($"[Nexus] 配置加载完成: IsBound={_configService.Config.IsBound}");
                
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                
                if (_configService.Config.IsBound)
                {
                    ShowLoadingWindow(desktop);
                }
                else
                {
                    ShowSplashScreen(desktop);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private Window CreateLoadingWindow()
        {
            return new Window
            {
                Title = "Nexus",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                Content = new Panel
                {
                    Children =
                    {
                        new StackPanel
                        {
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Spacing = 16,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "Nexus",
                                    FontSize = 28,
                                    FontWeight = FontWeight.Bold,
                                    Foreground = new SolidColorBrush(Color.Parse("#1976D2")),
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                                },
                                new ProgressBar
                                {
                                    IsIndeterminate = true,
                                    Width = 200,
                                    Height = 4
                                },
                                new TextBlock
                                {
                                    Text = "正在启动...",
                                    FontSize = 14,
                                    Foreground = new SolidColorBrush(Color.Parse("#666666")),
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                                }
                            }
                        }
                    }
                }
            };
        }

        private void ShowLoadingWindow(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loadingWindow = CreateLoadingWindow();
            desktop.MainWindow = loadingWindow;
            loadingWindow.Show();
            
            _ = VerifyAndNavigateAsync(desktop, loadingWindow);
        }

        private void ShowSplashScreen(IClassicDesktopStyleApplicationLifetime desktop)
        {
            _splashViewModel = new SplashScreenViewModel();
            var splashScreen = new SplashScreen
            {
                DataContext = _splashViewModel
            };
            
            _splashViewModel.NavigateToMainRequested += () =>
            {
                ShowBindWindow(desktop, splashScreen);
            };
            
            _splashViewModel.CloseRequested += () =>
            {
                splashScreen.Close();
                desktop.Shutdown();
            };
            
            desktop.MainWindow = splashScreen;
        }

        private async Task VerifyAndNavigateAsync(IClassicDesktopStyleApplicationLifetime desktop, Window loadingWindow)
        {
            System.Diagnostics.Debug.WriteLine("[Nexus] 开始验证设备...");
            
            var startTime = DateTime.Now;
            var (success, errorMsg) = await _authService!.VerifyDeviceAsync();
            System.Diagnostics.Debug.WriteLine($"[Nexus] 验证结果: success={success}, errorMsg={errorMsg}");
            
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            if (elapsed < 1500)
            {
                await Task.Delay((int)(1500 - elapsed));
            }
            
            if (success)
            {
                loadingWindow.Close();
                ShowMainView(desktop, null, true, false);
            }
            else
            {
                loadingWindow.Close();
                ShowSplashScreenWithError(desktop, errorMsg ?? "验证失败");
            }
        }

        private void ShowSplashScreenWithError(IClassicDesktopStyleApplicationLifetime desktop, string errorMessage)
        {
            _splashViewModel = new SplashScreenViewModel();
            var splashScreen = new SplashScreen
            {
                DataContext = _splashViewModel
            };
            
            _splashViewModel.SetErrorState(errorMessage);
            
            _splashViewModel.CloseRequested += () =>
            {
                splashScreen.Close();
                desktop.Shutdown();
            };
            
            _splashViewModel.RetryRequested += () =>
            {
                splashScreen.Close();
                ShowLoadingWindow(desktop);
            };
            
            desktop.MainWindow = splashScreen;
        }

        private void ShowBindWindow(IClassicDesktopStyleApplicationLifetime desktop, Window? closeWindow = null)
        {
            var mainWindowViewModel = new MainWindowViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
            
            mainWindowViewModel.BindSuccessAndReady += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.Close();
                    ShowMainView(desktop, null, true, true);
                });
            };
            
            mainWindow.Closed += (s, e) =>
            {
                if (desktop.MainWindow == mainWindow && mainWindowViewModel.BindState != BindState.BindSuccess)
                {
                    desktop.Shutdown();
                }
            };
            
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            closeWindow?.Close();
        }

        private void ShowMainView(IClassicDesktopStyleApplicationLifetime desktop, Window? closeWindow, bool registerTray, bool showWindow = true)
        {
            System.Diagnostics.Debug.WriteLine($"[Nexus] ShowMainView: registerTray={registerTray}, showWindow={showWindow}");
            
            var mainViewModel = new MainViewModel(_configService!, _authService!, _updateService!);
            var mainView = new MainView
            {
                DataContext = mainViewModel
            };

            mainViewModel.RequestLogout += () =>
            {
                _trayService?.Dispose();
                _trayService = null;
                mainView.Close();
                ShowSplashScreen(desktop);
            };

            if (registerTray)
            {
                System.Diagnostics.Debug.WriteLine("[Nexus] 准备初始化系统托盘...");
                _trayService = new TrayService();
                _trayService.Initialize(mainView);
                
                _trayService.ShowWindowRequested += () =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        mainView.Show();
                        mainView.WindowState = WindowState.Normal;
                        mainView.Activate();
                    });
                };
                _trayService.ExitRequested += () =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _trayService?.Dispose();
                        _trayService = null;
                        desktop.Shutdown();
                    });
                };

                mainView.Closing += (s, e) =>
                {
                    if (_trayService != null)
                    {
                        e.Cancel = true;
                        mainView.Hide();
                    }
                };
            }

            desktop.MainWindow = mainView;
            
            if (showWindow)
            {
                mainView.Show();
            }
            
            closeWindow?.Close();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
