using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsOsCollector
{
    public void Enrich(Tier1Identity tier1, List<string> warnings)
    {
        var os = WmiHelper.Query(
            "SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem"
        ).FirstOrDefault();

        if (os is null)
        {
            warnings.Add("OS information unavailable");
            return;
        }

        var caption = SafeConvert.ToString(os.GetValueOrDefault("Caption")) ?? "Windows";
        var version = SafeConvert.ToString(os.GetValueOrDefault("Version")) ?? "";
        var build = SafeConvert.ToString(os.GetValueOrDefault("BuildNumber"));

        tier1.OsVersion = string.IsNullOrWhiteSpace(version) ? caption : $"{caption} ({version})";
        tier1.OsBuild = build;
    }
}
