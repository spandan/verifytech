using System.Management;

namespace DeviceCertAgent.Core.Utilities;

public static class WmiHelper
{
    public static List<Dictionary<string, object?>> Query(string wql, string? scopePath = null)
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            var scope = scopePath is null
                ? new ManagementScope(@"\\.\root\cimv2")
                : new ManagementScope(scopePath);
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wql));
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in obj.Properties)
                {
                    row[prop.Name] = prop.Value is Array arr
                        ? string.Join(",", arr.Cast<object?>())
                        : prop.Value;
                }
                rows.Add(row);
                obj.Dispose();
            }
        }
        catch
        {
            // Caller handles missing WMI data
        }

        return rows;
    }

    public static string? FirstString(string wql, string property, string? scopePath = null)
    {
        var rows = Query(wql, scopePath);
        foreach (var row in rows)
        {
            if (row.TryGetValue(property, out var value))
            {
                var text = SafeConvert.ToString(value);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        return null;
    }

    public static bool DeviceExists(string wql)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            return searcher.Get().Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
