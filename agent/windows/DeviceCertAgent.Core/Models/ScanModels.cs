using System.ComponentModel;
using System.Runtime.CompilerServices;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Models.V2;

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

public sealed class ScanStepProgress : INotifyPropertyChanged
{
    public required string StepId { get; init; }
    public required string Title { get; init; }

    private ScanStepStatus _status = ScanStepStatus.Pending;
    public ScanStepStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    private string? _detail;
    public string? Detail
    {
        get => _detail;
        set
        {
            if (_detail == value) return;
            _detail = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

public enum AgentLaunchMode
{
    Standalone,
    Paired,
}

public sealed class AppLaunchOptions
{
    public string Mode { get; set; } = "certify";
    public AgentLaunchMode LaunchMode { get; set; } = AgentLaunchMode.Standalone;
    public string? CertificateCode { get; set; }
    public string? IntakeId { get; set; }
    public string? ApiUrlOverride { get; set; }
    public string? PairingCode { get; set; }
    public bool MockApi { get; set; }
    public bool Headless { get; set; }
    public bool EnhancedScanOnStartup { get; set; }
}

public sealed class EndpointSettings
{
    public string ApiBaseUrl { get; set; } = "https://api.yourdomain.com";
    public string Environment { get; set; } = "production";
    public bool MockApi { get; set; }
}

public sealed class AgentRuntimeSettings
{
    public string ApiBaseUrl { get; set; } = AgentConfig.ProductionApiBaseUrl;
    public string AppEnv { get; set; } = AgentConfig.AppEnvProduction;
    public string BuildChannel { get; set; } = AgentConfig.BuildChannelProduction;
    public string AgentVersion { get; set; } = AgentConfig.AgentVersion;
    public bool AllowEndpointOverride { get; set; }
    public bool ShowDeveloperUi { get; set; }
    public bool MockApi { get; set; }

    public bool IsProduction => AppEnv == AgentConfig.AppEnvProduction;
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
    public string Display { get; set; } = "N/A";
    public string Graphics { get; set; } = "N/A";
    public string Security { get; set; } = "N/A";
    public string CoreChecks { get; set; } = "";
    public double CompletenessPercent { get; set; }
    public string ScanType { get; set; } = "Standard";
    public IReadOnlyList<string> Warnings { get; set; } = [];
    public bool HasWarnings => Warnings.Count > 0;
    public List<ReportCheckItem> HealthChecks { get; set; } = [];
}
