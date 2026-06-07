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
    public const string ProtocolScheme = "certronx";

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
        return new BuildMetadata(AgentConfig.BuildChannelDevelopment, true, false);
#else
        return new BuildMetadata(AgentConfig.BuildChannelDevelopment, true, false);
#endif
    }

    private static AppLaunchOptions ParseArgs(string[] args)
    {
        var options = new AppLaunchOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (TryParseDeepLink(arg, out var pairingCode, out var certificationToken))
            {
                if (!string.IsNullOrWhiteSpace(certificationToken))
                {
                    options.CertificationToken = certificationToken;
                    options.LaunchMode = AgentLaunchMode.Certification;
                }
                else if (!string.IsNullOrWhiteSpace(pairingCode))
                {
                    options.PairingCode = pairingCode;
                    options.LaunchMode = AgentLaunchMode.Paired;
                }
                continue;
            }

            switch (arg)
            {
                case "--api-url" when i + 1 < args.Length:
                    options.ApiUrlOverride = args[++i];
                    break;
                case "--mock-api":
                    options.MockApi = true;
                    break;
                case "--enhanced-scan":
                    options.EnhancedScanOnStartup = true;
                    break;
                case "--paired-required":
                    options.LaunchMode = AgentLaunchMode.Paired;
                    break;
                case "--pairing-code" when i + 1 < args.Length:
                    options.PairingCode = args[++i].Trim();
                    options.LaunchMode = AgentLaunchMode.Paired;
                    break;
                case "--token" when i + 1 < args.Length:
                    options.CertificationToken = args[++i].Trim();
                    options.LaunchMode = AgentLaunchMode.Certification;
                    break;
            }
        }

        options.DeviceNonce = Guid.NewGuid().ToString("N");
        return options;
    }

    public static bool TryParseDeepLink(string arg, out string pairingCode, out string certificationToken)
    {
        pairingCode = "";
        certificationToken = "";
        if (string.IsNullOrWhiteSpace(arg))
            return false;

        if (!Uri.TryCreate(arg.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, ProtocolScheme, StringComparison.OrdinalIgnoreCase))
            return false;

        var path = uri.AbsolutePath.Trim('/');
        if (!path.Equals("scan/start", StringComparison.OrdinalIgnoreCase))
            return false;

        var query = uri.Query;
        var token = GetQueryParam(query, "token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            certificationToken = token.Trim();
            return true;
        }

        var code = GetQueryParam(query, "pairingCode") ?? GetQueryParam(query, "pairing_code");
        if (string.IsNullOrWhiteSpace(code))
            return false;

        pairingCode = code.Trim();
        return true;
    }

    private static string? GetQueryParam(string query, string key)
    {
        if (string.IsNullOrEmpty(query))
            return null;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                continue;
            if (!string.Equals(Uri.UnescapeDataString(kv[0]), key, StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.UnescapeDataString(kv[1]);
        }

        return null;
    }

    private sealed record BuildMetadata(string BuildChannel, bool AllowEndpointOverride, bool ShowDeveloperUi);
}
