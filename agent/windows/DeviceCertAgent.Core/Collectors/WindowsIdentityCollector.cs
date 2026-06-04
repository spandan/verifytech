using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Services;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsIdentityCollector
{
    private static readonly HashSet<int> LaptopChassisTypes =
    [
        8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32,
    ];

    public Tier1Identity Collect(List<string> warnings)
    {
        var system = WmiHelper.Query("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem").FirstOrDefault();
        var bios = WmiHelper.Query("SELECT SerialNumber, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS").FirstOrDefault();
        var board = WmiHelper.Query("SELECT SerialNumber, Product FROM Win32_BaseBoard").FirstOrDefault();
        var uuidRow = WmiHelper.Query("SELECT UUID FROM Win32_ComputerSystemProduct").FirstOrDefault();

        var manufacturer = SafeConvert.ToString(system?.GetValueOrDefault("Manufacturer")) ?? "Unknown";
        var model = SafeConvert.ToString(system?.GetValueOrDefault("Model")) ?? "Unknown";
        var serial = SafeConvert.ToString(bios?.GetValueOrDefault("SerialNumber"));
        var uuid = SafeConvert.ToString(uuidRow?.GetValueOrDefault("UUID"));
        var boardSerial = SafeConvert.ToString(board?.GetValueOrDefault("SerialNumber"));

        if (IsPlaceholder(serial))
        {
            warnings.Add("BIOS serial appears placeholder; identity hash may be weak");
            serial = null;
        }

        if (IsPlaceholder(uuid))
        {
            warnings.Add("System UUID unavailable or placeholder");
            uuid = null;
        }

        var ramBytes = SafeConvert.ToDouble(system?.GetValueOrDefault("TotalPhysicalMemory"));
        var ramGb = ramBytes is > 0 ? SafeConvert.BytesToGb((long)ramBytes.Value) : 0;

        return new Tier1Identity
        {
            Manufacturer = CleanName(manufacturer),
            Model = CleanName(model),
            DeviceType = DetectDeviceType(),
            OsFamily = "windows",
            SerialNumberHash = HashingService.HashIdentifier(serial),
            HardwareUuidHash = HashingService.HashIdentifier(uuid),
            MotherboardSerialHash = string.IsNullOrWhiteSpace(boardSerial) || IsPlaceholder(boardSerial)
                ? null
                : HashingService.HashIdentifier(boardSerial),
            RamTotalGb = ramGb,
        };
    }

    private static string DetectDeviceType()
    {
        var enclosures = WmiHelper.Query("SELECT ChassisTypes FROM Win32_SystemEnclosure");
        foreach (var row in enclosures)
        {
            var raw = SafeConvert.ToString(row.GetValueOrDefault("ChassisTypes"));
            if (raw is null) continue;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var chassis) && LaptopChassisTypes.Contains(chassis))
                    return "laptop";
            }
        }
        return "desktop";
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var v = value.Trim().ToUpperInvariant();
        return v is "DEFAULT STRING" or "TO BE FILLED BY O.E.M." or "SYSTEM SERIAL NUMBER"
            or "00000000" or "NONE" or "NOT APPLICABLE" or "0123456789";
    }

    private static string CleanName(string value) =>
        value.Replace("\0", "").Trim();
}
