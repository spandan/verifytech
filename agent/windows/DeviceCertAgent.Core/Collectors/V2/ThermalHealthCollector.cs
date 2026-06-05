using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class ThermalHealthCollector
{
    public ThermalAssessment Collect(List<StorageDriveAssessment> storage, List<string> warnings)
    {
        var t = new ThermalAssessment();
        var zones = WmiHelper.Query("SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature", @"\\.\root\wmi");
        if (zones.Count > 0)
        {
            var raw = SafeConvert.ToInt(zones[0].GetValueOrDefault("CurrentTemperature"));
            if (raw is > 0)
            {
                var c = (raw.Value - 2732) / 10;
                if (c is > 0 and < 120)
                {
                    t.CpuTempC = ConfidenceValue<int?>.Collected(c, "MSAcpi_ThermalZoneTemperature", "wmi");
                    t.IdleCpuTempC = t.CpuTempC;
                }
            }
        }

        var ssd = storage.Select(s => s.TemperatureC.Value).FirstOrDefault(v => v is > 0);
        if (ssd is > 0)
            t.SsdTempC = ConfidenceValue<int?>.Collected(ssd, "storage_reliability", "passthrough");

        if (t.CpuTempC.Value is null)
        {
            warnings.Add("thermal: CPU temperature sensor unavailable");
            t.Condition = ConfidenceValue<string?>.Unavailable("thermal", "classifier", "no sensors");
            return t;
        }

        var peak = t.CpuTempC.Value.Value;
        t.PeakCpuTempC = ConfidenceValue<int?>.Collected(peak, "snapshot", "idle_reading", ConfidenceLevel.Medium);
        t.AverageCpuTempC = t.IdleCpuTempC;

        var (condition, score) = peak switch
        {
            < 55 => ("Excellent", 95),
            < 70 => ("Good", 80),
            < 85 => ("Fair", 65),
            < 95 => ("Warning", 45),
            _ => ("Critical", 25),
        };
        t.Condition = ConfidenceValue<string?>.Collected(condition, "engine", "thermal_classifier", ConfidenceLevel.Medium);
        t.ConditionScore = ConfidenceValue<int?>.Collected(score, "engine", "thermal_classifier");
        return t;
    }

    public void ApplyBenchmarkThermals(ThermalAssessment t, int idleC, int peakC, bool throttled)
    {
        if (idleC > 0)
            t.IdleCpuTempC = ConfidenceValue<int?>.Collected(idleC, "benchmark", "cpu_load_test");
        if (peakC > 0)
            t.PeakCpuTempC = ConfidenceValue<int?>.Collected(peakC, "benchmark", "cpu_load_test");
        if (idleC > 0 && peakC > 0)
            t.AverageCpuTempC = ConfidenceValue<int?>.Collected((idleC + peakC) / 2, "benchmark", "average");
        t.ThrottlingDetected = TriStateValue.Verified(throttled, "benchmark", "frequency_monitor");
        if (peakC >= 90)
            t.Condition = ConfidenceValue<string?>.Collected("Warning", "benchmark", "thermal_classifier");
    }
}
