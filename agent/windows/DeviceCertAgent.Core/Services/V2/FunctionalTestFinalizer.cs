using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

/// <summary>
/// Maps interactive user tests onto passive port/display monitors so completed checks
/// are not reported as "detected but not tested".
/// </summary>
public static class FunctionalTestFinalizer
{
    public static void Reconcile(FunctionalCertificationResults f, PortInventoryAssessment? ports = null)
    {
        ReconcileMicrophone(f);
        ReconcileAudioJack(f, ports);
    }

    private static void ReconcileMicrophone(FunctionalCertificationResults f)
    {
        var t = f.MicrophoneTest;
        if (FunctionalValidationMapper.IsUserSkipped(t))
            return;
        if (t.Result == ValidationResults.Passed)
            return;

        if (t.Result == ValidationResults.Failed)
            return;

        var userConfirmed = t.PlaybackConfirmed
            || f.Audio.PlaybackConfirmed.Value == true
            || f.Audio.MicrophoneWorking.Value == true;

        if (!userConfirmed)
            return;

        t.Present = true;
        t.Tested = true;
        t.PlaybackConfirmed = true;
        t.Result = ValidationResults.Passed;
        t.Reason = "user_confirmed_playback";
    }

    private static void ReconcileAudioJack(FunctionalCertificationResults f, PortInventoryAssessment? ports)
    {
        if (FunctionalValidationMapper.IsUserSkipped(f.AudioJackTest))
            return;

        if (ports?.AudioJack.Value == false)
        {
            f.AudioJackTest.Present = false;
            f.AudioJackTest.Tested = false;
            f.AudioJackTest.Result = ValidationResults.PresentNotTested;
            f.AudioJackTest.Reason = "no_jack_detected";
            return;
        }

        if (f.AudioJackTest.Result == ValidationResults.Passed)
            return;

        if (ports?.AudioJack.Value == true)
            f.AudioJackTest.Present = true;

        var micOk = f.MicrophoneTest.Result == ValidationResults.Passed;
        var speakerOk = f.SpeakerTest.Result == ValidationResults.Passed;

        if (!micOk && !speakerOk)
            return;

        f.AudioJackTest.Present = true;
        f.AudioJackTest.Tested = true;
        f.AudioJackTest.Result = ValidationResults.Passed;
        f.AudioJackTest.Reason = micOk ? "microphone_functional_test" : "speaker_functional_test";
    }
}
