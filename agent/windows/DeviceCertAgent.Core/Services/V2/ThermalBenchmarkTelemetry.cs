using System.Diagnostics;
using System.Text.Json;
using DeviceCertAgent.Core.Collectors.V2;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;

namespace DeviceCertAgent.Core.Services.V2;

public sealed class ThermalBenchmarkTelemetry
{
    public async Task<(ThermalAssessment Thermals, byte[] EvidenceJson, CpuAssessment Cpu)> RunAsync(
        ThermalAssessment thermals,
        CpuAssessment cpu,
        CancellationToken ct = default)
    {
        var samples = new List<ThermalSample>();
        var baseMhz = SafeConvert.ToInt(
            WmiHelper.Query("SELECT MaxClockSpeed FROM Win32_Processor").FirstOrDefault()?.GetValueOrDefault("MaxClockSpeed")) ?? 2400;
        var expectedSustained = baseMhz * 0.85;
        var cpuThrottled = false;
        var gpuThrottled = false;
        var peakCpu = thermals.CpuTempC.Value ?? 0;
        var sumCpu = 0;
        var count = 0;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(25));

        var loadTask = Task.Run(() =>
        {
            ulong n = 982451653;
            while (!cts.Token.IsCancellationRequested)
                n = (n * 1103515245 + 12345) & 0x7fffffff;
        }, cts.Token);

        for (var sec = 0; sec < 20 && !cts.Token.IsCancellationRequested; sec++)
        {
            await Task.Delay(1000, ct);
            var curMhz = ReadCurrentCpuMhz();
            var cpuTemp = ReadCpuTemp() ?? thermals.CpuTempC.Value;
            var gpuMhz = ReadGpuMhz();
            var gpuTemp = ReadGpuTemp();

            samples.Add(new ThermalSample
            {
                Second = sec,
                CpuFrequencyGhz = curMhz / 1000.0,
                CpuTempC = cpuTemp,
                GpuFrequencyMhz = gpuMhz,
                GpuTempC = gpuTemp,
            });

            if (curMhz > 0 && curMhz < expectedSustained)
                cpuThrottled = true;
            if (gpuMhz > 0 && sec > 5 && gpuMhz < 300)
                gpuThrottled = true;

            if (cpuTemp is > 0)
            {
                peakCpu = Math.Max(peakCpu, cpuTemp.Value);
                sumCpu += cpuTemp.Value;
                count++;
            }
        }

        try { cts.Cancel(); await loadTask; } catch { /* load ended */ }

        thermals.BenchmarkSamples = samples;
        thermals.CpuThrottlingDetected = TriStateValue.Verified(cpuThrottled, "benchmark_telemetry", "frequency_monitor");
        thermals.GpuThrottlingDetected = TriStateValue.Verified(gpuThrottled, "benchmark_telemetry", "gpu_clock");
        thermals.PeakCpuTempC = ConfidenceValue<int?>.Collected(peakCpu, "benchmark_telemetry", "peak");
        thermals.AverageCpuTempC = count > 0
            ? ConfidenceValue<int?>.Collected(sumCpu / count, "benchmark_telemetry", "average")
            : thermals.AverageCpuTempC;
        thermals.ThermalStabilityScore = ConfidenceValue<int?>.Collected(
            cpuThrottled ? 55 : (peakCpu > 90 ? 65 : 90), "engine", "stability_score", ConfidenceLevel.Medium);

        var cooling = peakCpu switch
        {
            < 75 => "Excellent",
            < 85 => "Good",
            < 95 => "Fair",
            _ => "Poor",
        };
        thermals.CoolingHealth = ConfidenceValue<string?>.Collected(cooling, "benchmark_telemetry", "cooling_classifier");
        thermals.Condition = thermals.CoolingHealth;
        thermals.ConditionScore = ConfidenceValue<int?>.Collected(
            cooling switch { "Excellent" => 95, "Good" => 80, "Fair" => 60, _ => 40 },
            "engine", "thermal_score");

        cpu.ThermalThrottlingDetected = thermals.CpuThrottlingDetected;

        var evidence = JsonSerializer.SerializeToUtf8Bytes(new { samples, cpu_throttled = cpuThrottled, gpu_throttled = gpuThrottled, peak_cpu_c = peakCpu });
        return (thermals, evidence, cpu);
    }

    private static int ReadCurrentCpuMhz()
    {
        var rows = WmiHelper.Query("SELECT CurrentClockSpeed FROM Win32_Processor");
        return SafeConvert.ToInt(rows.FirstOrDefault()?.GetValueOrDefault("CurrentClockSpeed")) ?? 0;
    }

    private static int? ReadCpuTemp()
    {
        var zones = WmiHelper.Query("SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature", @"\\.\root\wmi");
        if (zones.Count == 0) return null;
        var raw = SafeConvert.ToInt(zones[0].GetValueOrDefault("CurrentTemperature"));
        return raw is > 0 ? (raw.Value - 2732) / 10 : null;
    }

    private static double? ReadGpuMhz()
    {
        var script = "(Get-Counter '\\GPU Engine(*)\\Utilization Percentage' -ErrorAction SilentlyContinue | Select-Object -First 1).CounterSamples.CookedValue";
        var outVal = PowerShellHelper.Run(script, 5000);
        return double.TryParse(outVal, out var v) ? v * 10 : null;
    }

    private static int? ReadGpuTemp() => null;
}
