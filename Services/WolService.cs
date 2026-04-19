using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Nexus.Services
{
    public class WolResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string? TargetMac { get; set; }
    }

    public class WolService
    {
        private const int WOL_PORT = 9;
        private const string BROADCAST_IP = "255.255.255.255";

        public event EventHandler<WolResult>? WolPacketSent;

        public static bool ValidateMacAddress(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
                return false;

            var macClean = macAddress.Replace(":", "").Replace("-", "").Replace(".", "");
            if (macClean.Length != 12)
                return false;

            try
            {
                Convert.FromHexString(macClean);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string NormalizeMacAddress(string macAddress)
        {
            var macClean = macAddress.Replace(":", "").Replace("-", "").Replace(".", "");
            return string.Join(":", Enumerable.Range(0, 6)
                .Select(i => macClean.Substring(i * 2, 2).ToUpper()));
        }

        public static byte[] CreateMagicPacket(string macAddress)
        {
            var macClean = macAddress.Replace(":", "").Replace("-", "").Replace(".", "");
            var macBytes = Convert.FromHexString(macClean);

            var magicPacket = new byte[6 + 16 * 6];
            
            for (int i = 0; i < 6; i++)
            {
                magicPacket[i] = 0xFF;
            }

            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    magicPacket[6 + i * 6 + j] = macBytes[j];
                }
            }

            return magicPacket;
        }

        public WolResult SendWolPacket(string macAddress, string? broadcastIp = null)
        {
            var result = new WolResult
            {
                TargetMac = macAddress
            };

            try
            {
                if (!ValidateMacAddress(macAddress))
                {
                    result.Success = false;
                    result.Message = $"无效的MAC地址: {macAddress}";
                    return result;
                }

                var normalizedMac = NormalizeMacAddress(macAddress);
                var magicPacket = CreateMagicPacket(normalizedMac);

                broadcastIp ??= BROADCAST_IP;

                using var udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                var endPoint = new IPEndPoint(IPAddress.Parse(broadcastIp), WOL_PORT);
                udpClient.Send(magicPacket, magicPacket.Length, endPoint);

                result.Success = true;
                result.Message = $"开机指令已发送到 {normalizedMac}";
                
                Debug.WriteLine($"[WolService] WOL魔术包已发送: MAC={normalizedMac}, IP={broadcastIp}:{WOL_PORT}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"发送WOL魔术包失败: {ex.Message}";
                Debug.WriteLine($"[WolService] 发送失败: {ex.Message}");
            }

            WolPacketSent?.Invoke(this, result);
            return result;
        }

        public static string? GetLocalBroadcastIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 80);
                var localEndPoint = socket.LocalEndPoint as IPEndPoint;
                
                if (localEndPoint != null)
                {
                    var ipParts = localEndPoint.Address.ToString().Split('.');
                    if (ipParts.Length == 4)
                    {
                        return $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.255";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WolService] 获取广播地址失败: {ex.Message}");
            }

            return BROADCAST_IP;
        }

        public WolResult SendWolToMultiple(string[] macAddresses, string? broadcastIp = null)
        {
            var successCount = 0;
            var failedCount = 0;
            var lastError = "";

            foreach (var mac in macAddresses)
            {
                var result = SendWolPacket(mac, broadcastIp);
                if (result.Success)
                {
                    successCount++;
                }
                else
                {
                    failedCount++;
                    lastError = result.Message;
                }
            }

            return new WolResult
            {
                Success = failedCount == 0,
                Message = failedCount == 0 
                    ? $"已发送 {successCount} 个开机指令" 
                    : $"成功 {successCount} 个，失败 {failedCount} 个。{lastError}"
            };
        }
    }
}
