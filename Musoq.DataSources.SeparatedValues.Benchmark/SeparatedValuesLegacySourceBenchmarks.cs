using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using CsvHelper;
using CsvHelper.Configuration;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesLegacySourceBenchmarks
{
    private readonly byte[] _fileBuffer = new byte[1024 * 1024];
    private byte[] _bytes = null!;
    private string _path = null!;
    private SourceExecutionContext _executionContext = null!;
    private SourceExecutionContext _oneColumnContext = null!;
    private SourceExecutionContext _rejectedPredicateContext = null!;
    private SourceExecutionContext _zeroColumnContext = null!;

    [Params(100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = SeparatedValuesBenchmarkData.EnsureOneBrcFile(RowCount);
        _bytes = File.ReadAllBytes(_path);
        ISchemaColumn[] columns =
        [
            new SchemaColumn("Column1", 0, typeof(string)),
            new SchemaColumn("Column2", 1, typeof(string))
        ];
        _executionContext = RuntimeV2TestContexts.CreateExecutionContext(allColumns: columns);

        var schema = new SeparatedValuesSchema();
        var zeroPlan = schema.TryPlanSource(
            "semicolon",
            CreateRequest([]),
            _path,
            false,
            0).ExecutionPlan;
        _zeroColumnContext = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [],
            executionPlan: zeroPlan);

        var oneColumnPlan = schema.TryPlanSource(
            "semicolon",
            CreateRequest([new SourceColumnRef("Column2")]),
            _path,
            false,
            0).ExecutionPlan;
        _oneColumnContext = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [new SchemaColumn("Column2", 0, typeof(decimal))],
            executionPlan: oneColumnPlan);

        var rejectedPredicatePlan = schema.TryPlanSource(
            "semicolon",
            CreateRequest(
                [new SourceColumnRef("Column1")],
                new SourcePredicateComparison(
                    SourcePredicateComparisonOperator.GreaterThan,
                    new SourcePredicateColumn(new SourceColumnRef("Column2")),
                    new SourcePredicateLiteral(1000m))),
            _path,
            false,
            0).ExecutionPlan;
        _rejectedPredicateContext = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [new SchemaColumn("Column1", 0, typeof(string))],
            executionPlan: rejectedPredicatePlan);
    }

    [Benchmark(Baseline = true)]
    public long RawFileRead()
    {
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
            FileOptions.SequentialScan);
        long checksum = 0;
        int read;

        while ((read = stream.Read(_fileBuffer)) != 0)
            checksum += _fileBuffer[read - 1] + read;

        return checksum;
    }

    [Benchmark]
    public long RawMemoryScan()
    {
        long checksum = 0;
        foreach (var value in _bytes)
            checksum += value;
        return checksum;
    }

    [Benchmark]
    public long CsvHelperTokenScan()
    {
        using var stream = File.OpenRead(_path);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024 * 1024);
        using var parser = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = false,
            BadDataFound = null
        });
        long checksum = 0;

        while (parser.Read())
        {
            checksum += parser.Count;
            checksum += parser[0]?.GetHashCode() ?? 0;
            checksum += parser[1]?.GetHashCode() ?? 0;
        }

        return checksum;
    }

    [Benchmark]
    public long LegacyDataSource()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _executionContext,
            _path,
            false,
            0);
        long checksum = 0;

        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
        {
            checksum += row.Length;
            checksum += row[0]?.GetHashCode() ?? 0;
            checksum += row[1]?.GetHashCode() ?? 0;
        }

        return checksum;
    }

    [Benchmark]
    public long ZeroColumnDataSource()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _zeroColumnContext,
            _path,
            false,
            0);
        long checksum = 0;

        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
            checksum += row.Length + 1;

        return checksum;
    }

    [Benchmark]
    public long OneColumnDataSource()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _oneColumnContext,
            _path,
            false,
            0);
        long checksum = 0;

        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
            checksum += row.Length + (row[0]?.GetHashCode() ?? 0);

        return checksum;
    }

    [Benchmark]
    public long RejectedPredicateDataSource()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _rejectedPredicateContext,
            _path,
            false,
            0);
        long checksum = 0;

        foreach (var chunk in source.Chunks)
            checksum += chunk.Count;

        return checksum;
    }

    [Benchmark]
    public long FrozenLegacyAdapter()
    {
        long checksum = 0;

        foreach (var row in FrozenLegacySeparatedValuesAdapter.Read(_path, ";"))
        {
            checksum += row.Length;
            checksum += row[0]?.GetHashCode() ?? 0;
            checksum += row[1]?.GetHashCode() ?? 0;
        }

        return checksum;
    }

    private static SourcePlanRequest CreateRequest(
        IReadOnlyList<SourceColumnRef> requiredColumns,
        SourcePredicateExpression? predicate = null)
    {
        return new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = null,
            Take = null
        };
    }
}
