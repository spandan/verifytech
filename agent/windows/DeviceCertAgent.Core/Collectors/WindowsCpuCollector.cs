using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsCpuCollector
{
    public CpuInfo Collect(List<string> warnings)
    {
        var rows = WmiHelper.Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
        if (rows.Count == 0)
        {
            warnings.Add("CPU information unavailable");
            return new CpuInfo();
        }

        var primary = rows[0];
        return new CpuInfo
        {
            Model = SafeConvert.ToString(primary.GetValueOrDefault("Name"))?.Trim(),
            CoreCount = SafeConvert.ToInt(primary.GetValueOrDefault("NumberOfCores")),
            ThreadCount = SafeConvert.ToInt(primary.GetValueOrDefault("NumberOfLogicalProcessors")),
        };
    }
}
