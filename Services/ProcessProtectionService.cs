using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Services
{
    public class ProcessProtectionService : IDisposable
    {
        #region Windows API

        private const int PROCESS_TERMINATE = 0x0001;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_SET_INFORMATION = 0x0200;
        private const int READ_CONTROL = 0x00020000;
        private const int WRITE_DAC = 0x00040000;

        private const int SE_PROCESS = 6;
        private const int DACL_SECURITY_INFORMATION = 0x00000004;
        private const uint PROTECTED_DACL_SECURITY_INFORMATION = 0x80000000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetKernelObjectSecurity(
            IntPtr handle,
            int securityInformation,
            IntPtr pSecurityDescriptor,
            int nLength,
            out int lpnLengthNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetKernelObjectSecurity(
            IntPtr handle,
            int securityInformation,
            IntPtr pSecurityDescriptor);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string stringSecurityDescriptor,
            int stringSDRevision,
            out IntPtr securityDescriptor,
            out int securityDescriptorLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool InitializeAcl(
            IntPtr pAcl,
            int nAclLength,
            int dwAclRevision);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetAclInformation(
            IntPtr pAcl,
            IntPtr pAclInformation,
            int nAclInformationLength,
            int dwAclInformationClass);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetAce(
            IntPtr pAcl,
            int dwAceIndex,
            out IntPtr pAce);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AddAce(
            IntPtr pAcl,
            int dwAceRevision,
            int dwStartingAceIndex,
            IntPtr pAceList,
            int nAceListLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AddAccessAllowedAce(
            IntPtr pAcl,
            int dwAceRevision,
            int accessMask,
            IntPtr pSid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AddAccessDeniedAce(
            IntPtr pAcl,
            int dwAceRevision,
            int accessMask,
            IntPtr pSid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetSecurityDescriptorDacl(
            IntPtr pSecurityDescriptor,
            out bool lpbDaclPresent,
            out IntPtr pDacl,
            out bool lpbDaclDefaulted);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetSecurityDescriptorDacl(
            IntPtr pSecurityDescriptor,
            bool bDaclPresent,
            IntPtr pDacl,
            bool bDaclDefaulted);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool InitializeSecurityDescriptor(
            IntPtr pSecurityDescriptor,
            int dwRevision);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetSecurityDescriptorOwner(
            IntPtr pSecurityDescriptor,
            out IntPtr pOwner,
            out bool lpbOwnerDefaulted);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetSecurityDescriptorOwner(
            IntPtr pSecurityDescriptor,
            IntPtr pOwner,
            bool bOwnerDefaulted);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AllocateAndInitializeSid(
            IntPtr pIdentifierAuthority,
            byte nSubAuthorityCount,
            int nSubAuthority0,
            int nSubAuthority1,
            int nSubAuthority2,
            int nSubAuthority3,
            int nSubAuthority4,
            int nSubAuthority5,
            int nSubAuthority6,
            int nSubAuthority7,
            out IntPtr pSid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool FreeSid(IntPtr pSid);

        [StructLayout(LayoutKind.Sequential)]
        private struct SID_IDENTIFIER_AUTHORITY
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] Value;
        }

        private static readonly IntPtr SECURITY_WORLD_SID_AUTHORITY = GetWorldSidAuthority();

        private static IntPtr GetWorldSidAuthority()
        {
            var auth = new SID_IDENTIFIER_AUTHORITY();
            auth.Value = new byte[] { 0, 0, 0, 0, 0, 1 };
            return Marshal.AllocHGlobal(Marshal.SizeOf(auth));
        }

        #endregion

        #region Critical Process API

        private const int SE_DEBUG_PRIVILEGE = 20;
        private const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const int TOKEN_QUERY = 0x0008;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            uint DesiredAccess,
            out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(
            string? lpSystemName,
            string lpName,
            ref LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlSetProcessIsCritical(
            [MarshalAs(UnmanagedType.Bool)] bool bNewValue,
            [MarshalAs(UnmanagedType.Bool)] out bool bOldValue,
            [MarshalAs(UnmanagedType.Bool)] bool bNeedScb);

        #endregion

        #region Shutdown Detection

        private const int WM_QUERYENDSESSION = 0x0011;
        private const int WM_ENDSESSION = 0x0016;
        private const int CTRL_SHUTDOWN_EVENT = 6;
        private const int CTRL_LOGOFF_EVENT = 5;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessShutdownParameters(
            uint dwLevel,
            uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool RegisterApplicationRestart(
            string? pwzCommandline,
            int dwFlags);

        private delegate bool ConsoleCtrlHandlerDelegate(int dwCtrlType);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(
            ConsoleCtrlHandlerDelegate? handler,
            bool add);

        private static ConsoleCtrlHandlerDelegate? _consoleCtrlHandler;

        #endregion

        private bool _isProtected;
        private bool _isCritical;
        private bool _isDisposed;
        private CancellationTokenSource? _monitorCts;
        private Task? _monitorTask;
        private IntPtr _originalSd = IntPtr.Zero;
        private int _originalSdLength = 0;

        public event Action<string>? ProtectionEvent;

        public ProcessProtectionService()
        {
            Debug.WriteLine("[ProcessProtection] 服务已创建");
            RegisterShutdownHandler();
        }

        private void RegisterShutdownHandler()
        {
            try
            {
                SetProcessShutdownParameters(0x3FF, 0);
                Debug.WriteLine("[ProcessProtection] 已设置关机优先级");

                _consoleCtrlHandler = new ConsoleCtrlHandlerDelegate(ConsoleCtrlHandler);
                SetConsoleCtrlHandler(_consoleCtrlHandler, true);
                Debug.WriteLine("[ProcessProtection] 已注册控制台关机处理器");

                Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
                Debug.WriteLine("[ProcessProtection] 已注册 SessionEnding 事件");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] 注册关机处理器失败: {ex.Message}");
            }
        }

        private bool ConsoleCtrlHandler(int dwCtrlType)
        {
            Debug.WriteLine($"[ProcessProtection] ConsoleCtrlHandler 触发: dwCtrlType={dwCtrlType}");

            if (dwCtrlType == CTRL_SHUTDOWN_EVENT || dwCtrlType == CTRL_LOGOFF_EVENT)
            {
                Debug.WriteLine("[ProcessProtection] 检测到关机/注销事件，正在取消关键进程状态...");
                DisableCriticalProcessFast();
                return true;
            }

            return false;
        }

        private void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            Debug.WriteLine($"[ProcessProtection] SessionEnding 事件触发: {e.Reason}");
            DisableCriticalProcessFast();
        }

        private void DisableCriticalProcessFast()
        {
            if (!_isCritical) return;

            try
            {
                EnableSeDebugPrivilege();
                bool oldValue;
                int result = RtlSetProcessIsCritical(false, out oldValue, false);

                if (result >= 0)
                {
                    _isCritical = false;
                    Debug.WriteLine("[ProcessProtection] 关键进程状态已快速取消");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] 快速取消关键进程失败: {ex.Message}");
            }
        }

        public bool EnableProtection()
        {
            if (_isProtected) return true;

            try
            {
                bool daclSuccess = EnableDaclProtection();
                bool criticalSuccess = EnableCriticalProcess();

                if (daclSuccess || criticalSuccess)
                {
                    _isProtected = true;
                    StartMonitor();
                    
                    string mode = "";
                    if (criticalSuccess) mode += "关键进程";
                    if (daclSuccess && criticalSuccess) mode += " + ";
                    if (daclSuccess) mode += "DACL保护";
                    
                    ProtectionEvent?.Invoke($"进程保护已启用（{mode}）");
                    Debug.WriteLine($"[ProcessProtection] 进程保护已启用（{mode}）");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] 启用保护失败: {ex.Message}");
                ProtectionEvent?.Invoke($"启用保护失败: {ex.Message}");
                return false;
            }
        }

        private bool EnableCriticalProcess()
        {
            try
            {
                if (!EnableSeDebugPrivilege())
                {
                    Debug.WriteLine("[ProcessProtection] 启用 SeDebugPrivilege 失败，需要管理员权限");
                    return false;
                }

                bool oldValue;
                int result = RtlSetProcessIsCritical(true, out oldValue, false);

                if (result >= 0)
                {
                    _isCritical = true;
                    Debug.WriteLine("[ProcessProtection] 关键进程保护已启用 - 强制终止将导致蓝屏");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[ProcessProtection] RtlSetProcessIsCritical 失败: 0x{result:X8}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] EnableCriticalProcess 异常: {ex.Message}");
                return false;
            }
        }

        private bool EnableSeDebugPrivilege()
        {
            try
            {
                IntPtr hToken;
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                {
                    Debug.WriteLine($"[ProcessProtection] OpenProcessToken 失败: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                LUID luid = new LUID();
                if (!LookupPrivilegeValue(null, "SeDebugPrivilege", ref luid))
                {
                    Debug.WriteLine($"[ProcessProtection] LookupPrivilegeValue 失败: {Marshal.GetLastWin32Error()}");
                    CloseHandle(hToken);
                    return false;
                }

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new LUID_AND_ATTRIBUTES
                    {
                        Luid = luid,
                        Attributes = 0x00000002
                    }
                };

                if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    Debug.WriteLine($"[ProcessProtection] AdjustTokenPrivileges 失败: {Marshal.GetLastWin32Error()}");
                    CloseHandle(hToken);
                    return false;
                }

                CloseHandle(hToken);
                Debug.WriteLine("[ProcessProtection] SeDebugPrivilege 已启用");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] EnableSeDebugPrivilege 异常: {ex.Message}");
                return false;
            }
        }

        private bool EnableDaclProtection()
        {
            try
            {
                var processHandle = GetCurrentProcess();

                int needed;
                GetKernelObjectSecurity(processHandle, DACL_SECURITY_INFORMATION, IntPtr.Zero, 0, out needed);

                if (needed == 0)
                {
                    Debug.WriteLine("[ProcessProtection] 无法获取安全描述符大小");
                    return false;
                }

                _originalSd = Marshal.AllocHGlobal(needed);
                _originalSdLength = needed;

                if (!GetKernelObjectSecurity(processHandle, DACL_SECURITY_INFORMATION, _originalSd, needed, out needed))
                {
                    Debug.WriteLine($"[ProcessProtection] 获取安全描述符失败: {Marshal.GetLastWin32Error()}");
                    Marshal.FreeHGlobal(_originalSd);
                    _originalSd = IntPtr.Zero;
                    return false;
                }

                bool daclPresent;
                IntPtr pDacl;
                bool daclDefaulted;

                if (!GetSecurityDescriptorDacl(_originalSd, out daclPresent, out pDacl, out daclDefaulted))
                {
                    Debug.WriteLine("[ProcessProtection] 获取DACL失败");
                    return false;
                }

                string sddl = "D:(D;;GA;;;WD)(A;;GA;;;BA)(A;;GA;;;SY)(A;;GA;;;OW)";
                IntPtr newSd;
                int newSdLength;

                if (!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, 1, out newSd, out newSdLength))
                {
                    Debug.WriteLine($"[ProcessProtection] 创建安全描述符失败: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                bool success = SetKernelObjectSecurity(processHandle, DACL_SECURITY_INFORMATION, newSd);
                LocalFree(newSd);

                if (success)
                {
                    Debug.WriteLine("[ProcessProtection] DACL保护已应用 - 已移除PROCESS_TERMINATE权限");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[ProcessProtection] 设置安全描述符失败: {Marshal.GetLastWin32Error()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] EnableDaclProtection 异常: {ex.Message}");
                return false;
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
                        Debug.WriteLine("[ProcessProtection] 进程运行正常");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProcessProtection] 监控异常: {ex.Message}");
                }
            }
        }

        public void DisableProtection()
        {
            if (!_isProtected) return;

            try
            {
                if (_isCritical)
                {
                    DisableCriticalProcess();
                }

                if (_originalSd != IntPtr.Zero)
                {
                    var processHandle = GetCurrentProcess();
                    SetKernelObjectSecurity(processHandle, DACL_SECURITY_INFORMATION, _originalSd);
                    Debug.WriteLine("[ProcessProtection] 已恢复原始DACL");
                }

                _monitorCts?.Cancel();
                _monitorCts?.Dispose();
                _monitorCts = null;

                _isProtected = false;
                ProtectionEvent?.Invoke("进程保护已禁用");
                Debug.WriteLine("[ProcessProtection] 进程保护已禁用");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] 禁用保护失败: {ex.Message}");
            }
        }

        private void DisableCriticalProcess()
        {
            try
            {
                if (!EnableSeDebugPrivilege())
                {
                    Debug.WriteLine("[ProcessProtection] 禁用关键进程时启用 SeDebugPrivilege 失败");
                    return;
                }

                bool oldValue;
                int result = RtlSetProcessIsCritical(false, out oldValue, false);

                if (result >= 0)
                {
                    _isCritical = false;
                    Debug.WriteLine("[ProcessProtection] 关键进程保护已禁用");
                }
                else
                {
                    Debug.WriteLine($"[ProcessProtection] 禁用关键进程失败: 0x{result:X8}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessProtection] DisableCriticalProcess 异常: {ex.Message}");
            }
        }

        public bool IsProtected => _isProtected;
        public bool IsCritical => _isCritical;

        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
                
                if (_consoleCtrlHandler != null)
                {
                    SetConsoleCtrlHandler(_consoleCtrlHandler, false);
                }
            }
            catch { }

            DisableProtection();

            if (_originalSd != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_originalSd);
                _originalSd = IntPtr.Zero;
            }

            _isDisposed = true;
            Debug.WriteLine("[ProcessProtection] 服务已释放");
            GC.SuppressFinalize(this);
        }
    }
}
