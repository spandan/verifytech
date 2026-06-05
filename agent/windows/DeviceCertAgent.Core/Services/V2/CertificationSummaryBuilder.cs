using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

public sealed class CertificationSummaryBuilder
{
    public CertificationSummaryReport Build(
        CollectionResult collected,
        CertificationAssessmentV2 assessment,
        FunctionalCertificationResults? functional)
    {
        var t1 = collected.Tier1;
        var warnings = collected.Metadata.CollectionWarnings;

        var storageSummary = assessment.Storage.Count > 0
            ? string.Join("; ", assessment.Storage.Select(s =>
                $"Drive {s.Index}: {s.Condition.Value ?? "?"} ({s.HealthPercent.Value ?? 0:0}% health)"))
            : "No storage assessment";

        var functionalLines = functional is null
            ? ["Functional tests not completed"]
            : FormatFunctionalLines(functional);
        var functionalText = string.Join(Environment.NewLine, functionalLines);

        return new CertificationSummaryReport
        {
            DeviceOverview = $"{t1.Manufacturer} {t1.Model} ({t1.DeviceType}) — {t1.OsVersion}. " +
                $"CPU: {t1.CpuModel}. RAM: {t1.RamTotalGb:0} GB. Storage: {t1.StorageTotalGb:0} GB.",
            HealthSummary = $"Overall resale grade {assessment.ResaleGrade.Grade.Value}. " +
                $"Performance: {assessment.Benchmark.PerformanceRating.Value}. " +
                $"Security score: {assessment.Security.SecurityScore.Value}/100.",
            BatteryCondition = $"{assessment.Battery.Condition.Value ?? "Unknown"} — " +
                $"{assessment.Battery.LifeRecommendation.Value ?? "No recommendation"} " +
                $"(wear {assessment.Battery.WearPercent.Value ?? 0:0}%)",
            StorageCondition = storageSummary,
            PerformanceRating = $"{assessment.Benchmark.PerformanceRating.Value} " +
                $"(CPU {assessment.Benchmark.CpuScore.Value}, RAM {assessment.Benchmark.MemoryScore.Value}, " +
                $"disk {assessment.Benchmark.StorageScore.Value})",
            SecurityRating = $"TPM: {FormatTri(assessment.Security.TpmPresent)}. " +
                $"Secure Boot: {FormatTri(assessment.Security.SecureBoot)}. " +
                $"Encryption: {FormatTri(assessment.Security.DeviceEncryption)}.",
            FunctionalTestResults = functionalText,
            FunctionalTestLines = functionalLines,
            RefurbisherNotes = string.IsNullOrWhiteSpace(assessment.ResaleGrade.RefurbishmentNeeded.Value)
                ? "No additional refurbisher actions required."
                : assessment.ResaleGrade.RefurbishmentNeeded.Value!,
            RecommendedResaleGrade = assessment.ResaleGrade.Grade.Value ?? "C",
            Warnings = warnings,
        };
    }

    private static List<string> FormatFunctionalLines(FunctionalCertificationResults f)
    {
        var parts = new List<string>();
        if (!f.Display.Skipped)
            parts.Add($"Display — {f.Display.Grade ?? (f.Display.DeadPixelTestPassed == true ? "Pass" : "Review")}");
        parts.Add($"Speakers — {FormatValidation(f.SpeakerTest)}");
        parts.Add($"Microphone — {FormatValidation(f.MicrophoneTest)}");
        parts.Add($"Camera — {FormatValidation(f.CameraTest)}");
        parts.Add($"USB — {FormatValidation(f.UsbTest)}");
        parts.Add($"Display output — {FormatValidation(f.DisplayOutputTest)}");
        parts.Add($"Audio jack — {FormatValidation(f.AudioJackTest)}");
        if (!f.Keyboard.Skipped)
            parts.Add(f.Keyboard.Passed == true
                ? "Keyboard — Pass"
                : $"Keyboard — Missing keys: {string.Join(", ", f.Keyboard.KeysMissing)}");
        if (!f.Touchpad.Skipped)
            parts.Add($"Touchpad — {FormatTri(f.Touchpad.Operational)}");
        return parts.Count > 0 ? parts : ["All functional tests skipped"];
    }

    private static string FormatValidation(ComponentValidationStatus s) =>
        s.Result switch
        {
            ValidationResults.Passed => "Verified",
            ValidationResults.Failed => "Failed",
            ValidationResults.Inconclusive => "Inconclusive",
            _ => s.Present ? "Detected but Not Tested" : "Not present",
        };

    private static string FormatTri(TriStateValue t) =>
        t.CollectionStatus == "verified" ? (t.Value == true ? "Yes" : "No") : "Not verified";
}
