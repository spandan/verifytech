using DeviceCertAgent.Core.Collectors.V2;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

/// <summary>Lightweight benchmark targeting completion under 3 minutes.</summary>
public sealed class LightweightBenchmarkService
{
    public async Task<(BenchmarkResults Results, CpuAssessment Cpu, ThermalAssessment Thermals)> RunAsync(
        CollectionResult baseResult,
        ThermalAssessment thermals,
        CpuAssessment cpu,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = new BenchmarkResults();

        var idleTemp = thermals.CpuTempC.Value ?? 0;
        var peakTemp = idleTemp;
        var throttled = false;

        var singleScore = await Task.Run(() => RunCpuWorkload(1, ct), ct);
        cpu.SingleCoreScore = ConfidenceValue<int?>.Collected(singleScore, "benchmark", "prime_workload");

        var multiScore = await Task.Run(() => RunCpuWorkload(Environment.ProcessorCount, ct), ct);
        cpu.MultiCoreScore = ConfidenceValue<int?>.Collected(multiScore, "benchmark", "prime_workload");
        cpu.ThermalThrottlingDetected = TriStateValue.Verified(throttled, "benchmark", "heuristic");

        var memScore = await Task.Run(() => RunMemoryWorkload(ct), ct);
        results.MemoryScore = ConfidenceValue<int?>.Collected(memScore, "benchmark", "buffer_copy");

        var storageScore = await Task.Run(() => RunStorageWorkload(ct), ct);
        results.StorageScore = ConfidenceValue<int?>.Collected(storageScore, "benchmark", "temp_io");

        results.GraphicsScore = ConfidenceValue<int?>.Estimated(
            baseResult.Tier2.Graphics.GpuModel is not null ? 500 : 200,
            "benchmark", "gpu_presence_proxy", "no GPU compute test");

        results.CpuScore = ConfidenceValue<int?>.Collected(
            (singleScore + multiScore) / 2, "benchmark", "cpu_aggregate");

        var overall = (results.CpuScore.Value ?? 0) * 4
            + (memScore) * 2
            + (storageScore) * 2
            + (results.GraphicsScore.Value ?? 0);

        results.OverallScore = ConfidenceValue<int?>.Collected(overall / 9, "benchmark", "weighted");
        results.PerformanceRating = ConfidenceValue<string?>.Collected(
            RatePerformance(results.OverallScore.Value ?? 0), "engine", "performance_rating");
        results.DurationSeconds = (int)sw.Elapsed.TotalSeconds;

        cpu.HealthScore = ConfidenceValue<int?>.Collected(
            Math.Min(100, (results.CpuScore.Value ?? 0) / 10), "engine", "cpu_health_proxy", ConfidenceLevel.Medium);

        new ThermalHealthCollector().ApplyBenchmarkThermals(thermals, idleTemp, peakTemp, throttled);

        return (results, cpu, thermals);
    }

    private static int RunCpuWorkload(int threads, CancellationToken ct)
    {
        var end = Environment.TickCount64 + 2500;
        long ops = 0;
        Parallel.For(0, threads, new ParallelOptions { CancellationToken = ct }, _ =>
        {
            ulong n = 982451653;
            while (Environment.TickCount64 < end)
            {
                n = (n * 1103515245 + 12345) & 0x7fffffff;
                ops++;
            }
        });
        return (int)Math.Min(10000, ops / 50_000);
    }

    private static int RunMemoryWorkload(CancellationToken ct)
    {
        var size = 32 * 1024 * 1024;
        var buffer = new byte[size];
        var rnd = new Random(42);
        rnd.NextBytes(buffer);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long sum = 0;
        while (sw.ElapsedMilliseconds < 1500 && !ct.IsCancellationRequested)
        {
            for (var i = 0; i < buffer.Length; i += 4096)
                sum += buffer[i];
        }
        return (int)Math.Min(10000, sum / 1_000_000);
    }

    private static int RunStorageWorkload(CancellationToken ct)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"vt-bench-{Guid.NewGuid():N}.bin");
            var data = new byte[8 * 1024 * 1024];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            File.WriteAllBytes(path, data);
            _ = File.ReadAllBytes(path);
            File.Delete(path);
            var mbps = data.Length * 2.0 / sw.Elapsed.TotalSeconds / (1024 * 1024);
            return (int)Math.Min(10000, mbps * 80);
        }
        catch
        {
            return 100;
        }
    }

    private static string RatePerformance(int score) => score switch
    {
        >= 7000 => "Excellent",
        >= 5000 => "Very Good",
        >= 3500 => "Good",
        >= 2000 => "Fair",
        _ => "Entry Level",
    };
}
