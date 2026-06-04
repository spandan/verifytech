using DeviceCertAgent.Models;
using DeviceCertAgent.Utilities;

namespace DeviceCertAgent.Collectors;

public sealed class WindowsDisplayCollector
{
    public DisplayInfo Collect(List<string> warnings)
    {
        var rows = WmiHelper.Query(
            "SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController WHERE CurrentHorizontalResolution IS NOT NULL"
        );

        if (rows.Count == 0)
        {
            warnings.Add("Display resolution unavailable");
            return new DisplayInfo();
        }

        var primary = rows.OrderByDescending(r =>
            (SafeConvert.ToInt(r.GetValueOrDefault("CurrentHorizontalResolution")) ?? 0) *
            (SafeConvert.ToInt(r.GetValueOrDefault("CurrentVerticalResolution")) ?? 0)
        ).First();

        var w = SafeConvert.ToInt(primary.GetValueOrDefault("CurrentHorizontalResolution"));
        var h = SafeConvert.ToInt(primary.GetValueOrDefault("CurrentVerticalResolution"));
        var refresh = SafeConvert.ToInt(primary.GetValueOrDefault("CurrentRefreshRate"));

        return new DisplayInfo
        {
            Resolution = w is > 0 && h is > 0 ? $"{w}x{h}" : null,
            RefreshRateHz = refresh is > 0 ? refresh : null,
        };
    }
}
