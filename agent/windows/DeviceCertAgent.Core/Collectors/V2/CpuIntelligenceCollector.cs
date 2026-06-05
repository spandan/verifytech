using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class CpuIntelligenceCollector
{
    public CpuAssessment Collect(List<string> warnings)
    {
        var cpu = new CpuAssessment();
        var rows = WmiHelper.Query(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, CurrentClockSpeed, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor");
        if (rows.Count == 0)
        {
            warnings.Add("cpu_v2: Win32_Processor unavailable");
            return cpu;
        }

        var p = rows[0];
        var name = SafeConvert.ToString(p.GetValueOrDefault("Name"))?.Trim();
        var cores = SafeConvert.ToInt(p.GetValueOrDefault("NumberOfCores"));
        var threads = SafeConvert.ToInt(p.GetValueOrDefault("NumberOfLogicalProcessors"));
        var maxMhz = SafeConvert.ToInt(p.GetValueOrDefault("MaxClockSpeed"));
        var curMhz = SafeConvert.ToInt(p.GetValueOrDefault("CurrentClockSpeed"));
        var l3 = SafeConvert.ToInt(p.GetValueOrDefault("L3CacheSize"));
        var virt = SafeConvert.ToBool(p.GetValueOrDefault("VirtualizationFirmwareEnabled"));

        if (!string.IsNullOrWhiteSpace(name))
            cpu.Model = ConfidenceValue<string?>.Collected(name, "Win32_Processor", "wmi");
        if (cores is > 0)
            cpu.CoreCount = ConfidenceValue<int?>.Collected(cores, "Win32_Processor", "wmi");
        if (threads is > 0)
            cpu.ThreadCount = ConfidenceValue<int?>.Collected(threads, "Win32_Processor", "wmi");
        if (maxMhz is > 0)
            cpu.BaseClockGhz = ConfidenceValue<double?>.Collected(Math.Round(maxMhz.Value / 1000.0, 2), "Win32_Processor", "wmi");
        if (curMhz is > 0)
            cpu.CurrentFrequencyGhz = ConfidenceValue<double?>.Collected(Math.Round(curMhz.Value / 1000.0, 2), "Win32_Processor", "wmi");
        if (l3 is > 0)
            cpu.L3CacheKb = ConfidenceValue<int?>.Collected(l3, "Win32_Processor", "wmi");

        cpu.VirtualizationSupport = TriStateValue.Verified(virt == true || name?.Contains("Virtualization", StringComparison.OrdinalIgnoreCase) == true,
            "Win32_Processor", "wmi");
        cpu.VirtualizationEnabled = virt is null
            ? TriStateValue.Unknown("wmi")
            : TriStateValue.Verified(virt.Value, "Win32_Processor", "wmi");

        return cpu;
    }
}
