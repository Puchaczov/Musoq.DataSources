using System.Diagnostics;
using System.Text.Json;

namespace Musoq.DataSources.Json.Benchmarks;

internal static class NativeExecutionMeasurements
{
    internal static int Run(string output)
    {
        if (File.Exists(output))
            throw new InvalidOperationException("Measurement evidence already exists.");
        var results = new List<Measurement>();
        foreach (var shape in Enum.GetValues<JsonQueryShape>())
        {
            var benchmark = new JsonCompiledExecutionBenchmarks { RowCount = 100_000, QueryShape = shape, TypedSource = true };
            var allocated = GC.GetTotalAllocatedBytes(true);
            var timer = Stopwatch.StartNew();
            benchmark.Setup();
            var compilationMs = benchmark.CompilationMilliseconds;
            var compilationBytes = benchmark.CompilationAllocatedBytes;
            for (var warm = 0; warm < 10; warm++)
                benchmark.RunCompiledQuery();
            var times = new List<double>();
            var bytes = new List<long>();
            long checksum = 0;
            for (var iteration = 0; iteration < 30; iteration++)
            {
                allocated = GC.GetTotalAllocatedBytes(true);
                timer.Restart();
                checksum = benchmark.RunCompiledQuery();
                timer.Stop();
                times.Add(timer.Elapsed.TotalMilliseconds);
                bytes.Add(GC.GetTotalAllocatedBytes(true) - allocated);
            }
            times.Sort();
            bytes.Sort();
            results.Add(new Measurement(shape.ToString(), 100_000, times[times.Count / 2], bytes[bytes.Count / 2], compilationMs, compilationBytes, checksum, times));
            Console.WriteLine($"{shape}: {times[times.Count / 2]:F3} ms; {bytes[bytes.Count / 2]} bytes");
        }
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    private sealed record Measurement(string Shape, int Rows, double MedianMs, long MedianBytes,
        double CompilationMs, long CompilationBytes, long Checksum, List<double> SamplesMs);

}
