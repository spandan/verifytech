using DeviceCertAgent.Core.Collectors;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Services;

public sealed class EnhancedScanService
{
    public async Task ApplyEnhancedAsync(
        CollectionResult result,
        bool adminGranted,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            progress?.Report("Checking advanced storage health...");
            if (!adminGranted)
            {
                result.Metadata.CollectionWarnings.Add("Enhanced storage SMART: permission_required");
                foreach (var drive in result.Tier2.Storage)
                {
                    drive.SmartStatus = "permission_required";
                }
                return;
            }

            TryEnrichSmart(result);

            progress?.Report("Checking advanced security status...");
            var security = new WindowsSecurityCollector();
            result.Tier3.Security = security.CollectSecurity(result.Metadata.CollectionWarnings);
            result.Tier3.Firmware = security.CollectFirmware(result.Metadata.CollectionWarnings);

            result.Metadata.CollectionWarnings.Add("Enhanced diagnostics included");
        }, cancellationToken);
    }

    public static bool IsRunningAsAdmin()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void TryEnrichSmart(CollectionResult result)
    {
        var rows = WmiHelper.Query(
            "SELECT HealthStatus, Usage, Size FROM MSFT_PhysicalDisk",
            @"\\.\root\Microsoft\Windows\Storage");

        for (var i = 0; i < result.Tier2.Storage.Count; i++)
        {
            if (i >= rows.Count) break;
            var health = SafeConvert.ToInt(rows[i].GetValueOrDefault("HealthStatus"));
            if (health is > 0)
            {
                result.Tier2.Storage[i].HealthPercent = health switch
                {
                    0 => 100,
                    1 => 90,
                    2 => 70,
                    _ => 50,
                };
                result.Tier2.Storage[i].SmartStatus = "ok";
            }
        }
    }
}
