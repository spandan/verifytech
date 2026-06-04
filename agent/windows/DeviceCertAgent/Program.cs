using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceCertAgent.Models;
using DeviceCertAgent.Services;

Console.WriteLine("DevicePassport Windows Agent v0.1.0");
Console.WriteLine("Collecting device information...\n");

AgentOptions options;
try
{
    options = ArgumentParser.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine("Run with --help for usage.");
    return 1;
}

try
{
    var builder = new ReportBuilder();
    var report = builder.Build(options);

    PrintCollectionSummary(report);

    if (options.PrintJson || options.DryRun)
    {
        var json = JsonSerializer.Serialize(report, JsonOptions());
        Console.WriteLine("\n--- Report JSON ---");
        Console.WriteLine(json);
    }

    if (options.DryRun)
    {
        Console.WriteLine("\nDry run complete. No data submitted.");
        return 0;
    }

    using var client = new ApiClient(options.ApiUrl);

    if (options.Mode == "certify")
    {
        var result = await client.CertifyAsync(report);
        PrintCertifyResult(result);
    }
    else
    {
        var result = await client.VerifyAsync(report);
        PrintVerifyResult(result);
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"\nError: {SanitizeError(ex.Message)}");
    return 1;
}

static JsonSerializerOptions JsonOptions() => new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
};

static void PrintCollectionSummary(DeviceReport report)
{
    var t1 = report.Tier1CertificationIdentity;
    var t2 = report.Tier2ValueDetermination;

    Console.WriteLine($"Device:     {t1.Manufacturer} {t1.Model} ({t1.DeviceType})");
    Console.WriteLine($"OS:         {t1.OsVersion}");
    Console.WriteLine($"CPU:        {t1.CpuModel}");
    Console.WriteLine($"Memory:     {t1.RamTotalGb} GB");
    Console.WriteLine($"Storage:    {t1.StorageTotalGb} GB total ({t2.Storage.Count} drive(s))");

    if (t2.Battery.Present == true)
        Console.WriteLine($"Battery:    {t2.Battery.HealthPercent ?? 0}% health");
    else
        Console.WriteLine("Battery:    not detected");

    if (report.AgentMetadata.CollectionWarnings.Count > 0)
    {
        Console.WriteLine("\nWarnings:");
        foreach (var w in report.AgentMetadata.CollectionWarnings)
            Console.WriteLine($"  - {w}");
    }
}

static void PrintCertifyResult(CertifyApiResponse result)
{
    Console.WriteLine("\n=== Certification Complete ===");
    Console.WriteLine($"Certificate:  {result.CertificateCode}");
    Console.WriteLine($"Level:        {result.CertificateLevel}");
    Console.WriteLine($"Status:       {result.Status}");
    Console.WriteLine($"View online:  {result.CertificateUrl}");
}

static void PrintVerifyResult(VerifyApiResponse result)
{
    Console.WriteLine("\n=== Verification Result ===");
    Console.WriteLine($"Result:   {result.Result}");
    Console.WriteLine($"Message:  {result.Message}");

    if (result.Changes.Count > 0)
    {
        Console.WriteLine("\nChanges detected:");
        foreach (var change in result.Changes.Take(10))
            Console.WriteLine($"  - {change.Field}");
    }

    if (!string.IsNullOrWhiteSpace(result.VerificationUrl))
        Console.WriteLine($"\nDetails:  {result.VerificationUrl}");
}

static string SanitizeError(string message)
{
    // Avoid echoing anything that might look like raw identifiers in error text
    return message.Length > 500 ? message[..500] + "..." : message;
}
