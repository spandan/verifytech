namespace DeviceCertAgent.Core.Utilities;

public static class ChassisHelper
{
    private static readonly HashSet<int> LaptopChassisTypes =
    [
        8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32,
    ];

    private static readonly HashSet<int> DesktopChassisTypes =
    [
        3, 4, 5, 6, 7, 13, 15, 16, 35,
    ];

    private static readonly HashSet<int> ServerChassisTypes =
    [
        17, 23,
    ];

    public static bool IsLaptopChassis() =>
        string.Equals(DetectDeviceType("", "").Type, "laptop", StringComparison.OrdinalIgnoreCase);

    public static (string Type, string Confidence) DetectDeviceType(string manufacturer, string model)
    {
        var chassisTypes = ReadChassisTypes();
        if (chassisTypes.Count == 0)
            return InferFromModel(manufacturer, model, "low");

        if (chassisTypes.Any(LaptopChassisTypes.Contains))
            return ("laptop", "high");

        if (chassisTypes.Any(ServerChassisTypes.Contains))
            return ("server", "high");

        if (chassisTypes.Any(DesktopChassisTypes.Contains))
        {
            var combined = $"{manufacturer} {model}".ToUpperInvariant();
            if (combined.Contains("WORKSTATION", StringComparison.Ordinal))
                return ("workstation", "high");
            if (combined.Contains("MINI", StringComparison.Ordinal) || combined.Contains("NUC", StringComparison.Ordinal))
                return ("mini_pc", "medium");
            return ("desktop", "high");
        }

        return InferFromModel(manufacturer, model, "medium");
    }

    private static List<int> ReadChassisTypes()
    {
        var result = new List<int>();
        var enclosures = WmiHelper.Query("SELECT ChassisTypes FROM Win32_SystemEnclosure");
        foreach (var row in enclosures)
        {
            var raw = SafeConvert.ToString(row.GetValueOrDefault("ChassisTypes"));
            if (raw is null) continue;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var chassis))
                    result.Add(chassis);
            }
        }

        return result;
    }

    private static (string Type, string Confidence) InferFromModel(string manufacturer, string model, string confidence)
    {
        var combined = $"{manufacturer} {model}".ToUpperInvariant();
        if (combined.Contains("LAPTOP", StringComparison.Ordinal)
            || combined.Contains("NOTEBOOK", StringComparison.Ordinal)
            || combined.Contains("BOOK", StringComparison.Ordinal))
            return ("laptop", "medium");

        if (combined.Contains("SERVER", StringComparison.Ordinal) || combined.Contains("POWEREDGE", StringComparison.Ordinal))
            return ("server", "medium");

        if (combined.Contains("WORKSTATION", StringComparison.Ordinal) || combined.Contains("PRECISION", StringComparison.Ordinal))
            return ("workstation", "medium");

        if (combined.Contains("MINI", StringComparison.Ordinal) || combined.Contains("NUC", StringComparison.Ordinal))
            return ("mini_pc", "medium");

        if (combined.Contains("DESKTOP", StringComparison.Ordinal) || combined.Contains("TOWER", StringComparison.Ordinal))
            return ("desktop", "medium");

        return ("unknown", confidence);
    }
}
