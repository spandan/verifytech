namespace DeviceCertAgent.Core.Models.V2;

/// <summary>Raw diagnostic evidence preserved for traceability (uploaded to server storage).</summary>
public sealed class CertificationEvidenceBundle
{
    public string BundleVersion { get; set; } = "2.1";
    public List<EvidenceArtifact> Artifacts { get; set; } = [];
    public AgentBuildProvenance? BuildProvenance { get; set; }
}

public sealed class EvidenceArtifact
{
    public required string ArtifactType { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
    public string CollectedAt { get; init; } = DateTime.UtcNow.ToString("o");
    public string Source { get; init; } = "";
}

public static class EvidenceArtifactTypes
{
    public const string BatteryReport = "battery_report";
    public const string StorageSmart = "storage_smart";
    public const string BenchmarkTelemetry = "benchmark_telemetry";
    public const string SecuritySnapshot = "security_snapshot";
    public const string ValidationResults = "validation_results";
    public const string MemoryStability = "memory_stability";
}

public sealed class AgentBuildProvenance
{
    public string AgentVersion { get; set; } = "";
    public string BuildChannel { get; set; } = "";
    public string? BuildVersion { get; set; }
    public string SigningStatus { get; set; } = "unsigned";
    public string? SigningCertificateThumbprint { get; set; }
}
