using System.Security.Cryptography;
using System.Text;
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
}
