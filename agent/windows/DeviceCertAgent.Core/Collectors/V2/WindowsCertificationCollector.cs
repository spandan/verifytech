using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;
using Microsoft.Win32;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class WindowsCertificationCollector
{
    public WindowsAssessment Collect(List<string> warnings)
    {
        var w = new WindowsAssessment();
        var os = WmiHelper.Query("SELECT Caption, BuildNumber, Version FROM Win32_OperatingSystem").FirstOrDefault();
        if (os is not null)
        {
            w.Edition = ConfidenceValue<string?>.Collected(
                SafeConvert.ToString(os.GetValueOrDefault("Caption")), "Win32_OperatingSystem", "wmi");
            w.Build = ConfidenceValue<string?>.Collected(
                SafeConvert.ToString(os.GetValueOrDefault("BuildNumber")), "Win32_OperatingSystem", "wmi");
        }

        var activation = PowerShellHelper.Run(
            "(Get-CimInstance SoftwareLicensingProduct | Where-Object { $_.PartialProductKey } | Select-Object -First 1 -ExpandProperty LicenseStatus)");
        if (int.TryParse(activation, out var status))
            w.ActivationStatus = TriStateValue.Verified(status == 1, "SoftwareLicensingProduct", "cim");
        else
            w.ActivationStatus = TriStateValue.Unknown("cim");

        w.PendingReboot = ReadPendingReboot();
        w.PendingUpdates = ReadPendingUpdates();

        var score = 70;
        if (w.ActivationStatus.Value == true) score += 15;
        if (w.PendingReboot.Value != true) score += 10;
        if (w.PendingUpdates.Value != true) score += 5;
        w.ReadinessScore = ConfidenceValue<int?>.Collected(Math.Min(100, score), "engine", "windows_readiness", ConfidenceLevel.Medium);

        return w;
    }

    private static TriStateValue ReadPendingReboot()
    {
        try
        {
            var reboot = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update",
                "RebootRequired", null) is not null;
            return TriStateValue.Verified(reboot, "registry", "WindowsUpdate");
        }
        catch
        {
            return TriStateValue.Unknown("registry");
        }
    }

    private static TriStateValue ReadPendingUpdates()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\OSUpgrade");
            var val = key?.GetValue("ReservationsEnabled");
            return val is null
                ? TriStateValue.Unknown("registry")
                : TriStateValue.Verified(false, "registry", "heuristic");
        }
        catch
        {
            return TriStateValue.Unknown("registry");
        }
    }
}
