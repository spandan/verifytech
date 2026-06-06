using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class BatteryIntelligenceCollector
{
    public BatteryAssessment Collect(bool adminMode, List<string> warnings)
    {
        var assessment = new BatteryAssessment();
        var rows = WmiHelper.Query(
            "SELECT DesignCapacity, FullChargeCapacity, CycleCount, BatteryStatus, EstimatedChargeRemaining, " +
            "EstimatedRunTime, TimeToFullCharge, Chemistry, DeviceName, ManufactureName FROM Win32_Battery");

        if (rows.Count == 0)
            rows = WmiHelper.Query(
                "SELECT DesignCapacity, FullChargeCapacity, CycleCount, BatteryStatus, EstimatedChargeRemaining FROM Win32_PortableBattery");

        if (rows.Count > 0)
        {
            var row = rows[0];
            var design = SafeConvert.ToInt(row.GetValueOrDefault("DesignCapacity"));
            var full = SafeConvert.ToInt(row.GetValueOrDefault("FullChargeCapacity"));
            var cycles = SafeConvert.ToInt(row.GetValueOrDefault("CycleCount"));
            var chem = SafeConvert.ToString(row.GetValueOrDefault("Chemistry"));
            var mfg = SafeConvert.ToString(row.GetValueOrDefault("ManufactureName"))
                ?? SafeConvert.ToString(row.GetValueOrDefault("DeviceName"));

            if (design is > 0)
                assessment.DesignCapacityMwh = ConfidenceValue<int?>.Collected(design, "Win32_Battery", "wmi");
            if (full is > 0)
                assessment.FullChargeCapacityMwh = ConfidenceValue<int?>.Collected(full, "Win32_Battery", "wmi");
            if (cycles is > 0)
                assessment.CycleCount = ConfidenceValue<int?>.Collected(cycles, "Win32_Battery", "wmi");
            if (!string.IsNullOrWhiteSpace(chem))
                assessment.Chemistry = ConfidenceValue<string?>.Collected(chem, "Win32_Battery", "wmi");
            if (!string.IsNullOrWhiteSpace(mfg))
                assessment.Manufacturer = ConfidenceValue<string?>.Collected(mfg, "Win32_Battery", "wmi", ConfidenceLevel.Medium);

            var status = SafeConvert.ToInt(row.GetValueOrDefault("BatteryStatus"));
            assessment.ChargingState = ConfidenceValue<string?>.Collected(
                MapChargingState(status), "Win32_Battery", "wmi", ConfidenceLevel.Medium);

            var remaining = SafeConvert.ToInt(row.GetValueOrDefault("EstimatedChargeRemaining"));
            if (remaining is > 0 and <= 100 && full is > 0)
                assessment.CurrentCapacityMwh = ConfidenceValue<int?>.Estimated(
                    (int)(full.Value * remaining.Value / 100.0),
                    "Win32_Battery",
                    "wmi_estimate",
                    "derived from charge %");

            var estRuntime = SafeConvert.ToInt(row.GetValueOrDefault("EstimatedRunTime"));
            if (estRuntime is > 0 and < 71582788)
                assessment.EstimatedRuntimeMinutes = ConfidenceValue<int?>.Collected(
                    estRuntime, "Win32_Battery", "wmi", ConfidenceLevel.Medium);

            var timeToFull = SafeConvert.ToInt(row.GetValueOrDefault("TimeToFullCharge"));
            if (timeToFull is > 0 and < 71582788)
                assessment.CapacityHistoryNotes.Add($"Time to full charge: ~{timeToFull} min (at scan time)");
        }

        if (PowerStatusHelper.TryGetBattery(out var power))
        {
            assessment.AcAdapterPresent = TriStateValue.Verified(power.OnAcPower, "power_status_api", "GetSystemPowerStatus");
            if (power.HasBattery && assessment.ChargingState.Value is null)
                assessment.ChargingState = ConfidenceValue<string?>.Collected(
                    power.OnAcPower ? "ac_power" : "on_battery", "power_status_api", "GetSystemPowerStatus", ConfidenceLevel.Medium);
        }

        TryPowerCfgReport(assessment, warnings);

        if (assessment.DesignCapacityMwh.Value is > 0 && assessment.FullChargeCapacityMwh.Value is > 0)
        {
            var wear = (assessment.DesignCapacityMwh.Value.Value - assessment.FullChargeCapacityMwh.Value.Value)
                * 100.0 / assessment.DesignCapacityMwh.Value.Value;
            wear = Math.Clamp(Math.Round(wear, 1), 0, 100);
            assessment.WearPercent = ConfidenceValue<double?>.Collected(wear, "calculated", "wear_formula", ConfidenceLevel.High);
            var (condition, life) = ClassifyBattery(wear, assessment.CycleCount.Value);
            assessment.Condition = ConfidenceValue<string?>.Collected(condition, "engine", "battery_classifier");
            assessment.LifeRecommendation = ConfidenceValue<string?>.Collected(life, "engine", "battery_classifier");
        }
        else if (assessment.FullChargeCapacityMwh.Value is null)
        {
            assessment.Condition = ConfidenceValue<string?>.Unavailable("battery", "classifier", "insufficient capacity data");
        }

        return assessment;
    }

    private static void TryPowerCfgReport(BatteryAssessment a, List<string> warnings)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vt-battery-{Guid.NewGuid():N}.xml");
        try
        {
            var script = $"powercfg /batteryreport /output \"{path}\" /xml";
            _ = PowerShellHelper.Run(script) ?? PowerShellHelper.RunViaCmd($"powercfg /batteryreport /output \"{path}\" /xml");
            if (!File.Exists(path))
            {
                warnings.Add("battery: powercfg report unavailable");
                return;
            }

            var xml = File.ReadAllText(path);
            a.CapacityHistoryNotes.Add("powercfg battery report captured");
            if (a.DesignCapacityMwh.Value is null or <= 0)
            {
                var design = ExtractXmlLong(xml, "DesignCapacity");
                if (design is > 0)
                    a.DesignCapacityMwh = ConfidenceValue<int?>.Collected((int)design, "powercfg", "batteryreport_xml");
            }
            if (a.FullChargeCapacityMwh.Value is null or <= 0)
            {
                var full = ExtractXmlLong(xml, "FullChargeCapacity");
                if (full is > 0)
                    a.FullChargeCapacityMwh = ConfidenceValue<int?>.Collected((int)full, "powercfg", "batteryreport_xml");
            }
            var cycles = ExtractXmlLong(xml, "CycleCount");
            if (cycles is > 0 && a.CycleCount.Value is null)
                a.CycleCount = ConfidenceValue<int?>.Collected((int)cycles, "powercfg", "batteryreport_xml");
        }
        catch (Exception ex)
        {
            warnings.Add($"battery: powercfg failed ({ex.Message})");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }
    }

    private static long? ExtractXmlLong(string xml, string tag)
    {
        var idx = xml.IndexOf($"<{tag}>", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var end = xml.IndexOf($"</{tag}>", idx, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return null;
        var inner = xml[(idx + tag.Length + 2)..end].Trim();
        return long.TryParse(inner, out var v) ? v : null;
    }

    private static (string Condition, string Life) ClassifyBattery(double wear, int? cycles) => wear switch
    {
        < 10 => ("Excellent", "Battery within excellent wear range for resale."),
        < 20 => ("Good", "Battery suitable for typical resale with normal expectations."),
        < 35 => ("Fair", "Battery shows moderate wear; disclose capacity to buyers."),
        < 50 => ("Poor", "Battery wear is elevated; recommend replacement for premium resale."),
        _ => ("Replace Soon", "Battery is heavily worn; replacement recommended before certification."),
    };

    private static string MapChargingState(int? status) => status switch
    {
        1 => "discharging",
        2 => "ac_power",
        3 => "fully_charged",
        6 or 7 or 8 or 9 => "charging",
        _ => "unknown",
    };
}
