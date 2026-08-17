using System.Buffers;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesNvmePipelineBenchmarks
{
    private readonly byte[] _buffer = new byte[4 * 1024 * 1024];
    private SourceExecutionContext _context = null!;
    private SourceExecutionContext _predicateContext = null!;
    private string _path = null!;

    [Params(8, 16)]
    public int SizeGiB { get; set; }

    [Params(0, 1, 4, 8, 16)]
    public int WorkerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = SeparatedValuesBenchmarkData.EnsureNvmeFile(SizeGiB);
        SeparatedValuesStructuralSummaryCache.Clear();
        var settings = new Dictionary<string, string>();
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [],
            SourceRuntimeSettings = settings,
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var schema = new SeparatedValuesSchema();
        var metadata = new SourceMetadataContext(
            "nvme-benchmark",
            CancellationToken.None,
            [
                new SchemaColumn("Column1", 0, typeof(string)),
                new SchemaColumn("Column2", 1, typeof(decimal))
            ],
            settings,
            new Mock<ILogger>().Object);
        _ = schema.DescribeSource(
            "semicolon",
            new SourceDescribeContext(SourceIdentity.Empty, metadata),
            _path,
            false,
            0);
        var plan = schema
            .TryPlanSource("semicolon", request, _path, false, 0)
            .ExecutionPlan;
        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [],
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = WorkerCount.ToString()
            },
            executionPlan: plan);

        var predicateRequest = request with
        {
            Predicate = new SourcePredicateComparison(
                SourcePredicateComparisonOperator.GreaterThan,
                new SourcePredicateColumn(new SourceColumnRef("Column2")),
                new SourcePredicateLiteral(1000m))
        };
        var predicateSchema = new SeparatedValuesSchema();
        _ = predicateSchema.DescribeSource(
            "semicolon",
            new SourceDescribeContext(SourceIdentity.Empty, metadata),
            _path,
            false,
            0);
        var predicatePlan = predicateSchema
            .TryPlanSource("semicolon", predicateRequest, _path, false, 0)
            .ExecutionPlan;
        _predicateContext = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [],
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = WorkerCount.ToString()
            },
            executionPlan: predicatePlan);
    }

    [Benchmark]
    public long RawMultiRead()
    {
        return RawMultiReadAsync().GetAwaiter().GetResult();
    }

    [Benchmark]
    public long RawNvmeMultiRead()
    {
        return OperatingSystem.IsWindows()
            ? WindowsUnbufferedMultiReader.Read(
                _path,
                Math.Clamp(Environment.ProcessorCount / 2, 4, 16))
            : RawMultiRead();
    }

    [Benchmark(Baseline = true)]
    public long RawSequentialRead()
    {
        using var stream = new FileStream(
            _path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            _buffer.Length,
            FileOptions.SequentialScan);
        long checksum = 0;
        int read;
        while ((read = stream.Read(_buffer)) != 0)
            checksum = unchecked(checksum + read + _buffer[read - 1]);
        return checksum;
    }

    [Benchmark]
    public long ZeroColumnPipeline()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _context,
            _path,
            false,
            0);
        long rows = 0;
        foreach (var chunk in source.Chunks)
            rows += chunk.Count;
        return rows;
    }

    [Benchmark]
    public long RejectedNumericPredicatePipeline()
    {
        var source = new SeparatedValuesSchema().GetRowSource<object?[]>(
            "semicolon",
            _predicateContext,
            _path,
            false,
            0);
        long rows = 0;
        foreach (var chunk in source.Chunks)
            rows += chunk.Count;
        return rows;
    }

    private async Task<long> RawMultiReadAsync()
    {
        const int blockSize = 4 * 1024 * 1024;
        var concurrency = Math.Clamp(Environment.ProcessorCount / 2, 4, 16);
        using var handle = File.OpenHandle(
            _path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        var length = RandomAccess.GetLength(handle);
        var lanes = new Task<long>[concurrency];
        for (var lane = 0; lane < lanes.Length; lane++)
            lanes[lane] = ReadLaneAsync(lane);
        var checksums = await Task.WhenAll(lanes).ConfigureAwait(false);
        return checksums.Sum();

        async Task<long> ReadLaneAsync(int lane)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(blockSize);
            try
            {
                long checksum = 0;
                for (var offset = lane * (long)blockSize;
                     offset < length;
                     offset += concurrency * (long)blockSize)
                {
                    var requested = (int)Math.Min(blockSize, length - offset);
                    var total = 0;
                    while (total < requested)
                    {
                        var read = await RandomAccess.ReadAsync(
                                handle,
                                buffer.AsMemory(total, requested - total),
                                offset + total)
                            .ConfigureAwait(false);
                        if (read == 0)
                            throw new EndOfStreamException("Raw NVMe ceiling read ended before the expected file length.");
                        total += read;
                    }

                    checksum = unchecked(checksum + total + buffer[total - 1]);
                }

                return checksum;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
