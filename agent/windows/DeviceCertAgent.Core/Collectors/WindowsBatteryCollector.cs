using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsBatteryCollector
{
    public BatteryInfo Collect(List<string> warnings)
    {
        var rows = WmiHelper.Query(
            "SELECT DesignCapacity, FullChargeCapacity, CycleCount, BatteryStatus, EstimatedChargeRemaining FROM Win32_Battery"
        );

        if (rows.Count == 0)
            return new BatteryInfo { Present = false };

        var battery = rows[0];
        var design = SafeConvert.ToInt(battery.GetValueOrDefault("DesignCapacity"));
        var full = SafeConvert.ToInt(battery.GetValueOrDefault("FullChargeCapacity"));
        var cycles = SafeConvert.ToInt(battery.GetValueOrDefault("CycleCount"));

        double? health = null;
        if (design is > 0 && full is > 0)
            health = Math.Round(Math.Min(100, full.Value * 100.0 / design.Value), 1);

        if (health is null)
            warnings.Add("Battery health could not be calculated");

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
