using System.Diagnostics;

namespace DeviceCertAgent.App.Services;

public static class CameraAccessHelper
{
    public const string AccessDeniedCode = "camera_access_denied";

    public static bool IsAccessDenied(string? error) =>
        !string.IsNullOrWhiteSpace(error) &&
        (error.Contains(AccessDeniedCode, StringComparison.OrdinalIgnoreCase)
         || error.Contains("access_denied", StringComparison.OrdinalIgnoreCase)
         || error.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
         || error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase));

    public static bool IsNoCameraDetected(string? error) =>
        !string.IsNullOrWhiteSpace(error) &&
        error.Contains("no_camera_detected", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetAccessInstructionSteps() =>
    [
        "1. Press Windows key + I to open Settings",
        "2. Select Privacy & security → Camera",
        "3. Turn on Camera access",
        "4. Turn on Let desktop apps access your camera",
        "5. Return to VerifyTech Agent and tap Retry camera",
    ];

    public static bool TryOpenCameraSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:privacy-webcam")
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
