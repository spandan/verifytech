using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.Add("User-Agent", $"DeviceCertAgent/{CollectorConstants.Version}");
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
