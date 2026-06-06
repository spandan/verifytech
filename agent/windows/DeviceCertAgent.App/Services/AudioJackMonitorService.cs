using DeviceCertAgent.Core.Models.V2;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DeviceCertAgent.App.Services;

/// <summary>Detects default audio endpoint changes (headset jack insert/remove).</summary>
public sealed class AudioJackMonitorService : IMMNotificationClient, IDisposable
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

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (role != Role.Multimedia) return;
        if (flow == DataFlow.Capture && defaultDeviceId != _initialCaptureId)
            _insertDetected = true;
        if (flow == DataFlow.Render && defaultDeviceId != _initialRenderId)
            _insertDetected = true;
    }

    public void OnDeviceAdded(string deviceId) => _insertDetected = true;

    public void OnDeviceRemoved(string deviceId) => _removeDetected = true;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }

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

        if (TryGetEndpointChange(out var endpointReason))
            return new AudioJackTestResult
            {
                Present = true,
                Tested = true,
                Result = ValidationResults.Passed,
                Reason = endpointReason,
            };

        return new AudioJackTestResult
        {
            Present = true,
            Tested = true,
            Result = ValidationResults.PresentNotTested,
            Reason = "no_headset_event_during_test",
        };
    }

    private bool TryGetEndpointChange(out string reason)
    {
        reason = "";
        if (_initialCaptureId is null && _initialRenderId is null)
            return false;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var captureId = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia).ID;
            var renderId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;

            if (_initialCaptureId is not null && captureId != _initialCaptureId)
            {
                reason = "alternate_capture_endpoint";
                return true;
            }

            if (_initialRenderId is not null && renderId != _initialRenderId)
            {
                reason = "alternate_render_endpoint";
                return true;
            }
        }
        catch
        {
            // unsupported
        }

        return false;
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
