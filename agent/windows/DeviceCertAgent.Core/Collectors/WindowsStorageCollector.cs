using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Services;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsStorageCollector
{
    public List<StorageDriveInfo> Collect(List<string> warnings)
    {
        var drives = new List<StorageDriveInfo>();
        var rows = WmiHelper.Query(
            "SELECT Index, Model, Size, MediaType, SerialNumber, InterfaceType FROM Win32_DiskDrive"
        );

        if (rows.Count == 0)
        {
            warnings.Add("Storage drive information unavailable");
            return drives;
        }

        var index = 0;
        foreach (var row in rows)
        {
            var size = SafeConvert.ToDouble(row.GetValueOrDefault("Size"));
            if (size is null or <= 0) continue;

            var media = SafeConvert.ToString(row.GetValueOrDefault("MediaType"));
            var model = SafeConvert.ToString(row.GetValueOrDefault("Model"));
            var iface = SafeConvert.ToString(row.GetValueOrDefault("InterfaceType"));
            var serial = SafeConvert.ToString(row.GetValueOrDefault("SerialNumber"));

            drives.Add(new StorageDriveInfo
            {
                Index = SafeConvert.ToInt(row.GetValueOrDefault("Index")) ?? index,
                DriveType = InferDriveType(media, model, iface),
                CapacityGb = SafeConvert.BytesToGb((long)size.Value),
                HealthPercent = null,
                SmartStatus = "unknown",
                SerialHash = string.IsNullOrWhiteSpace(serial) ? null : HashingService.HashIdentifier(serial.Trim()),
            });
            index++;
        }

        TryEnrichPhysicalDiskInfo(drives);

        if (drives.Count == 0)
            warnings.Add("No storage drives with valid capacity detected");

        return drives;
    }

    private static void TryEnrichPhysicalDiskInfo(List<StorageDriveInfo> drives)
    {
        var physical = WmiHelper.Query(
            "SELECT FriendlyName, MediaType, Size FROM MSFT_PhysicalDisk",
            @"\\.\root\Microsoft\Windows\Storage"
        );

        for (var i = 0; i < drives.Count && i < physical.Count; i++)
        {
            var row = physical[i];
            var media = SafeConvert.ToInt(row.GetValueOrDefault("MediaType"));
            drives[i].DriveType = media switch
            {
                4 => "SSD",
                3 => "HDD",
                5 => "NVMe",
                _ => drives[i].DriveType,
            };
        }
    }

    private static string InferDriveType(string? media, string? model, string? iface)
    {
        var combined = $"{media} {model} {iface}".ToUpperInvariant();
        if (combined.Contains("NVME") || combined.Contains("NVMe")) return "NVMe";
        if (combined.Contains("SSD") || combined.Contains("SOLID STATE")) return "SSD";
        if (combined.Contains("HDD") || combined.Contains("FIXED")) return "HDD";
        return "Unknown";
    }
}
