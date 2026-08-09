using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesZeroColumnCeilingBenchmarks
{
    private readonly byte[] _fileBuffer = new byte[1024 * 1024];
    private SourceExecutionContext _context = null!;
    private string _path = null!;

    [Params(2_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = SeparatedValuesBenchmarkData.EnsureOneBrcFile(RowCount);
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
            .TryPlanSource("semicolon", request, _path, false, 0)
            .ExecutionPlan;
        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [],
            executionPlan: plan);
        _ = ZeroColumnSourceScan();
    }

    [Benchmark(Baseline = true)]
    public long RawFileRead()
    {
        using var stream = new FileStream(
            _path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.SequentialScan);
        long checksum = 0;
        int read;

        while ((read = stream.Read(_fileBuffer)) != 0)
            checksum += _fileBuffer[read - 1] + read;

        return checksum;
    }

    [Benchmark]
    public long ZeroColumnSourceScan()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _context,
            _path,
            false,
            0);
        long rows = 0;
        foreach (var chunk in source.Chunks)
            rows += chunk.Count;
        return rows;
    }
}
