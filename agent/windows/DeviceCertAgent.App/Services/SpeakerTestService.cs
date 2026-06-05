using NAudio.Wave;

namespace DeviceCertAgent.App.Services;

/// <summary>Plays left-only and right-only test tones locally (no upload).</summary>
public sealed class SpeakerTestService : IDisposable
{
    private const int SampleRate = 44100;
    private const double DurationSec = 1.2;
    private const double FrequencyHz = 440;
    private WaveOutEvent? _player;

    public void PlayLeftChannel()
    {
        Stop();
        _player = new WaveOutEvent();
        _player.Init(new ChannelToneProvider(SampleRate, DurationSec, FrequencyHz, leftOnly: true));
        _player.Play();
    }

    public void PlayRightChannel()
    {
        Stop();
        _player = new WaveOutEvent();
        _player.Init(new ChannelToneProvider(SampleRate, DurationSec, FrequencyHz, leftOnly: false));
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

    private sealed class ChannelToneProvider : WaveProvider32
    {
        private readonly int _totalSamples;
        private int _position;
        private readonly double _freq;
        private readonly bool _leftOnly;

        public ChannelToneProvider(int sampleRate, double durationSec, double freqHz, bool leftOnly)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
            _totalSamples = (int)(sampleRate * durationSec);
            _freq = freqHz;
            _leftOnly = leftOnly;
        }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            var frames = sampleCount / 2;
            for (var i = 0; i < frames; i++)
            {
                if (_position >= _totalSamples)
                {
                    buffer[offset + i * 2] = 0;
                    buffer[offset + i * 2 + 1] = 0;
                    continue;
                }

                var t = _position / (double)WaveFormat.SampleRate;
                var sample = (float)(Math.Sin(2 * Math.PI * _freq * t) * 0.25);
                buffer[offset + i * 2] = _leftOnly ? sample : 0;
                buffer[offset + i * 2 + 1] = _leftOnly ? 0 : sample;
                _position++;
            }

            return frames * 2;
        }
    }
}
