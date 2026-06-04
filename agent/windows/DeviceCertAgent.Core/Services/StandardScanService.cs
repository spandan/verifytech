using DeviceCertAgent.Core.Collectors;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public sealed class StandardScanService
{
    private static readonly (string Id, string Title)[] StepDefinitions =
    [
        ("identity", "Checking device identity"),
        ("os", "Reading Windows version"),
        ("cpu_memory", "Checking processor and memory"),
        ("storage", "Checking storage"),
        ("battery", "Checking battery"),
        ("display_gpu", "Checking display and graphics"),
        ("audio_camera", "Checking camera, microphone, and speaker"),
        ("connectivity", "Checking Wi-Fi and Bluetooth"),
        ("prepare", "Preparing secure report"),
    ];

    public IReadOnlyList<ScanStepProgress> CreateSteps() =>
        StepDefinitions.Select(s => new ScanStepProgress { StepId = s.Id, Title = s.Title }).ToList();

    public async Task<CollectionResult> RunAsync(
        IProgress<ScanStepProgress>? progress,
        IReadOnlyList<ScanStepProgress> steps,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var tier1 = new Tier1Identity();
        var tier2 = new Tier2Value();
        var tier3 = new Tier3Intelligence();

        async Task RunStep(string id, Func<Task> action)
        {
            var step = steps.First(s => s.StepId == id);
            step.Status = ScanStepStatus.InProgress;
            progress?.Report(step);
            try
            {
                await action();
                step.Status = ScanStepStatus.Completed;
            }
            catch (Exception ex)
            {
                step.Status = ScanStepStatus.Warning;
                step.Detail = "Some details unavailable";
                warnings.Add($"{id}: {ex.Message}");
            }
            progress?.Report(step);
            await Task.Delay(120, cancellationToken);
        }

        await RunStep("identity", () => Task.Run(() =>
        {
            tier1 = new WindowsIdentityCollector().Collect(warnings);
        }, cancellationToken));

        await RunStep("os", () => Task.Run(() =>
        {
            new WindowsOsCollector().Enrich(tier1, warnings);
        }, cancellationToken));

        await RunStep("cpu_memory", () => Task.Run(() =>
        {
            tier2.Cpu = new WindowsCpuCollector().Collect(warnings);
            tier2.Memory = new WindowsMemoryCollector().Collect(warnings);
            if (string.IsNullOrWhiteSpace(tier1.CpuModel) && !string.IsNullOrWhiteSpace(tier2.Cpu.Model))
                tier1.CpuModel = tier2.Cpu.Model!;
            if (tier1.RamTotalGb <= 0 && tier2.Memory.TotalGb is > 0)
                tier1.RamTotalGb = tier2.Memory.TotalGb.Value;
        }, cancellationToken));

        await RunStep("storage", () => Task.Run(() =>
        {
            tier2.Storage = new WindowsStorageCollector().Collect(warnings);
            if (tier1.StorageTotalGb <= 0 && tier2.Storage.Count > 0)
                tier1.StorageTotalGb = Math.Round(tier2.Storage.Sum(d => d.CapacityGb), 2);
            var primary = tier2.Storage.OrderByDescending(d => d.CapacityGb).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(tier1.PrimaryStorageSerialHash) && !string.IsNullOrWhiteSpace(primary?.SerialHash))
                tier1.PrimaryStorageSerialHash = primary.SerialHash;
        }, cancellationToken));

        await RunStep("battery", () => Task.Run(() =>
        {
            tier2.Battery = new WindowsBatteryCollector().Collect(warnings);
        }, cancellationToken));

        await RunStep("display_gpu", () => Task.Run(() =>
        {
            tier2.Display = new WindowsDisplayCollector().Collect(warnings);
            tier2.Graphics = new WindowsGraphicsCollector().Collect(warnings);
        }, cancellationToken));

        await RunStep("audio_camera", () => Task.Run(() =>
        {
            tier2.FunctionalReadiness = new WindowsFunctionalCollector().Collect(warnings);
        }, cancellationToken));

        await RunStep("connectivity", () => Task.Run(async () =>
        {
            await Task.Yield();
            tier3.Network = new WindowsSecurityCollector().CollectNetwork(warnings);
        }, cancellationToken));

        await RunStep("prepare", () => Task.Run(() =>
        {
            tier3.Security = new WindowsSecurityCollector().CollectSecurity(warnings);
            tier3.Firmware = new WindowsSecurityCollector().CollectFirmware(warnings);
            tier3.Performance = new WindowsSecurityCollector().CollectPerformance(warnings);
            tier3.Software = new WindowsSecurityCollector().CollectSoftware(warnings);
            ValidateTier1(tier1, warnings);
        }, cancellationToken));

        return new CollectionResult
        {
            Tier1 = tier1,
            Tier2 = tier2,
            Tier3 = tier3,
            Metadata = new AgentMetadata
            {
                MachineArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                AgentRuntime = $".NET {Environment.Version.Major}",
                CollectionWarnings = warnings,
            },
        };
    }

    private static void ValidateTier1(Tier1Identity tier1, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(tier1.SerialNumberHash))
            warnings.Add("Serial identity unavailable");
        if (string.IsNullOrWhiteSpace(tier1.HardwareUuidHash))
            warnings.Add("Hardware UUID unavailable");
    }
}
