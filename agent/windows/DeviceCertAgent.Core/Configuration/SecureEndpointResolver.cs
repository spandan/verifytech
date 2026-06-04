using System.Text.Json;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Configuration;

/// <summary>
/// Resolves API base URL with production-safe defaults.
/// Production builds only accept VERIFYTECH_API_BASE_URL when VERIFYTECH_ALLOW_ENDPOINT_OVERRIDE=1.
/// </summary>
public sealed class SecureEndpointResolver
{
    public const string LocalConfigFile = "appsettings.local.json";

    public (AgentRuntimeSettings Settings, AppLaunchOptions Launch) Resolve(string[] args)
    {
        var launch = ParseArgs(args);
        var build = ReadBuildMetadata();
        var settings = ResolveSettings(build, launch);
        return (settings, launch);
    }

    private static AgentRuntimeSettings ResolveSettings(BuildMetadata build, AppLaunchOptions launch)
    {
        var settings = new AgentRuntimeSettings
        {
            ApiBaseUrl = DefaultUrlForChannel(build.BuildChannel),
            AppEnv = MapEnv(build.BuildChannel),
            BuildChannel = build.BuildChannel,
            AgentVersion = AgentConfig.AgentVersion,
            AllowEndpointOverride = build.AllowEndpointOverride,
            ShowDeveloperUi = build.ShowDeveloperUi,
            MockApi = launch.MockApi,
        };

        if (launch.MockApi)
            return settings;

        string? overrideUrl = null;

        if (build.AllowEndpointOverride)
        {
            overrideUrl = Environment.GetEnvironmentVariable(AgentConfig.EnvApiBaseUrl);
            if (string.IsNullOrWhiteSpace(overrideUrl))
                overrideUrl = TryLoadLocalConfigUrl();
            if (string.IsNullOrWhiteSpace(overrideUrl) && !string.IsNullOrWhiteSpace(launch.ApiUrlOverride))
                overrideUrl = launch.ApiUrlOverride;
        }
        else if (IsOverrideExplicitlyAllowed())
        {
            overrideUrl = Environment.GetEnvironmentVariable(AgentConfig.EnvApiBaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            var normalized = NormalizeUrl(overrideUrl);
            settings.ApiBaseUrl = normalized;
            if (normalized.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                settings.AppEnv = AgentConfig.AppEnvDevelopment;
        }

        return settings;
    }

    private static string DefaultUrlForChannel(string channel) => channel switch
    {
        AgentConfig.BuildChannelStaging => AgentConfig.StagingApiBaseUrl,
        AgentConfig.BuildChannelDevelopment => "http://localhost:8000",
        _ => AgentConfig.ProductionApiBaseUrl,
    };

    private static string MapEnv(string channel) => channel switch
    {
        AgentConfig.BuildChannelStaging => AgentConfig.AppEnvStaging,
        AgentConfig.BuildChannelDevelopment => AgentConfig.AppEnvDevelopment,
        _ => AgentConfig.AppEnvProduction,
    };

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        if (!trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("API URL must start with http:// or https://");

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only HTTPS endpoints are allowed outside local development.");

        return trimmed;
    }

    private static bool IsOverrideExplicitlyAllowed() =>
        string.Equals(
            Environment.GetEnvironmentVariable(AgentConfig.EnvAllowOverride),
            "1",
            StringComparison.Ordinal);

    private static string? TryLoadLocalConfigUrl()
    {
        try
        {
            foreach (var path in LocalConfigPaths())
            {
                if (!File.Exists(path)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("apiBaseUrl", out var url))
                    return url.GetString();
            }
        }
        catch
        {
            // ignore malformed local config
        }
        return null;
    }

    private static IEnumerable<string> LocalConfigPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, LocalConfigFile);
        yield return Path.Combine(Directory.GetCurrentDirectory(), LocalConfigFile);
    }

    private static BuildMetadata ReadBuildMetadata()
    {
#if PRODUCTION_BUILD
        return new BuildMetadata(AgentConfig.BuildChannelProduction, false, false);
#elif STAGING_BUILD
        return new BuildMetadata(AgentConfig.BuildChannelStaging, false, false);
#elif DEV_BUILD
        return new BuildMetadata(AgentConfig.BuildChannelDevelopment, true, true);
#else
        return new BuildMetadata(AgentConfig.BuildChannelDevelopment, true, true);
#endif
    }

    private static AppLaunchOptions ParseArgs(string[] args)
    {
        var options = new AppLaunchOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--api-url" when i + 1 < args.Length:
                    options.ApiUrlOverride = args[++i];
                    break;
                case "--mock-api":
                    options.MockApi = true;
                    break;
            }
        }
        return options;
    }

    private sealed record BuildMetadata(string BuildChannel, bool AllowEndpointOverride, bool ShowDeveloperUi);
}
