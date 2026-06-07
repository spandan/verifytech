using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public sealed class ApiClient : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly HttpClient _http;
    private readonly string _agentVersion;
    private readonly string _buildChannel;

    public string BaseUrl { get; }

    public ApiClient(string baseUrl, string? agentVersion = null, string? buildChannel = null)
    {
        var normalized = baseUrl.TrimEnd('/');
        if (!normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !normalized.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            && !normalized.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("VerifyTech API must use HTTPS.");

        BaseUrl = normalized;
        _agentVersion = agentVersion ?? AgentConfig.AgentVersion;
        _buildChannel = buildChannel ?? AgentConfig.BuildChannelProduction;

        _http = new HttpClient
        {
            BaseAddress = new Uri(normalized + "/"),
            Timeout = TimeSpan.FromSeconds(90),
        };
        _http.DefaultRequestHeaders.Add("User-Agent", $"{AgentConfig.ProductName}/{_agentVersion}");
        _http.DefaultRequestHeaders.Add("X-VerifyTech-Agent-Version", _agentVersion);
        _http.DefaultRequestHeaders.Add("X-VerifyTech-Build-Channel", _buildChannel);
    }

    public string EndpointUrl(string relativePath) =>
        $"{BaseUrl}/{relativePath.TrimStart('/')}";

    public async Task<ScanSessionStartResponse> StartScanSessionAsync(
        string agentVersion,
        string buildChannel,
        CancellationToken ct = default)
    {
        var linkToken = Environment.GetEnvironmentVariable("VERIFYTECH_ACCOUNT_LINK_TOKEN");
        var notifyEmail = Environment.GetEnvironmentVariable("VERIFYTECH_NOTIFICATION_EMAIL");
        var body = new Dictionary<string, object?>
        {
            ["agent_version"] = agentVersion,
            ["platform"] = "windows",
            ["build_channel"] = buildChannel,
        };
        if (!string.IsNullOrWhiteSpace(linkToken))
            body["account_link_token"] = linkToken.Trim();
        if (!string.IsNullOrWhiteSpace(notifyEmail))
            body["notification_email"] = notifyEmail.Trim();
        var response = await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("api/scan-sessions/start", body, JsonOptions, ct),
            ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ScanSessionStartResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty scan session response.");
    }

    public async Task<ScanSessionSubmitResponse> SubmitScanSessionAsync(
        string sessionId,
        ScanSessionSubmitPayload payload,
        CancellationToken ct = default)
    {
        var response = await SendWithRetryAsync(
            () => _http.PostAsJsonAsync($"api/scan-sessions/{sessionId}/submit", payload, JsonOptions, ct),
            ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ScanSessionSubmitResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty scan submit response.");
    }

    public async Task<ScanPairingExchangeResponse> ExchangePairingAsync(
        string pairingCode,
        string deviceFingerprint,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["pairing_code"] = pairingCode.Trim(),
            ["device_fingerprint"] = deviceFingerprint,
            ["agent_version"] = _agentVersion,
        };
        var response = await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("api/scan-sessions/exchange", body, JsonOptions, ct),
            ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ScanPairingExchangeResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty pairing exchange response.");
    }

    public async Task<CertificationSessionValidateResponse> ValidateCertificationSessionAsync(
        string token,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["token"] = token.Trim() };
        var response = await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("api/certification-sessions/validate", body, JsonOptions, ct),
            ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<CertificationSessionValidateResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty certification validation response.");
    }

    public async Task<ScanPairingExchangeResponse> BeginCertificationScanAsync(
        string token,
        string deviceFingerprint,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["token"] = token.Trim(),
            ["device_fingerprint"] = deviceFingerprint,
            ["agent_version"] = _agentVersion,
        };
        var response = await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("api/certification-sessions/begin-scan", body, JsonOptions, ct),
            ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ScanPairingExchangeResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty certification begin-scan response.");
    }

    public async Task<AgentPairingCreateResponse> CreateAgentPairingAsync(
        string deviceNonce,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["device_nonce"] = deviceNonce.Trim() };
        var response = await SendWithRetryAsync(
            () => _http.PostAsJsonAsync("api/agent/pairing/create", body, JsonOptions, ct),
            ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<AgentPairingCreateResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty agent pairing create response.");
    }

    public async Task<AgentPairingStatusResponse> GetAgentPairingStatusForDeviceAsync(
        string deviceNonce,
        CancellationToken ct = default)
    {
        var query = $"api/agent/pairing/status-for-device?device_nonce={Uri.EscapeDataString(deviceNonce.Trim())}";
        var response = await SendWithRetryAsync(() => _http.GetAsync(query, ct), ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<AgentPairingStatusResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty agent pairing status response.");
    }

    public async Task<AgentPairingStatusResponse> GetAgentPairingStatusAsync(
        string pairingCode,
        string deviceNonce,
        CancellationToken ct = default)
    {
        var query = $"api/agent/pairing/status?pairing_code={Uri.EscapeDataString(pairingCode.Trim())}&device_nonce={Uri.EscapeDataString(deviceNonce.Trim())}";
        var response = await SendWithRetryAsync(() => _http.GetAsync(query, ct), ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<AgentPairingStatusResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty agent pairing status response.");
    }

    public async Task<ScanSessionSubmitResponse> UploadPairedScanAsync(
        string uploadToken,
        ScanSessionSubmitPayload payload,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/scans/upload");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", uploadToken);
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        var response = await SendWithRetryAsync(() => _http.SendAsync(request, ct), ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ScanSessionSubmitResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty paired upload response.");
    }

    public async Task<CertifyApiResponse> CertifyAsync(DeviceReport report, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/reports/certify", report, JsonOptions, ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<CertifyApiResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty certify response from API.");
    }

    public async Task<VerifyApiResponse> VerifyAsync(DeviceReport report, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/reports/verify", report, JsonOptions, ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<VerifyApiResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty verify response from API.");
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken ct)
    {
        var delays = new[] { 0, 1000, 2500 };
        HttpResponseMessage? last = null;
        foreach (var delay in delays)
        {
            if (delay > 0)
                await Task.Delay(delay, ct);
            last = await send();
            if ((int)last.StatusCode < 500)
                return last;
        }
        return last!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        string message;
        try
        {
            var err = JsonSerializer.Deserialize<ApiErrorResponse>(body, JsonOptions);
            message = err?.Detail ?? body;
        }
        catch
        {
            message = body;
        }

        throw new HttpRequestException($"API request failed ({(int)response.StatusCode}): {message}");
    }

    public void Dispose() => _http.Dispose();
}
