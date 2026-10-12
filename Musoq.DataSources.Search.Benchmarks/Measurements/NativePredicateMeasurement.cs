using System.Diagnostics;
using System.Text.Json;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

internal static class NativePredicateMeasurement
{
    internal static int Run(string output)
    {
        if (File.Exists(output)) throw new InvalidOperationException("Evidence already exists.");
        var root = Path.Combine(Path.GetTempPath(), "musoq-native-predicate-search-v1");
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "fixture.txt");
        if (!File.Exists(file)) File.WriteAllLines(file, Enumerable.Repeat("TODO", 100_000));
        var workloads = new[] { ("Fraction", "LineNumber > 50000.5", 50_000L, 3_750_075_000L),
            ("In", "LineNumber in (20000,40000,60000,80000)", 4L, 200_004L),
            ("NullableOr", "Origin = 'x' or LineNumber > 50000.5", 50_000L, 3_750_075_000L) };
        var results = new List<Measurement>();
        foreach (var (name, predicate, expectedCount, expectedChecksum) in workloads)
        {
            var query = $"select LineNumber, OccurrenceCount from search.lines('{root.Replace('\\','/')}','TODO') where {predicate}";
            var before = GC.GetTotalAllocatedBytes(true);
            var timer = Stopwatch.StartNew();
            using var compiled = InstanceCreator.CompileForExecution(query, "SearchMeasurement" + Guid.NewGuid().ToString("N"), new Provider(), new LoggerResolver());
            var compilationMs = timer.Elapsed.TotalMilliseconds;
            var compilationBytes = GC.GetTotalAllocatedBytes(true) - before;
            long Consume()
            {
                using var table = compiled.Run();
                long count = 0, checksum = 0;
                foreach (var row in table) { count++; checksum += row.GetValue<long>(0) + row.GetValue<long>(1); }
                if (count != expectedCount || checksum != expectedChecksum) throw new InvalidOperationException($"{name}: incorrect consumed results {count}/{checksum}.");
                return checksum;
            }
            for (var warmup = 0; warmup < 100; warmup++) _ = Consume();
            var timings = new List<double>(); var allocations = new List<long>();
            for (var iteration = 0; iteration < 30; iteration++)
            {
                before = GC.GetTotalAllocatedBytes(true); timer.Restart(); _ = Consume(); timer.Stop();
                timings.Add(timer.Elapsed.TotalMilliseconds); allocations.Add(GC.GetTotalAllocatedBytes(true)-before);
            }
            var sortedTimes = timings.Order().ToArray(); var sortedBytes = allocations.Order().ToArray();
            results.Add(new(name, sortedTimes[15], sortedBytes[15], compilationMs, compilationBytes, timings));
            Console.WriteLine($"{name}: {sortedTimes[15]:F4} ms; {sortedBytes[15]} bytes");
            File.WriteAllText(output + "." + name + ".cs", InstanceCreator.CompileForInspection(query, "SearchInspection" + Guid.NewGuid().ToString("N"), new Provider(), new LoggerResolver()).GeneratedCSharpCode);
        }
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private sealed record Measurement(string Shape, double MedianMs, long MedianBytes, double CompilationMs, long CompilationBytes, List<double> SamplesMs);
    private sealed class Provider : ISchemaProvider { public ISchema GetSchema(string name) => new SearchSchema(); }
    private sealed class LoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;
        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }
}
