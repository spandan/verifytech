using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Media.Imaging;
using DeviceCertAgent.Core.Services;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;

namespace DeviceCertAgent.App.Services;

/// <summary>WinRT in-app camera preview — no photos/video uploaded.</summary>
public sealed class CameraLivePreviewService : IAsyncDisposable
{
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs>? _frameHandler;
    private string? _deviceId;
    private string? _deviceName;
    private string? _resolution;
    private int? _fps;

    public bool IsPreviewRunning => _reader is not null;
    public string? CameraName => _deviceName;
    public string? Resolution => _resolution;
    public int? FrameRateFps => _fps;

    public async Task<(bool Ok, string? Error)> StartPreviewAsync(Action<BitmapSource?> frameCallback)
    {
        try
        {
            await StopPreviewAsync();
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            if (devices.Count == 0)
                return (false, "no_camera_detected");

            var device = devices[0];
            _deviceId = device.Id;
            _deviceName = device.Name;

            _capture = new MediaCapture();
            await _capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MediaCategory = MediaCategory.Other,
                VideoDeviceId = device.Id,
            });

            var source = _capture.FrameSources.Values
                .FirstOrDefault(s => s.Info.MediaStreamType == MediaStreamType.VideoPreview
                    || s.Info.MediaStreamType == MediaStreamType.VideoRecord)
                ?? _capture.FrameSources.Values.FirstOrDefault();

            if (source is null)
                return (false, "no_video_frame_source");

            var format = source.CurrentFormat ?? source.SupportedFormats
                .OrderByDescending(f => f.VideoFormat?.Width ?? 0)
                .FirstOrDefault();

            if (format?.VideoFormat is { } vf)
            {
                _resolution = $"{vf.Width}x{vf.Height}";
                _fps = (int)Math.Round(format.FrameRate ?? 30);
            }
            else
            {
                _resolution = "unknown";
                _fps = 30;
            }

            _reader = await _capture.CreateFrameReaderAsync(source);
            _frameHandler = (_, args) =>
            {
                try
                {
                    using var frame = args.TryAcquireLatestFrame();
                    var sb = frame?.VideoMediaFrame?.SoftwareBitmap;
                    if (sb is null) return;
                    var image = ConvertToBitmap(sb);
                    Application.Current?.Dispatcher.BeginInvoke(() => frameCallback(image));
                }
                catch
                {
                    // drop frame
                }
            };
            _reader.FrameArrived += _frameHandler;
            await _reader.StartAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static BitmapSource? ConvertToBitmap(SoftwareBitmap bitmap)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream).AsTask().GetAwaiter().GetResult();
        var converted = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8
            ? bitmap
            : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        encoder.SetSoftwareBitmap(converted);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
        stream.Seek(0);

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = stream.AsStream();
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public string? GetDeviceIdHash() =>
        string.IsNullOrWhiteSpace(_deviceId) ? null : HashingService.HashIdentifier(_deviceId);

    public async Task StopPreviewAsync()
    {
        if (_reader is not null && _frameHandler is not null)
        {
            try { await _reader.StopAsync(); } catch { /* ignore */ }
            _reader.FrameArrived -= _frameHandler;
            _reader.Dispose();
            _reader = null;
            _frameHandler = null;
        }

        _capture?.Dispose();
        _capture = null;
    }

    public async ValueTask DisposeAsync() => await StopPreviewAsync();
}
