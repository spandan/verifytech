using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class PortInventoryCollector
{
    public PortInventoryAssessment Collect(List<string> warnings)
    {
        var p = new PortInventoryAssessment();
        var devices = WmiHelper.Query("SELECT Name, PNPClass, Description FROM Win32_PnPEntity WHERE Status = 'OK'");

        int usbA = 0, usbC = 0, hdmi = 0, dp = 0;
        var thunderbolt = false;
        var ethernet = false;
        var audioJack = false;
        var sd = false;

        foreach (var d in devices)
        {
            var name = (SafeConvert.ToString(d.GetValueOrDefault("Name")) ?? "").ToUpperInvariant();
            var desc = (SafeConvert.ToString(d.GetValueOrDefault("Description")) ?? "").ToUpperInvariant();
            var combined = $"{name} {desc}";

            if (combined.Contains("THUNDERBOLT")) thunderbolt = true;
            if (combined.Contains("USB4") || combined.Contains("TYPE-C") || combined.Contains("USB-C")) usbC++;
            else if (combined.Contains("USB ROOT") || combined.Contains("USB HOST") || combined.Contains("USB HUB")) usbA++;
            if (combined.Contains("HDMI")) hdmi++;
            if (combined.Contains("DISPLAYPORT")) dp++;
            if (combined.Contains("ETHERNET") || combined.Contains("GIGABIT")) ethernet = true;
            if (combined.Contains("AUDIO") && (combined.Contains("JACK") || combined.Contains("HEADPHONE"))) audioJack = true;
            if (combined.Contains("SD CARD") || combined.Contains("CARD READER")) sd = true;
        }

        p.UsbACount = ConfidenceValue<int?>.Collected(usbA, "Win32_PnPEntity", "pnp_inventory", ConfidenceLevel.Medium);
        p.UsbCCount = ConfidenceValue<int?>.Collected(usbC, "Win32_PnPEntity", "pnp_inventory", ConfidenceLevel.Medium);
        p.Thunderbolt = thunderbolt
            ? TriStateValue.Verified(true, "Win32_PnPEntity", "pnp_inventory")
            : TriStateValue.Unknown("pnp");
        p.HdmiCount = ConfidenceValue<int?>.Collected(hdmi, "Win32_PnPEntity", "pnp_inventory", ConfidenceLevel.Medium);
        p.DisplayPortCount = ConfidenceValue<int?>.Collected(dp, "Win32_PnPEntity", "pnp_inventory", ConfidenceLevel.Medium);
        p.Ethernet = ethernet
            ? TriStateValue.Verified(true, "Win32_PnPEntity", "pnp_inventory")
            : TriStateValue.Unknown("pnp");
        p.AudioJack = audioJack
            ? TriStateValue.Verified(true, "Win32_PnPEntity", "pnp_inventory")
            : TriStateValue.Unknown("pnp");
        p.SdCardReader = sd
            ? TriStateValue.Verified(true, "Win32_PnPEntity", "pnp_inventory")
            : TriStateValue.Unknown("pnp");

        p.InventoryNotes.Add($"Enumerated {devices.Count} OK PnP devices for port classes");
        return p;
    }
}
