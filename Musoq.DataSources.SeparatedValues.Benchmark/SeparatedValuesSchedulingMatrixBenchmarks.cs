using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

/// <summary>
/// Warm-cache scheduling matrix. Hardware qualification should run this class
/// against the target NVMe and compare medians for all three shapes before a
/// production default is changed.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesSchedulingMatrixBenchmarks
{
    private SourceExecutionContext _context = null!;
    private bool _hasHeader;
    private string _path = null!;
    private SeparatedValuesScanPipeline _pipeline = null!;
    private string _separator = null!;

    [Params("framing", "projected-numeric", "quoted-multiline")]
    public string Shape { get; set; } = "framing";

    [Params(1, 2, 4, 8)]
    public int BlockSizeMiB { get; set; }

    [Params(1, 2, 4, 8)]
    public int IoDepth { get; set; }

    [Params(true, false)]
    public bool YieldBeforeCpuWork { get; set; }

    [Params(1_000_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var declared = Shape == "quoted-multiline"
            ? new ISchemaColumn[]
            {
                new SchemaColumn("Id", 0, typeof(long)),
                new SchemaColumn("Name", 1, typeof(string)),
                new SchemaColumn("Notes", 2, typeof(string)),
                new SchemaColumn("Empty", 3, typeof(string))
            }
            :
            [
                new SchemaColumn("Column1", 0, typeof(string)),
                new SchemaColumn("Column2", 1, typeof(decimal))
            ];
        (_path, _separator, _hasHeader) = Shape == "quoted-multiline"
            ? (SeparatedValuesBenchmarkData.EnsureQuotedMultilineFile(RowCount), ",", true)
            : (SeparatedValuesBenchmarkData.EnsureOneBrcFile(RowCount), ";", false);
        SourceColumnRef[] required = Shape switch
        {
            "framing" => [],
            "projected-numeric" => [new SourceColumnRef("Column2")],
            _ => [new SourceColumnRef("Id")]
        };
        var settings = new Dictionary<string, string>
        {
            [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] =
                Math.Max(2, Environment.ProcessorCount - 1).ToString()
        };
        var tableName = _separator == ";" ? "semicolon" : "comma";
        var schema = new SeparatedValuesSchema();
        var metadata = new SourceMetadataContext(
            "scheduling-matrix",
            CancellationToken.None,
            declared,
            settings,
            NullLogger.Instance);
        _ = schema.DescribeSource(
            tableName,
            new SourceDescribeContext(SourceIdentity.Empty, metadata),
            _path,
            _hasHeader,
            0);
        var plan = schema.TryPlanSource(
                tableName,
                new SourcePlanRequest
                {
                    Identity = SourceIdentity.Empty,
                    RequiredColumns = required,
                    SourceRuntimeSettings = settings,
                    Predicate = null,
                    OrderBy = [],
                    Skip = null,
                    Take = null
                },
                _path,
                _hasHeader,
                0)
            .ExecutionPlan;
        var outputColumns = required.Select((column, index) =>
        {
            var sourceColumn = declared.Single(item => item.ColumnName == column.Name);
            return (ISchemaColumn)new SchemaColumn(column.Name, index, sourceColumn.ColumnType);
        }).ToArray();
        _context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: outputColumns,
            sourceRuntimeSettings: settings,
            executionPlan: plan);
        _pipeline = new SeparatedValuesScanPipeline(
            new SeparatedValuesParallelBlockScanPipeline(
                blockSize: BlockSizeMiB * 1024 * 1024,
                ioDepth: IoDepth,
                yieldBeforeCpuWork: YieldBeforeCpuWork),
            forceParallel: true);
        _ = Scan();
    }

    [Benchmark]
    public long Scan()
    {
        return Shape switch
        {
            "framing" => ScanZeroField(),
            "projected-numeric" => ScanDecimal(),
            _ => ScanLong()
        };
    }

    private long ScanZeroField()
    {
        var source = SeparatedValuesNativeBenchmarkSource.Create(
            _path, _separator, _hasHeader, _context, _pipeline);
        long rows = 0;
        long checksum = 17;
        foreach (var chunk in source.Chunks)
        {
            rows += chunk.Count;
            foreach (var _ in chunk)
                checksum = unchecked(checksum * 31);
        }

        return unchecked(checksum * 31 + rows);
    }

    private long ScanDecimal()
    {
        var source = SeparatedValuesNativeBenchmarkSource.Create<decimal>(
            _path, _separator, _hasHeader, _context, _pipeline);
        long rows = 0;
        long checksum = 17;
        foreach (var chunk in source.Chunks)
        {
            rows += chunk.Count;
            foreach (var row in chunk)
            {
                checksum = unchecked(checksum * 31 + 1);
                checksum = unchecked(checksum * 31 + row.Item0.GetHashCode());
            }
        }

        return unchecked(checksum * 31 + rows);
    }

    private long ScanLong()
    {
        var source = SeparatedValuesNativeBenchmarkSource.Create<long>(
            _path, _separator, _hasHeader, _context, _pipeline);
        long rows = 0;
        long checksum = 17;
        foreach (var chunk in source.Chunks)
        {
            rows += chunk.Count;
            foreach (var row in chunk)
            {
                checksum = unchecked(checksum * 31 + 1);
                checksum = unchecked(checksum * 31 + row.Item0.GetHashCode());
            }
        }

        return unchecked(checksum * 31 + rows);
    }
}

internal static class SeparatedValuesSchedulingMatrixRunner
{
    public static int RunCase(
        int rowCount,
        string shape,
        int blockSizeMiB,
        int ioDepth,
        bool yieldBeforeCpuWork)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        var benchmark = new SeparatedValuesSchedulingMatrixBenchmarks
        {
            Shape = shape,
            BlockSizeMiB = blockSizeMiB,
            IoDepth = ioDepth,
            YieldBeforeCpuWork = yieldBeforeCpuWork,
            RowCount = rowCount
        };
        benchmark.Setup();
        var samples = new double[3];
        long checksum = 0;
        for (var sample = 0; sample < samples.Length; sample++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            checksum = benchmark.Scan();
            stopwatch.Stop();
            samples[sample] = rowCount / stopwatch.Elapsed.TotalSeconds;
        }

        Array.Sort(samples);
        Console.WriteLine(
            $"block={blockSizeMiB}MiB io-depth={ioDepth} yield={yieldBeforeCpuWork} shape={shape} " +
            $"median-rows/s={samples[1]:F0} checksum={checksum}");
        return 0;
    }

    public static int Run(int rowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        var results = new List<MatrixResult>();
        foreach (var blockSize in new[] { 1, 2, 4, 8 })
        foreach (var ioDepth in new[] { 1, 2, 4, 8 })
        foreach (var yieldBeforeCpuWork in new[] { true, false })
        {
            var rates = new List<double>(3);
            long checksum = 0;
            foreach (var shape in new[] { "framing", "projected-numeric", "quoted-multiline" })
            {
                Console.WriteLine(
                    $"running block={blockSize}MiB io-depth={ioDepth} " +
                    $"yield={yieldBeforeCpuWork} shape={shape}");
                var benchmark = new SeparatedValuesSchedulingMatrixBenchmarks
                {
                    Shape = shape,
                    BlockSizeMiB = blockSize,
                    IoDepth = ioDepth,
                    YieldBeforeCpuWork = yieldBeforeCpuWork,
                    RowCount = rowCount
                };
                benchmark.Setup();
                var samples = new double[3];
                for (var sample = 0; sample < samples.Length; sample++)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    checksum = unchecked(checksum * 31 + benchmark.Scan());
                    stopwatch.Stop();
                    samples[sample] = rowCount / stopwatch.Elapsed.TotalSeconds;
                }

                Array.Sort(samples);
                rates.Add(samples[1]);
            }

            var geometricMean = Math.Exp(rates.Average(Math.Log));
            results.Add(new MatrixResult(blockSize, ioDepth, yieldBeforeCpuWork, geometricMean, checksum));
        }

        if (results.Select(result => result.Checksum).Distinct().Skip(1).Any())
            throw new InvalidDataException("Scheduling matrix configurations produced different ordered-row checksums.");

        foreach (var result in results.OrderByDescending(result => result.GeometricMeanRowsPerSecond))
        {
            Console.WriteLine(
                $"block={result.BlockSizeMiB}MiB io-depth={result.IoDepth} " +
                $"yield={result.YieldBeforeCpuWork,-5} geo-rows/s={result.GeometricMeanRowsPerSecond:F0} " +
                $"checksum={result.Checksum}");
        }

        var best = results.Max(result => result.GeometricMeanRowsPerSecond);
        var production = results.Single(result =>
            result.BlockSizeMiB == SeparatedValuesParallelBlockScanPipeline.DefaultBlockSize / (1024 * 1024) &&
            result.IoDepth == SeparatedValuesParallelBlockScanPipeline.DefaultIoDepth &&
            result.YieldBeforeCpuWork);
        Console.WriteLine(
            $"production-vs-best={production.GeometricMeanRowsPerSecond / best * 100:F1}% " +
            $"(warm-cache smoke only; not target-NVMe qualification)");
        return 0;
    }

    private readonly record struct MatrixResult(
        int BlockSizeMiB,
        int IoDepth,
        bool YieldBeforeCpuWork,
        double GeometricMeanRowsPerSecond,
        long Checksum);
}
