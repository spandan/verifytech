using System.Diagnostics;
using System.IO;

namespace DeviceCertAgent.App.Services;

public static class AdminElevationService
{
    public static bool TryRelaunchAsAdmin(IEnumerable<string> extraArgs)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return false;

        var args = string.Join(" ", extraArgs.Select(QuoteArg));
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string QuoteArg(string arg) =>
        arg.Contains(' ') || arg.Contains('"') ? $"\"{arg.Replace("\"", "\\\"")}\"" : arg;
}
