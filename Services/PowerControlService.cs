using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nexus.Services
{
    public enum PowerAction
    {
        Shutdown,
        Reboot
    }

    public class PowerControlResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public PowerAction Action { get; set; }
    }

    public class PowerControlService
    {
        public event EventHandler<PowerControlResult>? PowerControlExecuted;

        public PowerControlResult ExecutePowerControl(PowerAction action)
        {
            var result = new PowerControlResult
            {
                Action = action
            };

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    result = ExecuteWindowsPowerControl(action);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    result = ExecuteLinuxPowerControl(action);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    result = ExecuteMacOSPowerControl(action);
                }
                else
                {
                    result.Success = false;
                    result.Message = "不支持的操作系统";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PowerControl] 执行失败: {ex.Message}");
                result.Success = false;
                result.Message = $"执行失败: {ex.Message}";
            }

            PowerControlExecuted?.Invoke(this, result);
            return result;
        }

        private PowerControlResult ExecuteWindowsPowerControl(PowerAction action)
        {
            var result = new PowerControlResult { Action = action };

            try
            {
                string arguments = action switch
                {
                    PowerAction.Shutdown => "/s /t 10 /c \"珠海市红旗中学集控：系统将在10秒后关机\"",
                    PowerAction.Reboot => "/r /t 10 /c \"珠海市红旗中学集控：系统将在10秒后重启\"",
                    _ => throw new ArgumentException("无效的操作类型")
                };

                var processInfo = new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);

                result.Success = true;
                result.Message = action switch
                {
                    PowerAction.Shutdown => "系统将在5秒后关机",
                    PowerAction.Reboot => "系统将在5秒后重启",
                    _ => ""
                };

                Debug.WriteLine($"[PowerControl] Windows {action} 命令已执行: shutdown {arguments}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"执行失败: {ex.Message}";
                Debug.WriteLine($"[PowerControl] Windows 执行失败: {ex.Message}");
            }

            return result;
        }

        private PowerControlResult ExecuteLinuxPowerControl(PowerAction action)
        {
            var result = new PowerControlResult { Action = action };

            try
            {
                string command = action switch
                {
                    PowerAction.Shutdown => "shutdown -h +1 \"Remote control: System will shutdown in 1 minute\"",
                    PowerAction.Reboot => "shutdown -r +1 \"Remote control: System will reboot in 1 minute\"",
                    _ => throw new ArgumentException("无效的操作类型")
                };

                var processInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"sudo {command}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(processInfo);

                result.Success = true;
                result.Message = action switch
                {
                    PowerAction.Shutdown => "系统将在1分钟后关机",
                    PowerAction.Reboot => "系统将在1分钟后重启",
                    _ => ""
                };

                Debug.WriteLine($"[PowerControl] Linux {action} 命令已执行");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"执行失败: {ex.Message}";
                Debug.WriteLine($"[PowerControl] Linux 执行失败: {ex.Message}");
            }

            return result;
        }

        private PowerControlResult ExecuteMacOSPowerControl(PowerAction action)
        {
            var result = new PowerControlResult { Action = action };

            try
            {
                string command = action switch
                {
                    PowerAction.Shutdown => "osascript -e 'tell app \"System Events\" to shut down'",
                    PowerAction.Reboot => "osascript -e 'tell app \"System Events\" to restart'",
                    _ => throw new ArgumentException("无效的操作类型")
                };

                var processInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(processInfo);

                result.Success = true;
                result.Message = action switch
                {
                    PowerAction.Shutdown => "系统即将关机",
                    PowerAction.Reboot => "系统即将重启",
                    _ => ""
                };

                Debug.WriteLine($"[PowerControl] macOS {action} 命令已执行");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"执行失败: {ex.Message}";
                Debug.WriteLine($"[PowerControl] macOS 执行失败: {ex.Message}");
            }

            return result;
        }

        public static PowerAction? ParseActionFromString(string? action)
        {
            return action?.ToLower() switch
            {
                "shutdown" => PowerAction.Shutdown,
                "reboot" => PowerAction.Reboot,
                _ => null
            };
        }
    }
}
