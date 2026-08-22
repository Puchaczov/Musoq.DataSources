#nullable enable

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Playground;

internal static class SeparatedValuesLargeProfile
{
    private const int ManifestVersion = 1;
    private const int ProjectedBlockRows = 8192;
    private const int WideBlockRows = 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static int Prepare(string directory, int sizeGiB)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeGiB);
        var targetBytes = checked(sizeGiB * 1024L * 1024L * 1024L);
        var manifestPath = PrepareCore(directory, targetBytes, sizeGiB);
        Console.WriteLine(manifestPath);
        Console.WriteLine("Fixture generation is a separate process by design. Run profile-large in a new process.");
        return 0;
    }

    public static int Run(string manifestPath, string shapeText, int workers, string cacheModeText)
    {
        var manifest = LoadManifest(manifestPath);
        if (!TryParseShape(shapeText, out var shape))
            return UsageError($"Unknown large-profile shape '{shapeText}'.");
        if (!TryParseCacheMode(cacheModeText, out var cacheMode))
            return UsageError($"Unknown cache mode '{cacheModeText}'.");
        if (cacheMode == LargeCacheMode.WindowsUnbufferedCeiling && shape != LargeProfileShape.RawCeiling)
            return UsageError("windows-unbuffered-ceiling is valid only with raw-ceiling.");
        if (shape == LargeProfileShape.RawCeiling && cacheMode != LargeCacheMode.WindowsUnbufferedCeiling)
            return UsageError("raw-ceiling requires windows-unbuffered-ceiling.");

        var operation = CreateOperation(manifestPath, manifest, shape, workers, cacheMode);
        if (cacheMode == LargeCacheMode.Warm)
            Warm(operation.Path);

        var measurement = Measure(
            shapeText,
            cacheModeText,
            workers,
            operation.Path,
            operation.Run);
        Console.WriteLine(JsonSerializer.Serialize(measurement, JsonOptions));
        return 0;
    }

    public static int Smoke()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musoq-separated-values-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var manifestPath = PrepareCore(directory, 8L * 1024L * 1024L, 0);
            foreach (var shape in Enum.GetValues<LargeProfileShape>())
            {
                if (shape == LargeProfileShape.RawCeiling)
                    continue;
                var operation = CreateOperation(
                    manifestPath,
                    LoadManifest(manifestPath),
                    shape,
                    1,
                    LargeCacheMode.BufferedUnprimed);
                var result = operation.Run();
                Console.WriteLine($"{ToText(shape)}: rows={result.Rows:N0}; checksum={result.Checksum}");
            }

            return 0;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string PrepareCore(string directory, long targetBytes, int sizeGiB)
    {
        var fullDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDirectory);
        EnsureFreeSpace(fullDirectory, checked(targetBytes * 2 + targetBytes / 4));

        var projectedPath = Path.Combine(fullDirectory, $"projected-{targetBytes}.csv");
        var widePath = Path.Combine(fullDirectory, $"wide100-{targetBytes}.csv");
        var projectedRows = WriteRepeatedFixture(
            projectedPath,
            "Id,Quantity,Price,LowCardinality,HighCardinality\n"u8,
            CreateProjectedBlock(),
            ProjectedBlockRows,
            targetBytes);
        var wideRows = WriteRepeatedFixture(
            widePath,
            CreateWideHeader(),
            CreateWideBlock(),
            WideBlockRows,
            targetBytes);

        var manifest = new LargeFixtureManifest(
            ManifestVersion,
            sizeGiB,
            targetBytes,
            DateTimeOffset.UtcNow,
            new LargeFixture("projected", Path.GetFileName(projectedPath), new FileInfo(projectedPath).Length, projectedRows, 5),
            new LargeFixture("wide100", Path.GetFileName(widePath), new FileInfo(widePath).Length, wideRows, 100));
        var manifestPath = Path.Combine(fullDirectory, $"separated-values-large-v{ManifestVersion}-{targetBytes}.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), new UTF8Encoding(false));
        return manifestPath;
    }

    private static ProfileOperation CreateOperation(
        string manifestPath,
        LargeFixtureManifest manifest,
        LargeProfileShape shape,
        int workers,
        LargeCacheMode cacheMode)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var fixture = shape == LargeProfileShape.ProjectedLateColumn100 ? manifest.Wide100 : manifest.Projected;
        var path = Path.Combine(root, fixture.RelativePath);
        ValidateFixture(path, fixture);

        if (shape == LargeProfileShape.RawCeiling)
        {
            if (cacheMode != LargeCacheMode.WindowsUnbufferedCeiling || !OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("The unbuffered raw ceiling is available only on Windows.");
            return new ProfileOperation(path, () =>
            {
                var result = WindowsUnbufferedCeiling.Read(path, Math.Max(1, workers));
                return new ProfileOperationResult(0, result.BytesRead, result.Checksum, null);
            });
        }

        var query = CompileProfileQuery(path, shape, workers);
        return new ProfileOperation(path, () => RunCompiled(query, fixture.Length));
    }

    private static CompiledQuery CompileProfileQuery(string path, LargeProfileShape shape, int workers)
    {
        var escapedPath = Path.GetFullPath(path).Replace('\\', '/').Replace("'", "''", StringComparison.Ordinal);
        var source = $"ProfileRows('{escapedPath}', true, 0)";
        var queryBody = shape switch
        {
            LargeProfileShape.ProjectedOneLong => $"select Id from {source}",
            LargeProfileShape.ProjectedTwoNumerics => $"select Quantity, Price from {source}",
            LargeProfileShape.ProjectedLateColumn100 => $"select Column100 from {source}",
            LargeProfileShape.ProjectedLowCardinalityString => $"select LowCardinality from {source}",
            LargeProfileShape.ProjectedHighCardinalityString => $"select HighCardinality from {source}",
            LargeProfileShape.ProjectedPredicateSameColumn =>
                $"select Price from {source} where Price > 5000",
            LargeProfileShape.RuntimeSum => $"select Sum(Price) from {source}",
            LargeProfileShape.RuntimeLowCardinalityGroupBy =>
                $"select LowCardinality, Sum(Price) from {source} group by LowCardinality",
            LargeProfileShape.RuntimeCountStar => $"select Count(*) from {source}",
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
        var tableDeclaration = shape == LargeProfileShape.ProjectedLateColumn100
            ? $"table ProfileFixture {{ {string.Join(", ", Enumerable.Range(1, 100).Select(index => $"Column{index:D3}: long"))} }};"
            : """
              table ProfileFixture {
                  Id: long,
                  Quantity: long,
                  Price: decimal,
                  LowCardinality: string,
                  HighCardinality: string
              };
              """;
        var queryText = $$"""
                          {{tableDeclaration}}
                          couple separatedvalues.comma with table ProfileFixture as ProfileRows;
                          {{queryBody}}
                          """;
        return InstanceCreatorHelpers.CompileForExecution(
            queryText,
            $"SeparatedValuesLarge_{shape}_{Guid.NewGuid():N}",
            new ProfileSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(
                new Dictionary<string, string>
                {
                    [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] =
                        workers.ToString(CultureInfo.InvariantCulture)
                }));
    }

    private static ProfileOperationResult RunCompiled(CompiledQuery query, long bytes)
    {
        var table = query.Run();
        long checksum = table.Count;
        foreach (var row in table)
        foreach (var value in row.Values)
            checksum = unchecked(checksum * 31 + (value?.GetHashCode() ?? 0));
        return new ProfileOperationResult(table.Count, bytes, checksum, null);
    }

    private static LargeProfileMeasurement Measure(
        string shape,
        string cacheMode,
        int workers,
        string path,
        Func<ProfileOperationResult> operation)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var sampler = new ResourceSampler();
        using var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var stopwatch = Stopwatch.StartNew();
        var result = operation();
        stopwatch.Stop();
        process.Refresh();
        var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, double.Epsilon);
        var allocated = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
        var cpu = process.TotalProcessorTime - cpuBefore;
        var emittedRows = Math.Max(1, result.Rows);
        return new LargeProfileMeasurement(
            shape,
            cacheMode,
            Path.GetFullPath(path),
            workers,
            result.BytesProcessed,
            result.Rows,
            stopwatch.Elapsed,
            result.FirstChunk,
            result.BytesProcessed / 1024d / 1024d / elapsedSeconds,
            result.Rows / elapsedSeconds,
            allocated,
            allocated / (double)emittedRows,
            GC.CollectionCount(0) - gen0,
            GC.CollectionCount(1) - gen1,
            GC.CollectionCount(2) - gen2,
            sampler.PeakHeapBytes,
            Math.Max(sampler.PeakWorkingSetBytes, process.WorkingSet64),
            cpu,
            cpu.TotalSeconds / elapsedSeconds,
            result.Checksum,
            cacheMode == "buffered-unprimed"
                ? "No explicit warm-up was performed; operating-system cache state is not guaranteed."
                : null);
    }

    private static void Warm(string path)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4 * 1024 * 1024);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length,
                FileOptions.SequentialScan);
            while (stream.Read(buffer) != 0)
            {
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static byte[] CreateProjectedBlock()
    {
        using var stream = new MemoryStream(1024 * 1024);
        Span<byte> formatted = stackalloc byte[32];
        for (var row = 0; row < ProjectedBlockRows; row++)
        {
            WriteNumber(stream, row, 'D', 12, formatted);
            stream.WriteByte((byte)',');
            WriteNumber(stream, row % 1000000, 'D', 6, formatted);
            stream.WriteByte((byte)',');
            WriteNumber(stream, row % 10000, 'D', 4, formatted);
            stream.Write(",group-"u8);
            WriteNumber(stream, row % 128, 'D', 3, formatted);
            stream.Write(",value-"u8);
            WriteNumber(stream, row, 'D', 12, formatted);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static byte[] CreateWideHeader()
    {
        using var stream = new MemoryStream();
        Span<byte> formatted = stackalloc byte[16];
        for (var column = 1; column <= 100; column++)
        {
            if (column > 1)
                stream.WriteByte((byte)',');
            stream.Write("Column"u8);
            WriteNumber(stream, column, 'D', 3, formatted);
        }

        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    private static byte[] CreateWideBlock()
    {
        using var stream = new MemoryStream(1024 * 1024);
        Span<byte> formatted = stackalloc byte[32];
        for (var row = 0; row < WideBlockRows; row++)
        {
            for (var column = 1; column <= 100; column++)
            {
                if (column > 1)
                    stream.WriteByte((byte)',');
                WriteNumber(stream, row + column, 'D', 6, formatted);
            }

            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static void WriteNumber(
        Stream stream,
        long value,
        char symbol,
        byte precision,
        Span<byte> buffer)
    {
        if (!Utf8Formatter.TryFormat(value, buffer, out var written, new StandardFormat(symbol, precision)))
            throw new InvalidOperationException("Cannot format a large-profile fixture value.");
        stream.Write(buffer[..written]);
    }

    private static long WriteRepeatedFixture(
        string path,
        ReadOnlySpan<byte> header,
        byte[] block,
        int blockRows,
        long targetBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4 * 1024 * 1024,
            FileOptions.SequentialScan);
        stream.Write(header);
        long rows = 0;
        while (stream.Position < targetBytes)
        {
            stream.Write(block);
            rows += blockRows;
        }

        return rows;
    }

    private static LargeFixtureManifest LoadManifest(string path)
    {
        var manifest = JsonSerializer.Deserialize<LargeFixtureManifest>(File.ReadAllText(path), JsonOptions)
                       ?? throw new InvalidDataException($"Large-profile manifest '{path}' is empty.");
        if (manifest.Version != ManifestVersion)
            throw new InvalidDataException(
                $"Large-profile manifest '{path}' has version {manifest.Version}; expected {ManifestVersion}.");
        return manifest;
    }

    private static void ValidateFixture(string path, LargeFixture fixture)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != fixture.Length)
            throw new InvalidDataException(
                $"Fixture '{path}' is missing or changed. Run prepare-large again in a separate process.");
    }

    private static void EnsureFreeSpace(string directory, long requiredBytes)
    {
        var root = Path.GetPathRoot(directory)
                   ?? throw new InvalidOperationException($"Cannot determine the drive for '{directory}'.");
        var available = new DriveInfo(root).AvailableFreeSpace;
        if (available < requiredBytes)
            throw new IOException(
                $"Large profiling requires approximately {requiredBytes:N0} free bytes; {available:N0} are available.");
    }

    private static bool TryParseShape(string text, out LargeProfileShape shape)
    {
        foreach (var candidate in Enum.GetValues<LargeProfileShape>())
        {
            if (!string.Equals(text, ToText(candidate), StringComparison.OrdinalIgnoreCase))
                continue;
            shape = candidate;
            return true;
        }

        shape = default;
        return false;
    }

    private static string ToText(LargeProfileShape shape)
    {
        return shape switch
        {
            LargeProfileShape.ProjectedOneLong => "projected-one-long",
            LargeProfileShape.ProjectedTwoNumerics => "projected-two-numerics",
            LargeProfileShape.ProjectedLateColumn100 => "projected-late-column-100",
            LargeProfileShape.ProjectedLowCardinalityString => "projected-low-cardinality-string",
            LargeProfileShape.ProjectedHighCardinalityString => "projected-high-cardinality-string",
            LargeProfileShape.ProjectedPredicateSameColumn => "projected-predicate-same-column",
            LargeProfileShape.RuntimeSum => "runtime-sum",
            LargeProfileShape.RuntimeLowCardinalityGroupBy => "runtime-low-cardinality-group-by",
            LargeProfileShape.RuntimeCountStar => "runtime-count-star",
            LargeProfileShape.RawCeiling => "raw-ceiling",
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
    }

    private static bool TryParseCacheMode(string text, out LargeCacheMode mode)
    {
        mode = text.ToLowerInvariant() switch
        {
            "buffered-unprimed" => LargeCacheMode.BufferedUnprimed,
            "warm" => LargeCacheMode.Warm,
            "windows-unbuffered-ceiling" => LargeCacheMode.WindowsUnbufferedCeiling,
            _ => (LargeCacheMode)(-1)
        };
        return Enum.IsDefined(mode);
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("Use --help for supported commands and values.");
        return 2;
    }

    private sealed class ProfileSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new SeparatedValuesSchema();
        }
    }

    private sealed class ResourceSampler : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _sampling;
        private long _peakHeapBytes;
        private long _peakWorkingSetBytes;

        public ResourceSampler()
        {
            _sampling = Task.Run(SampleAsync);
        }

        public long PeakHeapBytes => Volatile.Read(ref _peakHeapBytes);

        public long PeakWorkingSetBytes => Volatile.Read(ref _peakWorkingSetBytes);

        public void Dispose()
        {
            _stop.Cancel();
            try
            {
                _sampling.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            _stop.Dispose();
        }

        private async Task SampleAsync()
        {
            using var process = Process.GetCurrentProcess();
            while (!_stop.IsCancellationRequested)
            {
                process.Refresh();
                SetMaximum(ref _peakHeapBytes, GC.GetGCMemoryInfo().HeapSizeBytes);
                SetMaximum(ref _peakWorkingSetBytes, process.WorkingSet64);
                await Task.Delay(25, _stop.Token).ConfigureAwait(false);
            }
        }

        private static void SetMaximum(ref long target, long value)
        {
            var current = Volatile.Read(ref target);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }

    private enum LargeProfileShape
    {
        ProjectedOneLong,
        ProjectedTwoNumerics,
        ProjectedLateColumn100,
        ProjectedLowCardinalityString,
        ProjectedHighCardinalityString,
        ProjectedPredicateSameColumn,
        RuntimeSum,
        RuntimeLowCardinalityGroupBy,
        RuntimeCountStar,
        RawCeiling
    }

    private enum LargeCacheMode
    {
        BufferedUnprimed,
        Warm,
        WindowsUnbufferedCeiling
    }

    private sealed record LargeFixtureManifest(
        int Version,
        int SizeGiB,
        long TargetBytes,
        DateTimeOffset CreatedUtc,
        LargeFixture Projected,
        LargeFixture Wide100);

    private sealed record LargeFixture(
        string Shape,
        string RelativePath,
        long Length,
        long Rows,
        int Columns);

    private sealed record ProfileOperation(string Path, Func<ProfileOperationResult> Run);

    private readonly record struct ProfileOperationResult(
        long Rows,
        long BytesProcessed,
        long Checksum,
        TimeSpan? FirstChunk);

    private sealed record LargeProfileMeasurement(
        string Shape,
        string CacheMode,
        string Path,
        int Workers,
        long Bytes,
        long Rows,
        TimeSpan Elapsed,
        TimeSpan? FirstChunk,
        double MebibytesPerSecond,
        double RowsPerSecond,
        long AllocatedBytes,
        double AllocatedBytesPerRow,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections,
        long PeakManagedHeapBytes,
        long PeakWorkingSetBytes,
        TimeSpan CpuTime,
        double CpuSecondsPerWallSecond,
        long Checksum,
        string? CacheNote);
}
