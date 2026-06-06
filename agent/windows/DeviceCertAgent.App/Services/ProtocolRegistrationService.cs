using System.IO;
using Microsoft.Win32;

namespace DeviceCertAgent.App.Services;

/// <summary>
/// Registers certronx:// under HKCU so deep links work without administrator rights.
/// </summary>
public static class ProtocolRegistrationService
{
    private const string ProtocolName = "certronx";

    public static void EnsureRegistered()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return;

        exePath = Path.GetFullPath(exePath);
        var command = $"\"{exePath}\" \"%1\"";

        try
        {
            using var root = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
            if (root is null)
                return;

            root.SetValue("URL Protocol", "");
            root.SetValue("", "URL:Certronx Protocol");

            using var icon = root.CreateSubKey("DefaultIcon");
            icon?.SetValue("", $"\"{exePath}\",0");

            using var shell = root.CreateSubKey(@"shell\open\command");
            var existing = shell?.GetValue("") as string;
            if (string.Equals(existing, command, StringComparison.OrdinalIgnoreCase))
                return;

            shell?.SetValue("", command);
        }
        catch
        {
            // Non-fatal — user can still paste a pairing code manually.
        }
    }
}
