using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesStringReuseBenchmarks
{
    private SourceExecutionContext _context = null!;
    private string _path = null!;

    [Params(StringReuseCardinality.Low, StringReuseCardinality.High)]
    public StringReuseCardinality Cardinality { get; set; }

    [Params(false, true)]
    public bool ReuseEnabled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        const int rowCount = 100_000;
        _path = Cardinality == StringReuseCardinality.Low
            ? SeparatedValuesBenchmarkData.EnsureOneBrcFile(rowCount)
            : SeparatedValuesBenchmarkData.EnsureUniqueStringsFile(rowCount);
        var required = Cardinality == StringReuseCardinality.Low
            ? new[] { new SourceColumnRef("Column1"), new SourceColumnRef("Column2") }
            : [new SourceColumnRef("Column1")];
        var columns = Cardinality == StringReuseCardinality.Low
            ? new ISchemaColumn[]
            {
                new SchemaColumn("Column1", 0, typeof(string)),
                new SchemaColumn("Column2", 1, typeof(decimal))
            }
            : [new SchemaColumn("Column1", 0, typeof(string))];
        var plan = new SeparatedValuesSchema().TryPlanSource(
            "semicolon",
            Request(required),
            _path,
            false,
            0).ExecutionPlan;
        var snapshot = SeparatedValuesSchemaDiscovery.GetSnapshot(_path, ";", false, 0);
        if (!ReuseEnabled)
            snapshot.StringPool.Disable();

        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns,
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = "1"
            },
            executionPlan: plan);
        _ = Scan();

        if (ReuseEnabled && Cardinality == StringReuseCardinality.High && !snapshot.StringPool.IsDisabled)
            throw new InvalidOperationException("The high-cardinality fixture did not disable string reuse.");
    }

    [Benchmark]
    public long Scan()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _context,
            _path,
            false,
            0);
        long checksum = 0;
        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
            foreach (var value in row)
                checksum += value?.GetHashCode() ?? 0;
        return checksum;
    }

    private static SourcePlanRequest Request(IReadOnlyList<SourceColumnRef> requiredColumns)
    {
        return new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = null
        };
    }
}

public enum StringReuseCardinality
{
    Low,
    High
}
