using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class NetworkingCollector
{
    public NetworkAssessment Collect(List<string> warnings)
    {
        var n = new NetworkAssessment();
        var adapters = WmiHelper.Query(
            "SELECT Name, AdapterType, Speed, NetConnectionStatus FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE AND NetEnabled = TRUE");

        var wifi = adapters.FirstOrDefault(a =>
        {
            var name = SafeConvert.ToString(a.GetValueOrDefault("Name")) ?? "";
            return name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
                || name.Contains("802.11", StringComparison.OrdinalIgnoreCase);
        });

        if (wifi is not null)
        {
            var name = SafeConvert.ToString(wifi.GetValueOrDefault("Name")) ?? "";
            n.WifiStandard = ConfidenceValue<string?>.Collected(name, "Win32_NetworkAdapter", "wmi", ConfidenceLevel.Medium);
            n.WifiGeneration = ConfidenceValue<string?>.Collected(
                InferWifiGen(name), "inference", "adapter_name", ConfidenceLevel.Medium);
            var speed = SafeConvert.ToLong(wifi.GetValueOrDefault("Speed"));
            if (speed is > 0)
                n.LinkSpeedSummary = ConfidenceValue<string?>.Collected(
                    $"{speed / 1_000_000} Mbps max", "Win32_NetworkAdapter", "wmi");
        }

        var bt = WmiHelper.Query("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%Bluetooth%'");
        if (bt.Count > 0)
            n.BluetoothVersion = ConfidenceValue<string?>.Collected(
                SafeConvert.ToString(bt[0].GetValueOrDefault("Name")), "Win32_PnPEntity", "pnp", ConfidenceLevel.Low);

        n.CapabilitySummary = ConfidenceValue<string?>.Collected(
            $"{adapters.Count} physical adapter(s) enumerated", "Win32_NetworkAdapter", "wmi_count", ConfidenceLevel.Medium);

        return n;
    }

    private static string InferWifiGen(string name)
    {
        var u = name.ToUpperInvariant();
        if (u.Contains("AX") || u.Contains("WI-FI 6")) return "Wi-Fi 6";
        if (u.Contains("AC") || u.Contains("WI-FI 5")) return "Wi-Fi 5";
        if (u.Contains("N ") || u.Contains("802.11N")) return "Wi-Fi 4";
        return "Unknown";
    }
}
