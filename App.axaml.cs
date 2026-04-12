using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Models.Schedule;
using Nexus.Plugins.Contracts;
using Nexus.Plugins.Core;
using Nexus.Plugins.Services;
using Nexus.Services;
using Nexus.Services.Http;
using Nexus.Services.Widget;
using Nexus.ViewModels;
using Nexus.ViewModels.Pages;
using Nexus.Views;
using Nexus.Views.Pages;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Nexus
{
    public partial class App : Application
    {
        public static readonly string Version = UpdateService.CurrentVersion;

        private ConfigService? _configService;
        private ToastService? _toastService;
        private AuthService? _authService;
        private UpdateService? _updateService;
        private TrayService? _trayService;
        private SplashScreenViewModel? _splashViewModel;
        private WidgetService? _widgetService;
        private ScheduleService? _scheduleService;
        private PasswordService? _passwordService;
        private ProcessProtectionService? _processProtectionService;
        private MainView? _mainView;

        internal static PluginHost? PluginHostInstance { get; private set; }
        internal static WebSocketBridgeService? WSBridgeInstance { get; private set; }
        internal static PluginService? PluginServiceInstance { get; private set; }
        internal static PluginUIService? PluginUIServiceInstance { get; private set; }

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
                _toastService = new ToastService();
                _authService = new AuthService(_configService, _toastService);
                _updateService = new UpdateService(_configService, _toastService);
                _scheduleService = new ScheduleService(_configService, _toastService);
                _passwordService = new PasswordService();
                _passwordService.InitializeDefaultPassword();
                _processProtectionService = new ProcessProtectionService();

                EnsureAutoStartEnabled();
                _ = ConfigureWolAsync();

                System.Diagnostics.Debug.WriteLine($"[Nexus] 配置加载完成: IsBound={_configService.Config.IsBound}");

                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                desktop.Exit += OnApplicationExit;

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

        private void EnsureAutoStartEnabled()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    
                    var existingValue = key?.GetValue("Nexus");
                    if (existingValue == null)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key?.SetValue("Nexus", "\"" + exePath + "\"");
                            System.Diagnostics.Debug.WriteLine("[Nexus] 已自动启用开机自启");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Nexus] 设置开机自启失败: {ex.Message}");
            }
        }

        private async Task ConfigureWolAsync()
        {
            try
            {
                var wolConfigService = new WolConfigService();
                var result = await wolConfigService.ConfigureWolAsync();
                
                if (result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[Nexus] WOL配置成功: {result.Message}");
                    
                    var macAddress = wolConfigService.GetCurrentMacAddress();
                    if (!string.IsNullOrEmpty(macAddress))
                    {
                        _configService?.SetNetworkInfo(macAddress, null);
                        System.Diagnostics.Debug.WriteLine($"[Nexus] 已保存MAC地址: {macAddress}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Nexus] WOL配置跳过: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Nexus] WOL配置异常: {ex.Message}");
            }
        }

        private void OnApplicationExit(object? sender, EventArgs e)
        {
            _processProtectionService?.DisableProtection();
            _processProtectionService?.Dispose();
            PluginServiceInstance?.Dispose();
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
            splashScreen.Show();
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
            var mainWindowViewModel = new MainWindowViewModel(_configService!, _toastService!, _scheduleService!);
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

            mainWindowViewModel.RequestOpenScheduleSetup += (classId, className) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.Close();
                    ShowScheduleSetupWindow(desktop, classId, className);
                });
            };

            mainWindow.Closed += (s, e) =>
            {
                if (desktop.MainWindow == mainWindow && mainWindowViewModel.BindState != BindState.BindSuccess && mainWindowViewModel.BindState != BindState.ScheduleIncomplete)
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
            System.Diagnostics.Debug.WriteLine($"[Nexus] ShowScheduleSetupWindow: classId={classId}, className={className}");
            
            var viewModel = new ScheduleSetupViewModel(_scheduleService!, _configService!);
            var scheduleSetupWindow = new ScheduleSetupWindow
            {
                DataContext = viewModel
            };

            viewModel.SetupCompleted += () =>
            {
                System.Diagnostics.Debug.WriteLine($"[Nexus] SetupCompleted 事件触发");
                desktop.MainWindow = null;
                Dispatcher.UIThread.Post(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Nexus] SetupCompleted 处理: MainWindow={desktop.MainWindow?.GetType().Name ?? "null"}");
                    scheduleSetupWindow.Close();
                    ShowMainView(desktop, null, true, true);
                });
            };

            viewModel.RequestSkip += () =>
            {
                System.Diagnostics.Debug.WriteLine($"[Nexus] RequestSkip 事件触发");
                desktop.MainWindow = null;
                Dispatcher.UIThread.Post(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Nexus] RequestSkip 处理: MainWindow={desktop.MainWindow?.GetType().Name ?? "null"}");
                    scheduleSetupWindow.Close();
                    ShowMainView(desktop, null, true, true);
                });
            };

            scheduleSetupWindow.Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Nexus] ScheduleSetupWindow.Closed: MainWindow={desktop.MainWindow?.GetType().Name ?? "null"}");
                if (desktop.MainWindow == scheduleSetupWindow)
                {
                    System.Diagnostics.Debug.WriteLine($"[Nexus] Closed 事件触发 ShowMainView");
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
            
            var mainViewModel = new MainViewModel(_configService!, _authService!, _updateService!, powerControlService, wolService, _widgetService, _scheduleService!, _toastService!);
            _mainView = new MainView
            {
                DataContext = mainViewModel
            };

            mainViewModel.RequestLogout += () =>
            {
                _trayService?.Dispose();
                _trayService = null;
                _widgetService?.Stop();
                _widgetService = null;
                _mainView.Close();
                ShowSplashScreen(desktop);
            };

            if (registerTray)
            {
                System.Diagnostics.Debug.WriteLine("[Nexus] 准备初始化系统托盘...");
                _trayService = new TrayService();
                _trayService.Initialize(_mainView);

                _trayService.ShowWindowRequested += () =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _mainView.Show();
                        _mainView.WindowState = WindowState.Normal;
                        _mainView.Activate();
                    });
                };
                _trayService.ExitRequested += () =>
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await HandleExitRequest(desktop);
                    });
                };

                _mainView.Closing += async (s, e) =>
                {
                    if (_trayService != null)
                    {
                        e.Cancel = true;
                        _mainView.Hide();
                    }
                };
            }

            desktop.MainWindow = _mainView;

            if (showWindow)
            {
                _mainView.Show();
            }

            _processProtectionService?.EnableProtection();
            System.Diagnostics.Debug.WriteLine("[Nexus] 进程保护已启用");

            _ = InitializeWidgetAsync();
            _ = InitializePluginSystemAsync();

            closeWindow?.Close();
        }

        private async Task InitializePluginSystemAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Nexus] 正在初始化插件系统...");

                var hostServices = new ServiceCollection();
                hostServices.AddSingleton(_configService!);
                hostServices.AddSingleton(_toastService!);
                hostServices.AddSingleton(_authService!);

                var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalPlugins");
                var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Config");

                PluginHostInstance = new PluginHost(pluginDir, configDir, hostServices.BuildServiceProvider());
                WSBridgeInstance = new WebSocketBridgeService(PluginHostInstance);
                PluginServiceInstance = new PluginService(PluginHostInstance, WSBridgeInstance);
                PluginUIServiceInstance = new PluginUIService(PluginHostInstance);

                var appServices = new ServiceCollection();
                await PluginServiceInstance.InitializeAsync(appServices);

                PluginUIServiceInstance.CollectAllUIExtensions();

                System.Diagnostics.Debug.WriteLine($"[Nexus] 插件系统初始化完成，已加载 {PluginHostInstance.LoadedPlugins.Count} 个插件");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Nexus] 插件系统初始化失败: {ex.Message}");
                _toastService?.ShowError($"插件系统初始化失败: {ex.Message}");
            }
        }

        private async Task HandleExitRequest(IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (_passwordService == null || _mainView == null)
            {
                _trayService?.Dispose();
                _trayService = null;
                _widgetService?.Stop();
                _widgetService = null;
                _processProtectionService?.DisableProtection();
                desktop.Shutdown();
                return;
            }

            _mainView.Show();
            _mainView.WindowState = WindowState.Normal;
            _mainView.Activate();

            var (success, password) = await PasswordDialog.ShowDialogAsync(_mainView, "退出确认 - 请输入密码");

            if (success && !string.IsNullOrEmpty(password))
            {
                if (_passwordService.VerifyPassword(password))
                {
                    _trayService?.Dispose();
                    _trayService = null;
                    _widgetService?.Stop();
                    _widgetService = null;
                    _processProtectionService?.DisableProtection();
                    desktop.Shutdown();
                }
                else
                {
                    _toastService?.ShowError("密码错误，无法退出程序");
                    _mainView.Hide();
                }
            }
            else
            {
                _mainView.Hide();
            }
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
