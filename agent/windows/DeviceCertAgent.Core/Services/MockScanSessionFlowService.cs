using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public sealed class MockScanSessionFlowService
{
    public Task<ScanSessionStartResponse> StartSessionAsync(CancellationToken ct = default) =>
        Task.FromResult(new ScanSessionStartResponse
        {
            SessionId = Guid.NewGuid().ToString(),
            Nonce = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
        });

    public Task<ScanSessionSubmitResponse> SubmitAsync(
        ScanSessionStartResponse session,
        CollectionResult collected,
        DateTime scanStartedAt,
        DateTime scanCompletedAt,
        bool adminMode,
        CancellationToken ct = default) =>
        Task.FromResult(new ScanSessionSubmitResponse
        {
            CertificateId = Guid.NewGuid().ToString(),
            CertificateCode = "MOCK-1234-5678",
            ReportUrl = "http://localhost:3000/c/MOCK-1234-5678",
            VerificationUrl = "http://localhost:3000/c/MOCK-1234-5678",
            QrCodeUrl = "http://localhost:3000/c/MOCK-1234-5678",
        });
}
