using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Nexus.Models.Schedule;
using Nexus.Services;
using Nexus.Services.Widget;
using Nexus.ViewModels;
using Nexus.ViewModels.Pages;
using Nexus.Views;
using Nexus.Views.Pages;
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
        private WidgetService? _widgetService;
        private ScheduleService? _scheduleService;

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
                _scheduleService = new ScheduleService(_configService);

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

            _splashViewModel.NavigateToMainRequested += () =>
            {
                ShowBindWindow(desktop, splashScreen);
            };

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
            splashScreen.Show();
        }

        private void ShowBindWindow(IClassicDesktopStyleApplicationLifetime desktop, Window? closeWindow = null)
        {
            var mainWindowViewModel = new MainWindowViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            mainWindowViewModel.BindSuccessAndReady += async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    mainWindow.Close();
                    
                    var classId = _configService?.Config.BindInfo?.ClassId ?? 0;
                    var className = _configService?.Config.BindInfo?.ClassName ?? "";
                    
                    if (classId > 0 && _scheduleService != null)
                    {
                        var completeness = await _scheduleService.CheckCompletenessAsync(classId);
                        
                        if (completeness != null && !completeness.IsComplete)
                        {
                            ShowScheduleSetupWindow(desktop, classId, className);
                        }
                        else
                        {
                            ShowMainView(desktop, null, true, true);
                        }
                    }
                    else
                    {
                        ShowMainView(desktop, null, true, true);
                    }
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

        private void ShowScheduleSetupWindow(IClassicDesktopStyleApplicationLifetime desktop, int classId, string className)
        {
            var viewModel = new ScheduleSetupViewModel(_scheduleService!, _configService!);
            var scheduleSetupWindow = new ScheduleSetupWindow
            {
                DataContext = viewModel
            };

            viewModel.SetupCompleted += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    scheduleSetupWindow.Close();
                    ShowMainView(desktop, null, true, true);
                });
            };

            viewModel.RequestSkip += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    scheduleSetupWindow.Close();
                    ShowMainView(desktop, null, true, true);
                });
            };

            scheduleSetupWindow.Closed += (s, e) =>
            {
                if (desktop.MainWindow == scheduleSetupWindow)
                {
                    ShowMainView(desktop, null, true, true);
                }
            };

            desktop.MainWindow = scheduleSetupWindow;
            scheduleSetupWindow.Show();
            
            _ = viewModel.InitializeAsync(classId, className);
        }

        private void ShowMainView(IClassicDesktopStyleApplicationLifetime desktop, Window? closeWindow, bool registerTray, bool showWindow = true)
        {
            System.Diagnostics.Debug.WriteLine($"[Nexus] ShowMainView: registerTray={registerTray}, showWindow={showWindow}");

            var powerControlService = new PowerControlService();
            var wolService = new WolService();
            _widgetService = new WidgetService(_configService!);
            
            var mainViewModel = new MainViewModel(_configService!, _authService!, _updateService!, powerControlService, wolService, _widgetService, _scheduleService!);
            var mainView = new MainView
            {
                DataContext = mainViewModel
            };

            mainViewModel.RequestLogout += () =>
            {
                _trayService?.Dispose();
                _trayService = null;
                _widgetService?.Stop();
                _widgetService = null;
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
                        _widgetService?.Stop();
                        _widgetService = null;
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

            _ = InitializeWidgetAsync();

            closeWindow?.Close();
        }

        private async Task InitializeWidgetAsync()
        {
            if (_widgetService == null) return;

            await _widgetService.InitializeAsync();

            var widgetConfig = _widgetService.GetConfig();
            if (widgetConfig.IsEnabled)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _widgetService.ShowWidget();
                });
            }
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
