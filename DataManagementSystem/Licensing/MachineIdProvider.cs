using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace DataManagementSystem.Licensing;

public static class MachineIdProvider
{
    private static string? _cached;

    public static string GetMachineId()
    {
        if (_cached != null) return _cached;

        var parts = new List<string>();
        parts.Add(GetWmi("SELECT ProcessorId FROM Win32_Processor", "ProcessorId"));
        parts.Add(GetWmi("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=0", "SerialNumber"));

        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderBy(n => n.NetworkInterfaceType)
                .FirstOrDefault()
                ?.GetPhysicalAddress().ToString();
            if (!string.IsNullOrWhiteSpace(mac)) parts.Add(mac);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"MachineId: MAC lookup failed — {ex.Message}", "MachineIdProvider");
        }

        var input = parts.Any(p => !string.IsNullOrWhiteSpace(p))
            ? string.Join("|", parts.Where(p => !string.IsNullOrWhiteSpace(p)))
            : Environment.MachineName;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToUpperInvariant()));
        var hex  = Convert.ToHexString(hash)[..16];
        _cached  = $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";

        Logger.LogInfo($"MachineId resolved: {_cached}", "MachineIdProvider");
        return _cached;
    }

    private static string GetWmi(string query, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject obj in searcher.Get())
                return obj[property]?.ToString()?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"MachineId: WMI '{property}' lookup failed — {ex.Message}", "MachineIdProvider");
        }
        return string.Empty;
    }
}
