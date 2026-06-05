using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

/// <summary>Maps v2.3 validation results to legacy TriState/tier2 fields for backward compatibility.</summary>
public static class FunctionalValidationMapper
{
    public static void ApplyLegacyFields(FunctionalCertificationResults f)
    {
        SyncCamera(f);
        SyncMicrophone(f);
        SyncSpeaker(f);
        SyncUsb(f);
    }

    public static void ApplyToTier2(FunctionalCertificationResults f, FunctionalReadiness fr)
    {
        fr.CameraPresent = f.CameraTest.Present;
        fr.MicrophonePresent = f.MicrophoneTest.Present;
        fr.SpeakerPresent = f.SpeakerTest.Present;

        fr.CameraTestPassed = ToLegacyBool(f.CameraTest);
        fr.MicrophoneTestPassed = ToLegacyBool(f.MicrophoneTest);
        fr.SpeakerTestPassed = ToLegacyBool(f.SpeakerTest);
        fr.KeyboardTestPassed = f.Keyboard.Skipped ? null : f.Keyboard.Passed;
        fr.TouchpadTestPassed = f.Touchpad.Skipped ? null : f.Touchpad.Operational.Value;
    }

    private static void SyncCamera(FunctionalCertificationResults f)
    {
        var t = f.CameraTest;
        f.Camera.DeviceName = t.CameraName;
        f.Camera.DetectedResolution = t.Resolution;
        f.Camera.FrameRateFps = t.Fps;
        f.Camera.FeedConfirmed = t.UserConfirmed && t.Result == ValidationResults.Passed;
        f.Camera.ValidationTimestamp = t.Tested ? DateTime.UtcNow.ToString("o") : null;
        f.Camera.Skipped = !t.Tested && t.Result == ValidationResults.PresentNotTested;
        f.Camera.CameraOperational = t.Result switch
        {
            ValidationResults.Passed => TriStateValue.Verified(true, "user", "camera_live_preview"),
            ValidationResults.Failed => TriStateValue.Verified(false, "user", "camera_live_preview"),
            _ => TriStateValue.Unknown("camera_live_preview"),
        };
    }

    private static void SyncMicrophone(FunctionalCertificationResults f)
    {
        var t = f.MicrophoneTest;
        f.Audio.MicrophoneDeviceName = t.DeviceName;
        f.Audio.SampleRateHz = t.SampleRateHz;
        f.Audio.MicrophoneSignalLevel = t.AverageLevel;
        f.Audio.SignalDetected = t.SignalDetected
            ? TriStateValue.Verified(true, "local_test", "naudio_peak")
            : TriStateValue.Verified(false, "local_test", "naudio_peak");
        f.Audio.PlaybackConfirmed = t.PlaybackConfirmed
            ? TriStateValue.Verified(true, "user", "local_playback")
            : TriStateValue.Unknown("local_playback");
        f.Audio.Skipped = !t.Tested && t.Result == ValidationResults.PresentNotTested;
        f.Audio.MicrophoneWorking = t.Result switch
        {
            ValidationResults.Passed => TriStateValue.Verified(true, "user", "microphone_test"),
            ValidationResults.Failed => TriStateValue.Verified(false, "user", "microphone_test"),
            _ => TriStateValue.Unknown("microphone_test"),
        };
    }

    private static void SyncSpeaker(FunctionalCertificationResults f)
    {
        var t = f.SpeakerTest;
        f.Audio.SpeakerWorking = t.Result switch
        {
            ValidationResults.Passed => TriStateValue.Verified(true, "user", "stereo_speaker_test"),
            ValidationResults.Failed => TriStateValue.Verified(false, "user", "stereo_speaker_test"),
            _ => TriStateValue.Unknown("stereo_speaker_test"),
        };
    }

    private static void SyncUsb(FunctionalCertificationResults f)
    {
        var t = f.UsbTest;
        f.Ports.UsbDeviceDetected = t.InsertDetected;
        f.Ports.Skipped = !t.Tested;
        f.Ports.Operational = t.Result switch
        {
            ValidationResults.Passed => TriStateValue.Verified(true, "usb_hotplug", "insert_remove"),
            ValidationResults.Failed => TriStateValue.Verified(false, "usb_hotplug", "insert_remove"),
            _ => TriStateValue.Unknown("usb_hotplug"),
        };
    }

    private static bool? ToLegacyBool(ComponentValidationStatus s) =>
        s.Result == ValidationResults.Passed ? true
        : s.Tested && s.Result == ValidationResults.Failed ? false
        : null;
}
