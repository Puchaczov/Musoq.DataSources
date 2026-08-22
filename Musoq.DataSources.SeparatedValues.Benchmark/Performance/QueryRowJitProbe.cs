namespace Musoq.DataSources.SeparatedValues.Benchmark.Performance;

internal static class QueryRowJitProbe
{
    public static int Run(TextWriter output)
    {
        var benchmark = new SeparatedValuesQueryScopedSourceMaterializationBenchmarks { FieldCount = 8 };
        try
        {
            benchmark.Setup();
            var checksum = 0L;
            for (var iteration = 0; iteration < 256; iteration++)
                checksum ^= benchmark.QueryScopedNumericStructRows();
            output.WriteLine($"Query-row JIT probe checksum: {checksum}.");
            return 0;
        }
        finally
        {
            benchmark.Cleanup();
        }
    }
}
