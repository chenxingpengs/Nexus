using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Nexus.Services
{
    public class WolConfigResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> ConfiguredAdapters { get; set; } = new();
        public List<string> FailedAdapters { get; set; } = new();
    }

    public class WolConfigService
    {
        public List<NetworkAdapterInfo> GetNetworkAdapters()
        {
            var adapters = new List<NetworkAdapterInfo>();

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    return adapters;
                }

                var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var adapter = new NetworkAdapterInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "",
                        Description = obj["Description"]?.ToString() ?? "",
                        MacAddress = obj["MACAddress"]?.ToString() ?? "",
                        IsConnected = (obj["NetConnectionStatus"]?.ToString() == "2")
                    };

                    adapters.Add(adapter);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WolConfigService] GetNetworkAdapters error: {ex.Message}");
            }

            return adapters;
        }

        public async Task<WolConfigResult> ConfigureWolAsync()
        {
            var result = new WolConfigResult();

            if (!OperatingSystem.IsWindows())
            {
                result.Success = false;
                result.Message = "此功能仅支持 Windows 系统";
                return result;
            }

            try
            {
                var adapters = GetWolCapableAdapters();

                if (adapters.Count == 0)
                {
                    result.Success = false;
                    result.Message = "未找到支持网络唤醒的网卡";
                    return result;
                }

                foreach (var adapter in adapters)
                {
                    try
                    {
                        var success = await EnableWolForAdapterAsync(adapter);
                        if (success)
                        {
                            result.ConfiguredAdapters.Add(adapter.Name);
                        }
                        else
                        {
                            result.FailedAdapters.Add(adapter.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WolConfigService] Configure adapter {adapter.Name} failed: {ex.Message}");
                        result.FailedAdapters.Add(adapter.Name);
                    }
                }

                if (result.ConfiguredAdapters.Count > 0)
                {
                    result.Success = true;
                    result.Message = $"已成功配置 {result.ConfiguredAdapters.Count} 个网卡";
                }
                else
                {
                    result.Success = false;
                    result.Message = "所有网卡配置失败，请尝试手动配置";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"配置失败: {ex.Message}";
            }

            return result;
        }

        private List<NetworkAdapterInfo> GetWolCapableAdapters()
        {
            var wolCapableAdapters = new List<NetworkAdapterInfo>();

            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2 AND PhysicalAdapter = true");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    var description = obj["Description"]?.ToString() ?? "";
                    var macAddress = obj["MACAddress"]?.ToString() ?? "";
                    var deviceId = obj["DeviceID"]?.ToString() ?? "";

                    if (IsEthernetAdapter(name, description))
                    {
                        wolCapableAdapters.Add(new NetworkAdapterInfo
                        {
                            Name = name,
                            Description = description,
                            MacAddress = macAddress,
                            IsConnected = true,
                            IsWolCapable = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WolConfigService] GetWolCapableAdapters error: {ex.Message}");
            }

            return wolCapableAdapters;
        }

        private bool IsEthernetAdapter(string name, string description)
        {
            var keywords = new[] { "Ethernet", "以太网", "Realtek", "Intel", "Broadcom", "Killer", "Qualcomm" };
            var excludeKeywords = new[] { "Bluetooth", "Virtual", "Wi-Fi", "Wireless", "虚拟", "蓝牙" };

            var combined = $"{name} {description}".ToLower();

            foreach (var exclude in excludeKeywords)
            {
                if (combined.Contains(exclude.ToLower()))
                    return false;
            }

            foreach (var keyword in keywords)
            {
                if (combined.Contains(keyword.ToLower()))
                    return true;
            }

            return false;
        }

        private async Task<bool> EnableWolForAdapterAsync(NetworkAdapterInfo adapter)
        {
            try
            {
                var powerShellScript = $@"
$adapters = Get-NetAdapter | Where-Object {{ $_.Name -like '*{adapter.Name.Split(' ').FirstOrDefault()}*' -or $_.InterfaceDescription -like '*{adapter.Description.Split(' ').FirstOrDefault()}*' }}
foreach ($adapter in $adapters) {{
    try {{
        Set-NetAdapterPowerManagement -Name $adapter.Name -WakeOnMagicPacket Enabled -WakeOnPattern Enabled -ErrorAction SilentlyContinue
        Write-Output ""Configured: $($adapter.Name)""
    }} catch {{
        Write-Output ""Failed: $($adapter.Name)""
    }}
}}
";

                var result = await RunPowerShellAsync(powerShellScript);
                return result.Contains("Configured:");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WolConfigService] EnableWolForAdapterAsync error: {ex.Message}");
                return false;
            }
        }

        private async Task<string> RunPowerShellAsync(string script)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                
                await process.WaitForExitAsync();

                return outputBuilder.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WolConfigService] RunPowerShellAsync error: {ex.Message}");
                return string.Empty;
            }
        }

        public string GetCurrentMacAddress()
        {
            try
            {
                var firstEthernet = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                                         ni.OperationalStatus == OperationalStatus.Up);

                return firstEthernet?.GetPhysicalAddress().ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
