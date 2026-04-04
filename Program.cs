using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Nexus.Views;

namespace Nexus
{
    internal sealed class Program
    {
        private static Mutex? _mutex;
        private const string MutexName = "Nexus_SingleInstance_Mutex";
        private static bool _isShuttingDown = false;

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                ShowAlreadyRunningMessage();
                return;
            }

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                if (!_isShuttingDown)
                {
                    ShowCustomError(ex.Message, ex.ToString());
                }
            }
            finally
            {
                try
                {
                    _mutex?.ReleaseMutex();
                    _mutex?.Dispose();
                }
                catch { }
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var message = exception?.Message ?? "未知错误";
            var stackTrace = exception?.ToString() ?? "";
            
            if (e.IsTerminating && !_isShuttingDown)
            {
                _isShuttingDown = true;
                ShowCustomError(message, stackTrace);
            }

            if (e.IsTerminating)
            {
                try
                {
                    _mutex?.ReleaseMutex();
                    _mutex?.Dispose();
                }
                catch { }
            }
        }

        private static void ShowAlreadyRunningMessage()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MessageBox(IntPtr.Zero, "程序已在运行中！", "Nexus", 0x40);
            }
            else
            {
                Console.WriteLine("程序已在运行中！");
            }
        }

        private static void ShowCustomError(string errorMessage, string stackTrace)
        {
            try
            {
                var builder = BuildAvaloniaApp();
                builder.SetupWithoutStarting();
                
                var errorWindow = new ErrorWindow(errorMessage, stackTrace);
                
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = errorWindow;
                    errorWindow.Show();
                    
                    var cts = new CancellationTokenSource();
                    errorWindow.Closed += (s, e) => cts.Cancel();
                    
                    Avalonia.Threading.Dispatcher.UIThread.MainLoop(cts.Token);
                }
                else
                {
                    ShowFallbackError(errorMessage, stackTrace);
                }
            }
            catch
            {
                ShowFallbackError(errorMessage, stackTrace);
            }
        }

        private static void ShowFallbackError(string errorMessage, string stackTrace)
        {
            var hint = "程序严重损坏，请重新运行安装程序或联系管理员处理";
            var fullMessage = $"{hint}\n\n错误信息：{errorMessage}\n\n堆栈跟踪：\n{stackTrace}";
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MessageBox(IntPtr.Zero, fullMessage, "Nexus - 程序错误", 0x10);
            }
            else
            {
                Console.WriteLine($"错误: {fullMessage}");
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, int options);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}