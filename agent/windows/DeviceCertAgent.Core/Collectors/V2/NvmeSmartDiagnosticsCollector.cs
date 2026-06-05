using System.Text.Json;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Services;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class NvmeSmartDiagnosticsCollector
{
    public (List<StorageDriveAssessment> Drives, string? RawJson) Collect(bool adminMode, List<string> warnings)
    {
        var rawPayload = TryCollectRawSmart(adminMode);
        var drives = new List<StorageDriveAssessment>();
        var rows = WmiHelper.Query(
            "SELECT Index, Model, Size, MediaType, SerialNumber, InterfaceType, FirmwareRevision FROM Win32_DiskDrive");

        var index = 0;
        foreach (var row in rows)
        {
            var size = SafeConvert.ToDouble(row.GetValueOrDefault("Size"));
            if (size is null or <= 0) continue;

            var model = SafeConvert.ToString(row.GetValueOrDefault("Model"));
            var serial = SafeConvert.ToString(row.GetValueOrDefault("SerialNumber"));
            var iface = SafeConvert.ToString(row.GetValueOrDefault("InterfaceType"));
            var firmware = SafeConvert.ToString(row.GetValueOrDefault("FirmwareRevision"));

            var drive = new StorageDriveAssessment
            {
                Index = SafeConvert.ToInt(row.GetValueOrDefault("Index")) ?? index,
                Model = Cv(model, "Win32_DiskDrive", "wmi"),
                FirmwareVersion = Cv(firmware, "Win32_DiskDrive", "wmi", ConfidenceLevel.Medium),
                CapacityGb = ConfidenceValue<double?>.Collected(SafeConvert.BytesToGb((long)size.Value), "Win32_DiskDrive", "wmi"),
                BusType = ConfidenceValue<string?>.Collected(InferBus(iface, model), "inference", "interface", ConfidenceLevel.Medium),
                DriveType = ConfidenceValue<string?>.Collected(InferType(iface, model), "inference", "media", ConfidenceLevel.Medium),
                SerialHash = string.IsNullOrWhiteSpace(serial)
                    ? ConfidenceValue<string?>.Unknown("hash")
                    : ConfidenceValue<string?>.Collected(HashingService.HashIdentifier(serial.Trim()), "hash", "sha256"),
            };

            ApplyReliabilityFromJson(drive, rawPayload, index);
            ApplyWmiPhysicalDisk(drive, index);
            ApplyStorageHonesty(drive, rawPayload, adminMode);
            ClassifyDrive(drive);
            drives.Add(drive);
            index++;
        }

        if (drives.Count == 0)
            warnings.Add("storage_v2.1: no drives with capacity");

        return (drives, rawPayload);
    }

    private static string? TryCollectRawSmart(bool admin)
    {
        if (!admin) return null;
        var script = """
            $out = @()
            Get-PhysicalDisk | ForEach-Object {
              $disk = $_
              $rel = $disk | Get-StorageReliabilityCounter -ErrorAction SilentlyContinue
              $out += [PSCustomObject]@{
                FriendlyName = $disk.FriendlyName
                MediaType = $disk.MediaType
                Size = $disk.Size
                HealthStatus = $disk.HealthStatus
                OperationalStatus = $disk.OperationalStatus
                Temperature = $rel.Temperature
                Wear = $rel.Wear
                PowerOnHours = $rel.PowerOnHours
                ReadErrorsTotal = $rel.ReadErrorsTotal
                WriteErrorsTotal = $rel.WriteErrorsTotal
                TemperatureMax = $rel.TemperatureMax
                StartStopCycleCount = $rel.StartStopCycleCount
                LoadUnloadCycleCount = $rel.LoadUnloadCycleCount
                WearLevel = $rel.Wear
                FlushLatencyMax = $rel.FlushLatencyMax
              }
            }
            $out | ConvertTo-Json -Compress -Depth 5
            """;
        return PowerShellHelper.Run(script, 60000);
    }

    private static void ApplyReliabilityFromJson(StorageDriveAssessment d, string? json, int index)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json.StartsWith('[') ? json : $"[{json}]");
            if (index >= doc.RootElement.GetArrayLength()) return;
            var el = doc.RootElement[index];

            SetLong(d, el, "PowerOnHours", (drive, v) => drive.PowerOnHours = CvLong(v, "StorageReliabilityCounter", "powershell"));
            SetLong(d, el, "StartStopCycleCount", (drive, v) => drive.PowerCycleCount = CvLong(v, "StorageReliabilityCounter", "powershell"));
            SetLong(d, el, "ReadErrorsTotal", (drive, v) => drive.MediaErrorCount = CvLong(v, "StorageReliabilityCounter", "powershell"));
            SetLong(d, el, "WriteErrorsTotal", (drive, v) => drive.UncorrectableErrors = CvLong(v, "StorageReliabilityCounter", "powershell"));
            SetLong(d, el, "Wear", (drive, v) =>
            {
                drive.PercentageUsed = ConfidenceValue<double?>.Collected(v, "StorageReliabilityCounter", "powershell");
                drive.WearLevel = ConfidenceValue<double?>.Collected(v, "StorageReliabilityCounter", "powershell");
                drive.HealthPercent = ConfidenceValue<double?>.Collected(Math.Max(0, 100 - v), "calculated", "wear_inverse");
            });
            SetInt(d, el, "Temperature", (drive, v) => drive.TemperatureC = ConfidenceValue<int?>.Collected(v, "StorageReliabilityCounter", "powershell"));
            SetInt(d, el, "TemperatureMax", (drive, v) => drive.MaxTemperatureC = ConfidenceValue<int?>.Collected(v, "StorageReliabilityCounter", "powershell"));

            var wear = GetDouble(el, "Wear");
            if (wear is > 0)
                d.SpareCapacityRemaining = ConfidenceValue<double?>.Estimated(100 - wear, "calculated", "spare_estimate", "from wear");

            d.SmartOverallStatus = (d.HealthPercent.Value ?? 100) >= 80
                ? ConfidenceValue<string?>.Collected("healthy", "StorageReliabilityCounter", "powershell")
                : ConfidenceValue<string?>.Collected("degraded", "StorageReliabilityCounter", "powershell", ConfidenceLevel.Medium);

            d.DeviceHealthScore = ConfidenceValue<int?>.Collected((int)(d.HealthPercent.Value ?? 70), "engine", "health_score");
            d.NvmeCriticalWarnings = ConfidenceValue<string?>.Collected(
                (d.HealthPercent.Value ?? 100) < 50 ? "wear_warning" : "none", "heuristic", "nvme_warnings", ConfidenceLevel.Medium);

            foreach (var prop in el.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                    d.NvmeStatistics[prop.Name] = ConfidenceValue<long?>.Collected(prop.Value.GetInt64(), "raw_smart", "powershell");
            }
        }
        catch
        {
            // partial parse ok
        }
    }

    private static void ApplyWmiPhysicalDisk(StorageDriveAssessment d, int index)
    {
        var rows = WmiHelper.Query("SELECT HealthStatus, Temperature FROM MSFT_PhysicalDisk", @"\\.\root\Microsoft\Windows\Storage");
        if (index >= rows.Count) return;
        var health = SafeConvert.ToInt(rows[index].GetValueOrDefault("HealthStatus"));
        if (health is >= 0 && d.HealthPercent.Value is null)
        {
            var pct = health switch { 0 => 100.0, 1 => 90.0, 2 => 70.0, _ => 50.0 };
            d.HealthPercent = ConfidenceValue<double?>.Collected(pct, "MSFT_PhysicalDisk", "wmi");
        }
    }

    private static void ApplyStorageHonesty(StorageDriveAssessment d, string? rawPayload, bool adminMode)
    {
        var hasReliability = !string.IsNullOrWhiteSpace(rawPayload)
            && (d.PowerOnHours.Value is not null || d.PercentageUsed.Value is not null);
        d.StorageHealth = new StorageHealthHonesty
        {
            BasicHealthStatus = d.SmartOverallStatus.Value ?? d.Condition.Value,
            WindowsReliabilityCountersCollected = hasReliability,
            FullSmartAttributesCollected = false,
            NvmeLogPagesCollected = false,
            CollectionLevel = adminMode && hasReliability ? "windows_storage_api" : "windows_storage_api_limited",
            Confidence = hasReliability ? "medium" : "low",
        };
    }

    private static void ClassifyDrive(StorageDriveAssessment d)
    {
        var used = d.PercentageUsed.Value ?? (100 - (d.HealthPercent.Value ?? 70));
        var (cond, life) = used switch
        {
            < 15 => ("Excellent", "2+ years"),
            < 35 => ("Good", "1-2 years"),
            < 55 => ("Fair", "6-12 months"),
            < 75 => ("Warning", "3-6 months"),
            _ => ("Critical", "Replace before resale"),
        };
        d.Condition = ConfidenceValue<string?>.Collected(cond, "engine", "storage_classifier");
        d.RemainingLifeEstimate = ConfidenceValue<string?>.Collected(life, "engine", "life_estimate", ConfidenceLevel.Medium);
    }

    private static ConfidenceValue<string?> Cv(string? v, string src, string method, ConfidenceLevel c = ConfidenceLevel.High) =>
        string.IsNullOrWhiteSpace(v) ? ConfidenceValue<string?>.Unknown(method) : ConfidenceValue<string?>.Collected(v.Trim(), src, method, c);

    private static ConfidenceValue<long?> CvLong(long v, string src, string method) =>
        ConfidenceValue<long?>.Collected(v, src, method);

    private static void SetLong(StorageDriveAssessment d, JsonElement el, string name, Action<StorageDriveAssessment, long> apply)
    {
        if (el.TryGetProperty(name, out var p) && p.TryGetInt64(out var v)) apply(d, v);
    }

    private static void SetInt(StorageDriveAssessment d, JsonElement el, string name, Action<StorageDriveAssessment, int> apply)
    {
        if (el.TryGetProperty(name, out var p) && p.TryGetInt32(out var v)) apply(d, v);
    }

    private static double? GetDouble(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.TryGetDouble(out var v) ? v : null;

    private static string InferBus(string? iface, string? model)
    {
        var c = $"{iface} {model}".ToUpperInvariant();
        if (c.Contains("NVME")) return "NVMe";
        if (c.Contains("USB")) return "USB";
        if (c.Contains("SAS")) return "SAS";
        return "SATA";
    }

    private static string InferType(string? iface, string? model)
    {
        var c = $"{iface} {model}".ToUpperInvariant();
        if (c.Contains("NVME")) return "NVMe";
        if (c.Contains("SSD")) return "SSD";
        if (c.Contains("HDD")) return "HDD";
        return "Unknown";
    }
}
