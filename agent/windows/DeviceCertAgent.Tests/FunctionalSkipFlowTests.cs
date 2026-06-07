using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Services.V2;
using Xunit;

namespace DeviceCertAgent.Tests;

public sealed class FunctionalSkipFlowTests
{
    [Fact]
    public void FormatFunctionalLines_omits_hub_skipped_optional_tests()
    {
        var functional = new FunctionalCertificationResults
        {
            Display = new DisplayFunctionalTest { Skipped = false, Grade = "Pass", DeadPixelTestPassed = true },
            Keyboard = new KeyboardFunctionalTest { Skipped = false, Passed = true },
            Touchpad = new TouchpadFunctionalTest { Skipped = true },
            SpeakerTest = SkippedComponent<SpeakerTestResult>(),
            MicrophoneTest = SkippedComponent<MicrophoneTestResult>(),
            CameraTest = SkippedComponent<CameraTestResult>(),
            UsbTest = SkippedComponent<UsbTestResult>(),
            AudioJackTest = SkippedComponent<AudioJackTestResult>(),
        };

        var collected = new DeviceCertAgent.Core.Models.CollectionResult();
        var assessment = new CertificationAssessmentV2();
        var lines = new CertificationSummaryBuilder().Build(collected, assessment, functional).FunctionalTestLines;

        Assert.Contains(lines, l => l.StartsWith("Display —"));
        Assert.Contains(lines, l => l.StartsWith("Keyboard —"));
        Assert.DoesNotContain(lines, l => l.StartsWith("Speakers —"));
        Assert.DoesNotContain(lines, l => l.StartsWith("Microphone —"));
        Assert.DoesNotContain(lines, l => l.StartsWith("Camera —"));
        Assert.DoesNotContain(lines, l => l.StartsWith("USB —"));
        Assert.DoesNotContain(lines, l => l.StartsWith("Touchpad —"));
        Assert.DoesNotContain(lines, l => l.StartsWith("Audio jack —"));
    }

    private static T SkippedComponent<T>() where T : ComponentValidationStatus, new() =>
        new()
        {
            Present = true,
            Tested = false,
            Result = ValidationResults.PresentNotTested,
            Reason = "skipped",
        };
}
