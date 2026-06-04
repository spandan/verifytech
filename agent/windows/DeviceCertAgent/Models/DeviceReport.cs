namespace DeviceCertAgent.Models;

public sealed class AgentOptions
{
    public string Mode { get; set; } = "certify";
    public string? CertificateCode { get; set; }
    public string ApiUrl { get; set; } = "http://localhost:8000";
    public string? IntakeId { get; set; }
    public bool DryRun { get; set; }
    public bool PrintJson { get; set; }
}

public sealed class DeviceReport
{
    public string SchemaVersion { get; set; } = "1.0";
    public string Platform { get; set; } = "windows";
    public CollectionContext CollectionContext { get; set; } = new();
    public Tier1Identity Tier1CertificationIdentity { get; set; } = new();
    public Tier2Value Tier2ValueDetermination { get; set; } = new();
    public Tier3Intelligence Tier3OptionalIntelligence { get; set; } = new();
    public AgentMetadata AgentMetadata { get; set; } = new();
}

public sealed class CollectionContext
{
    public string Mode { get; set; } = "initial_certification";
    public string CollectorVersion { get; set; } = CollectorConstants.Version;
    public string CollectedAt { get; set; } = "";
    public string? CertificateCode { get; set; }
    public string? IntakeId { get; set; }
}

public static class CollectorConstants
{
    public const string Version = "0.1.0";
}

public sealed class Tier1Identity
{
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string DeviceType { get; set; } = "laptop";
    public string OsFamily { get; set; } = "windows";
    public string OsVersion { get; set; } = "";
    public string? OsBuild { get; set; }
    public string SerialNumberHash { get; set; } = "";
    public string HardwareUuidHash { get; set; } = "";
    public string? MotherboardSerialHash { get; set; }
    public string? PrimaryStorageSerialHash { get; set; }
    public string CpuModel { get; set; } = "";
    public double RamTotalGb { get; set; }
    public double StorageTotalGb { get; set; }
}

public sealed class Tier2Value
{
    public CpuInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public List<StorageDriveInfo> Storage { get; set; } = [];
    public BatteryInfo Battery { get; set; } = new();
    public DisplayInfo Display { get; set; } = new();
    public GraphicsInfo Graphics { get; set; } = new();
    public FunctionalReadiness FunctionalReadiness { get; set; } = new();
}

public sealed class CpuInfo
{
    public string? Model { get; set; }
    public int? CoreCount { get; set; }
    public int? ThreadCount { get; set; }
}

public sealed class MemoryInfo
{
    public double? TotalGb { get; set; }
    public string? Details { get; set; }
}

public sealed class StorageDriveInfo
{
    public int Index { get; set; }
    public string? DriveType { get; set; }
    public double CapacityGb { get; set; }
    public double? HealthPercent { get; set; }
    public string? SmartStatus { get; set; }
    public string? SerialHash { get; set; }
}

public sealed class BatteryInfo
{
    public bool? Present { get; set; }
    public int? DesignCapacityMwh { get; set; }
    public int? FullChargeCapacityMwh { get; set; }
    public double? HealthPercent { get; set; }
    public int? CycleCount { get; set; }
}

public sealed class DisplayInfo
{
    public string? Resolution { get; set; }
    public int? RefreshRateHz { get; set; }
}

public sealed class GraphicsInfo
{
    public string? GpuModel { get; set; }
    public string? DriverVersion { get; set; }
}

public sealed class FunctionalReadiness
{
    public bool? CameraPresent { get; set; }
    public bool? MicrophonePresent { get; set; }
    public bool? SpeakerPresent { get; set; }
    public bool? WifiPresent { get; set; }
    public bool? BluetoothPresent { get; set; }
    public bool? KeyboardPresent { get; set; }
    public bool? TouchpadPresent { get; set; }
    public string? ChargingStatus { get; set; }
}

public sealed class Tier3Intelligence
{
    public SecurityInfo Security { get; set; } = new();
    public FirmwareInfo Firmware { get; set; } = new();
    public NetworkInfo Network { get; set; } = new();
    public PerformanceInfo Performance { get; set; } = new();
    public SoftwareInfo Software { get; set; } = new();
}

public sealed class SecurityInfo
{
    public bool? TpmPresent { get; set; }
    public string? TpmVersion { get; set; }
    public bool? SecureBootEnabled { get; set; }
    public string? BitlockerStatus { get; set; }
}

public sealed class FirmwareInfo
{
    public string? BiosVersion { get; set; }
    public string? BiosDate { get; set; }
}

public sealed class NetworkInfo
{
    public List<NetworkAdapterInfo> Adapters { get; set; } = [];
    public string? PortSummary { get; set; }
}

public sealed class NetworkAdapterInfo
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool? Connected { get; set; }
}

public sealed class PerformanceInfo
{
    public double? BootTimeSeconds { get; set; }
    public bool? PendingReboot { get; set; }
}

public sealed class SoftwareInfo
{
    public List<string> DriverIssues { get; set; } = [];
}

public sealed class AgentMetadata
{
    public string MachineArchitecture { get; set; } = "";
    public string AgentRuntime { get; set; } = ".NET 8";
    public List<string> CollectionWarnings { get; set; } = [];
}

public sealed class CollectionResult
{
    public Tier1Identity Tier1 { get; set; } = new();
    public Tier2Value Tier2 { get; set; } = new();
    public Tier3Intelligence Tier3 { get; set; } = new();
    public AgentMetadata Metadata { get; set; } = new();
}
