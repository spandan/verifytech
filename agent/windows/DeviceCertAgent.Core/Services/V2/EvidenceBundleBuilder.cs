using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

public sealed class EvidenceBundleBuilder
{
    public CertificationEvidenceBundle Build(
        byte[]? batteryReport,
        string? storageSmartJson,
        byte[]? benchmarkTelemetry,
        CertificationAssessmentV2 assessment,
        FunctionalCertificationResults? functional)
    {
        var bundle = new CertificationEvidenceBundle
        {
            BuildProvenance = ReadBuildProvenance(),
        };

        if (batteryReport is { Length: > 0 })
            bundle.Artifacts.Add(new EvidenceArtifact
            {
                ArtifactType = EvidenceArtifactTypes.BatteryReport,
                ContentType = "application/xml",
                Content = Gzip(batteryReport),
                Source = "powercfg /batteryreport",
            });

        if (!string.IsNullOrWhiteSpace(storageSmartJson))
            bundle.Artifacts.Add(new EvidenceArtifact
            {
                ArtifactType = EvidenceArtifactTypes.StorageSmart,
                ContentType = "application/json",
                Content = Gzip(System.Text.Encoding.UTF8.GetBytes(storageSmartJson)),
                Source = "StorageReliabilityCounter",
            });

        if (benchmarkTelemetry is { Length: > 0 })
            bundle.Artifacts.Add(new EvidenceArtifact
            {
                ArtifactType = EvidenceArtifactTypes.BenchmarkTelemetry,
                ContentType = "application/json",
                Content = benchmarkTelemetry,
                Source = "ThermalBenchmarkTelemetry",
            });

        var securityJson = JsonSerializer.SerializeToUtf8Bytes(assessment.Security);
        bundle.Artifacts.Add(new EvidenceArtifact
        {
            ArtifactType = EvidenceArtifactTypes.SecuritySnapshot,
            ContentType = "application/json",
            Content = Gzip(securityJson),
            Source = "SecurityAssessmentCollector",
        });

        var validationJson = JsonSerializer.SerializeToUtf8Bytes(new { functional, assessment_version = assessment.AssessmentVersion });
        bundle.Artifacts.Add(new EvidenceArtifact
        {
            ArtifactType = EvidenceArtifactTypes.ValidationResults,
            ContentType = "application/json",
            Content = Gzip(validationJson),
            Source = "functional_and_assessment",
        });

        return bundle;
    }

    private static AgentBuildProvenance ReadBuildProvenance()
    {
        var asm = Assembly.GetExecutingAssembly();
        var prov = new AgentBuildProvenance
        {
            AgentVersion = AgentConfig.AgentVersion,
            BuildChannel = Environment.GetEnvironmentVariable("VERIFYTECH_BUILD_CHANNEL") ?? "production",
            BuildVersion = asm.GetName().Version?.ToString(),
            SigningStatus = "unsigned",
        };

        try
        {
            if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(asm.Location))
            {
                var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(asm.Location);
                if (cert is not null)
                {
                    prov.SigningStatus = "signed";
                    prov.SigningCertificateThumbprint = cert.GetCertHashString();
                }
            }
        }
        catch
        {
            prov.SigningStatus = "unsigned";
        }

        return prov;
    }

    private static byte[] Gzip(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
            gz.Write(data, 0, data.Length);
        return ms.ToArray();
    }
}
