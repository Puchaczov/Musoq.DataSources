using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesDialectMatrixBenchmarks
{
    private SourceExecutionContext _context = null!;
    private string _path = null!;
    private bool _hasHeader;
    private string _tableName = null!;

    [Params("one-brc", "wide", "quoted-multiline")]
    public string Shape { get; set; } = "one-brc";

    [Params(100_000, 1_000_000)]
    public int RowCount { get; set; }

    [Params(1, 2, 4, 8, 16)]
    public int WorkerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_path, _tableName, _hasHeader) = Shape switch
        {
            "wide" => (SeparatedValuesBenchmarkData.EnsureWideFile(RowCount), "comma", true),
            "quoted-multiline" => (SeparatedValuesBenchmarkData.EnsureQuotedMultilineFile(RowCount), "comma", true),
            _ => (SeparatedValuesBenchmarkData.EnsureOneBrcFile(RowCount), "semicolon", false)
        };

        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var plan = new SeparatedValuesSchema()
            .TryPlanSource(_tableName, request, _path, _hasHeader, 0)
            .ExecutionPlan;
        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [],
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
        var separator = _tableName == "semicolon" ? ";" : ",";
        var source = SeparatedValuesNativeBenchmarkSource.Create(
            _path,
            separator,
            _hasHeader,
            _context);
        long rows = 0;
        foreach (var chunk in source.Chunks)
            rows += chunk.Count;
        return rows;
    }
}
