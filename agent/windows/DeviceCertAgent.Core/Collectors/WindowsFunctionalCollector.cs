using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors;

public sealed class WindowsFunctionalCollector
{
    public FunctionalReadiness Collect(List<string> warnings)
    {
        var pnp = WmiHelper.Query("SELECT Name, PNPClass, Status FROM Win32_PnPEntity WHERE Status = 'OK'");

        bool HasClass(string className) =>
            pnp.Any(r => string.Equals(SafeConvert.ToString(r.GetValueOrDefault("PNPClass")), className, StringComparison.OrdinalIgnoreCase));

        bool HasName(params string[] keywords) =>
            pnp.Any(r =>
            {
                var name = SafeConvert.ToString(r.GetValueOrDefault("Name"))?.ToUpperInvariant() ?? "";
                return keywords.Any(k => name.Contains(k.ToUpperInvariant()));
            });

        var batteryRows = WmiHelper.Query("SELECT BatteryStatus FROM Win32_Battery");
        string? chargingStatus = null;
        if (batteryRows.Count > 0)
        {
            var status = SafeConvert.ToInt(batteryRows[0].GetValueOrDefault("BatteryStatus"));
            chargingStatus = status switch
            {
                1 => "discharging",
                2 => "ac_power",
                3 => "fully_charged",
                4 => "low",
                5 => "critical",
                6 => "charging",
                7 => "charging_high",
                8 => "charging_low",
                9 => "charging_critical",
                _ => "unknown",
            };
        }

        return new FunctionalReadiness
        {
            CameraPresent = HasClass("Camera") || HasName("WEBCAM", "CAMERA", "INTEGRATED CAMERA"),
            MicrophonePresent = HasClass("AudioEndpoint") || HasName("MICROPHONE", "MIC"),
            SpeakerPresent = HasClass("AudioEndpoint") || HasClass("Media") || HasName("SPEAKER", "AUDIO"),
            WifiPresent = HasClass("Net") || HasName("WI-FI", "WIFI", "WIRELESS", "802.11"),
            BluetoothPresent = HasName("BLUETOOTH"),
            KeyboardPresent = HasClass("Keyboard") || HasName("KEYBOARD"),
            TouchpadPresent = HasName("TOUCHPAD", "PRECISION TOUCHPAD", "SYNAPTICS", "ELAN"),
            ChargingStatus = chargingStatus,
        };
    }
}
