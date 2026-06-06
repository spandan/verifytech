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

        var batteryCondition = BuildBatteryCondition(assessment);
        var storageSummary = BuildStorageSummary(assessment);
        var performanceRating = BuildPerformanceRating(assessment);
        var securityRating = BuildSecurityRating(assessment);

        var functionalLines = functional is null
            ? ["Functional tests not completed"]
            : FormatFunctionalLines(functional);
        var functionalText = string.Join(Environment.NewLine, functionalLines);

        var hardwareChecks = new List<ReportCheckItem>
        {
            ReportCheckFormatting.Create("Battery", batteryCondition),
        };
        hardwareChecks.AddRange(assessment.Storage.Select(d =>
        {
            var cond = d.Condition.Value ?? "Unknown";
            var health = d.HealthPercent.Value;
            var text = health is not null ? $"{cond} ({health:0}% health)" : cond;
            return ReportCheckFormatting.Create($"Drive {d.Index}", text);
        }));
        if (assessment.Storage.Count == 0)
            hardwareChecks.Add(ReportCheckFormatting.Create("Storage", storageSummary));
        hardwareChecks.Add(ReportCheckFormatting.Create("Performance", performanceRating));

        return new CertificationSummaryReport
        {
            DeviceOverview = $"{t1.Manufacturer} {t1.Model} ({t1.DeviceType}) — {t1.OsVersion}. " +
                $"CPU: {t1.CpuModel}. RAM: {t1.RamTotalGb:0} GB. Storage: {t1.StorageTotalGb:0} GB.",
            HealthSummary = $"Overall resale grade {assessment.ResaleGrade.Grade.Value}. " +
                $"Performance: {assessment.Benchmark.PerformanceRating.Value}. " +
                $"Security score: {assessment.Security.SecurityScore.Value}/100.",
            BatteryCondition = batteryCondition,
            StorageCondition = storageSummary,
            PerformanceRating = performanceRating,
            SecurityRating = securityRating,
            FunctionalTestResults = functionalText,
            FunctionalTestLines = functionalLines,
            RefurbisherNotes = string.IsNullOrWhiteSpace(assessment.ResaleGrade.RefurbishmentNeeded.Value)
                ? "No additional refurbisher actions required."
                : assessment.ResaleGrade.RefurbishmentNeeded.Value!,
            RecommendedResaleGrade = assessment.ResaleGrade.Grade.Value ?? "C",
            Warnings = warnings,
            HardwareChecks = hardwareChecks,
            SecurityChecks =
            [
                ReportCheckFormatting.Create("TPM", FormatTri(assessment.Security.TpmPresent)),
                ReportCheckFormatting.Create("Secure Boot", FormatTri(assessment.Security.SecureBoot)),
                ReportCheckFormatting.Create("Encryption", FormatTri(assessment.Security.DeviceEncryption)),
            ],
            FunctionalChecks = functionalLines.Select(ReportCheckFormatting.FromFunctionalLine).ToList(),
        };
    }

    private static string BuildBatteryCondition(CertificationAssessmentV2 assessment)
    {
        var cond = assessment.Battery.Condition.Value ?? "Unknown";
        var life = assessment.Battery.LifeRecommendation.Value ?? "No recommendation";
        var wear = assessment.Battery.WearPercent.Value is { } w ? $" ({w:0}% wear)" : "";
        return $"{cond} — {life}{wear}";
    }

    private static string BuildStorageSummary(CertificationAssessmentV2 assessment)
    {
        if (assessment.Storage.Count == 0)
            return "No storage assessment";

        return string.Join("; ", assessment.Storage.Select(s =>
        {
            var cond = s.Condition.Value ?? "Unknown";
            var health = s.HealthPercent.Value;
            return health is not null
                ? $"Drive {s.Index}: {cond} ({health:0}% health)"
                : $"Drive {s.Index}: {cond}";
        }));
    }

    private static string BuildPerformanceRating(CertificationAssessmentV2 assessment)
    {
        var rating = assessment.Benchmark.PerformanceRating.Value ?? "Not measured";
        var cpu = assessment.Benchmark.CpuScore.Value;
        var mem = assessment.Benchmark.MemoryScore.Value;
        var disk = assessment.Benchmark.StorageScore.Value;
        if (cpu is null && mem is null && disk is null)
            return rating;
        return $"{rating} (CPU {cpu}, RAM {mem}, disk {disk})";
    }

    private static string BuildSecurityRating(CertificationAssessmentV2 assessment) =>
        $"TPM: {FormatTri(assessment.Security.TpmPresent)}. " +
        $"Secure Boot: {FormatTri(assessment.Security.SecureBoot)}. " +
        $"Encryption: {FormatTri(assessment.Security.DeviceEncryption)}.";

    private static List<string> FormatFunctionalLines(FunctionalCertificationResults f)
    {
        var parts = new List<string>();
        if (!f.Display.Skipped)
            parts.Add($"Display — {f.Display.Grade ?? (f.Display.DeadPixelTestPassed == true ? "Pass" : "Review")}");
        parts.Add($"Speakers — {FormatValidation(f.SpeakerTest)}");
        parts.Add($"Microphone — {FormatValidation(f.MicrophoneTest)}");
        parts.Add($"Camera — {FormatValidation(f.CameraTest)}");
        parts.Add($"USB — {FormatValidation(f.UsbTest)}");
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
