using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesParallelCrossoverBenchmarks
{
    private SourceExecutionContext _context = null!;
    private string _path = null!;

    [Params(100_000, 250_000, 500_000, 750_000, 1_000_000)]
    public int RowCount { get; set; }

    [Params(1, 2)]
    public int WorkerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = SeparatedValuesBenchmarkData.EnsureOneBrcFile(RowCount);
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [new SourceColumnRef("Column2")],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var plan = new SeparatedValuesSchema()
            .TryPlanSource("semicolon", request, _path, false, 0)
            .ExecutionPlan;
        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [new SchemaColumn("Column2", 0, typeof(decimal))],
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = WorkerCount.ToString()
            },
            executionPlan: plan);
        _ = Scan();
    }

    [Benchmark]
    public long Scan()
    {
        var source = SeparatedValuesNativeBenchmarkSource.Create<decimal>(
            _path,
            ";",
            false,
            _context);
        long rows = 0;
        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
            rows += 1;
        return rows;
    }
}
