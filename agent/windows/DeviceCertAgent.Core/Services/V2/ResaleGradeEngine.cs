using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

public sealed class ResaleGradeEngine
{
    public ResaleGradeResult Compute(
        CertificationAssessmentV2 assessment,
        FunctionalCertificationResults? functional)
    {
        var points = 0;
        var max = 0;
        var justification = new List<string>();

        void Add(int score, int weight, string note)
        {
            points += score * weight;
            max += 100 * weight;
            justification.Add(note);
        }

        var batteryWear = assessment.Battery.WearPercent.Value ?? 50;
        Add(GradeComponent(batteryWear, reverse: true), 2, $"Battery wear {batteryWear:0}% ({assessment.Battery.Condition.Value ?? "unknown"})");

        var storageHealth = assessment.Storage.Count > 0
            ? assessment.Storage.Min(s => s.HealthPercent.Value ?? 70)
            : 70;
        Add(GradeComponent(storageHealth, reverse: false), 3, $"Storage health min {storageHealth:0}%");

        Add(assessment.Thermals.ConditionScore.Value ?? 60, 2,
            $"Thermal condition {assessment.Thermals.Condition.Value ?? "unknown"}");

        Add(assessment.Benchmark.OverallScore.Value ?? 50, 2,
            $"Performance {assessment.Benchmark.PerformanceRating.Value ?? "unknown"}");

        Add(assessment.Security.SecurityScore.Value ?? 50, 1,
            $"Security score {assessment.Security.SecurityScore.Value ?? 0}");

        if (functional is not null)
        {
            var funcScore = ScoreFunctional(functional);
            Add(funcScore, 2, $"Functional tests score {funcScore}/100");
        }

        var pct = max > 0 ? points * 100.0 / max : 50;
        var grade = pct switch
        {
            >= 92 => "A+",
            >= 85 => "A",
            >= 78 => "B+",
            >= 68 => "B",
            >= 55 => "C",
            _ => "D",
        };

        var life = grade is "A+" or "A" ? "2-4 years typical service life expected"
            : grade is "B+" or "B" ? "1-3 years with normal use"
            : "Short-term or parts-replacement outlook";

        var refurb = grade is "A+" or "A" ? "Minimal refurbishment expected"
            : grade is "B+" or "B" ? "Battery or cosmetic refresh may improve grade"
            : "Component replacement or deep refurb recommended";

        return new ResaleGradeResult
        {
            Grade = ConfidenceValue<string?>.Collected(grade, "ResaleGradeEngine", "weighted_scoring"),
            Justification = justification,
            ExpectedRemainingServiceLife = ConfidenceValue<string?>.Collected(life, "ResaleGradeEngine", "heuristic", ConfidenceLevel.Medium),
            RefurbishmentNeeded = ConfidenceValue<string?>.Collected(refurb, "ResaleGradeEngine", "heuristic", ConfidenceLevel.Medium),
        };
    }

    private static int GradeComponent(double value, bool reverse)
    {
        var v = reverse ? 100 - value : value;
        return (int)Math.Clamp(v, 0, 100);
    }

    private static int ScoreFunctional(FunctionalCertificationResults f)
    {
        var score = 100;
        if (f.Display is { Skipped: false } d)
        {
            if (d.DeadPixelTestPassed == false) score -= 25;
            if (d.BrightnessTestPassed == false) score -= 10;
        }
        if (f.SpeakerTest.Result == ValidationResults.Failed) score -= 15;
        if (f.MicrophoneTest.Result == ValidationResults.Failed) score -= 10;
        if (f.CameraTest.Result == ValidationResults.Failed) score -= 10;
        if (f.UsbTest.Result == ValidationResults.Failed) score -= 5;
        if (f.Keyboard.Passed == false) score -= 20;
        if (f.Touchpad.Operational.Value == false) score -= 10;
        return Math.Max(0, score);
    }
}
