using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Nexus.Services;

public static class DeviceIdentifier
{
    public static (string DeviceId, string? MacAddress, string? IpAddress) GetDeviceInfo()
    {
        try
        {
            var macAddress = GetPrimaryMacAddress();
            var ipAddress = GetPrimaryIpAddress();

            if (!string.IsNullOrEmpty(macAddress))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(macAddress.ToUpper()));
                var deviceId = "device_" + Convert.ToHexString(hash).Substring(0, 16).ToLower();

                return (deviceId, macAddress, ipAddress);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetDeviceInfo error: {ex.Message}");
        }

        return ("device_" + Guid.NewGuid().ToString("N").Substring(0, 16), null, null);
    }

    public static string? GetPrimaryMacAddress()
    {
        try
        {
            var adapters = GetAllNetworkAdapters();
            var primaryAdapter = adapters.FirstOrDefault(a => a.IsPrimary);

            if (primaryAdapter != null)
            {
                return primaryAdapter.MacAddress;
            }

            var ethernetAdapter = adapters.FirstOrDefault(a =>
                a.Type == "Ethernet" && !string.IsNullOrEmpty(a.MacAddress));

            if (ethernetAdapter != null)
            {
                return ethernetAdapter.MacAddress;
            }

            var anyAdapter = adapters.FirstOrDefault(a => !string.IsNullOrEmpty(a.MacAddress));
            return anyAdapter?.MacAddress;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetPrimaryMacAddress error: {ex.Message}");
        }

        return null;
    }

    public static string? GetPrimaryIpAddress()
    {
        try
        {
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in hostEntry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetPrimaryIpAddress error: {ex.Message}");
        }

        return null;
    }

    private static List<NetworkAdapterInfo> GetAllNetworkAdapters()
    {
        var result = new List<NetworkAdapterInfo>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var macBytes = nic.GetPhysicalAddress().GetAddressBytes();
                if (macBytes == null || macBytes.Length == 0)
                    continue;

                var macAddress = FormatMacAddress(macBytes);
                var ipAddress = GetAdapterIpAddress(nic);
                var isPrimary = nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet;

                result.Add(new NetworkAdapterInfo
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    Type = nic.NetworkInterfaceType.ToString(),
                    MacAddress = macAddress,
                    IpAddress = ipAddress,
                    IsPrimary = isPrimary
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAllNetworkAdapters error: {ex.Message}");
        }

        return result;
    }

    private static string? GetAdapterIpAddress(NetworkInterface nic)
    {
        try
        {
            var ipProps = nic.GetIPProperties();
            foreach (var ip in ipProps.UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.Address.ToString();
                }
            }
        }
        catch { }

        return null;
    }

    private static string FormatMacAddress(byte[] bytes)
    {
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
}

public class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? IpAddress { get; set; }
    public bool IsPrimary { get; set; }
}
