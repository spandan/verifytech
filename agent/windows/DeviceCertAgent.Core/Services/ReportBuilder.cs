using DeviceCertAgent.Core.Collectors;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public sealed class ReportBuilder
{
    private readonly List<string> _warnings = [];

    public DeviceReport Build(AgentOptions options)
    {
        var collected = CollectAll();

        var mode = options.Mode == "verify" ? "buyer_verification" : "initial_certification";
        var collectedAt = DateTime.UtcNow.ToString("o");

        return new DeviceReport
        {
            SchemaVersion = "1.0",
            Platform = "windows",
            CollectionContext = new CollectionContext
            {
                Mode = mode,
                CollectorVersion = CollectorConstants.Version,
                CollectedAt = collectedAt,
                CertificateCode = options.CertificateCode,
                IntakeId = options.IntakeId,
            },
            Tier1CertificationIdentity = collected.Tier1,
            Tier2ValueDetermination = collected.Tier2,
            Tier3OptionalIntelligence = collected.Tier3,
            AgentMetadata = collected.Metadata,
        };
    }

    public CollectionResult CollectAll()
    {
        var tier1 = new WindowsIdentityCollector().Collect(_warnings);
        new WindowsOsCollector().Enrich(tier1, _warnings);

        var tier2 = new Tier2Value
        {
            Cpu = new WindowsCpuCollector().Collect(_warnings),
            Memory = new WindowsMemoryCollector().Collect(_warnings),
            Storage = new WindowsStorageCollector().Collect(_warnings),
            Battery = new WindowsBatteryCollector().Collect(_warnings),
            Display = new WindowsDisplayCollector().Collect(_warnings),
            Graphics = new WindowsGraphicsCollector().Collect(_warnings),
            FunctionalReadiness = new WindowsFunctionalCollector().Collect(_warnings),
        };

        SyncTier1FromTier2(tier1, tier2);

        var tier3 = new Tier3Intelligence
        {
            Security = new WindowsSecurityCollector().CollectSecurity(_warnings),
            Firmware = new WindowsSecurityCollector().CollectFirmware(_warnings),
            Network = new WindowsSecurityCollector().CollectNetwork(_warnings),
            Performance = new WindowsSecurityCollector().CollectPerformance(_warnings),
            Software = new WindowsSecurityCollector().CollectSoftware(_warnings),
        };

        ValidateTier1(tier1);

        return new CollectionResult
        {
            Tier1 = tier1,
            Tier2 = tier2,
            Tier3 = tier3,
            Metadata = new AgentMetadata
            {
                MachineArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                AgentRuntime = $".NET {Environment.Version.Major}",
                CollectionWarnings = [.. _warnings],
            },
        };
    }

    private static void SyncTier1FromTier2(Tier1Identity tier1, Tier2Value tier2)
    {
        if (string.IsNullOrWhiteSpace(tier1.CpuModel) && !string.IsNullOrWhiteSpace(tier2.Cpu.Model))
            tier1.CpuModel = tier2.Cpu.Model!;

        if (tier1.RamTotalGb <= 0 && tier2.Memory.TotalGb is > 0)
            tier1.RamTotalGb = tier2.Memory.TotalGb.Value;

        if (tier1.StorageTotalGb <= 0 && tier2.Storage.Count > 0)
            tier1.StorageTotalGb = Math.Round(tier2.Storage.Sum(d => d.CapacityGb), 2);

        if (string.IsNullOrWhiteSpace(tier1.PrimaryStorageSerialHash))
        {
            var primary = tier2.Storage.OrderByDescending(d => d.CapacityGb).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(primary?.SerialHash))
                tier1.PrimaryStorageSerialHash = primary.SerialHash;
        }
    }

    private void ValidateTier1(Tier1Identity tier1)
    {
        if (string.IsNullOrWhiteSpace(tier1.Manufacturer))
            _warnings.Add("tier1.manufacturer unavailable");
        if (string.IsNullOrWhiteSpace(tier1.Model))
            _warnings.Add("tier1.model unavailable");
        if (string.IsNullOrWhiteSpace(tier1.SerialNumberHash))
            _warnings.Add("tier1.serial_number_hash unavailable — identity certification may fail");
        if (string.IsNullOrWhiteSpace(tier1.HardwareUuidHash))
            _warnings.Add("tier1.hardware_uuid_hash unavailable — identity certification may fail");
        if (string.IsNullOrWhiteSpace(tier1.CpuModel))
            _warnings.Add("tier1.cpu_model unavailable");
        if (tier1.RamTotalGb <= 0)
            _warnings.Add("tier1.ram_total_gb unavailable");
        if (tier1.StorageTotalGb <= 0)
            _warnings.Add("tier1.storage_total_gb unavailable");
    }
}
