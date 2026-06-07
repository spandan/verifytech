using DeviceCertAgent.Core.Collectors;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Services;

/// <summary>Lightweight pre-scan probe (target 1–3 seconds).</summary>
public sealed class EligibilityProbeService
{
    public EligibilityProbeResult Collect(string? expectedDeviceType = null)
    {
        var warnings = new List<string>();
        var system = WmiHelper.Query("SELECT Manufacturer, Model FROM Win32_ComputerSystem").FirstOrDefault();
        var manufacturer = Clean(system?.GetValueOrDefault("Manufacturer"));
        var model = Clean(system?.GetValueOrDefault("Model"));

        var (deviceType, confidence) = ChassisHelper.DetectDeviceType(manufacturer, model);
        var battery = new WindowsBatteryCollector().Collect(warnings);
        var os = WmiHelper.Query(
            "SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"
        ).FirstOrDefault();

        var vm = DetectVirtualMachine(manufacturer, model);

        return new EligibilityProbeResult
        {
            DeviceType = deviceType,
            DeviceTypeConfidence = confidence,
            Manufacturer = manufacturer,
            Model = model,
            BatteryPresent = battery.Present == true,
            OsCaption = Clean(os?.GetValueOrDefault("Caption")),
            OsVersion = Clean(os?.GetValueOrDefault("Version")),
            OsBuild = Clean(os?.GetValueOrDefault("BuildNumber")),
            IsVirtualMachine = vm.Detected,
            VirtualMachineHint = vm.Hint,
            ExpectedDeviceType = expectedDeviceType,
        };
    }

    private static string Clean(object? value) =>
        (value?.ToString() ?? "").Replace("\0", "").Trim();

    private static (bool Detected, string? Hint) DetectVirtualMachine(string manufacturer, string model)
    {
        var haystack = $"{manufacturer} {model}".ToUpperInvariant();
        var bios = WmiHelper.Query("SELECT SerialNumber, Version, Manufacturer FROM Win32_BIOS").FirstOrDefault();
        if (bios is not null)
            haystack += " " + Clean(bios.GetValueOrDefault("SerialNumber")).ToUpperInvariant()
                + " " + Clean(bios.GetValueOrDefault("Version")).ToUpperInvariant()
                + " " + Clean(bios.GetValueOrDefault("Manufacturer")).ToUpperInvariant();

        var board = WmiHelper.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard").FirstOrDefault();
        if (board is not null)
            haystack += " " + Clean(board.GetValueOrDefault("Manufacturer")).ToUpperInvariant()
                + " " + Clean(board.GetValueOrDefault("Product")).ToUpperInvariant();

        var cs = WmiHelper.Query("SELECT HypervisorPresent FROM Win32_ComputerSystem").FirstOrDefault();
        if (SafeConvert.ToBool(cs?.GetValueOrDefault("HypervisorPresent")) == true)
            return (true, "Hypervisor detected");

        string?[] vmMarkers =
        [
            "VMWARE", "VIRTUALBOX", "VBOX", "QEMU", "KVM", "XEN", "PARALLELS",
            "VIRTUAL MACHINE", "MICROSOFT VIRTUAL", "INNOTEK", "BOCHS", "UTM",
        ];
        foreach (var marker in vmMarkers)
        {
            if (haystack.Contains(marker, StringComparison.Ordinal))
                return (true, marker);
        }

        return (false, null);
    }
}
