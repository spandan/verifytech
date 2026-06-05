using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCertAgent.App.Services;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Services.V2;

namespace DeviceCertAgent.App.ViewModels;

public enum FunctionalTestStep
{
    Hub,
    Display,
    Speaker,
    Microphone,
    Camera,
    Keyboard,
    Touchpad,
    Ports,
    Complete,
}

public partial class FunctionalTestsViewModel : ObservableObject, IAsyncDisposable
{
    public FunctionalCertificationResults Results { get; } = new();

    [ObservableProperty] private FunctionalTestStep _currentStep = FunctionalTestStep.Hub;
    [ObservableProperty] private string _statusMessage = "Verify speakers, display, keyboard, and ports before submitting.";
    [ObservableProperty] private string _keyboardCaptureStatus = "Press keys on your keyboard — captured keys appear below.";
    [ObservableProperty] private string _keysPressedDisplay = "";
    [ObservableProperty] private bool _displayDeadPixelsOk = true;
    [ObservableProperty] private bool _displayBrightnessOk = true;
    [ObservableProperty] private bool _displayColorOk = true;
    [ObservableProperty] private bool _displayUniformityOk = true;
    [ObservableProperty] private bool _leftSpeakerConfirmed;
    [ObservableProperty] private bool _rightSpeakerConfirmed;
    [ObservableProperty] private bool _microphonePlaybackConfirmed;
    [ObservableProperty] private bool _isRecordingMic;
    [ObservableProperty] private bool _cameraPreviewActive;
    [ObservableProperty] private bool _isCameraStarting;
    [ObservableProperty] private string _cameraStatus = "";
    [ObservableProperty] private bool _showsCameraAccessInstructions;
    [ObservableProperty] private string _cameraHelpTitle = "";
    [ObservableProperty] private string? _cameraName;
    [ObservableProperty] private string? _cameraResolution;
    [ObservableProperty] private int? _cameraFps;
    [ObservableProperty] private BitmapSource? _cameraPreviewFrame;
    [ObservableProperty] private string _usbStatus = "Insert a USB device when prompted.";
    [ObservableProperty] private bool _touchpadMoved;
    [ObservableProperty] private bool _leftClickOk;
    [ObservableProperty] private bool _rightClickOk;

    private readonly HashSet<string> _keysPressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly CameraLivePreviewService _camera = new();
    private readonly MicrophoneTestService _mic = new();
    private readonly SpeakerTestService _speaker = new();
    private readonly UsbPortMonitorService _usb = new();
    private readonly DisplayHotplugService _displayHotplug = new();
    private readonly AudioJackMonitorService _audioJack = new();
    private bool _speakerLeftDone;
    private bool _micRecorded;

    private static readonly string[] ExpectedKeys =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "SPACE", "ENTER", "TAB", "SHIFT", "CONTROL", "ALT",
    ];

    public event Action? ReachedCompleteStep;

    public ObservableCollection<string> CameraAccessSteps { get; } =
        new(CameraAccessHelper.GetAccessInstructionSteps());

    public FunctionalTestsViewModel()
    {
        _displayHotplug.StartMonitoring();
        _audioJack.StartMonitoring();
    }

    [RelayCommand]
    private void OpenCameraSettings()
    {
        if (!CameraAccessHelper.TryOpenCameraSettings())
            StatusMessage = "Open Settings manually: Privacy & security → Camera.";
        else
            StatusMessage = "Enable camera access in Settings, then tap Retry camera.";
    }

    partial void OnCurrentStepChanged(FunctionalTestStep value)
    {
        if (value == FunctionalTestStep.Complete)
            ReachedCompleteStep?.Invoke();
    }

    [RelayCommand]
    private void StartTests()
    {
        _displayHotplug.StartMonitoring();
        CurrentStep = FunctionalTestStep.Display;
    }

    [RelayCommand]
    private void SkipAll()
    {
        MarkAllSkipped();
        CurrentStep = FunctionalTestStep.Complete;
    }

    private void MarkAllSkipped()
    {
        Results.Display.Skipped = true;
        Results.Keyboard.Skipped = true;
        Results.Touchpad.Skipped = true;
        Results.SpeakerTest = new SpeakerTestResult
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        Results.MicrophoneTest = new MicrophoneTestResult
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        Results.CameraTest = new CameraTestResult
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        Results.UsbTest = new UsbTestResult { Tested = false, Result = ValidationResults.PresentNotTested, Reason = "skipped" };
        Results.DisplayOutputTest = new DisplayOutputTestResult { Result = ValidationResults.PresentNotTested, Reason = "skipped" };
        Results.AudioJackTest = new AudioJackTestResult { Result = ValidationResults.PresentNotTested, Reason = "skipped" };
        FunctionalValidationMapper.ApplyLegacyFields(Results);
    }

    [RelayCommand]
    private void FinishDisplay()
    {
        Results.Display.DeadPixelTestPassed = DisplayDeadPixelsOk;
        Results.Display.BrightnessTestPassed = DisplayBrightnessOk;
        Results.Display.ColorTestPassed = DisplayColorOk;
        Results.Display.UniformityTestPassed = DisplayUniformityOk;
        Results.Display.Grade = DisplayDeadPixelsOk && DisplayBrightnessOk && DisplayColorOk ? "Pass" : "Review";
        Results.DisplayOutputTest = _displayHotplug.BuildResult();
        CurrentStep = FunctionalTestStep.Speaker;
        StatusMessage = "Play the left-channel tone and confirm you hear it.";
    }

    [RelayCommand]
    private void PlaySpeakerLeft()
    {
        _speaker.PlayLeftChannel();
        StatusMessage = "Left channel playing — confirm you hear sound on the left.";
    }

    [RelayCommand]
    private void ConfirmSpeakerLeft()
    {
        _speakerLeftDone = true;
        LeftSpeakerConfirmed = true;
        StatusMessage = "Play the right-channel tone and confirm you hear it.";
    }

    [RelayCommand]
    private void PlaySpeakerRight()
    {
        _speaker.PlayRightChannel();
        StatusMessage = "Right channel playing — confirm you hear sound on the right.";
    }

    [RelayCommand]
    private void ConfirmSpeakerRight()
    {
        RightSpeakerConfirmed = true;
        FinalizeSpeaker(passed: true);
        CurrentStep = FunctionalTestStep.Microphone;
        StatusMessage = "Record a short sample (3–5 seconds). Audio stays on this device.";
    }

    [RelayCommand]
    private void ConfirmSpeakerFailed()
    {
        FinalizeSpeaker(passed: false);
        CurrentStep = FunctionalTestStep.Microphone;
    }

    private void FinalizeSpeaker(bool passed)
    {
        _speaker.Stop();
        Results.SpeakerTest = new SpeakerTestResult
        {
            Present = true,
            Tested = true,
            LeftChannelConfirmed = LeftSpeakerConfirmed && _speakerLeftDone,
            RightChannelConfirmed = RightSpeakerConfirmed,
            Result = passed && LeftSpeakerConfirmed && RightSpeakerConfirmed
                ? ValidationResults.Passed
                : ValidationResults.Failed,
            Reason = passed ? null : "user_reported_no_audio",
        };
        FunctionalValidationMapper.ApplyLegacyFields(Results);
    }

    [RelayCommand]
    private async Task RecordMicrophoneSample()
    {
        IsRecordingMic = true;
        StatusMessage = "Recording… speak into the microphone.";
        try
        {
            Results.MicrophoneTest = await _mic.RecordAsync(4);
            _micRecorded = true;
            StatusMessage = $"Recorded {Results.MicrophoneTest.RecordedSeconds:0}s. Play back locally, then confirm.";
        }
        catch (Exception ex)
        {
            Results.MicrophoneTest = ComponentValidationStatus.Failed(true, ex.Message) as MicrophoneTestResult
                ?? new MicrophoneTestResult { Present = true, Tested = true, Result = ValidationResults.Failed, Reason = ex.Message };
        }
        finally
        {
            IsRecordingMic = false;
        }
    }

    [RelayCommand]
    private void PlayMicrophoneSample()
    {
        _mic.PlayLastSample();
        StatusMessage = "Playing local sample — confirm you hear your recording.";
    }

    [RelayCommand]
    private void ConfirmMicrophonePassed()
    {
        var t = Results.MicrophoneTest;
        t.PlaybackConfirmed = true;
        t.Present = true;
        t.Tested = true;
        t.Result = t.SignalDetected && _micRecorded ? ValidationResults.Passed : ValidationResults.Failed;
        t.TempAudioDeleted = _mic.DeleteTemp();
        t.Privacy = new FunctionalTestPrivacy { AudioUploaded = false };
        MicrophonePlaybackConfirmed = true;
        _micRecorded = false;
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        _ = StartCameraStepAsync();
    }

    [RelayCommand]
    private void ConfirmMicrophoneFailed()
    {
        Results.MicrophoneTest.Present = true;
        Results.MicrophoneTest.Tested = true;
        Results.MicrophoneTest.Result = ValidationResults.Failed;
        Results.MicrophoneTest.TempAudioDeleted = _mic.DeleteTemp();
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        _ = StartCameraStepAsync();
    }

    [RelayCommand]
    private async Task RetryCameraPreview()
    {
        await StartCameraStepAsync(restartOnly: true);
    }

    private async Task StartCameraStepAsync(bool restartOnly = false)
    {
        if (!restartOnly)
            CurrentStep = FunctionalTestStep.Camera;

        IsCameraStarting = true;
        CameraPreviewFrame = null;
        CameraPreviewActive = false;
        ShowsCameraAccessInstructions = false;
        CameraHelpTitle = "";
        CameraStatus = "Starting live camera preview…";
        StatusMessage = CameraStatus;

        try
        {
            await _camera.StopPreviewAsync();

            var hwnd = await CameraLivePreviewService.ResolveWindowHandleAsync();
            var (ok, err) = await _camera.StartPreviewAsync(hwnd, frame =>
            {
                if (frame is not null)
                    CameraPreviewFrame = frame;
            });

            CameraPreviewActive = ok;
            CameraName = _camera.CameraName;
            CameraResolution = _camera.Resolution;
            CameraFps = _camera.FrameRateFps;

            if (ok)
            {
                Results.CameraTest.Present = true;
                Results.CameraTest.PreviewStarted = true;
                Results.CameraTest.CameraName = CameraName;
                Results.CameraTest.DeviceIdHash = _camera.GetDeviceIdHash();
                Results.CameraTest.Resolution = CameraResolution;
                Results.CameraTest.Fps = CameraFps;
                Results.CameraTest.Privacy = new FunctionalTestPrivacy { MediaUploaded = false };
                CameraStatus = "Preview active — confirm you can see yourself.";
                StatusMessage = "Can you clearly see the camera preview?";
            }
            else
            {
                Results.CameraTest = new CameraTestResult
                {
                    Present = !CameraAccessHelper.IsNoCameraDetected(err),
                    Tested = true,
                    PreviewStarted = false,
                    Result = ValidationResults.Inconclusive,
                    Reason = err ?? "preview_failed",
                    Privacy = new FunctionalTestPrivacy { MediaUploaded = false },
                };
                ApplyCameraFailureUi(err);
            }
        }
        catch (Exception ex)
        {
            Results.CameraTest = new CameraTestResult
            {
                Present = true,
                Tested = true,
                PreviewStarted = false,
                Result = ValidationResults.Inconclusive,
                Reason = ex.Message,
                Privacy = new FunctionalTestPrivacy { MediaUploaded = false },
            };
            ApplyCameraFailureUi(ex.Message);
        }
        finally
        {
            IsCameraStarting = false;
            FunctionalValidationMapper.ApplyLegacyFields(Results);
        }
    }

    private void ApplyCameraFailureUi(string? err)
    {
        if (CameraAccessHelper.IsAccessDenied(err))
        {
            ShowsCameraAccessInstructions = true;
            CameraHelpTitle = "Camera access is turned off in Windows";
            CameraStatus = "Windows is blocking camera access for desktop apps.";
            StatusMessage = "Follow the steps below to enable your camera, then tap Retry camera.";
            return;
        }

        if (CameraAccessHelper.IsNoCameraDetected(err))
        {
            ShowsCameraAccessInstructions = false;
            CameraHelpTitle = "";
            CameraStatus = "No camera was detected on this device.";
            StatusMessage = "Connect or enable a camera, then tap Retry camera — or mark the test as failed.";
            return;
        }

        ShowsCameraAccessInstructions = false;
        CameraHelpTitle = "";
        CameraStatus = err ?? "Camera preview could not start.";
        StatusMessage = $"Camera preview unavailable. Retry or mark failed.";
    }

    [RelayCommand]
    private async Task ConfirmCameraPreviewYes()
    {
        await _camera.StopPreviewAsync();
        CameraPreviewActive = false;
        Results.CameraTest.UserConfirmed = true;
        Results.CameraTest.Tested = true;
        Results.CameraTest.Present = Results.CameraTest.Present || true;
        Results.CameraTest.Result = Results.CameraTest.PreviewStarted
            ? ValidationResults.Passed
            : ValidationResults.Inconclusive;
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        CurrentStep = FunctionalTestStep.Keyboard;
    }

    [RelayCommand]
    private async Task ConfirmCameraFailed()
    {
        await _camera.StopPreviewAsync();
        CameraPreviewActive = false;
        Results.CameraTest.UserConfirmed = false;
        Results.CameraTest.Tested = true;
        Results.CameraTest.Result = ValidationResults.Failed;
        Results.CameraTest.Reason = "user_reported_no_preview";
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        CurrentStep = FunctionalTestStep.Keyboard;
    }

    public void HandleKeyPress(Key key)
    {
        if (CurrentStep != FunctionalTestStep.Keyboard) return;
        var name = key == Key.Space ? "SPACE"
            : key == Key.Enter ? "ENTER"
            : key == Key.Tab ? "TAB"
            : key == Key.LeftShift || key == Key.RightShift ? "SHIFT"
            : key == Key.LeftCtrl || key == Key.RightCtrl ? "CONTROL"
            : key == Key.LeftAlt || key == Key.RightAlt ? "ALT"
            : key.ToString().ToUpperInvariant();
        _keysPressed.Add(name);
        KeysPressedDisplay = string.Join(", ", _keysPressed.OrderBy(k => k));
        KeyboardCaptureStatus = $"{_keysPressed.Count} keys captured";
    }

    [RelayCommand]
    private void FinishKeyboard()
    {
        Results.Keyboard.KeysPressed = _keysPressed;
        Results.Keyboard.KeysMissing = ExpectedKeys.Where(k => !_keysPressed.Contains(k)).Take(20).ToList();
        Results.Keyboard.Passed = Results.Keyboard.KeysMissing.Count <= 8;
        Results.Keyboard.Skipped = false;
        CurrentStep = FunctionalTestStep.Touchpad;
    }

    [RelayCommand]
    private void SkipKeyboard()
    {
        Results.Keyboard.Skipped = true;
        CurrentStep = FunctionalTestStep.Touchpad;
    }

    [RelayCommand]
    private void FinishTouchpad()
    {
        Results.Touchpad.MovementDetected = TouchpadMoved;
        Results.Touchpad.LeftClick = LeftClickOk;
        Results.Touchpad.RightClick = RightClickOk;
        Results.Touchpad.MultiTouch = TouchpadMoved;
        Results.Touchpad.Operational = TouchpadMoved && LeftClickOk
            ? TriStateValue.Verified(true, "user", "touchpad_test")
            : TriStateValue.Verified(false, "user", "touchpad_test");
        CurrentStep = FunctionalTestStep.Ports;
        BeginUsbTest();
    }

    [RelayCommand]
    private void SkipTouchpad()
    {
        Results.Touchpad.Skipped = true;
        CurrentStep = FunctionalTestStep.Ports;
        BeginUsbTest();
    }

    private void BeginUsbTest()
    {
        _usb.Start();
        UsbStatus = "Insert a USB device now. We only detect connection — files are never read.";
        StatusMessage = UsbStatus;
    }

    [RelayCommand]
    private void ConfirmUsbInsertDetected()
    {
        UsbStatus = "Remove the USB device to complete the test.";
        StatusMessage = UsbStatus;
    }

    [RelayCommand]
    private void FinishUsbTest()
    {
        Results.UsbTest = _usb.BuildResult(userSkipped: false);
        Results.DisplayOutputTest = _displayHotplug.BuildResult();
        Results.AudioJackTest = _audioJack.BuildResult();
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        CurrentStep = FunctionalTestStep.Complete;
        StatusMessage = "Functional tests complete.";
    }

    [RelayCommand]
    private void ConfirmUsbFailed()
    {
        Results.UsbTest = new UsbTestResult
        {
            Present = true,
            Tested = true,
            Result = ValidationResults.Failed,
            Reason = "user_reported_failure",
            FilesRead = false,
        };
        Results.DisplayOutputTest = _displayHotplug.BuildResult();
        Results.AudioJackTest = _audioJack.BuildResult();
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        CurrentStep = FunctionalTestStep.Complete;
    }

    [RelayCommand]
    private void SkipPorts()
    {
        Results.UsbTest = _usb.BuildResult(userSkipped: true);
        Results.Ports.Skipped = true;
        Results.DisplayOutputTest = _displayHotplug.BuildResult();
        Results.AudioJackTest = _audioJack.BuildResult();
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        CurrentStep = FunctionalTestStep.Complete;
    }

    public void FinalizeBeforeSubmit(CollectionResult collection)
    {
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        FunctionalValidationMapper.ApplyToTier2(Results, collection.Tier2.FunctionalReadiness);
    }

    public async ValueTask DisposeAsync()
    {
        await _camera.DisposeAsync();
        _mic.Dispose();
        _speaker.Dispose();
        _usb.Dispose();
        _displayHotplug.Dispose();
        _audioJack.Dispose();
    }
}
