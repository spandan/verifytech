using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsMemoryCollector
{
    public MemoryInfo Collect(List<string> warnings)
    {
        var modules = WmiHelper.Query(
            "SELECT Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory"
        );

        if (modules.Count == 0)
        {
            warnings.Add("Memory module details unavailable");
            return new MemoryInfo();
        }

        double totalBytes = 0;
        var parts = new List<string>();
        foreach (var mod in modules)
        {
            var cap = SafeConvert.ToDouble(mod.GetValueOrDefault("Capacity"));
            if (cap is > 0) totalBytes += cap.Value;

            var speed = SafeConvert.ToInt(mod.GetValueOrDefault("Speed"));
            var mfg = SafeConvert.ToString(mod.GetValueOrDefault("Manufacturer"));
            if (cap is > 0)
            {
                var gb = SafeConvert.BytesToGb((long)cap.Value);
                parts.Add(speed is > 0 ? $"{gb}GB @ {speed}MHz" : $"{gb}GB");
            }
            _ = mfg;
        }

        return new MemoryInfo
        {
            TotalGb = totalBytes > 0 ? SafeConvert.BytesToGb((long)totalBytes) : null,
            Details = parts.Count > 0 ? string.Join(", ", parts) : null,
        };
    }
}
