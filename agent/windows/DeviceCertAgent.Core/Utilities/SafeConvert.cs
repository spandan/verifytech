namespace DeviceCertAgent.Core.Utilities;

public static class SafeConvert
{
    public static string? ToString(object? value)
    {
        if (value is null) return null;
        var text = value.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static int? ToInt(object? value)
    {
        if (value is null) return null;
        return int.TryParse(value.ToString(), out var n) ? n : null;
    }

    public static long? ToLong(object? value)
    {
        if (value is null) return null;
        return long.TryParse(value.ToString(), out var n) ? n : null;
    }

    public static double? ToDouble(object? value)
    {
        if (value is null) return null;
        return double.TryParse(value.ToString(), out var n) ? n : null;
    }

    public static bool? ToBool(object? value)
    {
        if (value is null) return null;
        if (value is bool b) return b;
        if (int.TryParse(value.ToString(), out var n)) return n != 0;
        return bool.TryParse(value.ToString(), out var result) ? result : null;
    }

    public static double BytesToGb(long bytes) => Math.Round(bytes / 1024d / 1024d / 1024d, 2);

    public static double BytesToGb(ulong bytes) => Math.Round(bytes / 1024d / 1024d / 1024d, 2);
}
