using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Nexus.Models.Widget;

namespace Nexus.Services
{
    public class ShortcutService : IDisposable
    {
        private static readonly string[] DocumentCameraKeywords = { "展台", "DocumentCamera", "Camera", "实物展台", "视频展台" };

        private ManagementEventWatcher? _usbWatcher;
        private bool _disposed;
        private string? _cachedDocumentCameraPath;
        private string? _cachedDocumentCameraName;

        public event EventHandler? UsbDrivesChanged;

        public ShortcutService()
        {
            StartUsbWatcher();
        }

        public ObservableCollection<UsbDriveItem> GetUsbDrives()
        {
            var drives = new ObservableCollection<UsbDriveItem>();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return drives;

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    {
                        var item = new UsbDriveItem
                        {
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            Name = drive.VolumeLabel ?? "可移动磁盘",
                            TotalSize = FormatSize(drive.TotalSize),
                            AvailableSpace = FormatSize(drive.AvailableFreeSpace)
                        };
                        drives.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUsbDrives error: {ex.Message}");
            }

            return drives;
        }

        public void OpenUsbDrive(string driveLetter)
        {
            try
            {
                var path = driveLetter.EndsWith("\\") ? driveLetter : driveLetter + "\\";
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenUsbDrive error: {ex.Message}");
            }
        }

        public (DocumentCameraType type, string name, string? path) GetDocumentCamera()
        {
            if (!string.IsNullOrEmpty(_cachedDocumentCameraPath) && File.Exists(_cachedDocumentCameraPath))
            {
                return (DocumentCameraType.Seewo, _cachedDocumentCameraName ?? "展台", _cachedDocumentCameraPath);
            }

            var desktopPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var desktopPath in desktopPaths)
            {
                if (string.IsNullOrEmpty(desktopPath) || !Directory.Exists(desktopPath))
                    continue;

                var result = FindDocumentCameraShortcut(desktopPath);
                if (result != null)
                {
                    _cachedDocumentCameraPath = result.Value.path;
                    _cachedDocumentCameraName = result.Value.name;
                    return (DocumentCameraType.Seewo, result.Value.name, result.Value.path);
                }
            }

            _cachedDocumentCameraPath = null;
            _cachedDocumentCameraName = null;
            return (DocumentCameraType.None, "", null);
        }

        public void RefreshDocumentCameraCache()
        {
            _cachedDocumentCameraPath = null;
            _cachedDocumentCameraName = null;
        }

        private (string name, string path)? FindDocumentCameraShortcut(string directory)
        {
            try
            {
                Debug.WriteLine($"Scanning desktop: {directory}");
                var files = Directory.GetFiles(directory, "*.lnk");
                Debug.WriteLine($"Found {files.Length} .lnk files on desktop");
                
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    Debug.WriteLine($"Checking shortcut: {fileName}");
                    
                    if (ContainsAnyKeyword(fileName, DocumentCameraKeywords))
                    {
                        Debug.WriteLine($"Matched keyword in: {fileName}");
                        var targetPath = ResolveShortcut(file);
                        Debug.WriteLine($"Target path: {targetPath}");
                        
                        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                        {
                            Debug.WriteLine($"Found document camera: {fileName} -> {targetPath}");
                            return (fileName, targetPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FindDocumentCameraShortcut error: {ex.Message}");
            }

            return null;
        }

        private string? ResolveShortcut(string shortcutPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            try
            {
                var shellLink = (IShellLinkW)new ShellLink();
                var persistFile = (IPersistFile)shellLink;
                persistFile.Load(shortcutPath, 0);
                
                var buffer = new StringBuilder(260);
                shellLink.GetPath(buffer, 260, IntPtr.Zero, 0);
                
                return buffer.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResolveShortcut error: {ex.Message}");
                return null;
            }
        }

        private static bool ContainsAnyKeyword(string text, string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public bool OpenDocumentCamera()
        {
            var (type, _, path) = GetDocumentCamera();

            if (type == DocumentCameraType.None || string.IsNullOrEmpty(path))
                return false;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenDocumentCamera error: {ex.Message}");
                return false;
            }
        }

        private void StartUsbWatcher()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            try
            {
                var query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 OR EventType = 3");
                _usbWatcher = new ManagementEventWatcher(query);
                _usbWatcher.EventArrived += (s, e) => UsbDrivesChanged?.Invoke(this, EventArgs.Empty);
                _usbWatcher.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartUsbWatcher error: {ex.Message}");
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.#} {sizes[order]}";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _usbWatcher?.Stop();
            _usbWatcher?.Dispose();
            _disposed = true;
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
