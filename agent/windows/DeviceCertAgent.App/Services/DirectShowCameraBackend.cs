using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using DeviceCertAgent.Core.Services;

namespace DeviceCertAgent.App.Services;

/// <summary>DirectShow webcam preview — reliable for unpackaged WPF desktop apps.</summary>
public sealed class DirectShowCameraBackend : IDisposable
{
    private readonly AgentLogger _logger = new();
    private VideoCaptureDevice? _device;
    private Action<BitmapSource?>? _callback;
    private long _lastFrameTick;
    private volatile bool _stopping;

    public string? DeviceName { get; private set; }
    public string? DeviceId { get; private set; }
    public string? Resolution { get; private set; }
    public int? FrameRateFps { get; private set; } = 30;

    public Task<(bool Ok, string? Error)> StartAsync(Action<BitmapSource?> frameCallback)
    {
        _callback = frameCallback;
        _stopping = false;

        try
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
                return Task.FromResult<(bool, string?)>((false, "no_camera_detected"));

            var info = devices[0];
            DeviceName = info.Name;
            DeviceId = info.MonikerString;

            _device = new VideoCaptureDevice(info.MonikerString);
            _device.NewFrame += OnNewFrame;
            _device.Start();

            Resolution = "live";
            return Task.FromResult<(bool, string?)>((true, null));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult<(bool, string?)>((false, CameraAccessHelper.AccessDeniedCode));
        }
        catch (Exception ex)
        {
            _logger.Error("DirectShow camera start failed", ex);
            if (CameraAccessHelper.IsAccessDenied(ex.Message))
                return Task.FromResult<(bool, string?)>((false, CameraAccessHelper.AccessDeniedCode));
            return Task.FromResult<(bool, string?)>((false, ex.Message));
        }
    }

    private void OnNewFrame(object sender, NewFrameEventArgs e)
    {
        if (_stopping || _callback is null)
            return;

        try
        {
            var now = Environment.TickCount64;
            if (now - _lastFrameTick < 66)
                return;
            _lastFrameTick = now;

            using var bitmap = (Bitmap)e.Frame.Clone();
            if (bitmap.Width > 0 && bitmap.Height > 0)
                Resolution = $"{bitmap.Width}x{bitmap.Height}";

            var image = BitmapToBitmapSource(bitmap);
            if (image is null)
                return;

            PostFrame(image);
        }
        catch (Exception ex)
        {
            _logger.Warn($"DirectShow frame dropped: {ex.Message}");
        }
    }

    private void PostFrame(BitmapSource image)
    {
        var callback = _callback;
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
            _logger.Error("DirectShow preview UI update failed", ex);
        }
    }

    private static BitmapSource? BitmapToBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    public void Stop()
    {
        _stopping = true;
        _callback = null;

        if (_device is null)
            return;

        try
        {
            _device.NewFrame -= OnNewFrame;
            if (_device.IsRunning)
                _device.SignalToStop();
            _device.WaitForStop();
        }
        catch (Exception ex)
        {
            _logger.Warn($"DirectShow stop failed: {ex.Message}");
        }
        finally
        {
            _device = null;
        }
    }

    public void Dispose() => Stop();

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
