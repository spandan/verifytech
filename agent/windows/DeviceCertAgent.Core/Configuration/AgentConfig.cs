using System.Reflection;

namespace DeviceCertAgent.Core.Configuration;

/// <summary>Central agent configuration constants (no secrets).</summary>
public static class AgentConfig
{
    public const string ProductionApiBaseUrl = "https://verifytech-production.up.railway.app";
    public const string StagingApiBaseUrl = "https://verifytech-staging.up.railway.app";

    public const string AppEnvProduction = "production";
    public const string AppEnvStaging = "staging";
    public const string AppEnvDevelopment = "development";

    public const string BuildChannelProduction = "production";
    public const string BuildChannelStaging = "staging";
    public const string BuildChannelDevelopment = "development";

    public const string EnvApiBaseUrl = "VERIFYTECH_API_BASE_URL";
    public const string EnvAllowOverride = "VERIFYTECH_ALLOW_ENDPOINT_OVERRIDE";

    public static string AgentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static string ProductName => "VerifyTech Agent";
}
