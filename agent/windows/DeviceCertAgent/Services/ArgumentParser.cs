namespace DeviceCertAgent.Services;

using DeviceCertAgent.Models;

public static class ArgumentParser
{
    public static AgentOptions Parse(string[] args)
    {
        var options = new AgentOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--mode" when i + 1 < args.Length:
                    options.Mode = args[++i].ToLowerInvariant();
                    break;
                case "--certificate-code" when i + 1 < args.Length:
                    options.CertificateCode = args[++i].ToUpperInvariant();
                    break;
                case "--api-url" when i + 1 < args.Length:
                    options.ApiUrl = args[++i].TrimEnd('/');
                    break;
                case "--intake-id" when i + 1 < args.Length:
                    options.IntakeId = args[++i];
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--print-json":
                    options.PrintJson = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        Validate(options);
        return options;
    }

    private static void Validate(AgentOptions options)
    {
        if (options.Mode is not ("certify" or "verify"))
            throw new ArgumentException("Mode must be 'certify' or 'verify'.");

        if (options.Mode == "verify" && string.IsNullOrWhiteSpace(options.CertificateCode))
            throw new ArgumentException("Verify mode requires --certificate-code.");

        if (!Uri.TryCreate(options.ApiUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("API URL must be a valid http or https URL.");
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            DevicePassport Windows Agent v0.1.0

            Usage:
              DeviceCertAgent.exe --mode certify --api-url https://api.example.com
              DeviceCertAgent.exe --mode verify --certificate-code XXXX-XXXX-XXXX --api-url https://api.example.com

            Options:
              --mode              certify | verify (required)
              --certificate-code  Required for verify mode
              --api-url           Backend API base URL (required)
              --intake-id         Optional intake session ID
              --dry-run           Collect and print JSON without submitting
              --print-json        Print report JSON before submission
              --help              Show this help
            """);
    }
}
