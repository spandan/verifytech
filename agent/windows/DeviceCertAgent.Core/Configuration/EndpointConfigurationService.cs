using System.Text.Json;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Configuration;

public sealed class EndpointConfigurationService
{
    public const string ProductionDefault = "https://api.yourdomain.com";
    public const string LocalConfigFile = "appsettings.local.json";

    public (EndpointSettings Settings, AppLaunchOptions Launch) Resolve(string[] args)
    {
        var launch = ParseArgs(args);
        var settings = LoadLocalConfig() ?? new EndpointSettings();

        if (!string.IsNullOrWhiteSpace(launch.ApiUrlOverride))
            settings.ApiBaseUrl = launch.ApiUrlOverride.TrimEnd('/');

        if (launch.MockApi)
            settings.MockApi = true;

        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            settings.ApiBaseUrl = ProductionDefault;

        return (settings, launch);
    }

    private static AppLaunchOptions ParseArgs(string[] args)
    {
        var options = new AppLaunchOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode" when i + 1 < args.Length:
                    options.Mode = args[++i].ToLowerInvariant();
                    break;
                case "--certificate-code" when i + 1 < args.Length:
                    options.CertificateCode = args[++i].ToUpperInvariant();
                    break;
                case "--api-url" when i + 1 < args.Length:
                    options.ApiUrlOverride = args[++i].TrimEnd('/');
                    break;
                case "--intake-id" when i + 1 < args.Length:
                    options.IntakeId = args[++i];
                    break;
                case "--mock-api":
                    options.MockApi = true;
                    break;
                case "--headless":
                    options.Headless = true;
                    break;
            }
        }
        return options;
    }

    private static EndpointSettings? LoadLocalConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, LocalConfigFile);
            if (!File.Exists(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), LocalConfigFile);
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new EndpointSettings
            {
                ApiBaseUrl = root.TryGetProperty("apiBaseUrl", out var url)
                    ? url.GetString() ?? ProductionDefault
                    : ProductionDefault,
                Environment = root.TryGetProperty("environment", out var env)
                    ? env.GetString() ?? "production"
                    : "production",
                MockApi = root.TryGetProperty("mockApi", out var mock) && mock.GetBoolean(),
            };
        }
        catch
        {
            return null;
        }
    }
}
