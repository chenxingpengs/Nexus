using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nexus.Services
{
    public class VolumeControlService : IDisposable
    {
        private float _originalVolume;
        private bool _isMaximized = false;
        private bool _disposed = false;
        private IAudioEndpointVolume? _endpointVolume;

        public VolumeControlService()
        {
            _originalVolume = 0.5f;
            InitializeEndpointVolume();
        }

        private void InitializeEndpointVolume()
        {
            IntPtr pEnumerator = IntPtr.Zero;
            IntPtr pDevice = IntPtr.Zero;

            try
            {
                var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
                var iid = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
                
                int hr = CoCreateInstance(
                    ref clsid,
                    IntPtr.Zero,
                    CLSCTX.CLSCTX_INPROC_SERVER,
                    ref iid,
                    out pEnumerator);

                if (hr != 0 || pEnumerator == IntPtr.Zero)
                {
                    Debug.WriteLine($"[VolumeControlService] 创建MMDeviceEnumerator失败: 0x{hr:X8}");
                    return;
                }

                IntPtr pEnumeratorInterface = IntPtr.Zero;
                hr = Marshal.QueryInterface(pEnumerator, ref iid, out pEnumeratorInterface);
                if (hr != 0 || pEnumeratorInterface == IntPtr.Zero)
                {
                    Debug.WriteLine($"[VolumeControlService] QueryInterface IMMDeviceEnumerator失败: 0x{hr:X8}");
                    return;
                }

                var enumerator = Marshal.GetObjectForIUnknown(pEnumeratorInterface) as IMMDeviceEnumerator;
                Marshal.Release(pEnumeratorInterface);
                
                if (enumerator == null)
                {
                    Debug.WriteLine("[VolumeControlService] 获取IMMDeviceEnumerator接口失败");
                    return;
                }

                hr = enumerator.GetDefaultAudioEndpoint(0, 0, out pDevice);
                if (hr != 0 || pDevice == IntPtr.Zero)
                {
                    Debug.WriteLine($"[VolumeControlService] 获取默认音频设备失败: 0x{hr:X8}");
                    return;
                }

                var device = Marshal.GetObjectForIUnknown(pDevice) as IMMDevice;
                if (device == null)
                {
                    Debug.WriteLine("[VolumeControlService] 获取IMMDevice接口失败");
                    return;
                }

                var iidIAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;
                IntPtr pEndpointVolume;
                hr = device.Activate(ref iidIAudioEndpointVolume, 0, IntPtr.Zero, out pEndpointVolume);

                if (hr != 0 || pEndpointVolume == IntPtr.Zero)
                {
                    Debug.WriteLine($"[VolumeControlService] 获取IAudioEndpointVolume接口失败: 0x{hr:X8}");
                    return;
                }

                _endpointVolume = Marshal.GetObjectForIUnknown(pEndpointVolume) as IAudioEndpointVolume;
                Marshal.Release(pEndpointVolume);
                
                if (_endpointVolume != null)
                {
                    _endpointVolume.GetMasterVolumeLevelScalar(out float currentVolume);
                    Debug.WriteLine($"[VolumeControlService] 音频端点初始化成功，当前音量: {currentVolume:F2}");
                }
                else
                {
                    Debug.WriteLine("[VolumeControlService] 音频端点初始化失败，将使用 Fallback 方式");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VolumeControlService] 初始化音频端点失败: {ex.Message}");
            }
            finally
            {
                if (pDevice != IntPtr.Zero) Marshal.Release(pDevice);
                if (pEnumerator != IntPtr.Zero) Marshal.Release(pEnumerator);
            }
        }

        public void MaximizeVolume()
        {
            if (_isMaximized)
            {
                Debug.WriteLine("[VolumeControlService] 已经最大化，跳过");
                return;
            }

            bool success = false;

            try
            {
                if (_endpointVolume != null)
                {
                    _endpointVolume.GetMasterVolumeLevelScalar(out _originalVolume);
                    Debug.WriteLine($"[VolumeControlService] 当前音量: {_originalVolume:F2}");
                    
                    int hr = _endpointVolume.SetMasterVolumeLevelScalar(1.0f, Guid.Empty);
                    Debug.WriteLine($"[VolumeControlService] SetMasterVolumeLevelScalar 返回: 0x{hr:X8}");
                    
                    if (hr == 0)
                    {
                        hr = _endpointVolume.SetMute(false, Guid.Empty);
                        Debug.WriteLine($"[VolumeControlService] SetMute 返回: 0x{hr:X8}");
                    }
                    
                    if (hr == 0)
                    {
                        _isMaximized = true;
                        success = true;
                        
                        _endpointVolume.GetMasterVolumeLevelScalar(out float newVolume);
                        Debug.WriteLine($"[VolumeControlService] 系统音量已调至最大，验证音量: {newVolume:F2}");
                    }
                    else
                    {
                        Debug.WriteLine($"[VolumeControlService] Core Audio设置失败: 0x{hr:X8}");
                    }
                }
                else
                {
                    Debug.WriteLine("[VolumeControlService] _endpointVolume 为 null，使用 Fallback");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VolumeControlService] Core Audio调节失败: {ex.Message}");
            }

            if (!success)
            {
                MaximizeVolumeFallback();
            }
        }

        private void MaximizeVolumeFallback()
        {
            Debug.WriteLine("[VolumeControlService] 开始 Fallback 方式调节音量");
            
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    keybd_event(VK_VOLUME_UP, 0, 0, 0);
                    keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0);
                }
                
                keybd_event(VK_VOLUME_MUTE, 0, 0, 0);
                keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(VK_VOLUME_MUTE, 0, 0, 0);
                keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, 0);
                
                _isMaximized = true;
                Debug.WriteLine("[VolumeControlService] 系统音量已调至最大 (Fallback方式 - keybd_event)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VolumeControlService] keybd_event Fallback失败: {ex.Message}");
                
                try
                {
                    IntPtr hWnd = GetForegroundWindow();
                    Debug.WriteLine($"[VolumeControlService] 使用 SendMessage，窗口句柄: {hWnd}");
                    
                    for (int i = 0; i < 50; i++)
                    {
                        SendMessage(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)((APPCOMMAND_VOLUME_UP << 16) | 0));
                    }
                    SendMessage(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)((APPCOMMAND_VOLUME_MUTE << 16) | 0));
                    SendMessage(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)((APPCOMMAND_VOLUME_MUTE << 16) | 0));
                    _isMaximized = true;
                    Debug.WriteLine("[VolumeControlService] 系统音量已调至最大 (Fallback方式 - SendMessage)");
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"[VolumeControlService] SendMessage Fallback也失败: {ex2.Message}");
                }
            }
        }

        public void RestoreVolume()
        {
            if (!_isMaximized) return;

            bool success = false;

            try
            {
                if (_endpointVolume != null)
                {
                    int hr = _endpointVolume.SetMasterVolumeLevelScalar(_originalVolume, Guid.Empty);
                    if (hr == 0)
                    {
                        success = true;
                        _isMaximized = false;
                        Debug.WriteLine($"[VolumeControlService] 系统音量已恢复至 {_originalVolume:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VolumeControlService] 恢复音量失败: {ex.Message}");
            }

            if (!success)
            {
                RestoreVolumeFallback();
            }
        }

        private void RestoreVolumeFallback()
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    keybd_event(VK_VOLUME_DOWN, 0, 0, 0);
                    keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, 0);
                }
                
                int steps = (int)(_originalVolume * 100);
                for (int i = 0; i < steps && i < 100; i++)
                {
                    keybd_event(VK_VOLUME_UP, 0, 0, 0);
                    keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0);
                }
                
                _isMaximized = false;
                Debug.WriteLine("[VolumeControlService] 系统音量已恢复 (Fallback方式 - keybd_event)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VolumeControlService] Fallback恢复失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            RestoreVolume();
            
            if (_endpointVolume != null)
            {
                try
                {
                    Marshal.ReleaseComObject(_endpointVolume);
                }
                catch { }
                _endpointVolume = null;
            }
            
            _disposed = true;
        }

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(
            ref Guid rclsid,
            IntPtr pUnkOuter,
            CLSCTX dwClsContext,
            ref Guid riid,
            out IntPtr ppv);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const uint WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_VOLUME_UP = 10;
        private const int APPCOMMAND_VOLUME_DOWN = 11;
        private const int APPCOMMAND_VOLUME_MUTE = 8;

        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const int KEYEVENTF_KEYUP = 0x0002;

        private enum CLSCTX
        {
            CLSCTX_INPROC_SERVER = 0x1,
            CLSCTX_INPROC_HANDLER = 0x2,
            CLSCTX_LOCAL_SERVER = 0x4,
            CLSCTX_REMOTE_SERVER = 0x10,
            CLSCTX_ALL = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER
        }
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr device);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr activationParams, out IntPtr interfacePtr);
        int OpenPropertyStore(int stgmAccess, out IntPtr properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out int pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute(bool bMute, Guid pguidEventContext);
        int GetMute(out bool pbMute);
    }
}
