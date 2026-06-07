using System.Net.Http;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Models.V2;

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

    public async Task<ScanPairingExchangeResponse> ExchangePairingAsync(
        string pairingCode,
        string deviceFingerprint,
        CancellationToken ct = default)
    {
        var endpoint = _api.EndpointUrl("api/scan-sessions/exchange");
        _logger.Info($"Exchanging pairing code at {endpoint}");
        try
        {
            return await _api.ExchangePairingAsync(pairingCode, deviceFingerprint, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Pairing exchange failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint, "Pairing failed");
        }
    }

    public async Task<CertificationSessionValidateResponse> ValidateCertificationSessionAsync(
        string token,
        CancellationToken ct = default)
    {
        var endpoint = _api.EndpointUrl("api/certification-sessions/validate");
        _logger.Info($"Validating certification session at {endpoint}");
        try
        {
            return await _api.ValidateCertificationSessionAsync(token, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Certification validation failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint, "Session validation failed");
        }
    }

    public async Task<ScanPairingExchangeResponse> BeginCertificationScanAsync(
        string token,
        string deviceFingerprint,
        CancellationToken ct = default)
    {
        var endpoint = _api.EndpointUrl("api/certification-sessions/begin-scan");
        _logger.Info($"Beginning certification scan at {endpoint}");
        try
        {
            return await _api.BeginCertificationScanAsync(token, deviceFingerprint, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Certification begin-scan failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint, "Could not start certification scan");
        }
    }

    public async Task<AgentPairingCreateResponse> CreateAgentPairingAsync(
        string deviceNonce,
        CancellationToken ct = default)
    {
        var endpoint = _api.EndpointUrl("api/agent/pairing/create");
        _logger.Info($"Creating agent pairing session at {endpoint}");
        try
        {
            return await _api.CreateAgentPairingAsync(deviceNonce, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Agent pairing create failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint, "Could not create pairing code");
        }
    }

    public async Task<AgentPairingStatusResponse> GetAgentPairingStatusForDeviceAsync(
        string deviceNonce,
        CancellationToken ct = default)
    {
        var endpoint = _api.EndpointUrl("api/agent/pairing/status-for-device");
        try
        {
            return await _api.GetAgentPairingStatusForDeviceAsync(deviceNonce, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Agent pairing status failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint, "Could not check pairing status");
        }
    }

    public async Task<AgentPairingStatusResponse> GetAgentPairingStatusAsync(
        string pairingCode,
        string deviceNonce,
        CancellationToken ct = default)
    {
        var endpoint = _api.EndpointUrl("api/agent/pairing/status");
        try
        {
            return await _api.GetAgentPairingStatusAsync(pairingCode, deviceNonce, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Agent pairing status failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint, "Could not check pairing status");
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
        var payload = BuildSubmitPayload(session.SessionId, session.Nonce, collected, scanStartedAt, scanCompletedAt, adminMode);
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

    public async Task<ScanSessionSubmitResponse> UploadPairedAsync(
        string uploadToken,
        string sessionId,
        CollectionResult collected,
        DateTime scanStartedAt,
        DateTime scanCompletedAt,
        bool adminMode,
        CancellationToken ct = default)
    {
        var payload = BuildSubmitPayload(sessionId, nonce: "", collected, scanStartedAt, scanCompletedAt, adminMode);
        _logger.Info($"Uploading paired scan {MaskId(sessionId)}");
        var endpoint = _api.EndpointUrl("api/scans/upload");
        try
        {
            var result = await _api.UploadPairedScanAsync(uploadToken, payload, ct);
            _logger.Info($"Certificate issued {result.CertificateCode}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Paired upload failed at {endpoint}", ex);
            throw MapFriendly(ex, endpoint, "Upload failed");
        }
    }

    private ScanSessionSubmitPayload BuildSubmitPayload(
        string sessionId,
        string nonce,
        CollectionResult collected,
        DateTime scanStartedAt,
        DateTime scanCompletedAt,
        bool adminMode)
    {
        var launch = new AppLaunchOptions { Mode = "certify" };
        var report = _reportAssembly.Assemble(collected, launch, adminMode);
        report.EvidenceBundle = SlimEvidenceBundle(collected.Evidence);
        var fingerprint = HardwareFingerprintService.Compute(collected);

        return new ScanSessionSubmitPayload
        {
            SessionId = sessionId,
            Nonce = nonce,
            AgentVersion = _settings.AgentVersion,
            Platform = "windows",
            ScanStartedAt = scanStartedAt,
            ScanCompletedAt = scanCompletedAt,
            AdminMode = adminMode,
            HardwareFingerprint = fingerprint,
            ScanData = report,
            EvidenceArtifacts = SerializeEvidence(collected.Evidence),
        };
    }

    private static string MaskId(string id) =>
        id.Length <= 8 ? "****" : $"{id[..4]}…{id[^4..]}";

    private static CertificationEvidenceBundle? SlimEvidenceBundle(CertificationEvidenceBundle? bundle)
    {
        if (bundle is null)
            return null;

        return new CertificationEvidenceBundle
        {
            BundleVersion = bundle.BundleVersion,
            BuildProvenance = bundle.BuildProvenance,
            Artifacts = bundle.Artifacts
                .Select(a => new EvidenceArtifact
                {
                    ArtifactType = a.ArtifactType,
                    ContentType = a.ContentType,
                    Content = [],
                    CollectedAt = a.CollectedAt,
                    Source = a.Source,
                })
                .ToList(),
        };
    }

    private static Exception MapFriendly(Exception ex, string endpointUrl, string? prefix = null) => ex switch
    {
        HttpRequestException { Message: var m } when m.Contains("410") || m.Contains("expired", StringComparison.OrdinalIgnoreCase)
            => new InvalidOperationException(
                prefix is null
                    ? "Your scan session expired. Please start a new certification scan."
                    : $"{prefix}: pairing code expired. Request a new code from the website."),
        HttpRequestException { Message: var m } when m.Contains("409")
            => new InvalidOperationException("This scan was already submitted."),
        HttpRequestException { Message: var m } when m.Contains("422")
            => new InvalidOperationException(ParseApiDetail(m, prefix, "Submission was rejected")),
        HttpRequestException { Message: var m } when m.Contains("500")
            => new InvalidOperationException(
                prefix is null
                    ? "Certronx could not create your certificate (server error). Please try again in a few minutes."
                    : $"{prefix}: server error. Please try again shortly."),
        HttpRequestException { Message: var m } when m.Contains("503") || m.Contains("502")
            => new InvalidOperationException(
                prefix is null
                    ? "The Certronx server is temporarily unavailable. Please try again shortly."
                    : $"{prefix}: server unavailable. Please try again shortly."),
        HttpRequestException hre
            => new InvalidOperationException(
                prefix is null
                    ? $"Could not reach Certronx. Check your internet connection and try again.\n\n{ParseApiDetail(hre.Message, null, "Request failed")}"
                    : $"{prefix}.\n\n{ParseApiDetail(hre.Message, null, "Request failed")}"),
        TaskCanceledException
            => new InvalidOperationException(
                prefix is null
                    ? "The request timed out. Check your connection and try again."
                    : $"{prefix}: request timed out."),
        _ => ex,
    };

    private static string ParseApiDetail(string message, string? prefix, string fallback)
    {
        const string marker = "): ";
        var idx = message.IndexOf(marker, StringComparison.Ordinal);
        var detail = idx >= 0 ? message[(idx + marker.Length)..].Trim() : message.Trim();
        if (string.IsNullOrWhiteSpace(detail))
            return fallback;
        if (detail.StartsWith('{') || detail.StartsWith('['))
            return fallback;
        return detail;
    }

    private static List<EvidenceArtifactUpload>? SerializeEvidence(CertificationEvidenceBundle? bundle)
    {
        if (bundle is null || bundle.Artifacts.Count == 0) return null;
        return bundle.Artifacts.Select(a => new EvidenceArtifactUpload
        {
            ArtifactType = a.ArtifactType,
            ContentType = a.ContentType,
            ContentBase64 = Convert.ToBase64String(a.Content),
            CollectedAt = a.CollectedAt,
            Source = a.Source,
        }).ToList();
    }

    public void Dispose() => _api.Dispose();
}

public sealed class EvidenceArtifactUpload
{
    public string ArtifactType { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string ContentBase64 { get; set; } = "";
    public string CollectedAt { get; set; } = "";
    public string Source { get; set; } = "";
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
    public List<EvidenceArtifactUpload>? EvidenceArtifacts { get; set; }
}

public sealed class PairedScanContext
{
    public required string UploadToken { get; init; }
    public required string ScanSessionId { get; init; }
    public string? LinkedAccountName { get; init; }
}
