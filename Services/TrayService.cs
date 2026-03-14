using Avalonia.Controls;
using Avalonia.Platform;
using System;

namespace Nexus.Services
{
    public class TrayService : IDisposable
    {
        private TrayIcon? _trayIcon;
        private Window? _mainWindow;
        private bool _isDisposed;

        public event Action? ShowWindowRequested;
        public event Action? ExitRequested;

        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;

            if (_trayIcon != null) return;

            System.Diagnostics.Debug.WriteLine("[TrayService] 开始初始化托盘...");

            try
            {
                _trayIcon = new TrayIcon();

                try
                {
                    using var stream = AssetLoader.Open(new Uri("avares://Nexus/Assets/hqzx.png"));
                    if (stream != null)
                    {
                        _trayIcon.Icon = new WindowIcon(stream);
                        System.Diagnostics.Debug.WriteLine("[TrayService] 图标加载成功");
                    }
                }
                catch (Exception iconEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[TrayService] 加载图标失败: {iconEx.Message}");
                }

                var menu = new NativeMenu();

                var showItem = new NativeMenuItem("显示主窗口");
                showItem.Click += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[TrayService] 点击显示主窗口");
                    ShowWindowRequested?.Invoke();
                };
                menu.Add(showItem);

                menu.Add(new NativeMenuItemSeparator());

                var exitItem = new NativeMenuItem("退出");
                exitItem.Click += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[TrayService] 点击退出");
                    ExitRequested?.Invoke();
                };
                menu.Add(exitItem);

                _trayIcon.Menu = menu;
                _trayIcon.ToolTipText = "Nexus - 红旗中学智慧校园系统";
                _trayIcon.IsVisible = true;
                _trayIcon.Clicked += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[TrayService] 托盘图标被点击");
                    ShowWindowRequested?.Invoke();
                };

                System.Diagnostics.Debug.WriteLine($"[TrayService] 托盘已初始化, IsVisible={_trayIcon.IsVisible}, HasIcon={_trayIcon.Icon != null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrayService] 初始化失败: {ex.Message}");
            }
        }

        public void Show()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        public void Hide()
        {
            _mainWindow?.Hide();
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
            }
            _trayIcon = null;
        }
    }
}
