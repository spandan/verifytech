using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

/// <summary>Centralized Phase 1 certification eligibility rules.</summary>
public sealed class EligibilityEngine
{
    private static readonly HashSet<string> SupportedDeviceTypes = ["laptop"];
    private static readonly HashSet<string> SupportedOsMajorVersions = ["10.0"];

    public EligibilityResult Evaluate(EligibilityProbeResult probe)
    {
        if (probe.IsVirtualMachine)
        {
            return EligibilityResult.Fail(
                "virtual_machine",
                "Virtual machines cannot be certified.");
        }

        if (!string.Equals(probe.ExpectedDeviceType, "laptop", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(probe.ExpectedDeviceType))
        {
            return EligibilityResult.Fail(
                "expected_device_type_mismatch",
                "This certification session is for a Windows laptop only.");
        }

        if (!string.Equals(probe.DeviceType, "laptop", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceTypeFailure(probe.DeviceType);
        }

        if (!probe.BatteryPresent)
        {
            return EligibilityResult.Fail(
                "battery_not_detected",
                "Battery not detected.\n\nCertronx currently supports laptop certification only.");
        }

        var osCheck = EvaluateOperatingSystem(probe);
        if (!osCheck.Eligible)
            return osCheck;

        return EligibilityResult.Pass();
    }

    private static EligibilityResult DeviceTypeFailure(string deviceType) => deviceType switch
    {
        "desktop" or "workstation" or "mini_pc" or "server" =>
            EligibilityResult.Fail(
                $"unsupported_device_type:{deviceType}",
                "This device is currently not eligible for Certronx certification.\n\n" +
                "Certronx currently certifies Windows laptops only.\n\n" +
                "Desktop and workstation support is planned for a future release."),
        "virtual_machine" =>
            EligibilityResult.Fail("virtual_machine", "Virtual machines cannot be certified."),
        _ =>
            EligibilityResult.Fail(
                "unsupported_device_type:unknown",
                "This device is currently not eligible for Certronx certification.\n\n" +
                "Certronx currently certifies Windows laptops only."),
    };

    private static EligibilityResult EvaluateOperatingSystem(EligibilityProbeResult probe)
    {
        if (string.IsNullOrWhiteSpace(probe.OsVersion))
        {
            return EligibilityResult.Fail(
                "os_unknown",
                "This version of Windows is currently unsupported.");
        }

        var parts = probe.OsVersion.Split('.');
        if (parts.Length < 2 || !SupportedOsMajorVersions.Contains($"{parts[0]}.{parts[1]}"))
        {
            return EligibilityResult.Fail(
                "os_unsupported",
                "This version of Windows is currently unsupported.");
        }

        if (!int.TryParse(probe.OsBuild, out var build))
        {
            return EligibilityResult.Fail(
                "os_build_unknown",
                "This version of Windows is currently unsupported.");
        }

        // Windows 10 (10240+) and Windows 11 (22000+)
        if (build < 10240)
        {
            return EligibilityResult.Fail(
                "os_build_too_old",
                "This version of Windows is currently unsupported.");
        }

        return EligibilityResult.Pass();
    }
}
