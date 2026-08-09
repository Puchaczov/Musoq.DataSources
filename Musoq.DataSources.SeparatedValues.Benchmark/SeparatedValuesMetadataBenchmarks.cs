using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesMetadataBenchmarks
{
    private SourceMetadataContext _metadataContext = null!;
    private string _path = null!;

    [Params(100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = SeparatedValuesBenchmarkData.EnsureOneBrcFileWithHeader(RowCount);
        _metadataContext = new SourceMetadataContext(
            "benchmark",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            new Mock<ILogger>().Object);
        _ = SeparatedValuesSchemaDiscovery.GetSnapshot(_path, ";", true, 0);
    }

    [Benchmark(Baseline = true)]
    public int CachedDiscovery()
    {
        var table = new SeparatedValuesSchema().GetTableByName("semicolon", _metadataContext, _path, true, 0);
        return table.Columns.Length;
    }

    [Benchmark]
    public int ColdDiscovery()
    {
        SeparatedValuesSchemaDiscovery.ClearCache();
        var table = new SeparatedValuesSchema().GetTableByName("semicolon", _metadataContext, _path, true, 0);
        return table.Columns.Length;
    }
}
