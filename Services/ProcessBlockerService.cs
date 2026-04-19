using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;

namespace Nexus.Services
{
    public class ProcessBlockerService : IDisposable
    {
        private static readonly HashSet<string> BlockedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "WeGame",
            "WeGameMain",
            "steam",
            "SteamService"
        };

        private ManagementEventWatcher? _processWatcher;
        private Timer? _pollingTimer;
        private bool _isRunning;
        private bool _isDisposed;

        public event Action<string>? LogEvent;

        public ProcessBlockerService()
        {
            Debug.WriteLine("[ProcessBlocker] 服务已创建");
        }

        public void Start()
        {
            if (_isRunning || _isDisposed) return;

            try
            {
                _isRunning = true;

                StartWmiWatcher();

                StartPolling();

                LogEvent?.Invoke("进程拦截服务已启动");
                Debug.WriteLine("[ProcessBlocker] 进程拦截服务已启动");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessBlocker] 启动失败: {ex.Message}");
                LogEvent?.Invoke($"进程拦截服务启动失败: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                _isRunning = false;

                StopWmiWatcher();

                StopPolling();

                Debug.WriteLine("[ProcessBlocker] 进程拦截服务已停止");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessBlocker] 停止失败: {ex.Message}");
            }
        }

        private void StartWmiWatcher()
        {
            try
            {
                var query = new WqlEventQuery(
                    "SELECT * FROM __InstanceCreationEvent WITHIN 1 " +
                    "WHERE TargetInstance ISA 'Win32_Process'");

                _processWatcher = new ManagementEventWatcher(query);
                _processWatcher.EventArrived += OnProcessCreated;
                _processWatcher.Start();
                Debug.WriteLine("[ProcessBlocker] WMI 监控已启动");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessBlocker] WMI 监控启动失败: {ex.Message}");
            }
        }

        private void StopWmiWatcher()
        {
            if (_processWatcher != null)
            {
                try
                {
                    _processWatcher.EventArrived -= OnProcessCreated;
                    _processWatcher.Stop();
                    _processWatcher.Dispose();
                    _processWatcher = null;
                    Debug.WriteLine("[ProcessBlocker] WMI 监控已停止");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProcessBlocker] WMI 监控停止失败: {ex.Message}");
                }
            }
        }

        private void StartPolling()
        {
            _pollingTimer = new Timer(_ => CheckAndTerminateAll(), null,
                TimeSpan.Zero, TimeSpan.FromSeconds(5));
            Debug.WriteLine("[ProcessBlocker] 轮询监控已启动");
        }

        private void StopPolling()
        {
            if (_pollingTimer != null)
            {
                try
                {
                    _pollingTimer.Dispose();
                    _pollingTimer = null;
                    Debug.WriteLine("[ProcessBlocker] 轮询监控已停止");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProcessBlocker] 轮询监控停止失败: {ex.Message}");
                }
            }
        }

        private void OnProcessCreated(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                var processName = process["Name"]?.ToString() ?? "";

                if (IsBlockedProcess(processName))
                {
                    Debug.WriteLine($"[ProcessBlocker] 检测到目标进程启动: {processName}");
                    TryTerminateProcess(processName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessBlocker] 处理进程事件失败: {ex.Message}");
            }
        }

        private void CheckAndTerminateAll()
        {
            foreach (var processName in BlockedProcesses)
            {
                TryTerminateProcess(processName);
            }
        }

        private bool IsBlockedProcess(string processName)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(processName);
            return BlockedProcesses.Contains(nameWithoutExt);
        }

        private bool TryTerminateProcess(string processName)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(processName);
            Process[] processes;

            try
            {
                processes = Process.GetProcessesByName(nameWithoutExt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessBlocker] 获取进程列表失败: {processName} - {ex.Message}");
                return false;
            }

            if (processes.Length == 0)
            {
                return false;
            }

            bool anyTerminated = false;
            foreach (var proc in processes)
            {
                try
                {
                    var pid = proc.Id;
                    proc.Kill();
                    Debug.WriteLine($"[ProcessBlocker] 已终止进程: {processName} (PID: {pid})");
                    anyTerminated = true;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    Debug.WriteLine($"[ProcessBlocker] 权限不足，无法终止进程: {processName} - {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProcessBlocker] 终止进程失败: {processName} - {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return anyTerminated;
        }

        public bool IsRunning => _isRunning;

        public void Dispose()
        {
            if (_isDisposed) return;

            Stop();

            _isDisposed = true;
            Debug.WriteLine("[ProcessBlocker] 服务已释放");
            GC.SuppressFinalize(this);
        }
    }
}
