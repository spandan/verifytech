namespace DeviceCertAgent.App.Services;

public static class DesktopRuntimeRequirement
{
    public const string DotNetMajorVersion = "8";
    public const string DownloadPageUrl = "https://dotnet.microsoft.com/download/dotnet/8.0";
    public const string DesktopRuntimeX64Url =
        "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.17-windows-x64-installer";

    public static string MissingRuntimeMessage =>
        "VerifyTech Agent requires the .NET 8 Desktop Runtime (x64).\n\n" +
        $"Download: {DownloadPageUrl}\n\n" +
        "Install the runtime, then run VerifyTech Agent again.";
}
