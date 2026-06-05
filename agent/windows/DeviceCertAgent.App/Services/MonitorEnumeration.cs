using System.Runtime.InteropServices;

namespace DeviceCertAgent.App.Services;

internal static class MonitorEnumeration
{
    private static int _count;

    public static int Count()
    {
        _count = 0;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return Math.Max(1, _count);
    }

    private static bool Callback(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
    {
        _count++;
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}
