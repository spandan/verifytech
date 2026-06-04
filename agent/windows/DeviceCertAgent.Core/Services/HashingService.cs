using System.Security.Cryptography;
using System.Text;

namespace DeviceCertAgent.Core.Services;

public static class HashingService
{
    public static string HashIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().ToUpperInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool IsValidSha256Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            return false;
        return value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}
