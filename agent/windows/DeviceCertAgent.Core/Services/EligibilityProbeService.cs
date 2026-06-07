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
        var manUpper = manufacturer.ToUpperInvariant();
        var modelUpper = model.ToUpperInvariant();
        var haystack = $"{manUpper} {modelUpper}";

        if (IsLikelyPhysicalOem(manUpper, modelUpper))
            return (false, null);

        var bios = WmiHelper.Query("SELECT SerialNumber, Version, Manufacturer FROM Win32_BIOS").FirstOrDefault();
        if (bios is not null)
            haystack += " " + Clean(bios.GetValueOrDefault("SerialNumber")).ToUpperInvariant()
                + " " + Clean(bios.GetValueOrDefault("Version")).ToUpperInvariant()
                + " " + Clean(bios.GetValueOrDefault("Manufacturer")).ToUpperInvariant();

        var board = WmiHelper.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard").FirstOrDefault();
        if (board is not null)
            haystack += " " + Clean(board.GetValueOrDefault("Manufacturer")).ToUpperInvariant()
                + " " + Clean(board.GetValueOrDefault("Product")).ToUpperInvariant();

        if (manUpper.Contains("MICROSOFT CORPORATION", StringComparison.Ordinal)
            && modelUpper.Contains("VIRTUAL", StringComparison.Ordinal))
            return (true, "Microsoft Virtual Machine");

        string?[] vmPhrases =
        [
            "VMWARE", "VIRTUALBOX", "QEMU", "PARALLELS",
            "VIRTUAL MACHINE", "MICROSOFT VIRTUAL", "INNOTEK", "BOCHS",
        ];
        foreach (var phrase in vmPhrases)
        {
            if (haystack.Contains(phrase, StringComparison.Ordinal))
                return (true, phrase);
        }

        if (ContainsVmToken(haystack, "VBOX")
            || ContainsVmToken(haystack, "KVM")
            || ContainsVmToken(haystack, "XEN")
            || ContainsVmToken(haystack, "UTM"))
            return (true, "virtualization_guest");

        return (false, null);
    }

    /// <summary>
    /// HypervisorPresent is true on many physical PCs (VBS, WSL2, Hyper-V host features) — not proof of a VM guest.
    /// </summary>
    private static bool IsLikelyPhysicalOem(string manufacturerUpper, string modelUpper)
    {
        string?[] oemPrefixes =
        [
            "HP", "HEWLETT-PACKARD", "HEWLETT PACKARD", "DELL", "LENOVO", "ASUSTEK", "ASUS",
            "ACER", "MSI", "SAMSUNG", "LG", "RAZER", "GIGABYTE", "MICRO-STAR", "HUAWEI",
            "TOSHIBA", "FUJITSU", "PANASONIC", "SONY", "MICROSOFT SURFACE",
        ];
        foreach (var prefix in oemPrefixes)
        {
            if (manufacturerUpper.StartsWith(prefix, StringComparison.Ordinal)
                || modelUpper.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsVmToken(string haystack, string token)
    {
        var index = haystack.IndexOf(token, StringComparison.Ordinal);
        while (index >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);
            var afterIndex = index + token.Length;
            var afterOk = afterIndex >= haystack.Length || !char.IsLetterOrDigit(haystack[afterIndex]);
            if (beforeOk && afterOk)
                return true;
            index = haystack.IndexOf(token, index + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
