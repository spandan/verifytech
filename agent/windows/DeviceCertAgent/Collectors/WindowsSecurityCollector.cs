using DeviceCertAgent.Models;
using DeviceCertAgent.Utilities;
using Microsoft.Win32;

namespace DeviceCertAgent.Collectors;

public sealed class WindowsSecurityCollector
{
    public SecurityInfo CollectSecurity(List<string> warnings)
    {
        var tpmPresent = WmiHelper.DeviceExists("SELECT * FROM Win32_Tpm");
        string? tpmVersion = null;
        if (tpmPresent)
        {
            tpmVersion = WmiHelper.FirstString("SELECT SpecVersion FROM Win32_Tpm", "SpecVersion");
        }

        var bitlocker = PowerShellHelper.Run(
            "Try { (Get-BitLockerVolume -ErrorAction Stop | Select-Object -First 1 -ExpandProperty VolumeStatus) } Catch { 'unknown' }"
        ) ?? "unknown";

        return new SecurityInfo
        {
            TpmPresent = tpmPresent,
            TpmVersion = tpmVersion,
            SecureBootEnabled = ReadSecureBoot(),
            BitlockerStatus = bitlocker.ToLowerInvariant().Replace("\r", "").Replace("\n", ""),
        };
    }

    public FirmwareInfo CollectFirmware(List<string> warnings)
    {
        var bios = WmiHelper.Query("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS").FirstOrDefault();
        if (bios is null)
        {
            warnings.Add("BIOS/firmware information unavailable");
            return new FirmwareInfo();
        }

        var dateRaw = SafeConvert.ToString(bios.GetValueOrDefault("ReleaseDate"));
        string? biosDate = null;
        if (!string.IsNullOrWhiteSpace(dateRaw) && dateRaw.Length >= 8)
            biosDate = $"{dateRaw[..4]}-{dateRaw.Substring(4, 2)}-{dateRaw.Substring(6, 2)}";

        return new FirmwareInfo
        {
            BiosVersion = SafeConvert.ToString(bios.GetValueOrDefault("SMBIOSBIOSVersion")),
            BiosDate = biosDate,
        };
    }

    public NetworkInfo CollectNetwork(List<string> warnings)
    {
        var adapters = WmiHelper.Query(
            "SELECT Name, AdapterType, NetConnectionStatus, PhysicalAdapter FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE"
        );

        var list = adapters.Select(a =>
        {
            var status = SafeConvert.ToInt(a.GetValueOrDefault("NetConnectionStatus"));
            return new NetworkAdapterInfo
            {
                Name = SafeConvert.ToString(a.GetValueOrDefault("Name")),
                Type = SafeConvert.ToString(a.GetValueOrDefault("AdapterType")),
                Connected = status == 2,
            };
        }).Where(a => !string.IsNullOrWhiteSpace(a.Name)).Take(10).ToList();

        var usb = WmiHelper.Query("SELECT Name FROM Win32_USBHub").Count;
        var portSummary = usb > 0 ? $"usb_hubs:{usb}" : null;

        return new NetworkInfo
        {
            Adapters = list,
            PortSummary = portSummary,
        };
    }

    public PerformanceInfo CollectPerformance(List<string> warnings)
    {
        var os = WmiHelper.Query("SELECT LastBootUpTime FROM Win32_OperatingSystem").FirstOrDefault();
        double? bootSeconds = null;
        if (os?.TryGetValue("LastBootUpTime", out var bootRaw) == true &&
            bootRaw is string bootStr &&
            ManagementDateTimeConverter.TryParse(bootStr, out var bootTime))
        {
            bootSeconds = Math.Round((DateTime.Now - bootTime).TotalSeconds, 0);
        }

        var pendingReboot = false;
        try
        {
            pendingReboot = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update",
                "RebootRequired",
                null) is not null;
        }
        catch
        {
            // Registry unavailable off Windows
        }

        return new PerformanceInfo
        {
            BootTimeSeconds = bootSeconds,
            PendingReboot = pendingReboot,
        };
    }

    public SoftwareInfo CollectSoftware(List<string> warnings)
    {
        var issues = new List<string>();
        var problemDevices = WmiHelper.Query(
            "SELECT Name, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"
        );

        foreach (var dev in problemDevices.Take(5))
        {
            var name = SafeConvert.ToString(dev.GetValueOrDefault("Name"));
            var code = SafeConvert.ToInt(dev.GetValueOrDefault("ConfigManagerErrorCode"));
            if (!string.IsNullOrWhiteSpace(name))
                issues.Add($"{name} (error {code})");
        }

        return new SoftwareInfo { DriverIssues = issues };
    }

    private static bool? ReadSecureBoot()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var value = key?.GetValue("UEFISecureBootEnabled");
            return SafeConvert.ToBool(value);
        }
        catch
        {
            return null;
        }
    }
}

internal static class ManagementDateTimeConverter
{
    public static bool TryParse(string wmiDate, out DateTime result)
    {
        result = default;
        try
        {
            result = System.Management.ManagementDateTimeConverter.ToDateTime(wmiDate);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
