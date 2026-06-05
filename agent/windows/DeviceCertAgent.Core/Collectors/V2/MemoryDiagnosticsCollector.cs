using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class MemoryDiagnosticsCollector
{
    public MemoryAssessment Collect(List<string> warnings)
    {
        var assessment = new MemoryAssessment();
        var modules = WmiHelper.Query(
            "SELECT Capacity, Speed, Manufacturer, PartNumber, DeviceLocator, SMBIOSMemoryType, DataWidth FROM Win32_PhysicalMemory");

        var slots = WmiHelper.Query("SELECT MaxCapacityEx FROM Win32_PhysicalMemoryArray").FirstOrDefault();
        var maxCap = SafeConvert.ToDouble(slots?.GetValueOrDefault("MaxCapacityEx"));

        double total = 0;
        var speeds = new HashSet<int>();
        var capacities = new HashSet<double>();

        foreach (var mod in modules)
        {
            var cap = SafeConvert.ToDouble(mod.GetValueOrDefault("Capacity"));
            if (cap is > 0) total += cap.Value;
            var speed = SafeConvert.ToInt(mod.GetValueOrDefault("Speed"));
            if (speed is > 0) speeds.Add(speed.Value);
            var capGb = cap is > 0 ? SafeConvert.BytesToGb((long)cap.Value) : 0;
            if (capGb > 0) capacities.Add(capGb);

            assessment.Modules.Add(new MemoryModuleInfo
            {
                Manufacturer = ConfidenceValue<string?>.Collected(
                    SafeConvert.ToString(mod.GetValueOrDefault("Manufacturer")),
                    "Win32_PhysicalMemory", "wmi", ConfidenceLevel.Medium),
                Model = ConfidenceValue<string?>.Collected(
                    SafeConvert.ToString(mod.GetValueOrDefault("PartNumber")),
                    "Win32_PhysicalMemory", "wmi", ConfidenceLevel.Medium),
                CapacityGb = cap is > 0
                    ? ConfidenceValue<double?>.Collected(capGb, "Win32_PhysicalMemory", "wmi")
                    : ConfidenceValue<double?>.Unknown("wmi"),
                SpeedMhz = speed is > 0
                    ? ConfidenceValue<int?>.Collected(speed, "Win32_PhysicalMemory", "wmi")
                    : ConfidenceValue<int?>.Unknown("wmi"),
                Slot = ConfidenceValue<string?>.Collected(
                    SafeConvert.ToString(mod.GetValueOrDefault("DeviceLocator")),
                    "Win32_PhysicalMemory", "wmi", ConfidenceLevel.Medium),
            });
        }

        if (total > 0)
            assessment.TotalGb = ConfidenceValue<double?>.Collected(SafeConvert.BytesToGb((long)total), "Win32_PhysicalMemory", "wmi");

        assessment.SlotsUsed = ConfidenceValue<int?>.Collected(modules.Count, "Win32_PhysicalMemory", "wmi_count");
        if (maxCap is > 0)
            assessment.SlotCount = ConfidenceValue<int?>.Estimated(
                Math.Max(modules.Count, (int)(maxCap / (8L * 1024 * 1024 * 1024))),
                "Win32_PhysicalMemoryArray", "wmi_estimate", "from max capacity");

        var eccType = modules.Select(m => SafeConvert.ToInt(m.GetValueOrDefault("SMBIOSMemoryType"))).FirstOrDefault();
        if (eccType is 2 or 3 or 4)
            assessment.EccSupport = TriStateValue.Verified(true, "Win32_PhysicalMemory", "wmi");
        else if (modules.Count > 0)
            assessment.EccSupport = TriStateValue.Verified(false, "Win32_PhysicalMemory", "wmi");

        if (speeds.Count > 1)
            assessment.DiagnosticsNotes.Add("Mismatched memory module speeds detected");
        if (capacities.Count > 1)
            assessment.DiagnosticsNotes.Add("Mixed memory module capacities detected");
        if (speeds.Count == 1 && speeds.First() < 2400)
            assessment.DiagnosticsNotes.Add("Memory may be underclocked relative to modern standards");

        assessment.HealthSummary = ConfidenceValue<string?>.Collected(
            assessment.DiagnosticsNotes.Count == 0 ? "Healthy configuration" : "Review recommended",
            "engine", "memory_diagnostics", ConfidenceLevel.Medium);

        return assessment;
    }
}
