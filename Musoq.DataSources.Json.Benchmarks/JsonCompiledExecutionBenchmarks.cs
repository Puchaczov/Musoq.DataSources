using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Evaluator.Tables;
using Musoq.Schema.Optimization;
using Musoq.Converter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
[InProcess]
public class JsonCompiledExecutionBenchmarks
{
    private CompiledQuery _query = null!;
    private string _queryText = string.Empty;

    public double CompilationMilliseconds { get; private set; }
    public long CompilationAllocatedBytes { get; private set; }

    [Params(100_000)]
    public int RowCount { get; set; }

    [ParamsSource(nameof(TypedSourceModes))]
    public bool TypedSource { get; set; } = true;

    public IEnumerable<bool> TypedSourceModes
    {
        get
        {
            var selected = Environment.GetEnvironmentVariable("MUSOQ_JSON_BENCHMARK_TYPED_SOURCE");
            return string.IsNullOrWhiteSpace(selected) ? [true] : [bool.Parse(selected)];
        }
    }

    [ParamsSource(nameof(QueryShapes))]
    public JsonQueryShape QueryShape { get; set; }

    public IEnumerable<JsonQueryShape> QueryShapes
    {
        get
        {
            var selected = Environment.GetEnvironmentVariable("MUSOQ_JSON_BENCHMARK_SHAPES");
            return string.IsNullOrWhiteSpace(selected)
                ? Enum.GetValues<JsonQueryShape>()
                : selected.Split(',').Select(Enum.Parse<JsonQueryShape>);
        }
    }

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

        _queryText = queryText;
        var beforeCompilation = GC.GetTotalAllocatedBytes(true);
        var compilationTimer = System.Diagnostics.Stopwatch.StartNew();
        _query = InstanceCreatorHelpers.CompileForExecution(
            queryText,
            $"JsonBenchmark_{QueryShape}_{Guid.NewGuid():N}",
            new JsonBenchmarkSchemaProvider(TypedSource),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

        CompilationMilliseconds = compilationTimer.Elapsed.TotalMilliseconds;
        CompilationAllocatedBytes = GC.GetTotalAllocatedBytes(true) - beforeCompilation;
        _ = RunCompiledQuery();
    }

    [Benchmark]
    public long RunCompiledQuery()
    {
        using var table = _query.Run();
        long checksum = table.Count;

        foreach (var row in table)
        {
            var visitor = new ConsumptionVisitor();
            row.VisitColumns(ref visitor);
            checksum = unchecked(checksum * 31 + visitor.Count);
        }

        return checksum;
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/').Replace("'", "''", StringComparison.Ordinal);
    }

    public string InspectGeneratedExecution() => InstanceCreator.CompileForInspection(_queryText,
        $"JsonBenchmarkInspection_{Guid.NewGuid():N}", new JsonBenchmarkSchemaProvider(TypedSource),
        new NullLoggerResolver(), new CompilationOptions(ParallelizationMode.Full, usePrimitiveTypeValidation: true))
        .GeneratedCSharpCode;

    public static string InspectNativeQuery(string query) => InstanceCreator.CompileForInspection(query,
        $"JsonNativeInspection_{Guid.NewGuid():N}", new JsonBenchmarkSchemaProvider(true),
        new NullLoggerResolver(), new CompilationOptions(ParallelizationMode.Full, usePrimitiveTypeValidation: true))
        .GeneratedCSharpCode;

    private sealed class NullLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;
        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }

    private struct ConsumptionVisitor : IColumnValueVisitor
    {
        public int Count;

        public void Visit<T>(int columnIndex, in T value) => Count++;
    }

    private sealed class JsonBenchmarkSchemaProvider(bool typedSource) : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            if (!typedSource)
                throw new NotSupportedException("Legacy JSON transfer is removed; benchmark the native source.");
            return new JsonSchema();
        }
    }

}
