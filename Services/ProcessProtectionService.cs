using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services
{
    public class ProcessProtectionService : IDisposable
    {
        private const int PROCESS_TERMINATE = 0x0001;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_SET_INFORMATION = 0x0200;

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref int processInformation,
            int processInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessShutdownParameters(
            uint dwLevel,
            uint dwFlags);

        private const int ProcessBreakOnTermination = 29;
        private const int ShutdownNoQuit = 0x00000100;

        private bool _isProtected;
        private bool _isDisposed;
        private CancellationTokenSource? _monitorCts;
        private Task? _monitorTask;

        public event Action<string>? ProtectionEvent;

        public ProcessProtectionService()
        {
            System.Diagnostics.Debug.WriteLine("[ProcessProtection] 服务已创建");
        }

        public bool EnableProtection()
        {
            if (_isProtected) return true;

            try
            {
                EnableCriticalProcess();
                SetHighShutdownPriority();
                
                _isProtected = true;
                StartMonitor();
                
                ProtectionEvent?.Invoke("进程保护已启用");
                System.Diagnostics.Debug.WriteLine("[ProcessProtection] 进程保护已启用");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessProtection] 启用保护失败: {ex.Message}");
                ProtectionEvent?.Invoke($"启用保护失败: {ex.Message}");
                return false;
            }
        }

        private void EnableCriticalProcess()
        {
            try
            {
                var processHandle = GetCurrentProcess();
                int isCritical = 1;
                
                var result = NtSetInformationProcess(
                    processHandle,
                    ProcessBreakOnTermination,
                    ref isCritical,
                    sizeof(int));

                if (result != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProcessProtection] NtSetInformationProcess 返回: {result}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessProtection] EnableCriticalProcess 异常: {ex.Message}");
            }
        }

        private void SetHighShutdownPriority()
        {
            try
            {
                SetProcessShutdownParameters(0x3FF, ShutdownNoQuit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessProtection] SetHighShutdownPriority 异常: {ex.Message}");
            }
        }

        private void StartMonitor()
        {
            _monitorCts = new CancellationTokenSource();
            _monitorTask = MonitorProcessAsync(_monitorCts.Token);
        }

        private async Task MonitorProcessAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, cancellationToken);
                    
                    var currentProcess = Process.GetCurrentProcess();
                    if (currentProcess.Responding)
                    {
                        System.Diagnostics.Debug.WriteLine("[ProcessProtection] 进程运行正常");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProcessProtection] 监控异常: {ex.Message}");
                }
            }
        }

        public void DisableProtection()
        {
            if (!_isProtected) return;

            try
            {
                DisableCriticalProcess();
                
                _monitorCts?.Cancel();
                _monitorCts?.Dispose();
                _monitorCts = null;
                
                _isProtected = false;
                ProtectionEvent?.Invoke("进程保护已禁用");
                System.Diagnostics.Debug.WriteLine("[ProcessProtection] 进程保护已禁用");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessProtection] 禁用保护失败: {ex.Message}");
            }
        }

        private void DisableCriticalProcess()
        {
            try
            {
                var processHandle = GetCurrentProcess();
                int isCritical = 0;
                
                NtSetInformationProcess(
                    processHandle,
                    ProcessBreakOnTermination,
                    ref isCritical,
                    sizeof(int));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessProtection] DisableCriticalProcess 异常: {ex.Message}");
            }
        }

        public bool IsProtected => _isProtected;

        public void Dispose()
        {
            if (_isDisposed) return;

            DisableProtection();
            _isDisposed = true;
            
            System.Diagnostics.Debug.WriteLine("[ProcessProtection] 服务已释放");
            GC.SuppressFinalize(this);
        }
    }
}
