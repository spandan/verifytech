using DeviceCertAgent.Core.Models.V2;
using NAudio.Wave;

namespace DeviceCertAgent.App.Services;

/// <summary>Local-only microphone capture and playback — audio never leaves the device.</summary>
public sealed class MicrophoneTestService : IDisposable
{
    private string? _tempPath;
    private WaveOutEvent? _playback;

    public async Task<MicrophoneTestResult> RecordAsync(int seconds = 4, CancellationToken ct = default)
    {
        DeleteTemp();
        seconds = Math.Clamp(seconds, 3, 5);
        _tempPath = Path.Combine(Path.GetTempPath(), $"vt-mic-{Guid.NewGuid():N}.wav");

        var deviceName = "Default capture device";
        var sampleRate = 48000;
        double peak = 0;
        double sum = 0;
        long samples = 0;

        await Task.Run(() =>
        {
            using var waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(sampleRate, 16, 1),
                BufferMilliseconds = 50,
            };

            using var writer = new WaveFileWriter(_tempPath, waveIn.WaveFormat);
            var lockObj = new object();

            waveIn.DataAvailable += (_, e) =>
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
                var recorded = e.BytesRecorded / 2;
                for (var i = 0; i < recorded; i++)
                {
                    var sample = Math.Abs(BitConverter.ToInt16(e.Buffer, i * 2) / 32768.0);
                    lock (lockObj)
                    {
                        peak = Math.Max(peak, sample);
                        sum += sample;
                        samples++;
                    }
                }
            };

            waveIn.RecordingStopped += (_, _) => { };
            waveIn.StartRecording();
            Thread.Sleep(seconds * 1000);
            waveIn.StopRecording();
            writer.Flush();
        }, ct);

        var average = samples > 0 ? sum / samples : 0;
        return new MicrophoneTestResult
        {
            Present = true,
            Tested = true,
            RecordedSeconds = seconds,
            PeakLevel = Math.Round(peak, 4),
            AverageLevel = Math.Round(average, 4),
            SignalDetected = peak > 0.02 || average > 0.005,
            DeviceName = deviceName,
            SampleRateHz = sampleRate,
            TempAudioDeleted = false,
            Privacy = new FunctionalTestPrivacy { AudioUploaded = false },
            Result = peak > 0.02 ? ValidationResults.Passed : ValidationResults.Inconclusive,
            Reason = peak > 0.02 ? null : "low_signal_level",
        };
    }

    public void PlayLastSample()
    {
        if (_tempPath is null || !File.Exists(_tempPath)) return;
        StopPlayback();
        _playback = new WaveOutEvent();
        _playback.Init(new AudioFileReader(_tempPath));
        _playback.Play();
    }

    public void StopPlayback()
    {
        try
        {
            _playback?.Stop();
            _playback?.Dispose();
        }
        catch { /* ignore */ }
        _playback = null;
    }

    public bool DeleteTemp()
    {
        StopPlayback();
        if (_tempPath is null) return true;
        try
        {
            if (File.Exists(_tempPath))
                File.Delete(_tempPath);
            _tempPath = null;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => DeleteTemp();
}
