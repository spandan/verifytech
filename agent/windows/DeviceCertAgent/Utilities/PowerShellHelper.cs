using System.Diagnostics;

namespace DeviceCertAgent.Utilities;

public static class PowerShellHelper
{
    public static string? Run(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(15000);
            return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
        }
        catch
        {
            return null;
        }
    }
}
