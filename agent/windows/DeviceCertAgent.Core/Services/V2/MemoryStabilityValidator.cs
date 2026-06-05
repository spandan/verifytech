using System.Diagnostics;
using DeviceCertAgent.Core.Models.V2;

namespace DeviceCertAgent.Core.Services.V2;

/// <summary>Lightweight non-destructive memory validation (&lt; 60 seconds).</summary>
public sealed class MemoryStabilityValidator
{
    public MemoryAssessment Run(MemoryAssessment inventory, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        var score = 100;

        try
        {
            // Allocation test
            var blocks = new List<byte[]>();
            for (var i = 0; i < 8 && sw.ElapsedMilliseconds < 15000; i++)
            {
                ct.ThrowIfCancellationRequested();
                blocks.Add(new byte[16 * 1024 * 1024]);
            }
            blocks.Clear();
            GC.Collect();
        }
        catch (OutOfMemoryException ex)
        {
            errors.Add($"allocation_failed: {ex.Message}");
            score -= 40;
        }

        // Read/write verification
        var buffer = new byte[32 * 1024 * 1024];
        var pattern = (byte)0xA5;
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = pattern;

        for (var pass = 0; pass < 3 && sw.ElapsedMilliseconds < 40000; pass++)
        {
            ct.ThrowIfCancellationRequested();
            for (var i = 0; i < buffer.Length; i += 4096)
            {
                if (buffer[i] != pattern)
                {
                    errors.Add($"read_error at offset {i}");
                    score -= 25;
                    break;
                }
            }
            pattern ^= 0xFF;
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = pattern;
        }

        // Memory pressure
        while (sw.ElapsedMilliseconds < 55000 && !ct.IsCancellationRequested)
        {
            var tmp = new byte[4 * 1024 * 1024];
            tmp[0] = 1;
            tmp[^1] = 2;
        }

        inventory.StabilityScore = ConfidenceValue<int?>.Collected(Math.Max(0, score), "MemoryStabilityValidator", "lightweight_test");
        inventory.PotentialFaults = errors.Count > 0
            ? TriStateValue.Verified(true, "memory_test", "fault_detected")
            : TriStateValue.Verified(false, "memory_test", "no_faults");
        inventory.DiagnosticsNotes.AddRange(errors);
        inventory.HealthSummary = ConfidenceValue<string?>.Collected(
            score >= 80 ? "Memory stable" : "Potential memory faults detected",
            "MemoryStabilityValidator", "summary");

        return inventory;
    }
}
