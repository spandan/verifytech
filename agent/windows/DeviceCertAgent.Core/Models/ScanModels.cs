namespace DeviceCertAgent.Core.Models;

public enum CollectorStatus
{
    Collected,
    Unavailable,
    PermissionRequired,
    Failed,
}

public enum ScanStepStatus
{
    Pending,
    InProgress,
    Completed,
    Warning,
    Failed,
}

public sealed class CollectorResult<T>
{
    public T? Data { get; init; }
    public CollectorStatus Status { get; init; } = CollectorStatus.Unavailable;
    public string? Message { get; init; }

    public static CollectorResult<T> Ok(T data, string? message = null) =>
        new() { Data = data, Status = CollectorStatus.Collected, Message = message };

    public static CollectorResult<T> Unavailable(string? message = null) =>
        new() { Status = CollectorStatus.Unavailable, Message = message };

    public static CollectorResult<T> PermissionRequired(string? message = null) =>
        new() { Status = CollectorStatus.PermissionRequired, Message = message };

    public static CollectorResult<T> Failed(string? message = null) =>
        new() { Status = CollectorStatus.Failed, Message = message };
}

public sealed class ScanStepProgress
{
    public required string StepId { get; init; }
    public required string Title { get; init; }
    public ScanStepStatus Status { get; set; } = ScanStepStatus.Pending;
    public string? Detail { get; set; }
}

public sealed class ScanSession
{
    public CollectionResult? Result { get; set; }
    public DeviceReport? Report { get; set; }
    public bool EnhancedScanCompleted { get; set; }
    public bool EnhancedScanSkipped { get; set; }
    public string ScanType { get; set; } = "Standard";
    public double CompletenessPercent { get; set; }
    public List<ScanStepProgress> Steps { get; set; } = [];
}

public sealed class AppLaunchOptions
{
    public string Mode { get; set; } = "certify";
    public string? CertificateCode { get; set; }
    public string? IntakeId { get; set; }
    public string? ApiUrlOverride { get; set; }
    public bool MockApi { get; set; }
    public bool Headless { get; set; }
}

public sealed class EndpointSettings
{
    public string ApiBaseUrl { get; set; } = "https://api.yourdomain.com";
    public string Environment { get; set; } = "production";
    public bool MockApi { get; set; }
}

public sealed class ScanSummary
{
    public string DeviceName { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public string Cpu { get; set; } = "";
    public string Ram { get; set; } = "";
    public string Storage { get; set; } = "";
    public string Battery { get; set; } = "N/A";
    public string StorageHealth { get; set; } = "N/A";
    public string CoreChecks { get; set; } = "";
    public double CompletenessPercent { get; set; }
    public string ScanType { get; set; } = "Standard";
}
