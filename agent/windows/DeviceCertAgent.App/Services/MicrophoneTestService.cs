using System.IO;
using DeviceCertAgent.Core.Models.V2;
using NAudio.Wave;

namespace DeviceCertAgent.App.Services;

/// <summary>Local-only microphone capture and playback — audio never leaves the device.</summary>
public sealed class MicrophoneTestService : IDisposable
{
    private string? _tempPath;
    private WaveOutEvent? _playback;
    private WaveStream? _playbackStream;

    public async Task<MicrophoneTestResult> RecordAsync(int seconds = 4, CancellationToken ct = default)
    {
        DeleteTemp();
        seconds = Math.Clamp(seconds, 3, 5);
        _tempPath = Path.Combine(Path.GetTempPath(), $"certronx-mic-{Guid.NewGuid():N}.wav");

        var deviceName = "Default capture device";
        var sampleRate = 44100;
        double peak = 0;
        double sum = 0;
        long samples = 0;

        await Task.Run(() =>
        {
            WaveInEvent? waveIn = null;
            WaveFileWriter? writer = null;
            var done = new ManualResetEventSlim(false);
            try
            {
                waveIn = new WaveInEvent { BufferMilliseconds = 100 };
                sampleRate = waveIn.WaveFormat.SampleRate;
                deviceName = $"Capture device ({waveIn.WaveFormat.SampleRate} Hz)";

                writer = new WaveFileWriter(_tempPath, waveIn.WaveFormat);

                waveIn.DataAvailable += (_, e) =>
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                    var bytesPerSample = Math.Max(1, waveIn.WaveFormat.BitsPerSample / 8);
                    var channels = Math.Max(1, waveIn.WaveFormat.Channels);
                    var frameCount = e.BytesRecorded / (bytesPerSample * channels);
                    for (var frame = 0; frame < frameCount; frame++)
                    {
                        var offset = frame * bytesPerSample * channels;
                        double sample = waveIn.WaveFormat.BitsPerSample switch
                        {
                            16 => Math.Abs(BitConverter.ToInt16(e.Buffer, offset) / 32768.0),
                            32 when waveIn.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat =>
                                Math.Abs(BitConverter.ToSingle(e.Buffer, offset)),
                            _ => Math.Abs(e.Buffer[offset] / 128.0),
                        };
                        peak = Math.Max(peak, sample);
                        sum += sample;
                        samples++;
                    }
                };

                waveIn.RecordingStopped += (_, _) => done.Set();
                waveIn.StartRecording();
                Thread.Sleep(seconds * 1000);
                waveIn.StopRecording();
                done.Wait(TimeSpan.FromSeconds(5));
            }
            finally
            {
                writer?.Dispose();
                waveIn?.Dispose();
            }
        }, ct);

        var fileReady = _tempPath is not null && File.Exists(_tempPath) && new FileInfo(_tempPath).Length > 44;
        var average = samples > 0 ? sum / samples : 0;
        return new MicrophoneTestResult
        {
            Present = true,
            Tested = true,
            RecordedSeconds = seconds,
            PeakLevel = Math.Round(peak, 4),
            AverageLevel = Math.Round(average, 4),
            SignalDetected = fileReady && (peak > 0.015 || average > 0.004),
            DeviceName = deviceName,
            SampleRateHz = sampleRate,
            TempAudioDeleted = false,
            Privacy = new FunctionalTestPrivacy { AudioUploaded = false },
            Result = fileReady ? ValidationResults.Inconclusive : ValidationResults.Failed,
            Reason = fileReady ? "awaiting_user_confirmation" : "recording_file_missing",
        };
    }

    public bool TryPlayLastSample(out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (_tempPath is null || !File.Exists(_tempPath))
            return false;

        if (new FileInfo(_tempPath).Length <= 44)
            return false;

        StopPlayback();
        try
        {
            _playbackStream = new WaveFileReader(_tempPath);
            duration = _playbackStream.TotalTime;
            _playback = new WaveOutEvent { DesiredLatency = 150 };
            _playback.Init(_playbackStream);
            _playback.PlaybackStopped += OnPlaybackStopped;
            _playback.Play();
            return true;
        }
        catch
        {
            StopPlayback();
            return false;
        }
    }

    public event EventHandler? PlaybackStopped;

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _playback!.PlaybackStopped -= OnPlaybackStopped;
        }
        catch { /* ignore */ }

        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void StopPlayback()
    {
        try
        {
            if (_playback is not null)
                _playback.PlaybackStopped -= OnPlaybackStopped;
            _playback?.Stop();
            _playback?.Dispose();
            _playbackStream?.Dispose();
        }
        catch { /* ignore */ }
        _playback = null;
        _playbackStream = null;
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
