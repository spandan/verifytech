using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DeviceCertAgent.App.Services;

/// <summary>Plays left-only and right-only test tones locally (no upload).</summary>
public sealed class SpeakerTestService : IDisposable
{
    private const int SampleRate = 44100;
    private const double DurationSec = 1.5;
    private const double FrequencyHz = 440;
    private WaveOutEvent? _player;

    public void PlayLeftChannel() => PlayChannel(leftOnly: true);

    public void PlayRightChannel() => PlayChannel(leftOnly: false);

    private void PlayChannel(bool leftOnly)
    {
        Stop();
        var tone = new SignalGenerator(SampleRate, 1)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = FrequencyHz,
            Gain = 0.55,
        }.Take(TimeSpan.FromSeconds(DurationSec));

        var mux = new MultiplexingSampleProvider(new[] { tone }, 2);
        mux.ConnectInputToOutput(0, leftOnly ? 0 : 1);

        _player = new WaveOutEvent { DesiredLatency = 120 };
        _player.Init(mux);
        _player.Play();
    }

    public void Stop()
    {
        try
        {
            _player?.Stop();
            _player?.Dispose();
        }
        catch { /* ignore */ }
        _player = null;
    }

    public void Dispose() => Stop();
}
