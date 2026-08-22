using BenchmarkDotNet.Attributes;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

public enum SeparatedValuesQueryShape
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
public class SeparatedValuesCompiledExecutionBenchmarks
{
    private CompiledQuery _query = null!;

    [Params(100_000)]
    public int RowCount { get; set; }

    [ParamsSource(nameof(QueryShapes))]
    public SeparatedValuesQueryShape QueryShape { get; set; }

    public IEnumerable<SeparatedValuesQueryShape> QueryShapes => Enum.GetValues<SeparatedValuesQueryShape>();

    [GlobalSetup]
    public void Setup()
    {
        var path = QueryPath(SeparatedValuesBenchmarkData.EnsureOneBrcFileWithHeader(RowCount));
        const string prefix = """
                              table Measurements {
                                  Station: string,
                                  Temperature: decimal
                              };
                              couple separatedvalues.semicolon with table Measurements as MeasurementRows;
                              """;
        var source = $"MeasurementRows('{path}', true, 0)";
        var queryText = QueryShape switch
        {
            SeparatedValuesQueryShape.Count => $"{prefix} select Count(Station) from {source}",
            SeparatedValuesQueryShape.OneColumn => $"{prefix} select Station from {source}",
            SeparatedValuesQueryShape.FullRow => $"{prefix} select Station, Temperature from {source}",
            SeparatedValuesQueryShape.PredicateTenPercent =>
                $"{prefix} select Station, Temperature from {source} where Temperature >= 7.9",
            SeparatedValuesQueryShape.PredicateHalf =>
                $"{prefix} select Station, Temperature from {source} where Temperature >= 0.0",
            SeparatedValuesQueryShape.EarlyTake => $"{prefix} select Station from {source} take 100",
            SeparatedValuesQueryShape.GroupedAggregates =>
                $"{prefix} select Station, Min(Temperature), Max(Temperature), Avg(Temperature) from {source} group by Station",
            _ => throw new ArgumentOutOfRangeException()
        };

        _query = InstanceCreatorHelpers.CompileForExecution(
            queryText,
            $"SeparatedValuesBenchmark_{QueryShape}_{Guid.NewGuid():N}",
            new SeparatedValuesBenchmarkSchemaProvider(),
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

    private sealed class SeparatedValuesBenchmarkSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new SeparatedValuesSchema();
        }
    }
}
