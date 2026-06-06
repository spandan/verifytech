using DeviceCertAgent.Core.Collectors.V2;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

public sealed class DeepCertificationOrchestrator
{
    public sealed class RunResult
    {
        public CertificationAssessmentV2 Assessment { get; init; } = new();
        public CertificationEvidenceBundle Evidence { get; init; } = new();
    }

    public async Task<RunResult> RunAsync(
        CollectionResult collected,
        bool adminMode,
        FunctionalCertificationResults? functional,
        CancellationToken ct = default,
        bool runStressBenchmarks = true)
    {
        var warnings = collected.Metadata.CollectionWarnings;
        var assessment = new CertificationAssessmentV2 { AssessmentVersion = "2.3" };

        var (battery, batteryRaw) = new BatteryHistoryAnalyzer().Analyze(
            adminMode, warnings, includeHistoryReport: runStressBenchmarks);
        assessment.Battery = battery;

        var (storage, smartJson) = new NvmeSmartDiagnosticsCollector().Collect(adminMode, warnings);
        assessment.Storage = storage;

        assessment.Memory = new MemoryDiagnosticsCollector().Collect(warnings);
        if (runStressBenchmarks)
            assessment.Memory = new MemoryStabilityValidator().Run(assessment.Memory, ct);

        assessment.Cpu = new CpuIntelligenceCollector().Collect(warnings);
        assessment.Thermals = new ThermalHealthCollector().Collect(assessment.Storage, warnings);

        byte[]? telemetryBytes = null;
        if (runStressBenchmarks)
        {
            var (thermals, telemetry, cpu) = await new ThermalBenchmarkTelemetry().RunAsync(
                assessment.Thermals, assessment.Cpu, ct);
            assessment.Thermals = thermals;
            assessment.Cpu = cpu;
            telemetryBytes = telemetry;

            var (benchmark, cpu2, thermals2) = await new LightweightBenchmarkService().RunAsync(
                collected, assessment.Thermals, assessment.Cpu, ct);
            assessment.Benchmark = benchmark;
            assessment.Cpu = cpu2;
            assessment.Thermals = thermals2;
        }

        assessment.Display = new DisplayDiagnosticsCollector().Collect(warnings);
        assessment.Security = new SecurityAssessmentCollector().Collect(adminMode, warnings);
        assessment.Windows = new WindowsCertificationCollector().Collect(warnings);
        assessment.Network = new NetworkingCollector().Collect(warnings);
        assessment.Ports = new PortInventoryCollector().Collect(warnings);
        if (functional is not null)
            FunctionalTestFinalizer.Reconcile(functional, assessment.Ports);
        ApplyPortFunctionalStatus(assessment.Ports, functional);

        ApplyFunctionalToDisplay(assessment.Display, functional);
        ApplyFunctionalToPorts(assessment, functional);

        assessment.ResaleGrade = new ResaleGradeEngine().Compute(assessment, functional);
        assessment.Summary = new CertificationSummaryBuilder().Build(collected, assessment, functional);

        var evidence = new EvidenceBundleBuilder().Build(
            batteryRaw, smartJson, telemetryBytes, assessment, functional);

        collected.Certification = assessment;
        collected.Evidence = evidence;

        return new RunResult { Assessment = assessment, Evidence = evidence };
    }

    private static void ApplyPortFunctionalStatus(PortInventoryAssessment ports, FunctionalCertificationResults? f)
    {
        ports.PortCertificationStatus["usb"] = MapPortStatus(f?.UsbTest);
        ports.PortCertificationStatus["audio_jack"] = MapPortStatus(f?.AudioJackTest);
        if (ports.AudioJack.Value == true && !ports.PortCertificationStatus.ContainsKey("audio_jack"))
            ports.PortCertificationStatus["audio_jack"] = "Detected but Not Tested";
    }

    private static void ApplyFunctionalToPorts(CertificationAssessmentV2 a, FunctionalCertificationResults? f)
    {
        if (f is null) return;
        a.Ports.PortCertificationStatus["usb"] = MapPortStatus(f.UsbTest);
        a.Ports.PortCertificationStatus["audio_jack"] = MapPortStatus(f.AudioJackTest);
    }

    private static string MapPortStatus(ComponentValidationStatus? test) =>
        test?.Result switch
        {
            ValidationResults.Passed => "Verified",
            ValidationResults.Failed => "Failed",
            ValidationResults.Inconclusive => "Inconclusive",
            _ => "Detected but Not Tested",
        };

    private static void ApplyFunctionalToDisplay(DisplayAssessment d, FunctionalCertificationResults? f)
    {
        if (f?.Display is null or { Skipped: true }) return;
        var pass = f.Display.DeadPixelTestPassed == true
            && f.Display.BrightnessTestPassed != false
            && f.Display.ColorTestPassed != false;
        d.Grade = ConfidenceValue<string?>.Collected(
            f.Display.Grade ?? (pass ? "Pass" : "Review"), "user_functional_test", "display_wizard");
    }
}
