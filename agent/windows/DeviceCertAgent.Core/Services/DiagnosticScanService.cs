using DeviceCertAgent.Core.Collectors;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Services;

/// <summary>Guided diagnostic scan with resale-friendly progress categories.</summary>
public sealed class DiagnosticScanService
{
    private static readonly (string Id, string Title)[] StepDefinitions =
    [
        ("system", "System"),
        ("cpu", "CPU"),
        ("ram", "RAM"),
        ("storage", "Storage"),
        ("battery", "Battery"),
        ("display", "Display"),
        ("gpu", "Graphics"),
        ("os", "Operating system"),
        ("bios", "BIOS / motherboard"),
        ("network", "Network"),
        ("security", "Security & tamper checks"),
    ];

    public IReadOnlyList<ScanStepProgress> CreateSteps() =>
        StepDefinitions.Select(s => new ScanStepProgress { StepId = s.Id, Title = s.Title }).ToList();

    public async Task<CollectionResult> RunAsync(
        bool adminMode,
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
                if (step.Status == ScanStepStatus.InProgress)
                {
                    if (string.IsNullOrWhiteSpace(step.Detail))
                        step.Detail = "Collected";
                    step.Status = ScanStepStatus.Completed;
                }
            }
            catch (Exception ex)
            {
                step.Status = ScanStepStatus.Warning;
                step.Detail = "Some details unavailable";
                warnings.Add($"{id}: {ex.Message}");
            }
            progress?.Report(step);
            await Task.Delay(80, cancellationToken);
        }

        await RunStep("system", () => Task.Run(() =>
        {
            tier1 = new WindowsIdentityCollector().Collect(warnings);
            var step = steps.First(s => s.StepId == "system");
            step.Detail = $"{tier1.Manufacturer} {tier1.Model}".Trim();
        }, cancellationToken));

        await RunStep("cpu", () => Task.Run(() =>
        {
            tier2.Cpu = new WindowsCpuCollector().Collect(warnings);
            if (string.IsNullOrWhiteSpace(tier1.CpuModel) && !string.IsNullOrWhiteSpace(tier2.Cpu.Model))
                tier1.CpuModel = tier2.Cpu.Model!;
            steps.First(s => s.StepId == "cpu").Detail = tier1.CpuModel;
        }, cancellationToken));

        await RunStep("ram", () => Task.Run(() =>
        {
            tier2.Memory = new WindowsMemoryCollector().Collect(warnings);
            if (tier1.RamTotalGb <= 0 && tier2.Memory.TotalGb is > 0)
                tier1.RamTotalGb = tier2.Memory.TotalGb.Value;
            steps.First(s => s.StepId == "ram").Detail = $"{tier1.RamTotalGb:0} GB";
        }, cancellationToken));

        await RunStep("storage", () => Task.Run(() =>
        {
            tier2.Storage = new WindowsStorageCollector().Collect(warnings);
            if (tier1.StorageTotalGb <= 0 && tier2.Storage.Count > 0)
                tier1.StorageTotalGb = Math.Round(tier2.Storage.Sum(d => d.CapacityGb), 2);
            var primary = tier2.Storage.OrderByDescending(d => d.CapacityGb).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(tier1.PrimaryStorageSerialHash) && !string.IsNullOrWhiteSpace(primary?.SerialHash))
                tier1.PrimaryStorageSerialHash = primary.SerialHash;
            steps.First(s => s.StepId == "storage").Detail = tier2.Storage.Count > 0
                ? $"{tier2.Storage.Count} drive(s), {tier1.StorageTotalGb:0} GB total"
                : "No drives detected";
        }, cancellationToken));

        await RunStep("battery", () => Task.Run(() =>
        {
            tier2.Battery = new WindowsBatteryCollector().Collect(warnings);
            var step = steps.First(s => s.StepId == "battery");
            step.Detail = FormatBatteryDetail(tier2.Battery, warnings);
            if (tier2.Battery.Present == true && tier2.Battery.HealthPercent is null
                && ChassisHelper.IsLaptopChassis())
                step.Status = ScanStepStatus.Warning;
        }, cancellationToken));

        await RunStep("display", () => Task.Run(() =>
        {
            tier2.Display = new WindowsDisplayCollector().Collect(warnings);
            steps.First(s => s.StepId == "display").Detail =
                tier2.Display.Resolution ?? "Resolution unavailable";
        }, cancellationToken));

        await RunStep("gpu", () => Task.Run(() =>
        {
            tier2.Graphics = new WindowsGraphicsCollector().Collect(warnings);
            steps.First(s => s.StepId == "gpu").Detail =
                tier2.Graphics.GpuModel ?? "GPU model unavailable";
        }, cancellationToken));

        await RunStep("os", () => Task.Run(() =>
        {
            new WindowsOsCollector().Enrich(tier1, warnings);
            steps.First(s => s.StepId == "os").Detail = tier1.OsVersion;
        }, cancellationToken));

        await RunStep("bios", () => Task.Run(() =>
        {
            tier3.Firmware = new WindowsSecurityCollector().CollectFirmware(warnings);
            EnrichMotherboard(tier3.Firmware, warnings);
            steps.First(s => s.StepId == "bios").Detail =
                tier3.Firmware.BiosVersion ?? "BIOS version unavailable";
        }, cancellationToken));

        await RunStep("network", () => Task.Run(() =>
        {
            tier3.Network = new WindowsSecurityCollector().CollectNetwork(warnings);
            var adapters = tier3.Network.Adapters.Count(a => a.Connected == true);
            steps.First(s => s.StepId == "network").Detail = adapters > 0
                ? $"{adapters} active adapter(s)"
                : tier3.Network.PortSummary ?? "Adapters enumerated";
        }, cancellationToken));

        await RunStep("security", () => Task.Run(() =>
        {
            tier3.Security = new WindowsSecurityCollector().CollectSecurity(warnings);
            tier3.Performance = new WindowsSecurityCollector().CollectPerformance(warnings);
            tier3.Software = new WindowsSecurityCollector().CollectSoftware(warnings);
            tier2.FunctionalReadiness = new WindowsFunctionalCollector().Collect(warnings);
            ValidateTier1(tier1, warnings);
            var sec = tier3.Security;
            var parts = new List<string>();
            if (sec.TpmPresent == true) parts.Add("TPM");
            if (sec.SecureBootEnabled == true) parts.Add("Secure Boot");
            steps.First(s => s.StepId == "security").Detail = parts.Count > 0
                ? string.Join(", ", parts)
                : "Baseline checks complete";
        }, cancellationToken));

        if (adminMode)
        {
            var enhanced = new EnhancedScanService();
            var interim = new CollectionResult
            {
                Tier1 = tier1,
                Tier2 = tier2,
                Tier3 = tier3,
                Metadata = new AgentMetadata { CollectionWarnings = warnings },
            };
            await enhanced.ApplyEnhancedAsync(interim, true, null, cancellationToken);
            warnings.AddRange(interim.Metadata.CollectionWarnings);
        }

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
                AdminModeUsed = adminMode,
            },
        };
    }

    private static string FormatBatteryDetail(BatteryInfo battery, List<string> warnings)
    {
        if (battery.Present != true)
            return ChassisHelper.IsLaptopChassis() ? "Laptop — battery not reported" : "No battery detected";

        if (battery.HealthPercent is not null)
            return $"{battery.HealthPercent:0}% charge / health";

        if (battery.FullChargeCapacityMwh is > 0)
            return $"Installed ({battery.FullChargeCapacityMwh / 1000.0:0} Wh)";

        return "Present — level unavailable";
    }

    private static void EnrichMotherboard(FirmwareInfo firmware, List<string> warnings)
    {
        try
        {
            var board = WmiHelper.Query(
                "SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard"
            ).FirstOrDefault();
            if (board is null) return;

            var serial = SafeConvert.ToString(board.GetValueOrDefault("SerialNumber"));
            firmware.BiosVersion = string.Join(
                " · ",
                new[]
                {
                    firmware.BiosVersion,
                    SafeConvert.ToString(board.GetValueOrDefault("Manufacturer")),
                    SafeConvert.ToString(board.GetValueOrDefault("Product")),
                }.Where(s => !string.IsNullOrWhiteSpace(s))
            );
            if (!string.IsNullOrWhiteSpace(serial))
                warnings.Add($"motherboard_serial_hash:{HashingService.HashIdentifier(serial)[..16]}…");
        }
        catch
        {
            warnings.Add("Motherboard details unavailable");
        }
    }

    private static void ValidateTier1(Tier1Identity tier1, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(tier1.SerialNumberHash))
            warnings.Add("Serial identity unavailable");
        if (string.IsNullOrWhiteSpace(tier1.HardwareUuidHash))
            warnings.Add("Hardware UUID unavailable");
    }
}
