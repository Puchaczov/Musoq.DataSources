using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonStringReuseBenchmarks
{
    private SourceExecutionContext _context = null!;
    private string _path = null!;

    [Params(JsonStringReuseCardinality.Low, JsonStringReuseCardinality.High)]
    public JsonStringReuseCardinality Cardinality { get; set; }

    [Params(false, true)]
    public bool ReuseEnabled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        const int rowCount = 100_000;
        _path = Cardinality == JsonStringReuseCardinality.Low
            ? JsonBenchmarkData.EnsureFlatFile(rowCount)
            : JsonBenchmarkData.EnsureEvolvingFile(rowCount);
        var required = Cardinality == JsonStringReuseCardinality.Low
            ? new[]
            {
                new SourceColumnRef("Station"),
                new SourceColumnRef("Temperature"),
                new SourceColumnRef("Sequence")
            }
            : [new SourceColumnRef("Name")];
        var columns = Cardinality == JsonStringReuseCardinality.Low
            ? new ISchemaColumn[]
            {
                new SchemaColumn("Station", 0, typeof(string)),
                new SchemaColumn("Temperature", 1, typeof(decimal)),
                new SchemaColumn("Sequence", 2, typeof(long))
            }
            : [new SchemaColumn("Name", 0, typeof(string))];
        var plan = new JsonSchema().TryPlanSource("file", Request(required), _path).ExecutionPlan;
        var snapshot = JsonSchemaDiscovery.GetSnapshot(_path);
        if (!ReuseEnabled)
            snapshot.StringPool.Disable();

        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns,
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [JsonParallelScanOptions.MaximumParallelismSettingName] = "1"
            },
            executionPlan: plan);
        _ = Scan();

        if (ReuseEnabled && Cardinality == JsonStringReuseCardinality.High && !snapshot.StringPool.IsDisabled)
            throw new InvalidOperationException("The high-cardinality fixture did not disable string reuse.");
    }

    [Benchmark]
    public long Scan()
    {
        long checksum = 0;
        if (Cardinality == JsonStringReuseCardinality.Low)
        {
            var source = NativeJsonBenchmarkRows.Open<NativeJsonBenchmarkRows.FlatRow, NativeJsonBenchmarkRows.FlatMaterializer>(_path, _context);
            foreach (var chunk in source.Chunks)
            foreach (var row in chunk)
                checksum += (row.Station?.GetHashCode() ?? 0) + row.Temperature.GetHashCode() + row.Sequence.GetHashCode();
        }
        else
        {
            var source = NativeJsonBenchmarkRows.Open<NativeJsonBenchmarkRows.TextRow, NativeJsonBenchmarkRows.TextMaterializer>(_path, _context);
            foreach (var chunk in source.Chunks)
            foreach (var row in chunk)
                checksum += row.Value?.GetHashCode() ?? 0;
        }
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

public enum JsonStringReuseCardinality
{
    Low,
    High
}
