using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonMetadataBenchmarks
{
    private string _dataPath = null!;
    private SourceMetadataContext _metadataContext = null!;

    [Params(100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dataPath = JsonBenchmarkData.EnsureFlatFile(RowCount);
        _metadataContext = new SourceMetadataContext(
            "benchmark",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            new Mock<ILogger>().Object);
        _ = JsonSchemaDiscovery.GetSnapshot(_dataPath);
    }

    [Benchmark(Baseline = true)]
    public int CachedDiscovery()
    {
        var table = new JsonSchema().GetTableByName("file", _metadataContext, _dataPath);
        return table.Columns.Length;
    }

    [Benchmark]
    public int ColdDiscovery()
    {
        JsonSchemaDiscovery.ClearCache();
        var table = new JsonSchema().GetTableByName("file", _metadataContext, _dataPath);
        return table.Columns.Length;
    }
}
