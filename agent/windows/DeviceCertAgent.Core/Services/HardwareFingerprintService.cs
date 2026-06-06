using System.Security.Cryptography;
using System.Text;
using DeviceCertAgent.Core.Collectors;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Services;

public static class HardwareFingerprintService
{
    public static string Compute(CollectionResult result)
    {
        var parts = new[]
        {
            result.Tier1.SerialNumberHash,
            result.Tier1.HardwareUuidHash,
            result.Tier1.MotherboardSerialHash,
            result.Tier1.PrimaryStorageSerialHash,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var payload = string.Join("|", parts);
        if (string.IsNullOrWhiteSpace(payload))
            payload = $"{result.Tier1.Manufacturer}|{result.Tier1.Model}|{result.Tier1.CpuModel}";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Collect identity + primary storage hash for pairing exchange before full scan.</summary>
    public static CollectionResult CollectPairingBootstrap(out List<string> warnings)
    {
        warnings = [];
        var tier1 = new WindowsIdentityCollector().Collect(warnings);
        try
        {
            var drives = new WindowsStorageCollector().Collect(warnings);
            var primary = drives.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(primary?.SerialHash))
                tier1.PrimaryStorageSerialHash = primary.SerialHash;
        }
        catch (Exception ex)
        {
            warnings.Add($"storage bootstrap: {ex.Message}");
        }

        return new CollectionResult { Tier1 = tier1 };
    }
}
