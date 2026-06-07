namespace DeviceCertAgent.Core.Models;

public sealed class EligibilityProbeResult
{
    public string DeviceType { get; set; } = "unknown";
    public string DeviceTypeConfidence { get; set; } = "low";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public bool BatteryPresent { get; set; }
    public string OsCaption { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public string OsBuild { get; set; } = "";
    public bool IsVirtualMachine { get; set; }
    public string? VirtualMachineHint { get; set; }
    public string? ExpectedDeviceType { get; set; }
}

public sealed class EligibilityResult
{
    public bool Eligible { get; init; }
    public string? Reason { get; init; }
    public string? UserMessage { get; init; }

    public static EligibilityResult Pass() => new() { Eligible = true };

    public static EligibilityResult Fail(string reason, string? userMessage = null) =>
        new()
        {
            Eligible = false,
            Reason = reason,
            UserMessage = userMessage ?? reason,
        };
}
