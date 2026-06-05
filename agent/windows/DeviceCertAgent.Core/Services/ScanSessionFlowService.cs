using System.Net.Http;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public sealed class ScanSessionFlowService : IDisposable
{
    private readonly ApiClient _api;
    private readonly AgentRuntimeSettings _settings;
    private readonly AgentLogger _logger = new();
    private readonly ReportAssemblyService _reportAssembly = new();

    public ScanSessionFlowService(AgentRuntimeSettings settings)
    {
        _settings = settings;
        _api = new ApiClient(settings.ApiBaseUrl, settings.AgentVersion, settings.BuildChannel);
        _logger.Info(
            $"API configured: {_api.BaseUrl} (channel={settings.BuildChannel}, env={settings.AppEnv})");
    }

    public async Task<ScanSessionStartResponse> StartSessionAsync(CancellationToken ct = default)
    {
        var endpoint = _api.EndpointUrl("api/scan-sessions/start");
        _logger.Info($"Starting scan session at {endpoint}");
        try
        {
            return await _api.StartScanSessionAsync(_settings.AgentVersion, _settings.BuildChannel, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to start scan session at {endpoint}", ex);
            throw MapFriendly(ex, endpoint);
        }
    }

    public async Task<ScanSessionSubmitResponse> SubmitAsync(
        ScanSessionStartResponse session,
        CollectionResult collected,
        DateTime scanStartedAt,
        DateTime scanCompletedAt,
        bool adminMode,
        CancellationToken ct = default)
    {
        var launch = new AppLaunchOptions { Mode = "certify" };
        var report = _reportAssembly.Assemble(collected, launch, adminMode);
        var fingerprint = HardwareFingerprintService.Compute(collected);

        var payload = new ScanSessionSubmitPayload
        {
            SessionId = session.SessionId,
            Nonce = session.Nonce,
            AgentVersion = _settings.AgentVersion,
            Platform = "windows",
            ScanStartedAt = scanStartedAt,
            ScanCompletedAt = scanCompletedAt,
            AdminMode = adminMode,
            HardwareFingerprint = fingerprint,
            ScanData = report,
        };

        _logger.Info($"Submitting scan session {MaskId(session.SessionId)}");
        var endpoint = _api.EndpointUrl($"api/scan-sessions/{session.SessionId}/submit");
        try
        {
            var result = await _api.SubmitScanSessionAsync(session.SessionId, payload, ct);
            _logger.Info($"Certificate issued {result.CertificateCode}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Scan submission failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint);
        }
    }

    private static string MaskId(string id) =>
        id.Length <= 8 ? "****" : $"{id[..4]}…{id[^4..]}";

    private static Exception MapFriendly(Exception ex, string endpointUrl) => ex switch
    {
        HttpRequestException { Message: var m } when m.Contains("410") || m.Contains("expired", StringComparison.OrdinalIgnoreCase)
            => new InvalidOperationException("Your scan session expired. Please start a new certification scan."),
        HttpRequestException { Message: var m } when m.Contains("409")
            => new InvalidOperationException("This scan was already submitted."),
        HttpRequestException { Message: var m } when m.Contains("503") || m.Contains("502")
            => new InvalidOperationException(
                $"The VerifyTech server is temporarily unavailable.\n\nEndpoint: {endpointUrl}\n\nDetails: {m}"),
        HttpRequestException hre
            => new InvalidOperationException(
                $"Unable to reach VerifyTech. Check your internet connection and try again.\n\nEndpoint: {endpointUrl}\n\nDetails: {hre.Message}"),
        TaskCanceledException
            => new InvalidOperationException(
                $"The request timed out. Check your connection and try again.\n\nEndpoint: {endpointUrl}"),
        _ => ex,
    };

    public void Dispose() => _api.Dispose();
}

public sealed class ScanSessionSubmitPayload
{
    public string SessionId { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string AgentVersion { get; set; } = "";
    public string Platform { get; set; } = "windows";
    public DateTime ScanStartedAt { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public bool AdminMode { get; set; }
    public string HardwareFingerprint { get; set; } = "";
    public DeviceReport ScanData { get; set; } = new();
}
