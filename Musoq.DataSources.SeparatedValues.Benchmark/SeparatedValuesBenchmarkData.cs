using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

internal static class SeparatedValuesBenchmarkData
{
    private const int DefaultStationCount = 413;
    private const int DefaultWideColumnCount = 48;

    public static string EnsureOneBrcFile(int rowCount)
    {
        var path = GetFixturePath("one-brc", rowCount);

        if (!File.Exists(path))
            GenerateOneBrcFile(path, rowCount, false);

        return path;
    }

    public static string EnsureOneBrcFileWithHeader(int rowCount)
    {
        var path = GetFixturePath("one-brc-header", rowCount);

        if (!File.Exists(path))
            GenerateOneBrcFile(path, rowCount, true);

        return path;
    }

    public static string EnsureWideFile(int rowCount)
    {
        var path = GetFixturePath($"wide-{DefaultWideColumnCount}", rowCount);

        if (!File.Exists(path))
            GenerateWideFile(path, rowCount, DefaultWideColumnCount);

        return path;
    }

    public static string EnsureQuotedMultilineFile(int rowCount)
    {
        var path = GetFixturePath("quoted-multiline", rowCount);

        if (!File.Exists(path))
            GenerateQuotedMultilineFile(path, rowCount);

        return path;
    }

    public static string EnsureUniqueStringsFile(int rowCount)
    {
        var path = GetFixturePath("unique-strings", rowCount);

        if (!File.Exists(path))
            GenerateUniqueStringsFile(path, rowCount);

        return path;
    }

    public static long VerifyFixtures(int rowCount)
    {
        var fixtures = new[]
        {
            (Path: EnsureOneBrcFile(rowCount), Delimiter: ";", HasHeader: false, Width: 2),
            (Path: EnsureWideFile(rowCount), Delimiter: ",", HasHeader: true, Width: DefaultWideColumnCount),
            (Path: EnsureQuotedMultilineFile(rowCount), Delimiter: ",", HasHeader: true, Width: 4)
        };
        long checksum = 0;

        foreach (var fixture in fixtures)
        {
            using var stream = File.OpenRead(fixture.Path);
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024 * 1024);
            using var parser = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = fixture.Delimiter,
                HasHeaderRecord = fixture.HasHeader,
                BadDataFound = args => throw new InvalidDataException(args.RawRecord)
            });

            if (fixture.HasHeader && (!parser.Read() || parser.Count != fixture.Width))
                throw new InvalidDataException($"Fixture '{fixture.Path}' has an invalid header.");

            var rows = 0;
            while (parser.Read())
            {
                if (parser.Count != fixture.Width)
                    throw new InvalidDataException($"Fixture '{fixture.Path}' has an invalid row width.");
                rows++;
            }

            if (rows != rowCount)
                throw new InvalidDataException($"Fixture '{fixture.Path}' contains {rows} rows instead of {rowCount}.");

            checksum = checked(checksum + rows * 31L + fixture.Width);
        }

        return checksum;
    }

    public static void GenerateOneBrcFile(string path, long rowCount)
    {
        GenerateOneBrcFile(path, rowCount, false);
    }

    private static void GenerateOneBrcFile(string path, long rowCount, bool includeHeader)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        EnsureFreeSpace(path, checked(rowCount * 24L + (includeHeader ? 20L : 0L)));

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var stations = Enumerable.Range(0, DefaultStationCount)
            .Select(index => Encoding.UTF8.GetBytes($"station-{index:D3}"))
            .ToArray();

        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024,
            FileOptions.SequentialScan);
        using var stream = new BufferedStream(file, 4 * 1024 * 1024);
        Span<byte> number = stackalloc byte[32];

        if (includeHeader)
            stream.Write("Station;Temperature\n"u8);

        for (long row = 0; row < rowCount; row++)
        {
            stream.Write(stations[(int)(row % DefaultStationCount)]);
            stream.WriteByte((byte)';');

            var temperatureTenths = (int)(row % 199) - 99;
            if (!Utf8Formatter.TryFormat(temperatureTenths / 10m, number, out var written, new StandardFormat('F', 1)))
                throw new InvalidOperationException("Cannot format benchmark temperature.");

            stream.Write(number[..written]);
            stream.WriteByte((byte)'\n');
        }
    }

    private static void GenerateWideFile(string path, int rowCount, int columnCount)
    {
        EnsureFreeSpace(path, checked(rowCount * columnCount * 12L + columnCount * 12L));
        using var stream = CreateOutput(path);
        Span<byte> number = stackalloc byte[32];

        for (var column = 0; column < columnCount; column++)
        {
            if (column != 0)
                stream.WriteByte((byte)',');
            WriteAscii(stream, $"Value{column:D2}");
        }

        stream.WriteByte((byte)'\n');
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                if (column != 0)
                    stream.WriteByte((byte)',');
                if (!Utf8Formatter.TryFormat(row + column, number, out var written))
                    throw new InvalidOperationException("Cannot format benchmark value.");
                stream.Write(number[..written]);
            }

            stream.WriteByte((byte)'\n');
        }
    }

    private static void GenerateUniqueStringsFile(string path, int rowCount)
    {
        EnsureFreeSpace(path, checked(rowCount * 16L));
        using var stream = CreateOutput(path);
        Span<byte> number = stackalloc byte[32];

        for (var row = 0; row < rowCount; row++)
        {
            stream.Write("value-"u8);
            if (!Utf8Formatter.TryFormat(row, number, out var written, new StandardFormat('D', 8)))
                throw new InvalidOperationException("Cannot format unique benchmark value.");
            stream.Write(number[..written]);
            stream.WriteByte((byte)'\n');
        }
    }

    private static void GenerateQuotedMultilineFile(string path, int rowCount)
    {
        EnsureFreeSpace(path, checked(rowCount * 96L + 32L));
        using var stream = CreateOutput(path);
        stream.Write("Id,Name,Notes,Empty\r\n"u8);

        for (var row = 0; row < rowCount; row++)
        {
            WriteAscii(stream, row.ToString());
            stream.WriteByte((byte)',');
            WriteAscii(stream, $"\"station, {row % DefaultStationCount:D3}\"");
            stream.WriteByte((byte)',');
            WriteAscii(stream, $"\"line one {row}\r\nline \"\"two\"\"\"");
            stream.WriteByte((byte)',');
            if ((row & 1) == 0)
                stream.Write("\"\""u8);
            stream.Write("\r\n"u8);
        }
    }

    private static BufferedStream CreateOutput(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024,
            FileOptions.SequentialScan);
        return new BufferedStream(file, 4 * 1024 * 1024);
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes);
    }

    private static string GetFixturePath(string shape, int rowCount)
    {
        var root = Path.Combine(Path.GetTempPath(), "Musoq.DataSources.Benchmarks", "separated-values", "v1");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{shape}-{rowCount}.csv");
    }

    private static void EnsureFreeSpace(string path, long estimatedBytes)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
                   ?? throw new InvalidOperationException($"Cannot determine the drive for '{fullPath}'.");
        var requiredBytes = checked(estimatedBytes + estimatedBytes / 5);
        var availableBytes = new DriveInfo(root).AvailableFreeSpace;

        if (availableBytes < requiredBytes)
        {
            throw new IOException(
                $"Generating '{fullPath}' requires approximately {requiredBytes:N0} free bytes, " +
                $"but only {availableBytes:N0} bytes are available.");
        }
    }
}

internal static class SeparatedValuesBenchmarkDataCommand
{
    public static bool TryRun(IReadOnlyList<string> args, out int exitCode)
    {
        if (args.Count == 0 || !string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return false;
        }

        if (args.Count != 3 || !long.TryParse(args[1], out var rowCount) || rowCount < 0)
        {
            Console.Error.WriteLine("Usage: generate <row-count> <output-path>");
            exitCode = 2;
            return true;
        }

        SeparatedValuesBenchmarkData.GenerateOneBrcFile(args[2], rowCount);
        Console.WriteLine(Path.GetFullPath(args[2]));
        exitCode = 0;
        return true;
    }
}
