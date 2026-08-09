using System.Text.Json;

namespace Musoq.DataSources.Json.Benchmarks;

internal static class JsonBenchmarkData
{
    private const int DefaultStationCount = 413;
    private const int DefaultWideColumnCount = 48;

    public static string EnsureFlatFile(int rowCount)
    {
        var path = GetFixturePath("flat", rowCount);

        if (!File.Exists(path))
            GenerateFlatFile(path, rowCount);

        return path;
    }

    public static string EnsureSparseFile(int rowCount)
    {
        var path = GetFixturePath("sparse", rowCount);

        if (!File.Exists(path))
            GenerateSparseFile(path, rowCount);

        return path;
    }

    public static string EnsureEvolvingFile(int rowCount)
    {
        var path = GetFixturePath("evolving", rowCount);

        if (!File.Exists(path))
            GenerateEvolvingFile(path, rowCount);

        return path;
    }

    public static string EnsureWideFile(int rowCount)
    {
        var path = GetFixturePath($"wide-{DefaultWideColumnCount}", rowCount);

        if (!File.Exists(path))
            GenerateWideFile(path, rowCount, DefaultWideColumnCount);

        return path;
    }

    public static string EnsureNestedFile(int rowCount)
    {
        var path = GetFixturePath("nested", rowCount);

        if (!File.Exists(path))
            GenerateNestedFile(path, rowCount);

        return path;
    }

    public static long VerifyFixtures(int rowCount)
    {
        var fixtures = new[]
        {
            EnsureFlatFile(rowCount),
            EnsureSparseFile(rowCount),
            EnsureEvolvingFile(rowCount),
            EnsureWideFile(rowCount),
            EnsureNestedFile(rowCount)
        };
        long checksum = 0;

        foreach (var path in fixtures)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() != rowCount)
                throw new InvalidDataException($"Fixture '{path}' does not contain {rowCount} rows.");

            checksum = checked(checksum + document.RootElement.GetArrayLength());
        }

        using var flat = JsonDocument.Parse(File.ReadAllBytes(fixtures[0]));
        var expectedSequence = (long)rowCount * (rowCount - 1) / 2;
        var actualSequence = flat.RootElement.EnumerateArray().Sum(row => row.GetProperty("Sequence").GetInt64());
        if (actualSequence != expectedSequence)
            throw new InvalidDataException("Flat JSON fixture checksum does not match its deterministic reference.");

        return checked(checksum + actualSequence);
    }

    public static void GenerateFlatFile(string path, long rowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        EnsureFreeSpace(path, checked(rowCount * 64L + 2L));

        using var stream = CreateOutput(path);
        using var writer = CreateWriter(stream);

        writer.WriteStartArray();
        for (long row = 0; row < rowCount; row++)
        {
            writer.WriteStartObject();
            writer.WriteString("Station", $"station-{row % DefaultStationCount:D3}");
            writer.WriteNumber("Temperature", (row % 199 - 99) / 10m);
            writer.WriteNumber("Sequence", row);
            writer.WriteEndObject();

            if ((row & 0x3fff) == 0)
                writer.Flush();
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static void GenerateSparseFile(string path, int rowCount)
    {
        EnsureFreeSpace(path, checked(rowCount * 56L + 2L));
        using var stream = CreateOutput(path);
        using var writer = CreateWriter(stream);

        writer.WriteStartArray();
        for (var row = 0; row < rowCount; row++)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Id", row);
            if ((row & 1) == 0)
                writer.WriteString("Even", $"even-{row % 97:D2}");
            if (row % 3 == 0)
                writer.WriteNumber("Third", row / 3m);
            if (row % 5 == 0)
                writer.WriteBoolean("Fifth", true);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static void GenerateEvolvingFile(string path, int rowCount)
    {
        EnsureFreeSpace(path, checked(rowCount * 56L + 2L));
        using var stream = CreateOutput(path);
        using var writer = CreateWriter(stream);

        writer.WriteStartArray();
        for (var row = 0; row < rowCount; row++)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Id", row);
            writer.WriteString("Name", $"row-{row:D8}");
            if (row >= rowCount / 2)
                writer.WriteNumber("LateDecimal", row / 10m);
            if (row >= rowCount * 9 / 10)
                writer.WriteString("VeryLate", "present");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static void GenerateWideFile(string path, int rowCount, int columnCount)
    {
        EnsureFreeSpace(path, checked(rowCount * columnCount * 16L + 2L));
        using var stream = CreateOutput(path);
        using var writer = CreateWriter(stream);

        writer.WriteStartArray();
        for (var row = 0; row < rowCount; row++)
        {
            writer.WriteStartObject();
            for (var column = 0; column < columnCount; column++)
                writer.WriteNumber($"Value{column:D2}", row + column);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static void GenerateNestedFile(string path, int rowCount)
    {
        EnsureFreeSpace(path, checked(rowCount * 160L + 2L));
        using var stream = CreateOutput(path);
        using var writer = CreateWriter(stream);

        writer.WriteStartArray();
        for (var row = 0; row < rowCount; row++)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Id", row);
            writer.WriteStartObject("Payload");
            writer.WriteString("Name", $"nested-{row % 251:D3}");
            writer.WriteStartArray("Values");
            writer.WriteNumberValue(row);
            writer.WriteNumberValue(row + 1);
            writer.WriteNumberValue(row + 2);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteStartArray("Tags");
            writer.WriteStringValue("benchmark");
            writer.WriteStringValue($"tag-{row % 17:D2}");
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static FileStream CreateOutput(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024,
            FileOptions.SequentialScan);
    }

    private static Utf8JsonWriter CreateWriter(Stream stream)
    {
        return new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        });
    }

    private static string GetFixturePath(string shape, int rowCount)
    {
        var root = Path.Combine(Path.GetTempPath(), "Musoq.DataSources.Benchmarks", "json", "v2");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{shape}-{rowCount}.json");
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

internal static class JsonBenchmarkDataCommand
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

        JsonBenchmarkData.GenerateFlatFile(args[2], rowCount);
        Console.WriteLine(Path.GetFullPath(args[2]));
        exitCode = 0;
        return true;
    }
}
