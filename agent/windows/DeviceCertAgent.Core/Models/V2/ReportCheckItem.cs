using System.Text.RegularExpressions;

namespace DeviceCertAgent.Core.Models.V2;

public sealed class ReportCheckItem
{
    public string Label { get; init; } = "";
    public string Headline { get; init; } = "";
    public string? Detail { get; init; }
    /// <summary>good | bad | warn | muted | neutral</summary>
    public string Tone { get; init; } = "neutral";
}

public static class ReportCheckFormatting
{
    private static readonly Regex DashSplit = new(@"\s*[—–-]\s*", RegexOptions.Compiled);

    public static ReportCheckItem Create(string label, string? text)
    {
        var raw = (text ?? "").Trim();
        if (string.IsNullOrEmpty(raw) ||
            raw.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return new ReportCheckItem
            {
                Label = label,
                Headline = "Not checked",
                Tone = "muted",
            };
        }

        var dashParts = DashSplit.Split(raw, 2);
        if (dashParts.Length == 2)
        {
            return new ReportCheckItem
            {
                Label = label,
                Headline = dashParts[0].Trim(),
                Detail = dashParts[1].Trim(),
                Tone = InferTone(dashParts[0]),
            };
        }

        var paren = raw.IndexOf('(');
        if (paren > 0 && raw.EndsWith(')'))
        {
            return new ReportCheckItem
            {
                Label = label,
                Headline = raw[..paren].Trim(),
                Detail = raw[paren..].Trim(),
                Tone = InferTone(raw[..paren]),
            };
        }

        return new ReportCheckItem
        {
            Label = label,
            Headline = raw,
            Tone = InferTone(raw),
        };
    }

    public static ReportCheckItem FromFunctionalLine(string line)
    {
        var raw = (line ?? "").Trim();
        var dashParts = DashSplit.Split(raw, 2);
        if (dashParts.Length == 2)
        {
            return new ReportCheckItem
            {
                Label = dashParts[0].Trim(),
                Headline = dashParts[1].Trim(),
                Tone = InferTone(dashParts[1]),
            };
        }

        return Create("Check", raw);
    }

    public static string InferTone(string headline)
    {
        var h = headline.ToLowerInvariant();
        if (h.Contains("verified") || h.Contains("pass") || h.Contains("yes") ||
            h.Contains("enabled") || h.Contains("healthy") || h.Contains("excellent") ||
            h.Contains("good") || h.Contains("on"))
            return "good";
        if (h.Contains("failed") || h.Contains("poor") || h.Contains("no") ||
            h.Contains("disabled") || h.Contains("replacement") || h.Contains("missing"))
            return "bad";
        if (h.Contains("not tested") || h.Contains("not verified") || h.Contains("not checked") ||
            h.Contains("not present") || h.Contains("inconclusive") || h.Contains("skipped"))
            return "muted";
        if (h.Contains("fair") || h.Contains("review") || h.Contains("caution") || h.Contains("wear"))
            return "warn";
        return "neutral";
    }
}
