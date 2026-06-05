using System.Runtime.InteropServices;

namespace DeviceCertAgent.Core.Utilities;

/// <summary>Kernel power status when WMI battery classes return no instances.</summary>
public static class PowerStatusHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    public sealed record PowerBatterySnapshot(
        bool HasBattery,
        byte? ChargePercent,
        bool OnAcPower,
        string Source);

    public static bool TryGetBattery(out PowerBatterySnapshot snapshot)
    {
        snapshot = new PowerBatterySnapshot(false, null, false, "none");
        if (!GetSystemPowerStatus(out var status))
            return false;

        var onAc = status.ACLineStatus == 1;
        var noBattery = status.BatteryFlag == 128;
        var unknownBattery = status.BatteryFlag == 255;
        byte? percent = status.BatteryLifePercent is > 100 ? null : status.BatteryLifePercent;

        if (noBattery)
        {
            snapshot = new PowerBatterySnapshot(false, percent, onAc, "GetSystemPowerStatus");
            return true;
        }

        var hasBattery = !noBattery && (percent is not null || onAc || !unknownBattery);
        snapshot = new PowerBatterySnapshot(
            hasBattery,
            percent,
            onAc,
            "GetSystemPowerStatus");
        return true;
    }
}
