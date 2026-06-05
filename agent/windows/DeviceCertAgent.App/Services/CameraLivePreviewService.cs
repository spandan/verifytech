using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeviceCertAgent.Core.Services;
using WinRT.Interop;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;

namespace DeviceCertAgent.App.Services;

/// <summary>WinRT in-app camera preview — no photos/video uploaded.</summary>
public sealed class CameraLivePreviewService : IAsyncDisposable
{
    private readonly AgentLogger _logger = new();
    private readonly object _sync = new();
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private TypedEventHandler<MediaFrameReader, MediaFrameArrivedEventArgs>? _frameHandler;
    private Action<BitmapSource?>? _frameCallback;
    private string? _deviceId;
    private string? _deviceName;
    private string? _resolution;
    private int? _fps;
    private long _lastFrameUiTick;
    private volatile bool _stopping;

    public bool IsPreviewRunning => _reader is not null;
    public string? CameraName => _deviceName;
    public string? Resolution => _resolution;
    public int? FrameRateFps => _fps;

    public async Task<(bool Ok, string? Error)> StartPreviewAsync(
        IntPtr windowHandle,
        Action<BitmapSource?> frameCallback)
    {
        try
        {
            await StopPreviewAsync();
            _frameCallback = frameCallback;
            return await TryStartWinRtAsync(windowHandle);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, CameraAccessHelper.AccessDeniedCode);
        }
        catch (Exception ex)
        {
            _logger.Error("Camera preview start failed", ex);
            if (CameraAccessHelper.IsAccessDenied(ex.Message))
                return (false, CameraAccessHelper.AccessDeniedCode);
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string? Error)> TryStartWinRtAsync(IntPtr windowHandle)
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        if (devices.Count == 0)
            return (false, "no_camera_detected");

        Exception? lastError = null;
        foreach (var device in devices)
        {
            try
            {
                await DisposeCaptureSessionAsync();
                _deviceId = device.Id;
                _deviceName = device.Name;

                _capture = new MediaCapture();
                if (windowHandle != IntPtr.Zero)
                    InitializeWithWindow.Initialize(_capture, windowHandle);

                await _capture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    MediaCategory = MediaCategory.Other,
                    VideoDeviceId = device.Id,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                });

                var source = _capture.FrameSources.Values
                    .FirstOrDefault(s => s.Info.MediaStreamType == MediaStreamType.VideoRecord)
                    ?? _capture.FrameSources.Values
                        .FirstOrDefault(s => s.Info.MediaStreamType == MediaStreamType.VideoPreview)
                    ?? _capture.FrameSources.Values.FirstOrDefault();

                if (source is null)
                    continue;

                var format = source.SupportedFormats
                    .Where(f => f.VideoFormat is not null)
                    .OrderByDescending(f => f.VideoFormat!.Width * f.VideoFormat!.Height)
                    .FirstOrDefault()
                    ?? source.CurrentFormat;

                if (format is not null)
                {
                    try { await source.SetFormatAsync(format); } catch { /* use current */ }
                }

                if (format?.VideoFormat is { } vf)
                {
                    _resolution = $"{vf.Width}x{vf.Height}";
                    _fps = format.FrameRate is not null
                        ? (int)Math.Round(format.FrameRate.Numerator / (double)Math.Max(1, format.FrameRate.Denominator))
                        : 30;
                }
                else
                {
                    _resolution = "unknown";
                    _fps = 30;
                }

                _reader = await _capture.CreateFrameReaderAsync(source);
                _frameHandler = OnFrameArrived;
                _reader.FrameArrived += _frameHandler;
                await _reader.StartAsync();
                return (true, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
                await DisposeCaptureSessionAsync();
                return (false, CameraAccessHelper.AccessDeniedCode);
            }
            catch (Exception ex)
            {
                _logger.Warn($"WinRT camera init failed for {device.Name}: {ex.Message}");
                lastError = ex;
                await DisposeCaptureSessionAsync();
            }
        }

        return (false, lastError?.Message ?? "preview_failed");
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_stopping || _frameCallback is null)
            return;

        try
        {
            using var mediaFrame = sender.TryAcquireLatestFrame();
            var softwareBitmap = mediaFrame?.VideoMediaFrame?.SoftwareBitmap;
            if (softwareBitmap is null)
                return;

            var image = ConvertSoftwareBitmap(softwareBitmap);
            if (image is null)
                return;

            var now = Environment.TickCount64;
            if (now - _lastFrameUiTick < 100)
                return;
            _lastFrameUiTick = now;

            PostFrame(image);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Camera frame dropped: {ex.Message}");
        }
    }

    private void PostFrame(BitmapSource image)
    {
        var callback = _frameCallback;
        var dispatcher = Application.Current?.Dispatcher;
        if (callback is null || dispatcher is null)
            return;

        if (dispatcher.CheckAccess())
        {
            SafeInvoke(callback, image);
            return;
        }

        dispatcher.BeginInvoke(() => SafeInvoke(callback, image), DispatcherPriority.Render);
    }

    private void SafeInvoke(Action<BitmapSource?> callback, BitmapSource image)
    {
        try
        {
            if (!_stopping)
                callback(image);
        }
        catch (Exception ex)
        {
            _logger.Error("Camera preview UI update failed", ex);
        }
    }

    private static BitmapSource? ConvertSoftwareBitmap(SoftwareBitmap bitmap)
    {
        SoftwareBitmap? converted = null;
        try
        {
            converted = SoftwareBitmap.Convert(
                bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            if (width <= 0 || height <= 0)
                return null;

            var stride = width * 4;
            var buffer = new byte[stride * height];
            converted.CopyToBuffer(buffer.AsBuffer());

            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), buffer, stride, 0);
            wb.Freeze();
            return wb;
        }
        catch
        {
            return null;
        }
        finally
        {
            converted?.Dispose();
        }
    }

    public string? GetDeviceIdHash() =>
        string.IsNullOrWhiteSpace(_deviceId) ? null : HashingService.HashIdentifier(_deviceId);

    public async Task StopPreviewAsync()
    {
        lock (_sync)
            _stopping = true;

        _frameCallback = null;
        await DisposeCaptureSessionAsync();

        lock (_sync)
            _stopping = false;
    }

    private async Task DisposeCaptureSessionAsync()
    {
        if (_reader is not null && _frameHandler is not null)
        {
            try { await _reader.StopAsync(); } catch { /* ignore */ }
            _reader.FrameArrived -= _frameHandler;
            try { _reader.Dispose(); } catch { /* ignore */ }
            _reader = null;
            _frameHandler = null;
        }

        try { _capture?.Dispose(); } catch { /* ignore */ }
        _capture = null;
    }

    public async ValueTask DisposeAsync() => await StopPreviewAsync();

    public static IntPtr ResolveWindowHandle()
    {
        try
        {
            if (Application.Current?.MainWindow is not System.Windows.Window window)
                return IntPtr.Zero;
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero)
                helper.EnsureHandle();
            return helper.Handle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public static async Task<IntPtr> ResolveWindowHandleAsync(CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var hwnd = ResolveWindowHandle();
            if (hwnd != IntPtr.Zero)
                return hwnd;
            await Task.Delay(50, ct);
        }
        return IntPtr.Zero;
    }
}
