namespace DeviceCertAgent.Core.Models.V2;

public static class ValidationResults
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string PresentNotTested = "present_not_tested";
    public const string Inconclusive = "inconclusive";
}

/// <summary>Global functional validation status (v2.3).</summary>
public class ComponentValidationStatus
{
    public bool Present { get; set; }
    public bool Tested { get; set; }
    public string Result { get; set; } = ValidationResults.PresentNotTested;
    public string? Reason { get; set; }

    public bool IsPassed => Result == ValidationResults.Passed;

    public static ComponentValidationStatus NotPresent(string? reason = null) => new()
    {
        Present = false,
        Tested = false,
        Result = ValidationResults.PresentNotTested,
        Reason = reason,
    };

    public static ComponentValidationStatus PresentNotTested(string? reason = null) => new()
    {
        Present = true,
        Tested = false,
        Result = ValidationResults.PresentNotTested,
        Reason = reason,
    };

    public static ComponentValidationStatus Passed(bool present = true) => new()
    {
        Present = present,
        Tested = true,
        Result = ValidationResults.Passed,
    };

    public static ComponentValidationStatus Failed(bool present, string? reason = null) => new()
    {
        Present = present,
        Tested = true,
        Result = ValidationResults.Failed,
        Reason = reason,
    };

    public static ComponentValidationStatus Inconclusive(bool present, string? reason) => new()
    {
        Present = present,
        Tested = true,
        Result = ValidationResults.Inconclusive,
        Reason = reason,
    };
}

public sealed class FunctionalTestPrivacy
{
    public bool MediaUploaded { get; set; }
    public bool AudioUploaded { get; set; }
}

public sealed class CameraTestResult : ComponentValidationStatus
{
    public bool PreviewStarted { get; set; }
    public string? CameraName { get; set; }
    public string? DeviceIdHash { get; set; }
    public string? Resolution { get; set; }
    public int? Fps { get; set; }
    public bool UserConfirmed { get; set; }
    public FunctionalTestPrivacy Privacy { get; set; } = new();
}

public sealed class MicrophoneTestResult : ComponentValidationStatus
{
    public double RecordedSeconds { get; set; }
    public bool SignalDetected { get; set; }
    public double PeakLevel { get; set; }
    public double AverageLevel { get; set; }
    public bool PlaybackConfirmed { get; set; }
    public bool TempAudioDeleted { get; set; }
    public string? DeviceName { get; set; }
    public int? SampleRateHz { get; set; }
    public FunctionalTestPrivacy Privacy { get; set; } = new();
}

public sealed class SpeakerTestResult : ComponentValidationStatus
{
    public bool LeftChannelConfirmed { get; set; }
    public bool RightChannelConfirmed { get; set; }
}

public sealed class UsbTestResult : ComponentValidationStatus
{
    public bool InsertDetected { get; set; }
    public bool RemoveDetected { get; set; }
    public string? DeviceClass { get; set; }
    public bool FilesRead { get; set; }
}

public sealed class DisplayOutputTestResult : ComponentValidationStatus
{
}

public sealed class AudioJackTestResult : ComponentValidationStatus
{
}

public sealed class StorageHealthHonesty
{
    public string? BasicHealthStatus { get; set; }
    public bool WindowsReliabilityCountersCollected { get; set; }
    public bool FullSmartAttributesCollected { get; set; }
    public bool NvmeLogPagesCollected { get; set; }
    public string CollectionLevel { get; set; } = "windows_storage_api";
    public string Confidence { get; set; } = "medium";
    public string PublicDisclosure { get; set; } =
        "Storage health was assessed using Windows Storage APIs. Full vendor SMART and NVMe diagnostics were not available on this device.";
}
