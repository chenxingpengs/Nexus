using Avalonia;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nexus
{
    internal sealed class Program
    {
        private static Mutex? _mutex;
        private const string MutexName = "Nexus_SingleInstance_Mutex";

        [STAThread]
        public static void Main(string[] args)
        {
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
            finally
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, int options);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
