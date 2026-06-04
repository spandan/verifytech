using System.Text.Json;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public sealed class ReportAssemblyService
{
    public DeviceReport Assemble(CollectionResult collected, AppLaunchOptions launch, bool enhancedIncluded)
    {
        var mode = launch.Mode == "verify" ? "buyer_verification" : "initial_certification";
        var metadata = collected.Metadata;
        if (enhancedIncluded)
            metadata.CollectionWarnings.Add("scan_type:enhanced");
        else
            metadata.CollectionWarnings.Add("scan_type:standard");

        return new DeviceReport
        {
            SchemaVersion = "1.0",
            Platform = "windows",
            CollectionContext = new CollectionContext
            {
                Mode = mode,
                CollectorVersion = CollectorConstants.Version,
                CollectedAt = DateTime.UtcNow.ToString("o"),
                CertificateCode = launch.CertificateCode,
                IntakeId = launch.IntakeId,
            },
            Tier1CertificationIdentity = collected.Tier1,
            Tier2ValueDetermination = collected.Tier2,
            Tier3OptionalIntelligence = collected.Tier3,
            AgentMetadata = metadata,
        };
    }
}

public sealed class LocalCacheCleanupService
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DevicePassport",
        "AgentCache");

    public void SaveTemporaryReport(DeviceReport report)
    {
        Directory.CreateDirectory(CacheDir);
        var path = Path.Combine(CacheDir, "last-scan.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public void ClearCache()
    {
        if (Directory.Exists(CacheDir))
            Directory.Delete(CacheDir, recursive: true);
    }

    public bool HasCache() => Directory.Exists(CacheDir);
}

public interface ISubmissionService
{
    Task<CertifyApiResponse> CertifyAsync(DeviceReport report, CancellationToken ct = default);
    Task<VerifyApiResponse> VerifyAsync(DeviceReport report, CancellationToken ct = default);
}

public sealed class HttpSubmissionService : ISubmissionService, IDisposable
{
    private readonly ApiClient _client;
    public HttpSubmissionService(string apiBaseUrl) => _client = new ApiClient(apiBaseUrl);
    public Task<CertifyApiResponse> CertifyAsync(DeviceReport report, CancellationToken ct = default) =>
        _client.CertifyAsync(report, ct);
    public Task<VerifyApiResponse> VerifyAsync(DeviceReport report, CancellationToken ct = default) =>
        _client.VerifyAsync(report, ct);
    public void Dispose() => _client.Dispose();
}

public sealed class MockSubmissionService : ISubmissionService
{
    public Task<CertifyApiResponse> CertifyAsync(DeviceReport report, CancellationToken ct = default) =>
        Task.FromResult(new CertifyApiResponse
        {
            CertificateCode = "MOCK-1234-5678",
            CertificateUrl = "http://localhost:3000/c/MOCK-1234-5678",
            CertificateLevel = "condition_certified",
            Status = "active",
            Message = "Mock certification successful",
        });

    public Task<VerifyApiResponse> VerifyAsync(DeviceReport report, CancellationToken ct = default) =>
        Task.FromResult(new VerifyApiResponse
        {
            Result = "CERTIFIED_MATCH",
            Message = "This device matches the certified report.",
            Changes = [],
            VerificationUrl = "http://localhost:3000/verification-result/mock",
        });
}

public static class SubmissionServiceFactory
{
    public static ISubmissionService Create(EndpointSettings settings)
    {
        if (settings.MockApi) return new MockSubmissionService();
        return new HttpSubmissionService(settings.ApiBaseUrl);
    }
}
