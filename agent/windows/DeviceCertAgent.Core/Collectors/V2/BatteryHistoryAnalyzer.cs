using System.Xml.Linq;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class BatteryHistoryAnalyzer
{
    public (BatteryAssessment Assessment, byte[]? RawReportBytes) Analyze(
        bool adminMode,
        List<string> warnings,
        bool includeHistoryReport = true)
    {
        var assessment = new BatteryIntelligenceCollector().Collect(adminMode, warnings);
        if (!includeHistoryReport)
            return (assessment, null);

        if (!adminMode)
        {
            warnings.Add("battery_v2.1: admin required for full capacity history");
            return (assessment, null);
        }

        var path = Path.Combine(Path.GetTempPath(), $"vt-battery-{Guid.NewGuid():N}.xml");
        byte[]? raw = null;
        try
        {
            PowerShellHelper.RunViaCmd($"powercfg /batteryreport /output \"{path}\" /xml", 45000);
            if (!File.Exists(path))
                return (assessment, null);

            raw = File.ReadAllBytes(path);
            var doc = XDocument.Load(path);
            ParseXml(doc, assessment, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"battery_v2.1: history parse failed ({ex.Message})");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        return (assessment, raw);
    }

    private static void ParseXml(XDocument doc, BatteryAssessment a, List<string> warnings)
    {
        var batteries = doc.Descendants().Where(e => e.Name.LocalName == "Battery").ToList();
        foreach (var bat in batteries)
        {
            var design = ParseInt(bat, "DesignCapacity");
            var full = ParseInt(bat, "FullChargeCapacity");
            if (design is > 0 && a.DesignCapacityMwh.Value is null or <= 0)
                a.DesignCapacityMwh = ConfidenceValue<int?>.Collected(design, "powercfg", "batteryreport_xml");
            if (full is > 0 && a.FullChargeCapacityMwh.Value is null or <= 0)
                a.FullChargeCapacityMwh = ConfidenceValue<int?>.Collected(full, "powercfg", "batteryreport_xml");

            foreach (var point in bat.Descendants().Where(e => e.Name.LocalName == "Capacity"))
            {
                var period = point.Attribute("Date")?.Value ?? point.Parent?.Attribute("Id")?.Value ?? "";
                var cap = ParseInt(point, "Value") ?? ParseIntElement(point);
                if (cap is > 0 && design is > 0)
                {
                    a.CapacityHistory.Add(new BatteryCapacityHistoryPoint
                    {
                        Period = period,
                        FullChargeCapacityMwh = cap.Value,
                        DesignCapacityMwh = design.Value,
                    });
                }
            }
        }

        if (a.CapacityHistory.Count >= 2)
        {
            var ordered = a.CapacityHistory.OrderBy(h => h.Period).ToList();
            var first = ordered.First();
            var last = ordered.Last();
            if (first.DesignCapacityMwh > 0)
            {
                var lossPct = (first.FullChargeCapacityMwh - last.FullChargeCapacityMwh) * 100.0 / first.DesignCapacityMwh;
                var years = Math.Max(1, ordered.Count / 12);
                a.CertificationNotes.Add($"Battery has lost {lossPct:0}% capacity over ~{years} year(s) of history.");
                a.DegradationTrend = ConfidenceValue<string?>.Collected(
                    lossPct switch
                    {
                        < 10 => "Stable",
                        < 20 => "Normal Wear",
                        < 35 => "Accelerated Wear",
                        _ => "Severe Degradation",
                    },
                    "powercfg", "history_analysis");
            }
        }
        else if (a.WearPercent.Value is not null)
        {
            a.DegradationTrend = ConfidenceValue<string?>.Collected(
                a.WearPercent.Value switch
                {
                    < 10 => "Stable",
                    < 20 => "Normal Wear",
                    < 35 => "Accelerated Wear",
                    _ => "Severe Degradation",
                },
                "snapshot", "wear_estimate", ConfidenceLevel.Medium);
            a.CertificationNotes.Add("Battery degradation is within normal expectations for current wear level.");
        }

        if (a.CycleCount.Value is > 0)
        {
            var remainingCycles = Math.Max(0, 1000 - a.CycleCount.Value.Value);
            a.EstimatedRemainingCycles = ConfidenceValue<int?>.Estimated(
                remainingCycles, "engine", "cycle_heuristic", "based on typical 1000-cycle life");
            var months = (int)(remainingCycles / 30.0 * 12);
            a.EstimatedRemainingMonths = ConfidenceValue<int?>.Estimated(
                Math.Max(3, months), "engine", "month_heuristic", "from cycle estimate");
        }
    }

    private static int? ParseInt(XElement parent, string localName)
    {
        var el = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
        return ParseIntElement(el);
    }

    private static int? ParseIntElement(XElement? el)
    {
        if (el is null) return null;
        return int.TryParse(el.Value.Trim(), out var v) ? v : null;
    }
}
