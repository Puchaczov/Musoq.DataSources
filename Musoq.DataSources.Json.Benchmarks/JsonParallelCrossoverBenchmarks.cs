using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonParallelCrossoverBenchmarks
{
    private SourceExecutionContext _context = null!;
    private string _path = null!;

    [Params(10_000, 25_000, 50_000, 100_000)]
    public int RowCount { get; set; }

    [Params(1, 4)]
    public int WorkerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = JsonBenchmarkData.EnsureFlatFile(RowCount);
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [new SourceColumnRef("Sequence")],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var plan = new JsonSchema().TryPlanSource("file", request, _path).ExecutionPlan;
        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [new SchemaColumn("Sequence", 0, typeof(long))],
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [JsonParallelScanOptions.MaximumParallelismSettingName] = WorkerCount.ToString()
            },
            executionPlan: plan);
        _ = Scan();
    }

    [Benchmark]
    public long Scan()
    {
        var source = new JsonSource(_path, _context);
        long rows = 0;
        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
            rows += row.Length;
        return rows;
    }
}
