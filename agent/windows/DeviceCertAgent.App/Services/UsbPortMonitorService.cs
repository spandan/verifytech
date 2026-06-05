using System.Management;
using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.App.Services;

/// <summary>Detects USB insert/remove via WMI — does not read device files.</summary>
public sealed class UsbPortMonitorService : IDisposable
{
    private ManagementEventWatcher? _insertWatcher;
    private ManagementEventWatcher? _removeWatcher;
    private bool _started;

    public bool InsertDetected { get; private set; }
    public bool RemoveDetected { get; private set; }
    public string? LastDeviceClass { get; private set; }

    public void Start()
    {
        if (_started) return;
        _started = true;
        InsertDetected = false;
        RemoveDetected = false;

        try
        {
            var insertQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance PNPClass = 'USB'");
            _insertWatcher = new ManagementEventWatcher(insertQuery);
            _insertWatcher.EventArrived += OnInsert;
            _insertWatcher.Start();

            var removeQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance PNPClass = 'USB'");
            _removeWatcher = new ManagementEventWatcher(removeQuery);
            _removeWatcher.EventArrived += OnRemove;
            _removeWatcher.Start();
        }
        catch
        {
            // WMI may require elevation; caller marks inconclusive
        }
    }

    private void OnInsert(object sender, EventArrivedEventArgs e)
    {
        InsertDetected = true;
        LastDeviceClass = ReadClass(e);
    }

    private void OnRemove(object sender, EventArrivedEventArgs e)
    {
        RemoveDetected = true;
        LastDeviceClass ??= ReadClass(e);
    }

    private static string? ReadClass(EventArrivedEventArgs e)
    {
        try
        {
            if (e.NewEvent?["TargetInstance"] is not ManagementBaseObject target) return "USB";
            return SafeConvert.ToString(target["PNPClass"]) ?? "USB";
        }
        catch
        {
            return "USB";
        }
    }

    public UsbTestResult BuildResult(bool userSkipped)
    {
        if (userSkipped)
            return new UsbTestResult { Tested = false, Result = ValidationResults.PresentNotTested, FilesRead = false };

        if (!_started)
            return new UsbTestResult
            {
                Present = true,
                Tested = true,
                Result = ValidationResults.Inconclusive,
                Reason = "usb_wmi_unavailable",
                FilesRead = false,
            };

        if (InsertDetected && RemoveDetected)
            return new UsbTestResult
            {
                Present = true,
                Tested = true,
                InsertDetected = true,
                RemoveDetected = true,
                DeviceClass = LastDeviceClass,
                FilesRead = false,
                Result = ValidationResults.Passed,
            };

        if (InsertDetected)
            return new UsbTestResult
            {
                Present = true,
                Tested = true,
                InsertDetected = true,
                RemoveDetected = false,
                DeviceClass = LastDeviceClass,
                FilesRead = false,
                Result = ValidationResults.Inconclusive,
                Reason = "remove_not_detected",
            };

        return new UsbTestResult
        {
            Present = true,
            Tested = true,
            InsertDetected = false,
            RemoveDetected = false,
            FilesRead = false,
            Result = ValidationResults.Failed,
            Reason = "insert_not_detected",
        };
    }

    public void Dispose()
    {
        try
        {
            _insertWatcher?.Stop();
            _insertWatcher?.Dispose();
            _removeWatcher?.Stop();
            _removeWatcher?.Dispose();
        }
        catch { /* ignore */ }
    }
}

internal static class SafeConvert
{
    public static string? ToString(object? v) => v?.ToString();
}
