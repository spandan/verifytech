using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class DisplayDiagnosticsCollector
{
    public DisplayAssessment Collect(List<string> warnings)
    {
        var d = new DisplayAssessment();
        var rows = WmiHelper.Query(
            "SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, VideoModeDescription FROM Win32_VideoController WHERE CurrentHorizontalResolution IS NOT NULL");

        if (rows.Count == 0)
        {
            warnings.Add("display_v2: resolution unavailable");
            return d;
        }

        var primary = rows.OrderByDescending(r =>
            (SafeConvert.ToInt(r.GetValueOrDefault("CurrentHorizontalResolution")) ?? 0) *
            (SafeConvert.ToInt(r.GetValueOrDefault("CurrentVerticalResolution")) ?? 0)).First();

        var w = SafeConvert.ToInt(primary.GetValueOrDefault("CurrentHorizontalResolution"));
        var h = SafeConvert.ToInt(primary.GetValueOrDefault("CurrentVerticalResolution"));
        var hz = SafeConvert.ToInt(primary.GetValueOrDefault("CurrentRefreshRate"));

        if (w is > 0 && h is > 0)
            d.Resolution = ConfidenceValue<string?>.Collected($"{w}x{h}", "Win32_VideoController", "wmi");
        if (hz is > 0)
            d.RefreshRateHz = ConfidenceValue<int?>.Collected(hz, "Win32_VideoController", "wmi");

        d.InternalDisplay = ChassisHelper.IsLaptopChassis()
            ? TriStateValue.Verified(true, "chassis", "helper")
            : TriStateValue.Verified(false, "chassis", "helper");

        var touch = WmiHelper.DeviceExists("SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'HIDClass' AND Name LIKE '%touch%'");
        d.Touchscreen = touch
            ? TriStateValue.Verified(true, "Win32_PnPEntity", "pnp")
            : TriStateValue.Unknown("pnp");

        d.HdrSupport = TriStateValue.Unknown("wmi");
        d.ColorDepth = ConfidenceValue<int?>.Collected(32, "assumption", "default_high", ConfidenceLevel.Low);

        return d;
    }
}
