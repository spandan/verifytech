using DeviceCertAgent.Core.Configuration;

namespace DeviceCertAgent.Core.Services;

/// <summary>Local troubleshooting logs without storing raw serial numbers.</summary>
public sealed class AgentLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VerifyTech",
        "Agent",
        "logs");

    private readonly string _logPath;

    public AgentLogger()
    {
        Directory.CreateDirectory(LogDir);
        _logPath = Path.Combine(LogDir, $"agent-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] v{AgentConfig.AgentVersion} {message}{Environment.NewLine}";
        try
        {
            File.AppendAllText(_logPath, line);
        }
        catch
        {
            // logging must not break the agent
        }
    }
}
