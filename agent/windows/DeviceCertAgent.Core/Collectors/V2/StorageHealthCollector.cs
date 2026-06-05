using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Services;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class StorageHealthCollector
{
    public List<StorageDriveAssessment> Collect(bool adminMode, List<string> warnings)
    {
        var drives = new List<StorageDriveAssessment>();
        var rows = WmiHelper.Query(
            "SELECT Index, Model, Size, MediaType, SerialNumber, InterfaceType, FirmwareRevision FROM Win32_DiskDrive");

        var reliability = adminMode ? TryReliabilityCounters() : [];

        var index = 0;
        foreach (var row in rows)
        {
            var size = SafeConvert.ToDouble(row.GetValueOrDefault("Size"));
            if (size is null or <= 0) continue;

            var model = SafeConvert.ToString(row.GetValueOrDefault("Model"));
            var serial = SafeConvert.ToString(row.GetValueOrDefault("SerialNumber"));
            var iface = SafeConvert.ToString(row.GetValueOrDefault("InterfaceType"));
            var media = SafeConvert.ToString(row.GetValueOrDefault("MediaType"));
            var firmware = SafeConvert.ToString(row.GetValueOrDefault("FirmwareRevision"));

            var assessment = new StorageDriveAssessment
            {
                Index = SafeConvert.ToInt(row.GetValueOrDefault("Index")) ?? index,
                Model = string.IsNullOrWhiteSpace(model)
                    ? ConfidenceValue<string?>.Unavailable("Win32_DiskDrive", "wmi")
                    : ConfidenceValue<string?>.Collected(model.Trim(), "Win32_DiskDrive", "wmi"),
                FirmwareVersion = string.IsNullOrWhiteSpace(firmware)
                    ? ConfidenceValue<string?>.Unknown("wmi")
                    : ConfidenceValue<string?>.Collected(firmware, "Win32_DiskDrive", "wmi", ConfidenceLevel.Medium),
                CapacityGb = ConfidenceValue<double?>.Collected(
                    SafeConvert.BytesToGb((long)size.Value), "Win32_DiskDrive", "wmi"),
                BusType = ConfidenceValue<string?>.Collected(
                    InferBus(iface, model), "Win32_DiskDrive", "wmi_inference", ConfidenceLevel.Medium),
                DriveType = ConfidenceValue<string?>.Collected(
                    InferDriveType(media, model, iface), "Win32_DiskDrive", "wmi_inference", ConfidenceLevel.Medium),
                SerialHash = string.IsNullOrWhiteSpace(serial)
                    ? ConfidenceValue<string?>.Unknown("hash")
                    : ConfidenceValue<string?>.Collected(HashingService.HashIdentifier(serial.Trim()), "hash", "sha256"),
            };

            EnrichPhysicalDisk(assessment, index);
            if (index < reliability.Count)
                ApplyReliability(assessment, reliability[index]);

            ClassifyStorage(assessment);
            drives.Add(assessment);
            index++;
        }

        if (drives.Count == 0)
            warnings.Add("storage_v2: no drives detected");

        return drives;
    }

    private static List<Dictionary<string, string>> TryReliabilityCounters()
    {
        var script = """
            Get-PhysicalDisk | ForEach-Object {
              $r = $_ | Get-StorageReliabilityCounter -ErrorAction SilentlyContinue
              [PSCustomObject]@{
                FriendlyName = $_.FriendlyName
                Temperature = $r.Temperature
                Wear = $r.Wear
                PowerOnHours = $r.PowerOnHours
                ReadErrorsTotal = $r.ReadErrorsTotal
                WriteErrorsTotal = $r.WriteErrorsTotal
                TemperatureMax = $r.TemperatureMax
              }
            } | ConvertTo-Json -Compress
            """;
        var json = PowerShellHelper.Run(script, 45000);
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                json.StartsWith('[') ? json : $"[{json}]");
            var list = new List<Dictionary<string, string>>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in el.EnumerateObject())
                    dict[prop.Name] = prop.Value.ToString();
                list.Add(dict);
            }
            return list;
        }
        catch
        {
            return [];
        }
    }

    private static void ApplyReliability(StorageDriveAssessment d, Dictionary<string, string> r)
    {
        if (r.TryGetValue("Temperature", out var temp) && int.TryParse(temp, out var t) && t > 0)
            d.TemperatureC = ConfidenceValue<int?>.Collected(t, "StorageReliabilityCounter", "powershell");
        if (r.TryGetValue("Wear", out var wear) && double.TryParse(wear, out var w))
        {
            d.PercentageUsed = ConfidenceValue<double?>.Collected(w, "StorageReliabilityCounter", "powershell");
            d.HealthPercent = ConfidenceValue<double?>.Collected(Math.Max(0, 100 - w), "calculated", "wear_inverse");
        }
        if (r.TryGetValue("PowerOnHours", out var poh) && long.TryParse(poh, out var hours))
            d.PowerOnHours = ConfidenceValue<long?>.Collected(hours, "StorageReliabilityCounter", "powershell");
        if (r.TryGetValue("ReadErrorsTotal", out var re) && long.TryParse(re, out var reads))
            d.NvmeStatistics["lifetime_reads_errors"] = ConfidenceValue<long?>.Collected(reads, "StorageReliabilityCounter", "powershell");
        if (r.TryGetValue("WriteErrorsTotal", out var we) && long.TryParse(we, out var writes))
            d.NvmeStatistics["lifetime_writes_errors"] = ConfidenceValue<long?>.Collected(writes, "StorageReliabilityCounter", "powershell");

        d.SmartOverallStatus = d.HealthPercent.Value is >= 80
            ? ConfidenceValue<string?>.Collected("healthy", "StorageReliabilityCounter", "powershell")
            : ConfidenceValue<string?>.Collected("degraded", "StorageReliabilityCounter", "powershell", ConfidenceLevel.Medium);
    }

    private static void EnrichPhysicalDisk(StorageDriveAssessment d, int index)
    {
        var rows = WmiHelper.Query(
            "SELECT FriendlyName, MediaType, HealthStatus, Size FROM MSFT_PhysicalDisk",
            @"\\.\root\Microsoft\Windows\Storage");
        if (index >= rows.Count) return;
        var row = rows[index];
        var health = SafeConvert.ToInt(row.GetValueOrDefault("HealthStatus"));
        if (health is >= 0)
        {
            var pct = health switch { 0 => 100.0, 1 => 90.0, 2 => 70.0, _ => 50.0 };
            d.HealthPercent ??= ConfidenceValue<double?>.Collected(pct, "MSFT_PhysicalDisk", "wmi");
            d.SmartOverallStatus = ConfidenceValue<string?>.Collected(
                health == 0 ? "healthy" : "warning", "MSFT_PhysicalDisk", "wmi");
        }
        var media = SafeConvert.ToInt(row.GetValueOrDefault("MediaType"));
        if (media is > 0)
            d.DriveType = ConfidenceValue<string?>.Collected(
                media switch { 4 => "SSD", 3 => "HDD", 5 => "NVMe", _ => d.DriveType.Value },
                "MSFT_PhysicalDisk", "wmi");
    }

    private static void ClassifyStorage(StorageDriveAssessment d)
    {
        var health = d.HealthPercent.Value ?? 100;
        var used = d.PercentageUsed.Value ?? (100 - health);
        var (condition, life) = used switch
        {
            < 15 => ("Excellent", "2+ years expected under normal use"),
            < 35 => ("Good", "1-2 years expected"),
            < 55 => ("Fair", "Monitor wear; disclose to buyer"),
            < 75 => ("Warning", "Elevated wear; refurb or replace recommended"),
            _ => ("Critical", "Near end of life; replace before resale"),
        };
        d.Condition = ConfidenceValue<string?>.Collected(condition, "engine", "storage_classifier");
        d.RemainingLifeEstimate = ConfidenceValue<string?>.Collected(life, "engine", "storage_classifier", ConfidenceLevel.Medium);
    }

    private static string InferBus(string? iface, string? model)
    {
        var c = $"{iface} {model}".ToUpperInvariant();
        if (c.Contains("NVME")) return "NVMe";
        if (c.Contains("USB")) return "USB";
        if (c.Contains("SAS")) return "SAS";
        if (c.Contains("SATA") || c.Contains("IDE")) return "SATA";
        return iface ?? "Unknown";
    }

    private static string InferDriveType(string? media, string? model, string? iface)
    {
        var combined = $"{media} {model} {iface}".ToUpperInvariant();
        if (combined.Contains("NVME")) return "NVMe";
        if (combined.Contains("SSD") || combined.Contains("SOLID STATE")) return "SSD";
        if (combined.Contains("HDD") || combined.Contains("FIXED")) return "HDD";
        return "Unknown";
    }
}
