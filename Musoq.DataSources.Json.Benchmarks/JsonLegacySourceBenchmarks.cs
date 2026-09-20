using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Newtonsoft.Json;

namespace Musoq.DataSources.Json.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonLegacySourceBenchmarks
{
    private readonly byte[] _fileBuffer = new byte[1024 * 1024];
    private byte[] _bytes = null!;
    private string _path = null!;
    private Musoq.Schema.Optimization.SourceExecutionContext _executionContext = null!;
    private SourceExecutionContext _oneColumnContext = null!;
    private SourceExecutionContext _rejectedPredicateContext = null!;
    private SourceExecutionContext _zeroColumnContext = null!;

    [Params(100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = JsonBenchmarkData.EnsureFlatFile(RowCount);
        _bytes = File.ReadAllBytes(_path);
        ISchemaColumn[] columns =
        [
            new SchemaColumn("Station", 0, typeof(string)),
            new SchemaColumn("Temperature", 1, typeof(decimal)),
            new SchemaColumn("Sequence", 2, typeof(long))
        ];
        _executionContext = RuntimeV2TestContexts.CreateExecutionContext(allColumns: columns);

        _oneColumnContext = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [new SchemaColumn("Station", 0, typeof(string))],
            executionPlan: new SourceExecutionPlan
            {
                Identity = Identity("one"),
                AcceptedColumns = [new SourceColumnRef("Station")],
                AcceptedOrderBy = []
            });

        _zeroColumnContext = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: new SourceExecutionPlan
            {
                Identity = Identity("zero"),
                AcceptedColumns = [],
                AcceptedOrderBy = [],
                Properties = new Dictionary<string, object?>
                {
                    [JsonPlanning.ReadPlanPropertyName] = new JsonReadPlan(true)
                }
            });

        var predicate = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterThan,
            new SourcePredicateColumn(new SourceColumnRef("Sequence")),
            new SourcePredicateLiteral((long)RowCount));
        var request = new SourcePlanRequest
        {
            Identity = Identity("rejected"),
            RequiredColumns = [new SourceColumnRef("Station")],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var plan = new JsonSchema().TryPlanSource("file", request, _path).ExecutionPlan;
        _rejectedPredicateContext = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [new SchemaColumn("Station", 0, typeof(string))],
            executionPlan: plan);
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
    public long NewtonsoftTokenScan()
    {
        using var stream = File.OpenRead(_path);
        using var textReader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024 * 1024);
        using var reader = new JsonTextReader(textReader);
        long checksum = 0;

        while (reader.Read())
        {
            if (reader.TokenType is JsonToken.String or JsonToken.Integer or JsonToken.Float)
                checksum += reader.Value?.GetHashCode() ?? 0;
        }

        return checksum;
    }

    [Benchmark]
    public long SystemTextJsonTokenScan()
    {
        var reader = new Utf8JsonReader(_bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 256
        });
        long checksum = 0;

        while (reader.Read())
            checksum += (long)reader.TokenType + reader.TokenStartIndex;

        return checksum;
    }

    [Benchmark]
    public long LegacyDataSource()
    {
        var source = new JsonSource(_path, _executionContext);
        long checksum = 0;

        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
        {
            checksum += row.Length;
            checksum += row[0]?.GetHashCode() ?? 0;
            checksum += row[1]?.GetHashCode() ?? 0;
            checksum += row[2]?.GetHashCode() ?? 0;
        }

        return checksum;
    }

    [Benchmark]
    public long ZeroColumnDataSource()
    {
        var source = new JsonSource(_path, _zeroColumnContext);
        long rows = 0;
        foreach (var chunk in source.Chunks)
            rows += chunk.Count;
        return rows;
    }

    [Benchmark]
    public long OneColumnDataSource()
    {
        var source = new JsonSource(_path, _oneColumnContext);
        long checksum = 0;
        foreach (var chunk in source.Chunks)
        foreach (var row in chunk)
            checksum += row[0]?.GetHashCode() ?? 0;
        return checksum;
    }

    [Benchmark]
    public long RejectedPredicateDataSource()
    {
        var source = new JsonSource(_path, _rejectedPredicateContext);
        long rows = 0;
        foreach (var chunk in source.Chunks)
            rows += chunk.Count;
        return rows;
    }

    [Benchmark]
    public long FrozenLegacyAdapter()
    {
        var rows = FrozenLegacyJsonAdapter.Read(_path, ["Station", "Temperature", "Sequence"]);
        long checksum = 0;

        foreach (var row in rows)
        {
            checksum += row.Length;
            checksum += row[0]?.GetHashCode() ?? 0;
            checksum += row[1]?.GetHashCode() ?? 0;
            checksum += row[2]?.GetHashCode() ?? 0;
        }

        return checksum;
    }

    private static SourceIdentity Identity(string suffix)
    {
        return new SourceIdentity("json", "file", $"benchmark-{suffix}", "source");
    }
}
