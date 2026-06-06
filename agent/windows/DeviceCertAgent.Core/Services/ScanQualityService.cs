using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Services;

public sealed class ScanQualityService
{
    public double ComputeCompleteness(CollectionResult result)
    {
        var checks = new List<bool>
        {
            !string.IsNullOrWhiteSpace(result.Tier1.Manufacturer),
            !string.IsNullOrWhiteSpace(result.Tier1.Model),
            !string.IsNullOrWhiteSpace(result.Tier1.SerialNumberHash),
            !string.IsNullOrWhiteSpace(result.Tier1.HardwareUuidHash),
            !string.IsNullOrWhiteSpace(result.Tier1.CpuModel),
            result.Tier1.RamTotalGb > 0,
            result.Tier1.StorageTotalGb > 0,
            result.Tier2.Storage.Count > 0,
            result.Tier2.Battery.Present == true,
            result.Tier2.Display.Resolution is not null,
            result.Tier2.Graphics.GpuModel is not null,
            CountFunctional(result.Tier2.FunctionalReadiness) >= 4,
        };

        var passed = checks.Count(c => c);
        return Math.Round(passed * 100.0 / checks.Count, 0);
    }

    public ScanSummary BuildSummary(CollectionResult result, string scanType)
    {
        var fr = result.Tier2.FunctionalReadiness;
        var functionalCount = CountFunctional(fr);
        var storageDesc = result.Tier2.Storage.Count > 0
            ? $"{result.Tier1.StorageTotalGb:0}GB {result.Tier2.Storage.First().DriveType ?? "drive"}"
            : "Unknown";

        var healths = result.Tier2.Storage
            .Where(d => d.HealthPercent is not null)
            .Select(d => d.HealthPercent!.Value)
            .ToList();

        var security = result.Tier3.Security;
        var securityParts = new List<string>();
        if (security.TpmPresent == true)
            securityParts.Add($"TPM {security.TpmVersion ?? "present"}");
        else if (security.TpmPresent == false)
            securityParts.Add("TPM not detected");
        if (security.SecureBootEnabled == true)
            securityParts.Add("Secure Boot on");
        else if (security.SecureBootEnabled == false)
            securityParts.Add("Secure Boot off");

        var batteryText = result.Certification?.Battery.Condition.Value
            ?? FormatBatterySummary(result.Tier2.Battery);
        var storageHealthText = result.Certification?.Storage.FirstOrDefault()?.Condition.Value
            ?? (healths.Count > 0 ? $"{healths.Average():0}%" : "N/A");
        var securityText = securityParts.Count > 0 ? string.Join(" · ", securityParts) : "N/A";

        return new ScanSummary
        {
            DeviceName = $"{result.Tier1.Manufacturer} {result.Tier1.Model}".Trim(),
            OsVersion = result.Tier1.OsVersion,
            Cpu = result.Tier1.CpuModel,
            Ram = $"{result.Tier1.RamTotalGb:0}GB",
            Storage = storageDesc,
            Battery = batteryText,
            StorageHealth = storageHealthText,
            Display = result.Tier2.Display.Resolution ?? "N/A",
            Graphics = result.Tier2.Graphics.GpuModel ?? "N/A",
            Security = securityText,
            CoreChecks = $"{functionalCount}/8 passed",
            CompletenessPercent = ComputeCompleteness(result),
            ScanType = scanType,
            Warnings = result.Metadata.CollectionWarnings
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Distinct()
                .Take(8)
                .ToList(),
            HealthChecks =
            [
                ReportCheckFormatting.Create("Battery", batteryText),
                ReportCheckFormatting.Create("Storage", storageHealthText),
                ReportCheckFormatting.Create("Display", result.Tier2.Display.Resolution),
                ReportCheckFormatting.Create("Graphics", result.Tier2.Graphics.GpuModel),
                ReportCheckFormatting.Create("Security", securityText),
                ReportCheckFormatting.Create("Checks", $"{functionalCount}/8 passed"),
            ],
        };
    }

    public bool MeetsTier1Minimum(CollectionResult result) =>
        !string.IsNullOrWhiteSpace(result.Tier1.SerialNumberHash) &&
        !string.IsNullOrWhiteSpace(result.Tier1.HardwareUuidHash) &&
        !string.IsNullOrWhiteSpace(result.Tier1.Manufacturer) &&
        !string.IsNullOrWhiteSpace(result.Tier1.Model) &&
        result.Tier1.RamTotalGb > 0;

    private static string FormatBatterySummary(BatteryInfo battery)
    {
        if (battery.Present == true)
        {
            if (battery.HealthPercent is not null)
                return $"{battery.HealthPercent:0}%";
            return "Present (level unknown)";
        }

        if (battery.Present == false)
            return ChassisHelper.IsLaptopChassis() ? "Not reported (laptop)" : "Not present";

        return "N/A";
    }

    private static int CountFunctional(FunctionalReadiness fr)
    {
        var count = 0;
        if (fr.CameraPresent == true) count++;
        if (fr.MicrophonePresent == true) count++;
        if (fr.SpeakerPresent == true) count++;
        if (fr.WifiPresent == true) count++;
        if (fr.BluetoothPresent == true) count++;
        if (fr.KeyboardPresent == true) count++;
        if (fr.TouchpadPresent == true) count++;
        if (fr.ChargingStatus is not null) count++;
        return count;
    }
}
