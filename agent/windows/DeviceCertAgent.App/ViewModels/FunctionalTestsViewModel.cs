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
    [ObservableProperty] private string _keyboardCaptureStatus = "Press keys on your keyboard — at least 5 to continue.";
    [ObservableProperty] private string _keysPressedDisplay = "";
    [ObservableProperty] private bool _displayDeadPixelsOk;
    [ObservableProperty] private bool _displayBrightnessOk;
    [ObservableProperty] private bool _displayColorOk;
    [ObservableProperty] private bool _displayUniformityOk;
    [ObservableProperty] private bool? _leftSpeakerAnswer;
    [ObservableProperty] private bool? _rightSpeakerAnswer;
    [ObservableProperty] private bool _leftSpeakerConfirmed;
    [ObservableProperty] private bool _rightSpeakerConfirmed;
    [ObservableProperty] private bool _speakerLeftStepComplete;
    [ObservableProperty] private bool _leftSpeakerTonePlayed;
    [ObservableProperty] private bool _rightSpeakerTonePlayed;
    [ObservableProperty] private bool _isPlayingLeftSpeaker;
    [ObservableProperty] private bool _isPlayingRightSpeaker;
    [ObservableProperty] private bool _micSampleReady;
    [ObservableProperty] private bool _micPlaybackFinished;
    [ObservableProperty] private bool _isPlayingMicSample;
    [ObservableProperty] private string _micStepStatus = "";
    [ObservableProperty] private bool? _micAnswer;
    [ObservableProperty] private bool? _cameraAnswer;
    [ObservableProperty] private bool? _usbResultAnswer;
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
    [ObservableProperty] private bool _usbInsertAcknowledged;
    [ObservableProperty] private string _usbStatus = "Insert a USB device when prompted.";
    [ObservableProperty] private bool _touchpadMoved;
    [ObservableProperty] private bool _leftClickOk;
    [ObservableProperty] private bool _rightClickOk;

    private readonly HashSet<string> _keysPressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly CameraLivePreviewService _camera = new();
    private readonly MicrophoneTestService _mic = new();
    private readonly SpeakerTestService _speaker = new();
    private readonly UsbPortMonitorService _usb = new();
    private readonly AudioJackMonitorService _audioJack = new();
    private bool _speakerLeftDone;
    private bool _micRecorded;
    private bool _optionalFlowSkipped;

    private static bool IsMandatoryStep(FunctionalTestStep step) =>
        step is FunctionalTestStep.Display or FunctionalTestStep.Keyboard;

    private static bool IsOptionalStep(FunctionalTestStep step) => step switch
    {
        FunctionalTestStep.Speaker or FunctionalTestStep.Microphone or FunctionalTestStep.Camera
            or FunctionalTestStep.Touchpad or FunctionalTestStep.Ports => true,
        _ => false,
    };

    public string HubSkipToolTip =>
        "Skip speakers, microphone, camera, touchpad, and USB. Display and keyboard checks are still required.";

    public bool OptionalChecksSkipped => _optionalFlowSkipped;

    public string FlowModeHint => _optionalFlowSkipped
        ? "Display and keyboard only — optional checks were skipped."
        : "";

    private static readonly string[] ExpectedKeys =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "SPACE", "ENTER", "TAB", "SHIFT", "CONTROL", "ALT",
    ];

    public event Action? ReachedCompleteStep;
    public event Action? LeftCompleteStep;

    public bool CanGoBack => CurrentStep != FunctionalTestStep.Hub;

    public bool ShowStepToolbar => CurrentStep != FunctionalTestStep.Hub;

    public bool ShowSkipOnToolbar =>
        CurrentStep is not FunctionalTestStep.Hub and not FunctionalTestStep.Complete;

    public bool IsCurrentStepMandatory => IsMandatoryStep(CurrentStep);

    public bool CanSkipCurrentStep => IsOptionalStep(CurrentStep);

    public string CurrentStepTitle => CurrentStep switch
    {
        FunctionalTestStep.Hub => "Interactive checks",
        FunctionalTestStep.Display => "Display check",
        FunctionalTestStep.Speaker => "Speaker check",
        FunctionalTestStep.Microphone => "Microphone check",
        FunctionalTestStep.Camera => "Camera check",
        FunctionalTestStep.Keyboard => "Keyboard check",
        FunctionalTestStep.Touchpad => "Touchpad check",
        FunctionalTestStep.Ports => "USB port check",
        FunctionalTestStep.Complete => "Tests complete",
        _ => "Interactive checks",
    };

    public string CurrentSkipToolTip => IsCurrentStepMandatory
        ? CurrentStep switch
        {
            FunctionalTestStep.Display => "Display must be checked before this device can be certified.",
            FunctionalTestStep.Keyboard => "Keyboard must be tested before this device can be certified.",
            _ => "This check is required for certification.",
        }
        : "Skip this check. It will be marked not verified in your report.";

    public bool IsLeftSpeakerYesSelected => LeftSpeakerAnswer == true;
    public bool IsLeftSpeakerNoSelected => LeftSpeakerAnswer == false;
    public bool IsLeftSpeakerPending => LeftSpeakerAnswer is null;
    public bool IsRightSpeakerYesSelected => RightSpeakerAnswer == true;
    public bool IsRightSpeakerNoSelected => RightSpeakerAnswer == false;
    public bool IsRightSpeakerPending => RightSpeakerAnswer is null;
    public bool IsMicYesSelected => MicAnswer == true;
    public bool IsMicNoSelected => MicAnswer == false;
    public bool IsMicPending => MicAnswer is null;
    public bool IsCameraYesSelected => CameraAnswer == true;
    public bool IsCameraNoSelected => CameraAnswer == false;
    public bool IsCameraPending => CameraAnswer is null;
    public bool IsUsbPassSelected => UsbResultAnswer == true;
    public bool IsUsbFailSelected => UsbResultAnswer == false;
    public bool IsUsbPending => UsbResultAnswer is null;
    public string MicPlayButtonLabel => IsPlayingMicSample ? "Playing…" : "Play my recording";
    public string LeftSpeakerPlayLabel => IsPlayingLeftSpeaker ? "Playing left tone…" : "Play left tone";
    public string RightSpeakerPlayLabel => IsPlayingRightSpeaker ? "Playing right tone…" : "Play right tone";

    public string SpeakerStepIntro =>
        "We play a short tone on each side. Turn up volume, then confirm what you hear.";

    public string MicStepIntro =>
        "Record a short phrase, listen back, then confirm. Nothing leaves this device.";

    public string MicPromptPhrase =>
        "\"The quick brown fox jumps over the lazy dog.\"";

    public string CameraStepIntro =>
        "Your live preview stays on this device — no photos or video are uploaded.";

    public int SpeakerCurrentStep => SpeakerLeftStepComplete ? 2 : 1;

    public int MicCurrentPhase => MicPlaybackFinished ? 3 : MicSampleReady ? 2 : 1;

    public bool IsLeftSpeakerActive =>
        CurrentStep == FunctionalTestStep.Speaker && !SpeakerLeftStepComplete;

    public bool IsRightSpeakerActive =>
        CurrentStep == FunctionalTestStep.Speaker && SpeakerLeftStepComplete && RightSpeakerAnswer is null;

    public bool IsLeftSpeakerComplete => SpeakerLeftStepComplete;

    public bool IsRightSpeakerComplete => RightSpeakerAnswer is not null;

    public bool IsMicRecordPhaseActive =>
        CurrentStep == FunctionalTestStep.Microphone && !MicSampleReady;

    public bool IsMicListenPhaseActive =>
        CurrentStep == FunctionalTestStep.Microphone && MicSampleReady && !MicPlaybackFinished;

    public bool IsMicConfirmPhaseActive =>
        CurrentStep == FunctionalTestStep.Microphone && MicPlaybackFinished;

    public bool ShowCameraLiveBadge =>
        CameraPreviewActive && !IsCameraStarting && !ShowsCameraAccessInstructions;

    public bool ShowCameraConfirmPrompt =>
        CameraPreviewActive && !IsCameraStarting && !ShowsCameraAccessInstructions;

    public ObservableCollection<string> CameraAccessSteps { get; } =
        new(CameraAccessHelper.GetAccessInstructionSteps());

    partial void OnLeftSpeakerAnswerChanged(bool? value)
    {
        OnPropertyChanged(nameof(IsLeftSpeakerYesSelected));
        OnPropertyChanged(nameof(IsLeftSpeakerNoSelected));
        OnPropertyChanged(nameof(IsLeftSpeakerPending));
        NotifySubStepHighlight();
    }

    partial void OnRightSpeakerAnswerChanged(bool? value)
    {
        OnPropertyChanged(nameof(IsRightSpeakerYesSelected));
        OnPropertyChanged(nameof(IsRightSpeakerNoSelected));
        OnPropertyChanged(nameof(IsRightSpeakerPending));
        NotifySubStepHighlight();
    }

    partial void OnMicAnswerChanged(bool? value)
    {
        OnPropertyChanged(nameof(IsMicYesSelected));
        OnPropertyChanged(nameof(IsMicNoSelected));
        OnPropertyChanged(nameof(IsMicPending));
        NotifyMicConfirmCommands();
    }

    partial void OnCameraAnswerChanged(bool? value)
    {
        OnPropertyChanged(nameof(IsCameraYesSelected));
        OnPropertyChanged(nameof(IsCameraNoSelected));
        OnPropertyChanged(nameof(IsCameraPending));
    }

    partial void OnUsbResultAnswerChanged(bool? value)
    {
        OnPropertyChanged(nameof(IsUsbPassSelected));
        OnPropertyChanged(nameof(IsUsbFailSelected));
        OnPropertyChanged(nameof(IsUsbPending));
    }

    partial void OnIsPlayingMicSampleChanged(bool value)
    {
        OnPropertyChanged(nameof(MicPlayButtonLabel));
        PlayMicrophoneSampleCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPlayingLeftSpeakerChanged(bool value)
    {
        OnPropertyChanged(nameof(LeftSpeakerPlayLabel));
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
        NotifySubStepHighlight();
    }

    partial void OnIsPlayingRightSpeakerChanged(bool value)
    {
        OnPropertyChanged(nameof(RightSpeakerPlayLabel));
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
        NotifySubStepHighlight();
    }

    public FunctionalTestsViewModel()
    {
        _audioJack.StartMonitoring();
        NotifyInitialCommandStates();
    }

    private void NotifyInitialCommandStates()
    {
        FinishDisplayCommand.NotifyCanExecuteChanged();
        FinishKeyboardCommand.NotifyCanExecuteChanged();
        FinishTouchpadCommand.NotifyCanExecuteChanged();
        FinishUsbTestCommand.NotifyCanExecuteChanged();
        ConfirmUsbFailedCommand.NotifyCanExecuteChanged();
        ConfirmCameraPreviewYesCommand.NotifyCanExecuteChanged();
        ConfirmCameraFailedCommand.NotifyCanExecuteChanged();
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
        NotifySpeakerAnswerCommands();
        NotifyMicConfirmCommands();
        PlayMicrophoneSampleCommand.NotifyCanExecuteChanged();
        RecordMicrophoneSampleCommand.NotifyCanExecuteChanged();
        GoBackCommand.NotifyCanExecuteChanged();
        SkipCurrentStepCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSkipCurrentStep))]
    private async Task SkipCurrentStepAsync()
    {
        switch (CurrentStep)
        {
            case FunctionalTestStep.Speaker:
                SkipSpeakers();
                break;
            case FunctionalTestStep.Microphone:
                SkipMicrophone();
                break;
            case FunctionalTestStep.Camera:
                await SkipCameraAsync();
                break;
            case FunctionalTestStep.Touchpad:
                SkipTouchpad();
                break;
            case FunctionalTestStep.Ports:
                SkipPorts();
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task GoBackAsync()
    {
        switch (CurrentStep)
        {
            case FunctionalTestStep.Display:
                GoToHubStep();
                break;
            case FunctionalTestStep.Speaker:
                GoToDisplayStep();
                break;
            case FunctionalTestStep.Microphone:
                _mic.StopPlayback();
                IsPlayingMicSample = false;
                GoToSpeakerStep();
                break;
            case FunctionalTestStep.Camera:
                await _camera.StopPreviewAsync();
                CameraPreviewActive = false;
                GoToMicrophoneStep();
                break;
            case FunctionalTestStep.Keyboard:
                if (_optionalFlowSkipped)
                    GoToDisplayStep();
                else
                    _ = StartCameraStepAsync(preserveAnswer: true);
                break;
            case FunctionalTestStep.Touchpad:
                GoToKeyboardStep();
                break;
            case FunctionalTestStep.Ports:
                GoToTouchpadStep();
                break;
            case FunctionalTestStep.Complete:
                LeftCompleteStep?.Invoke();
                if (_optionalFlowSkipped)
                    GoToKeyboardStep();
                else
                    GoToPortsStep();
                break;
        }
    }

    private void NotifyOptionalFlowChanged()
    {
        OnPropertyChanged(nameof(OptionalChecksSkipped));
        OnPropertyChanged(nameof(FlowModeHint));
    }

    public void ResetForNewRun()
    {
        _optionalFlowSkipped = false;
        _keysPressed.Clear();
        KeysPressedDisplay = "";
        ResetSpeakerUi();
        ResetMicrophoneUi();
        Results.Display = new DisplayFunctionalTest();
        Results.Keyboard = new KeyboardFunctionalTest();
        Results.Touchpad = new TouchpadFunctionalTest();
        Results.Ports = new PortFunctionalTest();
        Results.Camera = new CameraFunctionalTest();
        Results.Audio = new AudioFunctionalTest();
        Results.SpeakerTest = new SpeakerTestResult();
        Results.MicrophoneTest = new MicrophoneTestResult();
        Results.CameraTest = new CameraTestResult();
        Results.UsbTest = new UsbTestResult();
        Results.AudioJackTest = new AudioJackTestResult();
        Results.DisplayOutputTest = new DisplayOutputTestResult();
        CurrentStep = FunctionalTestStep.Hub;
        NotifyOptionalFlowChanged();
        NotifyStepChromeChanged();
    }

    private void GoToHubStep()
    {
        CurrentStep = FunctionalTestStep.Hub;
    }

    private void GoToDisplayStep()
    {
        _speaker.Stop();
        EnterDisplayStep();
    }

    private void GoToSpeakerStep() => EnterSpeakerStep(resetUi: false);

    private void GoToMicrophoneStep() => EnterMicrophoneStep(resetUi: false);

    private void GoToKeyboardStep() => BeginKeyboardStep(preserveCapturedKeys: true);

    private void GoToTouchpadStep() => EnterTouchpadStep();

    private void GoToPortsStep() => EnterPortsStep();

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
        NotifyStepChromeChanged();
        GoBackCommand.NotifyCanExecuteChanged();
        if (value == FunctionalTestStep.Complete)
            ReachedCompleteStep?.Invoke();
    }

    private void NotifyStepChromeChanged()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(ShowStepToolbar));
        OnPropertyChanged(nameof(ShowSkipOnToolbar));
        OnPropertyChanged(nameof(IsCurrentStepMandatory));
        OnPropertyChanged(nameof(CanSkipCurrentStep));
        OnPropertyChanged(nameof(CurrentSkipToolTip));
        NotifySubStepHighlight();
        SkipCurrentStepCommand.NotifyCanExecuteChanged();
    }

    private void NotifySubStepHighlight()
    {
        OnPropertyChanged(nameof(SpeakerCurrentStep));
        OnPropertyChanged(nameof(MicCurrentPhase));
        OnPropertyChanged(nameof(IsLeftSpeakerActive));
        OnPropertyChanged(nameof(IsRightSpeakerActive));
        OnPropertyChanged(nameof(IsLeftSpeakerComplete));
        OnPropertyChanged(nameof(IsRightSpeakerComplete));
        OnPropertyChanged(nameof(IsMicRecordPhaseActive));
        OnPropertyChanged(nameof(IsMicListenPhaseActive));
        OnPropertyChanged(nameof(IsMicConfirmPhaseActive));
        OnPropertyChanged(nameof(ShowCameraLiveBadge));
        OnPropertyChanged(nameof(ShowCameraConfirmPrompt));
    }

    [RelayCommand]
    private void StartTests()
    {
        _optionalFlowSkipped = false;
        NotifyOptionalFlowChanged();
        EnterDisplayStep();
    }

    [RelayCommand]
    private void SkipOptionalChecks()
    {
        MarkOptionalComponentsSkipped();
        _optionalFlowSkipped = true;
        NotifyOptionalFlowChanged();
        ResetSpeakerUi();
        ResetMicrophoneUi();
        EnterDisplayStep();
    }

    private void MarkOptionalComponentsSkipped()
    {
        Results.Touchpad.Skipped = true;
        Results.Ports.Skipped = true;
        Results.Camera.Skipped = true;
        Results.Audio.Skipped = true;
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
        Results.UsbTest = new UsbTestResult
        {
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        Results.AudioJackTest = new AudioJackTestResult
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        FunctionalValidationMapper.ApplyLegacyFields(Results);
    }

    private void EnterDisplayStep()
    {
        CurrentStep = FunctionalTestStep.Display;
    }

    private void EnterSpeakerStep(bool resetUi)
    {
        if (resetUi)
            ResetSpeakerUi();
        CurrentStep = FunctionalTestStep.Speaker;
    }

    private void EnterMicrophoneStep(bool resetUi)
    {
        if (resetUi)
            ResetMicrophoneUi();
        CurrentStep = FunctionalTestStep.Microphone;
    }

    private void EnterTouchpadStep()
    {
        CurrentStep = FunctionalTestStep.Touchpad;
    }

    private void EnterPortsStep() => BeginUsbTest();

    private void EnterCompleteStep()
    {
        if (!FunctionalValidationMapper.IsUserSkipped(Results.AudioJackTest))
            Results.AudioJackTest = _audioJack.BuildResult();
        FunctionalTestFinalizer.Reconcile(Results);
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        CurrentStep = FunctionalTestStep.Complete;
    }

    private void AdvanceFromDisplay()
    {
        if (_optionalFlowSkipped)
            BeginKeyboardStep();
        else
            EnterSpeakerStep(resetUi: true);
    }

    private void AdvanceFromKeyboard()
    {
        if (_optionalFlowSkipped)
            EnterCompleteStep();
        else
            BeginTouchpadStep();
    }

    [RelayCommand]
    private void FinishDisplay()
    {
        Results.Display.Skipped = false;
        Results.Display.DeadPixelTestPassed = DisplayDeadPixelsOk;
        Results.Display.BrightnessTestPassed = DisplayBrightnessOk;
        Results.Display.ColorTestPassed = DisplayColorOk;
        Results.Display.UniformityTestPassed = DisplayUniformityOk;
        Results.Display.Grade = DisplayDeadPixelsOk && DisplayBrightnessOk && DisplayColorOk && DisplayUniformityOk
            ? "Pass"
            : "Review";
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        AdvanceFromDisplay();
    }

    private void ResetSpeakerUi()
    {
        LeftSpeakerAnswer = null;
        RightSpeakerAnswer = null;
        LeftSpeakerConfirmed = false;
        RightSpeakerConfirmed = false;
        SpeakerLeftStepComplete = false;
        LeftSpeakerTonePlayed = false;
        RightSpeakerTonePlayed = false;
        IsPlayingLeftSpeaker = false;
        IsPlayingRightSpeaker = false;
        _speakerLeftDone = false;
        NotifySpeakerAnswerCommands();
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
    }

    private void NotifySpeakerAnswerCommands()
    {
        AnswerLeftSpeakerYesCommand.NotifyCanExecuteChanged();
        AnswerLeftSpeakerNoCommand.NotifyCanExecuteChanged();
        AnswerRightSpeakerYesCommand.NotifyCanExecuteChanged();
        AnswerRightSpeakerNoCommand.NotifyCanExecuteChanged();
    }

    private bool CanAnswerLeftSpeaker() => LeftSpeakerTonePlayed;

    private bool CanAnswerRightSpeaker() => SpeakerLeftStepComplete && RightSpeakerTonePlayed;

    private bool CanPlayLeftSpeaker() => !IsPlayingLeftSpeaker && !IsPlayingRightSpeaker;

    private bool CanPlayRightSpeaker() => SpeakerLeftStepComplete && CanPlayLeftSpeaker();

    private void ResetMicrophoneUi()
    {
        MicSampleReady = false;
        MicPlaybackFinished = false;
        IsPlayingMicSample = false;
        MicStepStatus = "";
        MicAnswer = null;
        _micRecorded = false;
        MicrophonePlaybackConfirmed = false;
        _mic.StopPlayback();
        NotifyMicConfirmCommands();
        PlayMicrophoneSampleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPlayLeftSpeaker))]
    private async Task PlaySpeakerLeft()
    {
        IsPlayingLeftSpeaker = true;
        LeftSpeakerTonePlayed = false;
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
        _speaker.PlayLeftChannel();
        StatusMessage = "Playing left tone — listen on the left side.";
        await Task.Delay(TimeSpan.FromMilliseconds(1600));
        IsPlayingLeftSpeaker = false;
        LeftSpeakerTonePlayed = true;
        StatusMessage = "Did you hear the tone on the left? Select Yes or No.";
        NotifySpeakerAnswerCommands();
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAnswerLeftSpeaker))]
    private void AnswerLeftSpeakerYes()
    {
        _speakerLeftDone = true;
        LeftSpeakerAnswer = true;
        LeftSpeakerConfirmed = true;
        SpeakerLeftStepComplete = true;
        StatusMessage = "Step 2 of 2: play the right tone, then choose Yes or No.";
        NotifySpeakerAnswerCommands();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAnswerLeftSpeaker))]
    private void AnswerLeftSpeakerNo()
    {
        _speakerLeftDone = true;
        LeftSpeakerAnswer = false;
        LeftSpeakerConfirmed = false;
        SpeakerLeftStepComplete = true;
        StatusMessage = "Step 2 of 2: play the right tone, then choose Yes or No.";
        NotifySpeakerAnswerCommands();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPlayRightSpeaker))]
    private async Task PlaySpeakerRight()
    {
        IsPlayingRightSpeaker = true;
        RightSpeakerTonePlayed = false;
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
        _speaker.PlayRightChannel();
        StatusMessage = "Playing right tone — listen on the right side.";
        await Task.Delay(TimeSpan.FromMilliseconds(1600));
        IsPlayingRightSpeaker = false;
        RightSpeakerTonePlayed = true;
        StatusMessage = "Did you hear the tone on the right? Select Yes or No.";
        NotifySpeakerAnswerCommands();
        PlaySpeakerLeftCommand.NotifyCanExecuteChanged();
        PlaySpeakerRightCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAnswerRightSpeaker))]
    private void AnswerRightSpeakerYes()
    {
        RightSpeakerAnswer = true;
        RightSpeakerConfirmed = true;
        CompleteSpeakerTest();
    }

    [RelayCommand(CanExecute = nameof(CanAnswerRightSpeaker))]
    private void AnswerRightSpeakerNo()
    {
        RightSpeakerAnswer = false;
        RightSpeakerConfirmed = false;
        CompleteSpeakerTest();
    }

    [RelayCommand]
    private void SkipSpeakers()
    {
        _speaker.Stop();
        Results.SpeakerTest = new SpeakerTestResult
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        EnterMicrophoneStep(resetUi: true);
    }

    private void SkipMicrophone()
    {
        _mic.StopPlayback();
        _mic.DeleteTemp();
        Results.MicrophoneTest = new MicrophoneTestResult
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        _ = StartCameraStepAsync();
    }

    private async Task SkipCameraAsync()
    {
        await _camera.StopPreviewAsync();
        CameraPreviewActive = false;
        Results.CameraTest = new CameraTestResult
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        BeginKeyboardStep();
    }

    private void CompleteSpeakerTest()
    {
        FinalizeSpeaker(LeftSpeakerConfirmed && RightSpeakerConfirmed);
        EnterMicrophoneStep(resetUi: true);
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

    [RelayCommand(CanExecute = nameof(CanRecordMicrophone))]
    private async Task RecordMicrophoneSample()
    {
        IsRecordingMic = true;
        MicSampleReady = false;
        MicPlaybackFinished = false;
        MicAnswer = null;
        MicStepStatus = "";
        _mic.StopPlayback();
        IsPlayingMicSample = false;
        StatusMessage = "Recording… speak clearly into the microphone for 4 seconds.";
        try
        {
            Results.MicrophoneTest = await _mic.RecordAsync(4);
            _micRecorded = true;
            MicSampleReady = true;
            MicStepStatus = "Recording saved. Tap Play my recording and listen with your speakers or headphones.";
            StatusMessage = "Step 2 of 3: play back your recording.";
        }
        catch (Exception ex)
        {
            Results.MicrophoneTest = ComponentValidationStatus.Failed(true, ex.Message) as MicrophoneTestResult
                ?? new MicrophoneTestResult { Present = true, Tested = true, Result = ValidationResults.Failed, Reason = ex.Message };
            MicSampleReady = false;
            MicStepStatus = "Recording failed. Try again.";
            StatusMessage = "Recording failed. Try again or choose No on the next step.";
        }
        finally
        {
            IsRecordingMic = false;
            PlayMicrophoneSampleCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlayMicrophoneSample))]
    private async Task PlayMicrophoneSample()
    {
        MicPlaybackFinished = false;
        MicAnswer = null;

        if (!_mic.TryPlayLastSample(out var duration))
        {
            MicStepStatus = "Could not play the recording. Record a new sample and try again.";
            StatusMessage = MicStepStatus;
            return;
        }

        IsPlayingMicSample = true;
        var seconds = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds));
        MicStepStatus = $"Playing now ({seconds} s) — turn up volume and listen.";
        StatusMessage = MicStepStatus;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStopped(object? _, EventArgs __)
        {
            _mic.PlaybackStopped -= OnStopped;
            tcs.TrySetResult();
        }

        _mic.PlaybackStopped += OnStopped;
        await tcs.Task;

        IsPlayingMicSample = false;
        MicPlaybackFinished = true;
        MicStepStatus = "Playback finished. Did it sound like your voice? Choose Yes or No below.";
        StatusMessage = "Step 3 of 3: confirm whether the microphone works.";
        NotifyMicConfirmCommands();
        PlayMicrophoneSampleCommand.NotifyCanExecuteChanged();
    }

    private void NotifyMicConfirmCommands()
    {
        ConfirmMicrophonePassedCommand.NotifyCanExecuteChanged();
        ConfirmMicrophoneFailedCommand.NotifyCanExecuteChanged();
    }

    private bool CanRecordMicrophone() => !IsRecordingMic && !IsPlayingMicSample;

    private bool CanPlayMicrophoneSample() => MicSampleReady && !IsRecordingMic && !IsPlayingMicSample;

    private bool CanConfirmMicrophone() => MicPlaybackFinished && MicAnswer is null && !IsRecordingMic && !IsPlayingMicSample;

    partial void OnIsRecordingMicChanged(bool value)
    {
        RecordMicrophoneSampleCommand.NotifyCanExecuteChanged();
        PlayMicrophoneSampleCommand.NotifyCanExecuteChanged();
        NotifyMicConfirmCommands();
        NotifySubStepHighlight();
    }

    partial void OnMicSampleReadyChanged(bool value)
    {
        PlayMicrophoneSampleCommand.NotifyCanExecuteChanged();
        NotifyMicConfirmCommands();
        NotifySubStepHighlight();
    }

    partial void OnMicPlaybackFinishedChanged(bool value)
    {
        NotifyMicConfirmCommands();
        NotifySubStepHighlight();
    }

    partial void OnSpeakerLeftStepCompleteChanged(bool value) => NotifySubStepHighlight();

    [RelayCommand(CanExecute = nameof(CanConfirmMicrophone))]
    private void ConfirmMicrophonePassed()
    {
        MicAnswer = true;
        var t = Results.MicrophoneTest;
        t.PlaybackConfirmed = true;
        t.Present = true;
        t.Tested = true;
        // User confirmation after playback is the functional test — do not require peak thresholds.
        t.Result = ValidationResults.Passed;
        t.Reason = null;
        t.TempAudioDeleted = _mic.DeleteTemp();
        t.Privacy = new FunctionalTestPrivacy { AudioUploaded = false };
        MicrophonePlaybackConfirmed = true;
        _micRecorded = false;
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        _ = StartCameraStepAsync();
    }

    [RelayCommand(CanExecute = nameof(CanConfirmMicrophone))]
    private void ConfirmMicrophoneFailed()
    {
        MicAnswer = false;
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

    private async Task StartCameraStepAsync(bool restartOnly = false, bool preserveAnswer = false)
    {
        if (!restartOnly)
        {
            CurrentStep = FunctionalTestStep.Camera;
            if (!preserveAnswer)
                CameraAnswer = null;
        }

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

    private bool CanConfirmCameraPreviewYes() => CameraPreviewActive && !IsCameraStarting;

    private bool CanConfirmCameraFailed() => !IsCameraStarting;

    partial void OnCameraPreviewActiveChanged(bool value)
    {
        ConfirmCameraPreviewYesCommand.NotifyCanExecuteChanged();
        ConfirmCameraFailedCommand.NotifyCanExecuteChanged();
        NotifySubStepHighlight();
    }

    partial void OnIsCameraStartingChanged(bool value)
    {
        ConfirmCameraPreviewYesCommand.NotifyCanExecuteChanged();
        ConfirmCameraFailedCommand.NotifyCanExecuteChanged();
        NotifySubStepHighlight();
    }

    partial void OnShowsCameraAccessInstructionsChanged(bool value) => NotifySubStepHighlight();

    [RelayCommand(CanExecute = nameof(CanConfirmCameraPreviewYes))]
    private async Task ConfirmCameraPreviewYes()
    {
        CameraAnswer = true;
        await _camera.StopPreviewAsync();
        CameraPreviewActive = false;
        Results.CameraTest.UserConfirmed = true;
        Results.CameraTest.Tested = true;
        Results.CameraTest.Present = Results.CameraTest.Present || true;
        Results.CameraTest.Result = Results.CameraTest.PreviewStarted
            ? ValidationResults.Passed
            : ValidationResults.Inconclusive;
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        BeginKeyboardStep();
    }

    [RelayCommand(CanExecute = nameof(CanConfirmCameraFailed))]
    private async Task ConfirmCameraFailed()
    {
        CameraAnswer = false;
        await _camera.StopPreviewAsync();
        CameraPreviewActive = false;
        Results.CameraTest.UserConfirmed = false;
        Results.CameraTest.Tested = true;
        Results.CameraTest.Result = ValidationResults.Failed;
        Results.CameraTest.Reason = "user_reported_no_preview";
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        BeginKeyboardStep();
    }

    private void BeginKeyboardStep(bool preserveCapturedKeys = false)
    {
        if (!preserveCapturedKeys)
        {
            _keysPressed.Clear();
            KeysPressedDisplay = "";
        }

        CurrentStep = FunctionalTestStep.Keyboard;
        StatusMessage = "Press at least 5 different keys, then tap Done.";
        KeyboardCaptureStatus = $"{_keysPressed.Count} keys captured — press at least 5 to continue.";
        FinishKeyboardCommand.NotifyCanExecuteChanged();
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
        KeyboardCaptureStatus = $"{_keysPressed.Count} keys captured — press at least 5 to continue.";
        FinishKeyboardCommand.NotifyCanExecuteChanged();
    }

    private bool CanFinishKeyboard() => _keysPressed.Count >= 5;

    [RelayCommand(CanExecute = nameof(CanFinishKeyboard))]
    private void FinishKeyboard()
    {
        Results.Keyboard.KeysPressed = _keysPressed;
        Results.Keyboard.KeysMissing = ExpectedKeys.Where(k => !_keysPressed.Contains(k)).Take(20).ToList();
        Results.Keyboard.Passed = Results.Keyboard.KeysMissing.Count <= 8;
        Results.Keyboard.Skipped = false;
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        AdvanceFromKeyboard();
    }

    private void BeginTouchpadStep()
    {
        Results.Touchpad.Skipped = false;
        EnterTouchpadStep();
    }

    [RelayCommand(CanExecute = nameof(CanFinishTouchpad))]
    private void FinishTouchpad()
    {
        Results.Touchpad.MovementDetected = TouchpadMoved;
        Results.Touchpad.LeftClick = LeftClickOk;
        Results.Touchpad.RightClick = RightClickOk;
        Results.Touchpad.MultiTouch = TouchpadMoved;
        Results.Touchpad.Operational = TouchpadMoved && LeftClickOk
            ? TriStateValue.Verified(true, "user", "touchpad_test")
            : TriStateValue.Verified(false, "user", "touchpad_test");
        Results.Touchpad.Skipped = false;
        EnterPortsStep();
    }

    private bool CanFinishTouchpad() => TouchpadMoved && LeftClickOk && RightClickOk;

    partial void OnTouchpadMovedChanged(bool value) => FinishTouchpadCommand.NotifyCanExecuteChanged();
    partial void OnLeftClickOkChanged(bool value) => FinishTouchpadCommand.NotifyCanExecuteChanged();
    partial void OnRightClickOkChanged(bool value) => FinishTouchpadCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void SkipTouchpad()
    {
        Results.Touchpad.Skipped = true;
        Results.Touchpad.Operational = TriStateValue.Unknown("touchpad_test");
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        EnterPortsStep();
    }

    private void BeginUsbTest()
    {
        CurrentStep = FunctionalTestStep.Ports;
        _usb.Reset();
        UsbResultAnswer = null;
        UsbInsertAcknowledged = false;
        _usb.Start();
        UsbStatus = "Insert a USB device now. We only detect connection — files are never read.";
        StatusMessage = UsbStatus;
        FinishUsbTestCommand.NotifyCanExecuteChanged();
        ConfirmUsbFailedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ConfirmUsbInsertDetected()
    {
        UsbInsertAcknowledged = true;
        UsbStatus = "Remove the USB device to complete the test.";
        StatusMessage = UsbStatus;
        FinishUsbTestCommand.NotifyCanExecuteChanged();
        ConfirmUsbFailedCommand.NotifyCanExecuteChanged();
    }

    private bool CanFinishUsbTest() => UsbInsertAcknowledged;

    private bool CanConfirmUsbFailed() => UsbInsertAcknowledged;

    [RelayCommand(CanExecute = nameof(CanFinishUsbTest))]
    private void FinishUsbTest()
    {
        UsbResultAnswer = true;
        Results.Ports.Skipped = false;
        Results.UsbTest = _usb.BuildResult(userSkipped: false);
        EnterCompleteStep();
    }

    [RelayCommand(CanExecute = nameof(CanConfirmUsbFailed))]
    private void ConfirmUsbFailed()
    {
        UsbResultAnswer = false;
        Results.UsbTest = new UsbTestResult
        {
            Present = true,
            Tested = true,
            Result = ValidationResults.Failed,
            Reason = "user_reported_failure",
            FilesRead = false,
        };
        Results.Ports.Skipped = false;
        EnterCompleteStep();
    }

    [RelayCommand]
    private void SkipPorts()
    {
        Results.UsbTest = _usb.BuildResult(userSkipped: true);
        Results.Ports.Skipped = true;
        EnterCompleteStep();
    }

    public void FinalizeBeforeSubmit(CollectionResult collection)
    {
        FunctionalTestFinalizer.Reconcile(Results);
        FunctionalValidationMapper.ApplyLegacyFields(Results);
        FunctionalValidationMapper.ApplyToTier2(Results, collection.Tier2.FunctionalReadiness);
    }

    public async ValueTask DisposeAsync()
    {
        await _camera.DisposeAsync();
        _mic.Dispose();
        _speaker.Dispose();
        _usb.Dispose();
        _audioJack.Dispose();
    }
}
