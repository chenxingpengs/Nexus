using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services
{
    public class TrayService : IDisposable
    {
        private TrayIcon? _trayIcon;
        private Window? _mainWindow;
        private bool _isDisposed;
        private bool _isFlashing;
        private CancellationTokenSource? _flashCts;
        private WindowIcon? _normalIcon;
        private WindowIcon? _emptyIcon;
        private readonly object _flashLock = new();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_CAPTION = 1;
        private const uint FLASHW_TRAY = 2;
        private const uint FLASHW_TIMER = 4;
        private const uint FLASHW_TIMERNOFG = 12;
        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_TIMERNOFG_ALL = FLASHW_CAPTION | FLASHW_TRAY | FLASHW_TIMER;

        public event Action? ShowWindowRequested;
        public event Action? ExitRequested;
        public event Action? ShowMeetingRequested;
        public event Action? ShowChatRequested;
        public event Action? ShowSettingsRequested;

        public void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;

            if (_trayIcon != null) return;

            Debug.WriteLine("[TrayService] 开始初始化托盘...");

            try
            {
                _trayIcon = new TrayIcon();

                try
                {
                    using var stream = AssetLoader.Open(new Uri("avares://Nexus/Assets/hqzx.png"));
                    if (stream != null)
                    {
                        _normalIcon = new WindowIcon(stream);
                        _trayIcon.Icon = _normalIcon;
                        Debug.WriteLine("[TrayService] 图标加载成功");
                    }
                }
                catch (Exception iconEx)
                {
                    Debug.WriteLine($"[TrayService] 加载图标失败: {iconEx.Message}");
                }
                
                try
                {
                    _emptyIcon = CreateEmptyIcon();
                    Debug.WriteLine("[TrayService] 空白图标创建成功");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrayService] 创建空白图标失败: {ex.Message}");
                }

                var menu = new NativeMenu();

                var showItem = new NativeMenuItem("显示主窗口");
                showItem.Click += (s, e) =>
                {
                    Debug.WriteLine("[TrayService] 点击显示主窗口");
                    StopFlashing();
                    ShowWindowRequested?.Invoke();
                };
                menu.Add(showItem);

                menu.Add(new NativeMenuItemSeparator());

                var meetingItem = new NativeMenuItem("会议");
                meetingItem.Click += (s, e) =>
                {
                    Debug.WriteLine("[TrayService] 点击会议");
                    StopFlashing();
                    ShowMeetingRequested?.Invoke();
                };
                menu.Add(meetingItem);

                var chatItem = new NativeMenuItem("聊天");
                chatItem.Click += (s, e) =>
                {
                    Debug.WriteLine("[TrayService] 点击聊天");
                    StopFlashing();
                    ShowChatRequested?.Invoke();
                };
                menu.Add(chatItem);

                menu.Add(new NativeMenuItemSeparator());

                var settingsItem = new NativeMenuItem("设置");
                settingsItem.Click += (s, e) =>
                {
                    Debug.WriteLine("[TrayService] 点击设置");
                    StopFlashing();
                    ShowSettingsRequested?.Invoke();
                };
                menu.Add(settingsItem);

                menu.Add(new NativeMenuItemSeparator());

                var exitItem = new NativeMenuItem("退出");
                exitItem.Click += (s, e) =>
                {
                    Debug.WriteLine("[TrayService] 点击退出");
                    ExitRequested?.Invoke();
                };
                menu.Add(exitItem);

                _trayIcon.Menu = menu;
                _trayIcon.ToolTipText = "Nexus - 红旗中学智慧校园系统";
                _trayIcon.IsVisible = true;
                _trayIcon.Clicked += (s, e) =>
                {
                    Debug.WriteLine("[TrayService] 托盘图标被点击");
                    StopFlashing();
                    ShowWindowRequested?.Invoke();
                };

                Debug.WriteLine($"[TrayService] 托盘已初始化, IsVisible={_trayIcon.IsVisible}, HasIcon={_trayIcon.Icon != null}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayService] 初始化失败: {ex.Message}");
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

        public void StartFlashing()
        {
            lock (_flashLock)
            {
                if (_isFlashing)
                {
                    Debug.WriteLine($"[TrayService] 已经在闪烁中，跳过");
                    return;
                }

                _isFlashing = true;
                _flashCts = new CancellationTokenSource();
                var token = _flashCts.Token;

                Debug.WriteLine("[TrayService] 开始图标闪烁");

                StartTaskbarFlashing();

                if (_trayIcon != null && _emptyIcon != null && _normalIcon != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!token.IsCancellationRequested && _isFlashing)
                            {
                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    if (_trayIcon != null && !token.IsCancellationRequested)
                                    {
                                        _trayIcon.Icon = _emptyIcon;
                                    }
                                });

                                await Task.Delay(500, token);

                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    if (_trayIcon != null && !token.IsCancellationRequested)
                                    {
                                        _trayIcon.Icon = _normalIcon;
                                    }
                                });

                                await Task.Delay(500, token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine("[TrayService] 闪烁任务被取消");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[TrayService] 闪烁任务异常: {ex.Message}");
                        }
                    }, token);
                }
            }
        }

        private void StartTaskbarFlashing()
        {
            if (_mainWindow == null)
            {
                Debug.WriteLine("[TrayService] 无法闪烁任务栏: mainWindow 为空");
                return;
            }

            try
            {
                var platformHandle = _mainWindow.TryGetPlatformHandle();
                if (platformHandle != null)
                {
                    var hwnd = platformHandle.Handle;
                    Debug.WriteLine($"[TrayService] 开始闪烁任务栏图标, hwnd={hwnd}");

                    var fInfo = new FLASHWINFO
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                        hwnd = hwnd,
                        dwFlags = FLASHW_TIMERNOFG_ALL,
                        uCount = uint.MaxValue,
                        dwTimeout = 0
                    };

                    var result = FlashWindowEx(ref fInfo);
                    Debug.WriteLine($"[TrayService] FlashWindowEx 结果: {result}");

                    SetTaskbarProgressState(hwnd, TaskbarProgressState.Normal);
                }
                else
                {
                    Debug.WriteLine("[TrayService] 无法获取窗口句柄");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayService] 闪烁任务栏失败: {ex.Message}");
            }
        }

        private void StopTaskbarFlashing()
        {
            if (_mainWindow == null) return;

            try
            {
                var platformHandle = _mainWindow.TryGetPlatformHandle();
                if (platformHandle != null)
                {
                    var hwnd = platformHandle.Handle;
                    var fInfo = new FLASHWINFO
                    {
                        cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                        hwnd = hwnd,
                        dwFlags = FLASHW_STOP,
                        uCount = 0,
                        dwTimeout = 0
                    };

                    FlashWindowEx(ref fInfo);
                    Debug.WriteLine("[TrayService] 停止任务栏闪烁");

                    SetTaskbarProgressState(hwnd, TaskbarProgressState.NoProgress);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayService] 停止任务栏闪烁失败: {ex.Message}");
            }
        }

        public void StopFlashing()
        {
            lock (_flashLock)
            {
                if (!_isFlashing) return;

                _isFlashing = false;
                _flashCts?.Cancel();
                _flashCts?.Dispose();
                _flashCts = null;

                StopTaskbarFlashing();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_trayIcon != null && _normalIcon != null)
                    {
                        _trayIcon.Icon = _normalIcon;
                    }
                });

                Debug.WriteLine("[TrayService] 停止图标闪烁");
            }
        }

        public bool IsFlashing => _isFlashing;

        private enum TaskbarProgressState
        {
            NoProgress = 0,
            Indeterminate = 1,
            Normal = 2,
            Error = 4,
            Paused = 8
        }

        [ComImport]
        [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            [PreserveSig]
            int SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
            [PreserveSig]
            int SetProgressState(IntPtr hwnd, TaskbarProgressState state);
        }

        private static void SetTaskbarProgressState(IntPtr hwnd, TaskbarProgressState state)
        {
            try
            {
                var taskbar = (ITaskbarList3)new CoTaskbarList();
                taskbar.HrInit();
                taskbar.SetProgressState(hwnd, state);
                Marshal.ReleaseComObject(taskbar);
                Debug.WriteLine($"[TrayService] 设置任务栏状态: {state}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayService] 设置任务栏状态失败: {ex.Message}");
            }
        }

        [ComImport]
        [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class CoTaskbarList
        {
        }

        private WindowIcon CreateEmptyIcon()
        {
            var width = 16;
            var height = 16;
            var stride = width * 4;
            var pixels = new byte[height * stride];
            
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = 0;
            }

            using var memoryStream = new System.IO.MemoryStream();
            
            WritePng(memoryStream, pixels, width, height, stride);
            memoryStream.Position = 0;
            
            return new WindowIcon(memoryStream);
        }

        private void WritePng(System.IO.MemoryStream stream, byte[] pixels, int width, int height, int stride)
        {
            using var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.ASCII, true);
            
            writer.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            
            WriteChunk(writer, "IHDR", GetIhdrData(width, height));
            WriteChunk(writer, "IDAT", GetIdatData(pixels, width, height, stride));
            WriteChunk(writer, "IEND", new byte[0]);
        }

        private byte[] GetIhdrData(int width, int height)
        {
            var data = new byte[13];
            var widthBytes = BitConverter.GetBytes(width);
            var heightBytes = BitConverter.GetBytes(height);
            
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(widthBytes);
                Array.Reverse(heightBytes);
            }
            
            Array.Copy(widthBytes, 0, data, 0, 4);
            Array.Copy(heightBytes, 0, data, 4, 4);
            
            data[8] = 8;
            data[9] = 6;
            data[10] = 0;
            data[11] = 0;
            data[12] = 0;
            
            return data;
        }

        private byte[] GetIdatData(byte[] pixels, int width, int height, int stride)
        {
            using var memoryStream = new System.IO.MemoryStream();
            using var deflateStream = new System.IO.Compression.DeflateStream(memoryStream, System.IO.Compression.CompressionLevel.Optimal, true);
            
            for (int y = 0; y < height; y++)
            {
                deflateStream.WriteByte(0);
                var rowStart = y * stride;
                for (int x = 0; x < stride; x++)
                {
                    deflateStream.WriteByte(pixels[rowStart + x]);
                }
            }
            
            deflateStream.Flush();
            memoryStream.Position = 0;
            return memoryStream.ToArray();
        }

        private void WriteChunk(System.IO.BinaryWriter writer, string type, byte[] data)
        {
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            
            var lengthBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
            writer.Write(lengthBytes);
            
            writer.Write(typeBytes);
            writer.Write(data);
            
            var crc = CalculateCrc(typeBytes, data);
            var crcBytes = BitConverter.GetBytes(crc);
            if (BitConverter.IsLittleEndian) Array.Reverse(crcBytes);
            writer.Write(crcBytes);
        }

        private uint CalculateCrc(byte[] type, byte[] data)
        {
            var combined = new byte[type.Length + data.Length];
            Array.Copy(type, 0, combined, 0, type.Length);
            Array.Copy(data, 0, combined, type.Length, data.Length);
            
            return Crc32(combined);
        }

        private uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            var table = GetCrc32Table();
            
            foreach (byte b in data)
            {
                crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            
            return ~crc;
        }

        private static uint[]? _crc32Table;
        private static uint[] GetCrc32Table()
        {
            if (_crc32Table != null) return _crc32Table;
            
            _crc32Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ 0xEDB88320;
                    else
                        crc >>= 1;
                }
                _crc32Table[i] = crc;
            }
            return _crc32Table;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            
            StopFlashing();
            
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
            }
            _trayIcon = null;
            _normalIcon = null;
            _emptyIcon = null;
        }
    }
}
