using DeviceCertAgent.Core.Models.V2;
using Microsoft.Win32;

namespace DeviceCertAgent.App.Services;

/// <summary>Detects external monitor attach/detach via display count changes.</summary>
public sealed class DisplayHotplugService : IDisposable
{
    private int _baselineCount;
    private bool _attachDetected;
    private bool _detachDetected;
    private bool _subscribed;

    public void StartMonitoring()
    {
        _baselineCount = MonitorEnumeration.Count();
        _attachDetected = false;
        _detachDetected = false;
        if (_subscribed) return;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        _subscribed = true;
    }

    private void OnDisplayChanged(object? sender, EventArgs e)
    {
        var count = MonitorEnumeration.Count();
        if (count > _baselineCount) _attachDetected = true;
        if (count < _baselineCount) _detachDetected = true;
        _baselineCount = count;
    }

    public DisplayOutputTestResult BuildResult()
    {
        if (_attachDetected || _detachDetected)
            return new DisplayOutputTestResult
            {
                Present = true,
                Tested = true,
                Result = ValidationResults.Passed,
                Reason = _attachDetected ? "monitor_attached" : "monitor_detached",
            };

        return new DisplayOutputTestResult
        {
            Present = MonitorEnumeration.Count() > 0,
            Tested = true,
            Result = ValidationResults.PresentNotTested,
            Reason = "no_external_monitor_event_during_test",
        };
    }

    public void Dispose()
    {
        if (_subscribed)
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
    }
}
