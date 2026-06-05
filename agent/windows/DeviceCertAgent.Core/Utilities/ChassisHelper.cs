namespace DeviceCertAgent.Core.Utilities;

public static class ChassisHelper
{
    private static readonly HashSet<int> LaptopChassisTypes =
    [
        8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32,
    ];

    public static bool IsLaptopChassis()
    {
        var enclosures = WmiHelper.Query("SELECT ChassisTypes FROM Win32_SystemEnclosure");
        foreach (var row in enclosures)
        {
            var raw = SafeConvert.ToString(row.GetValueOrDefault("ChassisTypes"));
            if (raw is null) continue;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var chassis) && LaptopChassisTypes.Contains(chassis))
                    return true;
            }
        }

        return false;
    }
}
