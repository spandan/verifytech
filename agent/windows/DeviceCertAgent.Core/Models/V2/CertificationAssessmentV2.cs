namespace DeviceCertAgent.Core.Models.V2;

public sealed class CertificationAssessmentV2
{
    public string AssessmentVersion { get; set; } = "2.3";
    public BatteryAssessment Battery { get; set; } = new();
    public List<StorageDriveAssessment> Storage { get; set; } = [];
    public MemoryAssessment Memory { get; set; } = new();
    public CpuAssessment Cpu { get; set; } = new();
    public ThermalAssessment Thermals { get; set; } = new();
    public DisplayAssessment Display { get; set; } = new();
    public SecurityAssessment Security { get; set; } = new();
    public WindowsAssessment Windows { get; set; } = new();
    public NetworkAssessment Network { get; set; } = new();
    public PortInventoryAssessment Ports { get; set; } = new();
    public BenchmarkResults Benchmark { get; set; } = new();
    public ResaleGradeResult ResaleGrade { get; set; } = new();
    public CertificationSummaryReport Summary { get; set; } = new();
}

public sealed class BatteryAssessment
{
    public ConfidenceValue<int?> DesignCapacityMwh { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> FullChargeCapacityMwh { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> CurrentCapacityMwh { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> CycleCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> Manufacturer { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> Chemistry { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> SerialHash { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> ManufactureDate { get; set; } = ConfidenceValue<string?>.Unknown();
    public TriStateValue AcAdapterPresent { get; set; } = TriStateValue.Unknown("power_status");
    public ConfidenceValue<string?> ChargingState { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<double?> WearPercent { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<string?> Condition { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> LifeRecommendation { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> EstimatedRuntimeMinutes { get; set; } = ConfidenceValue<int?>.Unknown();
    public List<string> CapacityHistoryNotes { get; set; } = [];
    public ConfidenceValue<string?> DegradationTrend { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> EstimatedRemainingCycles { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> EstimatedRemainingMonths { get; set; } = ConfidenceValue<int?>.Unknown();
    public List<BatteryCapacityHistoryPoint> CapacityHistory { get; set; } = [];
    public List<string> CertificationNotes { get; set; } = [];
}

public sealed class BatteryCapacityHistoryPoint
{
    public string Period { get; set; } = "";
    public int FullChargeCapacityMwh { get; set; }
    public int DesignCapacityMwh { get; set; }
}

public sealed class StorageDriveAssessment
{
    public int Index { get; set; }
    public ConfidenceValue<string?> Model { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> FirmwareVersion { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> DriveType { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> BusType { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<double?> CapacityGb { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<string?> SerialHash { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> TemperatureC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> SmartOverallStatus { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<double?> HealthPercent { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<double?> PercentageUsed { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<long?> PowerOnHours { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> PowerCycleCount { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> UnsafeShutdownCount { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> ReallocatedSectorCount { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> PendingSectorCount { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> UncorrectableErrors { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> MediaErrorCount { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<double?> WearLevel { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<long?> LifetimeHostReads { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> LifetimeHostWrites { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<long?> NandWrites { get; set; } = ConfidenceValue<long?>.Unknown();
    public ConfidenceValue<double?> SpareCapacityRemaining { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<int?> MaxTemperatureC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> CriticalTemperatureThresholdC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> NvmeCriticalWarnings { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> DeviceHealthScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> Condition { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> RemainingLifeEstimate { get; set; } = ConfidenceValue<string?>.Unknown();
    public Dictionary<string, ConfidenceValue<long?>> NvmeStatistics { get; set; } = new();
    public string? RawSmartEvidenceId { get; set; }
    public StorageHealthHonesty StorageHealth { get; set; } = new();
}

public sealed class MemoryAssessment
{
    public ConfidenceValue<double?> TotalGb { get; set; } = ConfidenceValue<double?>.Unknown();
    public List<MemoryModuleInfo> Modules { get; set; } = [];
    public ConfidenceValue<int?> SlotCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> SlotsUsed { get; set; } = ConfidenceValue<int?>.Unknown();
    public TriStateValue EccSupport { get; set; } = TriStateValue.Unknown("wmi");
    public TriStateValue EccEnabled { get; set; } = TriStateValue.Unknown("wmi");
    public List<string> DiagnosticsNotes { get; set; } = [];
    public ConfidenceValue<string?> HealthSummary { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> StabilityScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public TriStateValue PotentialFaults { get; set; } = TriStateValue.Unknown("memory_test");
}

public sealed class MemoryModuleInfo
{
    public ConfidenceValue<string?> Manufacturer { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> Model { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<double?> CapacityGb { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<int?> SpeedMhz { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> Slot { get; set; } = ConfidenceValue<string?>.Unknown();
}

public sealed class CpuAssessment
{
    public ConfidenceValue<string?> Model { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> CoreCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> ThreadCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<double?> BaseClockGhz { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<double?> CurrentFrequencyGhz { get; set; } = ConfidenceValue<double?>.Unknown();
    public ConfidenceValue<int?> L3CacheKb { get; set; } = ConfidenceValue<int?>.Unknown();
    public TriStateValue VirtualizationSupport { get; set; } = TriStateValue.Unknown("wmi");
    public TriStateValue VirtualizationEnabled { get; set; } = TriStateValue.Unknown("wmi");
    public ConfidenceValue<int?> SingleCoreScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> MultiCoreScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public TriStateValue ThermalThrottlingDetected { get; set; } = TriStateValue.Unknown("benchmark");
    public ConfidenceValue<int?> HealthScore { get; set; } = ConfidenceValue<int?>.Unknown();
}

public sealed class ThermalAssessment
{
    public ConfidenceValue<int?> CpuTempC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> GpuTempC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> SsdTempC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> IdleCpuTempC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> PeakCpuTempC { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> AverageCpuTempC { get; set; } = ConfidenceValue<int?>.Unknown();
    public TriStateValue ThrottlingDetected { get; set; } = TriStateValue.Unknown("benchmark");
    public ConfidenceValue<string?> Condition { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> ConditionScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> ThermalStabilityScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> CoolingHealth { get; set; } = ConfidenceValue<string?>.Unknown();
    public TriStateValue CpuThrottlingDetected { get; set; } = TriStateValue.Unknown("telemetry");
    public TriStateValue GpuThrottlingDetected { get; set; } = TriStateValue.Unknown("telemetry");
    public List<ThermalSample> BenchmarkSamples { get; set; } = [];
}

public sealed class ThermalSample
{
    public int Second { get; set; }
    public double? CpuFrequencyGhz { get; set; }
    public int? CpuTempC { get; set; }
    public double? GpuFrequencyMhz { get; set; }
    public int? GpuTempC { get; set; }
    public double? PackagePowerW { get; set; }
}

public sealed class DisplayAssessment
{
    public ConfidenceValue<string?> Resolution { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<int?> RefreshRateHz { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<double?> SizeInches { get; set; } = ConfidenceValue<double?>.Unknown();
    public TriStateValue InternalDisplay { get; set; } = TriStateValue.Unknown("wmi");
    public TriStateValue Touchscreen { get; set; } = TriStateValue.Unknown("wmi");
    public TriStateValue HdrSupport { get; set; } = TriStateValue.Unknown("wmi");
    public ConfidenceValue<int?> ColorDepth { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> Grade { get; set; } = ConfidenceValue<string?>.Unknown();
}

public sealed class SecurityAssessment
{
    public TriStateValue TpmPresent { get; set; } = TriStateValue.Unknown("wmi");
    public ConfidenceValue<string?> TpmVersion { get; set; } = ConfidenceValue<string?>.Unknown();
    public TriStateValue TpmEnabled { get; set; } = TriStateValue.Unknown("wmi");
    public TriStateValue SecureBoot { get; set; } = TriStateValue.Unknown("registry");
    public ConfidenceValue<string?> BitLockerStatus { get; set; } = ConfidenceValue<string?>.Unknown();
    public TriStateValue DeviceEncryption { get; set; } = TriStateValue.Unknown("bitlocker");
    public TriStateValue VbsEnabled { get; set; } = TriStateValue.Unknown("registry");
    public TriStateValue CredentialGuard { get; set; } = TriStateValue.Unknown("registry");
    public ConfidenceValue<int?> SecurityScore { get; set; } = ConfidenceValue<int?>.Unknown();
}

public sealed class WindowsAssessment
{
    public ConfidenceValue<string?> Edition { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> Build { get; set; } = ConfidenceValue<string?>.Unknown();
    public TriStateValue ActivationStatus { get; set; } = TriStateValue.Unknown("cim");
    public TriStateValue PendingUpdates { get; set; } = TriStateValue.Unknown("registry");
    public TriStateValue PendingReboot { get; set; } = TriStateValue.Unknown("registry");
    public ConfidenceValue<int?> ReadinessScore { get; set; } = ConfidenceValue<int?>.Unknown();
}

public sealed class NetworkAssessment
{
    public ConfidenceValue<string?> WifiStandard { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> WifiGeneration { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> BluetoothVersion { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> LinkSpeedSummary { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> CapabilitySummary { get; set; } = ConfidenceValue<string?>.Unknown();
}

public sealed class PortInventoryAssessment
{
    public ConfidenceValue<int?> UsbACount { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> UsbCCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public TriStateValue Thunderbolt { get; set; } = TriStateValue.Unknown("pnp");
    public ConfidenceValue<int?> HdmiCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> DisplayPortCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public TriStateValue Ethernet { get; set; } = TriStateValue.Unknown("pnp");
    public TriStateValue AudioJack { get; set; } = TriStateValue.Unknown("pnp");
    public TriStateValue SdCardReader { get; set; } = TriStateValue.Unknown("pnp");
    public List<string> InventoryNotes { get; set; } = [];
    public ConfidenceValue<int?> ThunderboltCount { get; set; } = ConfidenceValue<int?>.Unknown();
    public Dictionary<string, string> PortCertificationStatus { get; set; } = new();
}

public sealed class BenchmarkResults
{
    public ConfidenceValue<int?> CpuScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> MemoryScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> StorageScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> GraphicsScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<int?> OverallScore { get; set; } = ConfidenceValue<int?>.Unknown();
    public ConfidenceValue<string?> PerformanceRating { get; set; } = ConfidenceValue<string?>.Unknown();
    public int DurationSeconds { get; set; }
}

public sealed class ResaleGradeResult
{
    public ConfidenceValue<string?> Grade { get; set; } = ConfidenceValue<string?>.Unknown();
    public List<string> Justification { get; set; } = [];
    public ConfidenceValue<string?> ExpectedRemainingServiceLife { get; set; } = ConfidenceValue<string?>.Unknown();
    public ConfidenceValue<string?> RefurbishmentNeeded { get; set; } = ConfidenceValue<string?>.Unknown();
}

public sealed class CertificationSummaryReport
{
    public string DeviceOverview { get; set; } = "";
    public string HealthSummary { get; set; } = "";
    public string BatteryCondition { get; set; } = "";
    public string StorageCondition { get; set; } = "";
    public string PerformanceRating { get; set; } = "";
    public string SecurityRating { get; set; } = "";
    public string FunctionalTestResults { get; set; } = "";
    public string RefurbisherNotes { get; set; } = "";
    public string RecommendedResaleGrade { get; set; } = "";
    public List<string> Warnings { get; set; } = [];
}

/// <summary>User-verified functional tests (filled by WPF UI).</summary>
public sealed class FunctionalCertificationResults
{
    public CameraTestResult CameraTest { get; set; } = new();
    public MicrophoneTestResult MicrophoneTest { get; set; } = new();
    public SpeakerTestResult SpeakerTest { get; set; } = new();
    public UsbTestResult UsbTest { get; set; } = new();
    public DisplayOutputTestResult DisplayOutputTest { get; set; } = new();
    public AudioJackTestResult AudioJackTest { get; set; } = new();

    public DisplayFunctionalTest Display { get; set; } = new();
    public AudioFunctionalTest Audio { get; set; } = new();
    public CameraFunctionalTest Camera { get; set; } = new();
    public KeyboardFunctionalTest Keyboard { get; set; } = new();
    public TouchpadFunctionalTest Touchpad { get; set; } = new();
    public PortFunctionalTest Ports { get; set; } = new();
}

public sealed class DisplayFunctionalTest
{
    public bool? DeadPixelTestPassed { get; set; }
    public bool? BrightnessTestPassed { get; set; }
    public bool? ColorTestPassed { get; set; }
    public bool? UniformityTestPassed { get; set; }
    public string? Grade { get; set; }
    public string? UserNotes { get; set; }
    public bool Skipped { get; set; }
}

public sealed class AudioFunctionalTest
{
    public TriStateValue SpeakerWorking { get; set; } = TriStateValue.Unknown("user_test");
    public TriStateValue MicrophoneWorking { get; set; } = TriStateValue.Unknown("user_test");
    public double? MicrophoneSignalLevel { get; set; }
    public int? SampleRateHz { get; set; }
    public string? MicrophoneDeviceName { get; set; }
    public TriStateValue SignalDetected { get; set; } = TriStateValue.Unknown("local_test");
    public TriStateValue PlaybackConfirmed { get; set; } = TriStateValue.Unknown("local_test");
    public bool Skipped { get; set; }
}

public sealed class CameraFunctionalTest
{
    public TriStateValue CameraOperational { get; set; } = TriStateValue.Unknown("user_test");
    public string? DetectedResolution { get; set; }
    public int? FrameRateFps { get; set; }
    public string? DeviceName { get; set; }
    public string? ValidationTimestamp { get; set; }
    public bool FeedConfirmed { get; set; }
    public bool Skipped { get; set; }
}

public sealed class KeyboardFunctionalTest
{
    public HashSet<string> KeysPressed { get; set; } = [];
    public List<string> KeysMissing { get; set; } = [];
    public List<string> KeysFailed { get; set; } = [];
    public bool? Passed { get; set; }
    public bool Skipped { get; set; }
}

public sealed class TouchpadFunctionalTest
{
    public bool? MovementDetected { get; set; }
    public bool? LeftClick { get; set; }
    public bool? RightClick { get; set; }
    public bool? MultiTouch { get; set; }
    public TriStateValue Operational { get; set; } = TriStateValue.Unknown("user_test");
    public bool Skipped { get; set; }
}

public sealed class PortFunctionalTest
{
    public bool? UsbDeviceDetected { get; set; }
    public string? UserConfirmedPort { get; set; }
    public TriStateValue Operational { get; set; } = TriStateValue.Unknown("user_test");
    public bool Skipped { get; set; }
}
