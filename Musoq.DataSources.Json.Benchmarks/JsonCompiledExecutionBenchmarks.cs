using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Json.Benchmarks;

public enum JsonQueryShape
{
    Count,
    OneColumn,
    FullRow,
    PredicateTenPercent,
    PredicateHalf,
    EarlyTake,
    GroupedAggregates
}

[MemoryDiagnoser]
[ShortRunJob]
public class JsonCompiledExecutionBenchmarks
{
    private CompiledQuery _query = null!;

    [Params(100_000)]
    public int RowCount { get; set; }

    [ParamsSource(nameof(QueryShapes))]
    public JsonQueryShape QueryShape { get; set; }

    public IEnumerable<JsonQueryShape> QueryShapes => Enum.GetValues<JsonQueryShape>();

    [GlobalSetup]
    public void Setup()
    {
        var dataPath = QueryPath(JsonBenchmarkData.EnsureFlatFile(RowCount));
        var source = $"json.file('{dataPath}')";
        var queryText = QueryShape switch
        {
            JsonQueryShape.Count => $"select Count(Station) from {source}",
            JsonQueryShape.OneColumn => $"select Station from {source}",
            JsonQueryShape.FullRow => $"select Station, Temperature, Sequence from {source}",
            JsonQueryShape.PredicateTenPercent =>
                $"select Station, Sequence from {source} where Sequence >= {RowCount - RowCount / 10}",
            JsonQueryShape.PredicateHalf =>
                $"select Station, Sequence from {source} where Sequence >= {RowCount / 2}",
            JsonQueryShape.EarlyTake => $"select Station from {source} take 100",
            JsonQueryShape.GroupedAggregates =>
                $"select Station, Min(Sequence), Max(Sequence), Avg(Sequence) from {source} group by Station",
            _ => throw new ArgumentOutOfRangeException()
        };

        _query = InstanceCreatorHelpers.CompileForExecution(
            queryText,
            $"JsonBenchmark_{QueryShape}_{Guid.NewGuid():N}",
            new JsonBenchmarkSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

        _ = RunCompiledQuery();
    }

    [Benchmark]
    public long RunCompiledQuery()
    {
        var table = _query.Run();
        long checksum = table.Count;

        foreach (var row in table)
            checksum = unchecked(checksum * 31 + row.Values.Count());

        return checksum;
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/').Replace("'", "''", StringComparison.Ordinal);
    }

    private sealed class JsonBenchmarkSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new JsonSchema();
        }
    }
}
