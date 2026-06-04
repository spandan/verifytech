using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsGraphicsCollector
{
    public GraphicsInfo Collect(List<string> warnings)
    {
        var rows = WmiHelper.Query(
            "SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController WHERE Name IS NOT NULL"
        );

        if (rows.Count == 0)
        {
            warnings.Add("Graphics adapter information unavailable");
            return new GraphicsInfo();
        }

        var primary = rows
            .OrderByDescending(r => SafeConvert.ToDouble(r.GetValueOrDefault("AdapterRAM")) ?? 0)
            .First();

        return new GraphicsInfo
        {
            GpuModel = SafeConvert.ToString(primary.GetValueOrDefault("Name"))?.Trim(),
            DriverVersion = SafeConvert.ToString(primary.GetValueOrDefault("DriverVersion")),
        };
    }
}
