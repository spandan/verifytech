namespace DeviceCertAgent.Core.Models;

public sealed class CertifyApiResponse
{
    public string CertificateCode { get; set; } = "";
    public string CertificateUrl { get; set; } = "";
    public string CertificateLevel { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Message { get; set; }
}

public sealed class VerifyApiResponse
{
    public string Result { get; set; } = "";
    public string Message { get; set; } = "";
    public List<VerifyChange> Changes { get; set; } = [];
    public string? AttemptId { get; set; }
    public string? VerificationUrl { get; set; }
}

public sealed class VerifyChange
{
    public string Field { get; set; } = "";
    public object? CertifiedValue { get; set; }
    public object? LiveValue { get; set; }
}

public sealed class ApiErrorResponse
{
    public string? Detail { get; set; }
}

public sealed class ScanSessionStartResponse
{
    public string SessionId { get; set; } = "";
    public string Nonce { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public sealed class ScanSessionSubmitResponse
{
    public string CertificateId { get; set; } = "";
    public string CertificateCode { get; set; } = "";
    public string ReportUrl { get; set; } = "";
    public string? VerificationUrl { get; set; }
    public string? QrCodeUrl { get; set; }
}
