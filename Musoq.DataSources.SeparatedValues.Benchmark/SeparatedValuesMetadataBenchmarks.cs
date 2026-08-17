using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesMetadataBenchmarks
{
    private SourceMetadataContext _declaredMetadataContext = null!;
    private SourceMetadataContext _dynamicMetadataContext = null!;
    private string _path = null!;

    [Params(100_000, 2_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = SeparatedValuesBenchmarkData.EnsureOneBrcFileWithHeader(RowCount);
        var settings = new Dictionary<string, string>();
        _dynamicMetadataContext = new SourceMetadataContext(
            "benchmark",
            CancellationToken.None,
            [],
            settings,
            new Mock<ILogger>().Object);
        _declaredMetadataContext = new SourceMetadataContext(
            "benchmark",
            CancellationToken.None,
            [
                new SchemaColumn("Station", 0, typeof(string)),
                new SchemaColumn("Temperature", 1, typeof(decimal))
            ],
            settings,
            new Mock<ILogger>().Object);
    }

    [Benchmark(Baseline = true)]
    public int DeclaredHeaderResolution()
    {
        var table = new SeparatedValuesSchema().GetTableByName(
            "semicolon",
            _declaredMetadataContext,
            _path,
            true,
            0);
        return table.Columns.Length;
    }

    [Benchmark]
    public int BoundedSampleResolution()
    {
        var table = new SeparatedValuesSchema().GetTableByName(
            "semicolon",
            _dynamicMetadataContext,
            _path,
            true,
            0);
        return table.Columns.Length;
    }
}
