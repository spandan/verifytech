using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsBatteryCollector
{
    public BatteryInfo Collect(List<string> warnings)
    {
        var fromWmi = TryCollectFromWmi(warnings);
        if (fromWmi is { Present: true })
            return fromWmi;

        var fromPortable = TryCollectPortableBattery(warnings);
        if (fromPortable is { Present: true })
            return fromPortable;

        var fromWmiRoot = TryCollectRootWmiBattery(warnings);
        if (fromWmiRoot is { Present: true })
            return fromWmiRoot;

        if (PowerStatusHelper.TryGetBattery(out var power) && power.HasBattery)
        {
            var estimated = WmiHelper.FirstString(
                "SELECT EstimatedChargeRemaining FROM Win32_Battery",
                "EstimatedChargeRemaining");

            double? percent = power.ChargePercent;
            if (percent is null or 0 && estimated is not null && int.TryParse(estimated, out var est) && est is > 0 and <= 100)
                percent = est;

            warnings.Add($"battery: detected via {power.Source} (WMI classes empty)");
            return new BatteryInfo
            {
                Present = true,
                HealthPercent = null,
                DesignCapacityMwh = null,
                FullChargeCapacityMwh = null,
                CycleCount = null,
            };
        }

        if (ChassisHelper.IsLaptopChassis())
        {
            warnings.Add("battery: laptop chassis detected but no battery telemetry from WMI or power APIs");
            return new BatteryInfo { Present = true };
        }

        if (fromWmi?.Present == false)
            return fromWmi;

        return new BatteryInfo { Present = false };
    }

    private static BatteryInfo? TryCollectFromWmi(List<string> warnings)
    {
        var rows = WmiHelper.Query(
            "SELECT DesignCapacity, FullChargeCapacity, CycleCount, BatteryStatus, EstimatedChargeRemaining FROM Win32_Battery"
        );

        if (rows.Count == 0)
            return null;

        return BuildFromRow(rows[0], warnings, "Win32_Battery");
    }

    private static BatteryInfo? TryCollectPortableBattery(List<string> warnings)
    {
        var rows = WmiHelper.Query(
            "SELECT DesignCapacity, FullChargeCapacity, CycleCount, BatteryStatus, EstimatedChargeRemaining FROM Win32_PortableBattery"
        );

        if (rows.Count == 0)
            return null;

        return BuildFromRow(rows[0], warnings, "Win32_PortableBattery");
    }

    private static BatteryInfo? TryCollectRootWmiBattery(List<string> warnings)
    {
        try
        {
            var rows = WmiHelper.Query(
                "SELECT EstimatedChargeRemaining, ChargeRemaining FROM BatteryStatus",
                @"\\.\root\WMI"
            );

            if (rows.Count == 0)
                return null;

            var row = rows[0];
            var remaining = SafeConvert.ToInt(row.GetValueOrDefault("EstimatedChargeRemaining"))
                ?? SafeConvert.ToInt(row.GetValueOrDefault("ChargeRemaining"));

            warnings.Add("battery: supplemental data from root\\WMI");
            return new BatteryInfo
            {
                Present = true,
                HealthPercent = null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static BatteryInfo BuildFromRow(Dictionary<string, object?> row, List<string> warnings, string source)
    {
        var design = SafeConvert.ToInt(row.GetValueOrDefault("DesignCapacity"));
        var full = SafeConvert.ToInt(row.GetValueOrDefault("FullChargeCapacity"));
        var cycles = SafeConvert.ToInt(row.GetValueOrDefault("CycleCount"));
        var estimated = SafeConvert.ToInt(row.GetValueOrDefault("EstimatedChargeRemaining"));

        double? health = null;
        if (design is > 0 && full is > 0)
            health = Math.Round(Math.Min(100, full.Value * 100.0 / design.Value), 1);

        if (health is null && estimated is > 0 and <= 100)
            warnings.Add($"battery: current charge {estimated}% (capacity health not available from {source})");
        else if (health is null)
            warnings.Add($"battery: capacity health unknown from {source}");

        return new BatteryInfo
        {
            Present = true,
            DesignCapacityMwh = design,
            FullChargeCapacityMwh = full,
            HealthPercent = health,
            CycleCount = cycles,
        };
    }
}
