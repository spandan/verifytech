using DeviceCertAgent.Core.Models.V2;
using NAudio.CoreAudioApi;

namespace DeviceCertAgent.App.Services;

/// <summary>Detects default audio endpoint changes (headset jack insert/remove).</summary>
public sealed class AudioJackMonitorService : MMNotificationClient, IDisposable
{
    private bool _insertDetected;
    private bool _removeDetected;
    private string? _initialCaptureId;
    private string? _initialRenderId;
    private bool _started;

    public void StartMonitoring()
    {
        if (_started) return;
        _started = true;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            _initialCaptureId = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia).ID;
            _initialRenderId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
            enumerator.RegisterEndpointNotificationCallback(this);
        }
        catch
        {
            // unsupported
        }
    }

    public override void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (role != Role.Multimedia) return;
        if (flow == DataFlow.Capture && defaultDeviceId != _initialCaptureId)
            _insertDetected = true;
        if (flow == DataFlow.Render && defaultDeviceId != _initialRenderId)
            _insertDetected = true;
    }

    public override void OnDeviceRemoved(string deviceId) => _removeDetected = true;

    public override void OnDeviceAdded(string deviceId) => _insertDetected = true;

    public AudioJackTestResult BuildResult()
    {
        if (!_started)
            return new AudioJackTestResult
            {
                Present = true,
                Tested = true,
                Result = ValidationResults.Inconclusive,
                Reason = "audio_endpoint_api_unavailable",
            };

        if (_insertDetected || _removeDetected)
            return new AudioJackTestResult
            {
                Present = true,
                Tested = true,
                Result = ValidationResults.Passed,
                Reason = _insertDetected ? "headset_inserted" : "headset_removed",
            };

        return new AudioJackTestResult
        {
            Present = true,
            Tested = true,
            Result = ValidationResults.PresentNotTested,
            Reason = "no_headset_event_during_test",
        };
    }

    public void Dispose()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            enumerator.UnregisterEndpointNotificationCallback(this);
        }
        catch { /* ignore */ }
    }
}
