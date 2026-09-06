#nullable enable

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Musoq.DataSources.Search.Components.Bytes;

using Musoq.DataSources.Search.Benchmarks.Harness;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public static class SearchBytePipelineMeasurement
{
    public const int DefaultSeed = 0x5EA4C004;

    private const int MeasuredTrials = BenchmarkHarnessContract.MinimumMeasuredTrials;
    private const int FileSize = 2 * 1024 * 1024;

    private static readonly IReadOnlyList<Workload> Workloads =
    [
        new Workload(
            "exact-signature",
            "Exact CA FE signature without a byte window.",
            "{\"version\":1,\"bytes\":\"ca fe\"}",
            ParseRecords: false),
        new Workload(
            "masked-signature",
            "Masked CA ?? signature without a byte window.",
            "{\"version\":1,\"bytes\":\"ca ??\"}",
            ParseRecords: false),
        new Workload(
            "match-to-parse",
            "Masked CA ?? candidates with a bounded parse window.",
            "{\"version\":1,\"bytes\":\"ca ??\",\"window\":{\"beforeBytes\":0,\"afterBytes\":16}}",
            ParseRecords: true)
    ];

    public static SearchBytePipelineReport Run(int seed = DefaultSeed)
    {
        using var corpus = SearchBytePipelineCorpus.Create(seed);
        var cells = Workloads
            .Select(workload => MeasureCell(corpus, workload))
            .ToArray();

        return new SearchBytePipelineReport(
            "measure-byte-pipelines",
            "W13-S05",
            seed,
            MeasuredTrials,
            "One recorded warmup per workload; seven complete measured trials; measured durations are retained and the median is reported.",
            "Filesystem cache was not flushed or independently verified on this unprivileged Windows host; cache state is unknown, not cold.",
            "Stopwatch surrounds eligible file traversal, sequential read, raw-byte matching, optional bounded-window materialization, record interpretation, normalized result construction and SHA-256 output hashing.",
            "This is a structured binary observation. It does not compare byte occurrences or parsed records with an external text matcher.",
            new SearchBytePipelineFixtureSummary(
                corpus.FixtureDigest,
                corpus.Files.Count,
                corpus.EligibleBytes,
                FileSize,
                corpus.Files.Select(static file => file.RelativePath).ToArray(),
                "Two deterministic 2 MiB binary files with dense CA candidates, exact CA FE candidates, malformed records, valid records and a truncated tail candidate."),
            CreateCorrectness(corpus, cells),
            cells);
    }

    public static SearchBytePipelineCorrectness Verify(int seed = DefaultSeed)
    {
        using var corpus = SearchBytePipelineCorpus.Create(seed);
        var executions = new Dictionary<string, (SearchBytePipelineRun Run, ExpectedPipeline Expected)>(
            StringComparer.Ordinal);

        foreach (var workload in Workloads)
        {
            var pattern = SearchBytePatternParser.Parse(workload.PatternJson);
            var expected = corpus.GetExpected(pattern, workload.ParseRecords);
            var run = Execute(corpus, pattern, workload.ParseRecords);
            AssertExpected(workload, expected, run);
            executions.Add(workload.Id, (run, expected));
        }

        return CreateCorrectness(corpus, executions);
    }

    private static SearchBytePipelineCell MeasureCell(
        SearchBytePipelineCorpus corpus,
        Workload workload)
    {
        var pattern = SearchBytePatternParser.Parse(workload.PatternJson);
        var expected = corpus.GetExpected(pattern, workload.ParseRecords);
        var warmup = Execute(corpus, pattern, workload.ParseRecords);
        AssertExpected(workload, expected, warmup);

        var trials = new List<SearchBytePipelineTrial>(MeasuredTrials);
        for (var trial = 1; trial <= MeasuredTrials; trial++)
        {
            var run = Execute(corpus, pattern, workload.ParseRecords);
            AssertExpected(workload, expected, run);
            trials.Add(new SearchBytePipelineTrial(trial, run));
        }

        return new SearchBytePipelineCell(
            workload.Id,
            workload.Description,
            workload.PatternJson,
            workload.ParseRecords ? "parsed_record" : "byte_occurrence",
            expected.CandidateCount,
            expected.ParsedCount,
            expected.ParseFailures,
            expected.OutputHash,
            expected.OutputBytes,
            workload.ParseRecords,
            warmup,
            trials,
            Median(trials.Select(static trial => trial.Run.DurationMilliseconds)));
    }

    private static SearchBytePipelineCorrectness CreateCorrectness(
        SearchBytePipelineCorpus corpus,
        IReadOnlyList<SearchBytePipelineCell> cells)
    {
        var exact = cells.Single(static cell => cell.Id == "exact-signature");
        var masked = cells.Single(static cell => cell.Id == "masked-signature");
        var parsed = cells.Single(static cell => cell.Id == "match-to-parse");
        return new SearchBytePipelineCorrectness(
            true,
            corpus.FixtureDigest,
            corpus.Files.Count,
            corpus.EligibleBytes,
            exact.ExpectedCandidates,
            masked.ExpectedCandidates,
            parsed.ExpectedParsedRecords,
            parsed.ExpectedParseFailures,
            parsed.Warmup.WindowValues,
            parsed.Warmup.CompleteWindows,
            parsed.Warmup.IncompleteWindows,
            exact.Warmup.WindowValues == 0 && masked.Warmup.WindowValues == 0);
    }

    private static SearchBytePipelineCorrectness CreateCorrectness(
        SearchBytePipelineCorpus corpus,
        IReadOnlyDictionary<string, (SearchBytePipelineRun Run, ExpectedPipeline Expected)> executions)
    {
        var exact = executions["exact-signature"];
        var masked = executions["masked-signature"];
        var parsed = executions["match-to-parse"];
        return new SearchBytePipelineCorrectness(
            true,
            corpus.FixtureDigest,
            corpus.Files.Count,
            corpus.EligibleBytes,
            exact.Expected.CandidateCount,
            masked.Expected.CandidateCount,
            parsed.Expected.ParsedCount,
            parsed.Expected.ParseFailures,
            parsed.Run.WindowValues,
            parsed.Run.CompleteWindows,
            parsed.Run.IncompleteWindows,
            exact.Run.WindowValues == 0 && masked.Run.WindowValues == 0);
    }

    private static SearchBytePipelineRun Execute(
        SearchBytePipelineCorpus corpus,
        SearchBytePattern pattern,
        bool parseRecords)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var workingSetBefore = GetWorkingSet();
        var start = Stopwatch.GetTimestamp();
        var candidateCount = 0;
        var parsedCount = 0;
        var parseFailures = 0;
        var windowValues = 0;
        var completeWindows = 0;
        var incompleteWindows = 0;
        var rawRows = new List<RawRow>();
        var parsedRows = new List<ParsedRow>();

        foreach (var file in corpus.Files)
        {
            SearchByteScanner.ScanFile(
                file.FullPath,
                pattern,
                materializeMatchedBytes: true,
                materializeWindowBytes: parseRecords,
                (offset, matchedBytes, window) =>
                {
                    if (matchedBytes is null)
                        throw new InvalidOperationException("Byte benchmark did not materialize matched bytes.");

                    candidateCount = checked(candidateCount + 1);
                    if (window is not null)
                    {
                        windowValues = checked(windowValues + 1);
                        if (window.Complete)
                            completeWindows = checked(completeWindows + 1);
                        else
                            incompleteWindows = checked(incompleteWindows + 1);
                    }

                    if (!parseRecords)
                    {
                        rawRows.Add(new RawRow(
                            file.RelativePath,
                            offset,
                            ToHex(matchedBytes)));
                        return;
                    }

                    if (window?.Bytes is not null &&
                        TryInterpretRecord(window.Bytes, out var payloadLength, out var checksum))
                    {
                        parsedCount = checked(parsedCount + 1);
                        parsedRows.Add(new ParsedRow(
                            file.RelativePath,
                            offset,
                            payloadLength,
                            checksum));
                    }
                    else
                    {
                        parseFailures = checked(parseFailures + 1);
                    }
                },
                readerOpened: null,
                CancellationToken.None);
        }

        var outputLines = parseRecords
            ? parsedRows
                .OrderBy(static row => row.RelativePath, StringComparer.Ordinal)
                .ThenBy(static row => row.Offset)
                .Select(static row =>
                    $"{row.RelativePath}|{row.Offset}|{row.PayloadLength}|{row.Checksum}")
                .ToArray()
            : rawRows
                .OrderBy(static row => row.RelativePath, StringComparer.Ordinal)
                .ThenBy(static row => row.Offset)
                .Select(static row => $"{row.RelativePath}|{row.Offset}|{row.MatchedHex}")
                .ToArray();
        var serialized = string.Join('\n', outputLines);
        var duration = Stopwatch.GetElapsedTime(start);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var workingSetAfter = GetWorkingSet();
        return new SearchBytePipelineRun(
            duration.TotalMilliseconds,
            allocatedBytes,
            Math.Max(workingSetBefore, workingSetAfter),
            corpus.Files.Count,
            corpus.EligibleBytes,
            candidateCount,
            parsedCount,
            parseFailures,
            windowValues,
            completeWindows,
            incompleteWindows,
            Encoding.UTF8.GetByteCount(serialized),
            Hash(serialized),
            true,
            "complete");
    }

    private static void AssertExpected(
        Workload workload,
        ExpectedPipeline expected,
        SearchBytePipelineRun actual)
    {
        if (actual.CandidateCount != expected.CandidateCount ||
            actual.ParsedRecords != expected.ParsedCount ||
            actual.ParseFailures != expected.ParseFailures ||
            actual.OutputBytes != expected.OutputBytes ||
            !string.Equals(actual.OutputHash, expected.OutputHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{workload.Id} oracle mismatch: expected candidates={expected.CandidateCount}, parsed={expected.ParsedCount}, failures={expected.ParseFailures}, hash={expected.OutputHash}; " +
                $"actual candidates={actual.CandidateCount}, parsed={actual.ParsedRecords}, failures={actual.ParseFailures}, hash={actual.OutputHash}.");
        }

        if (!actual.Complete || !string.Equals(actual.TerminalState, "complete", StringComparison.Ordinal))
            throw new InvalidOperationException($"{workload.Id} did not complete.");
    }

    private static bool TryInterpretRecord(
        ReadOnlySpan<byte> bytes,
        out int payloadLength,
        out int checksum)
    {
        payloadLength = 0;
        checksum = 0;
        if (bytes.Length < 4 || bytes[0] != 0xCA || bytes[1] != 0xFE)
            return false;

        payloadLength = bytes[2];
        if (payloadLength is < 1 or > 8)
        {
            payloadLength = 0;
            return false;
        }

        var trailerIndex = checked(payloadLength + 3);
        if (trailerIndex >= bytes.Length || bytes[trailerIndex] != 0x7F)
        {
            payloadLength = 0;
            return false;
        }

        for (var index = 3; index < trailerIndex; index++)
            checksum = checked(checksum + bytes[index]);

        return true;
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Hash(string serialized)
    {
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(serialized)))
            .ToLowerInvariant();
    }

    private static long GetWorkingSet()
    {
        using var process = Process.GetCurrentProcess();
        return process.WorkingSet64;
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(static value => value).ToArray();
        if (ordered.Length == 0)
            return 0;

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private sealed record Workload(
        string Id,
        string Description,
        string PatternJson,
        bool ParseRecords);

    private sealed record RawRow(
        string RelativePath,
        long Offset,
        string MatchedHex);

    private sealed record ParsedRow(
        string RelativePath,
        long Offset,
        int PayloadLength,
        int Checksum);

    internal sealed record ExpectedPipeline(
        int CandidateCount,
        int ParsedCount,
        int ParseFailures,
        int CompleteWindows,
        int IncompleteWindows,
        int OutputBytes,
        string OutputHash);
}

public sealed record SearchBytePipelineReport(
    string Command,
    string ScopeId,
    int Seed,
    int TrialsPerWorkload,
    string WarmupPolicy,
    string CachePolicy,
    string TimingBoundary,
    string ComparisonBoundary,
    SearchBytePipelineFixtureSummary Fixture,
    SearchBytePipelineCorrectness Correctness,
    IReadOnlyList<SearchBytePipelineCell> Cells);

public sealed record SearchBytePipelineFixtureSummary(
    string FixtureDigest,
    int FileCount,
    long EligibleBytes,
    int LargeFileBytes,
    IReadOnlyList<string> Files,
    string Description);

public sealed record SearchBytePipelineCorrectness(
    bool Passed,
    string FixtureDigest,
    int FileCount,
    long EligibleBytes,
    int ExactCandidateCount,
    int MaskedCandidateCount,
    int ParsedRecordCount,
    int ParseFailureCount,
    int ParseWindowValues,
    int CompleteWindows,
    int IncompleteWindows,
    bool OptionalWindowOmitted);

public sealed record SearchBytePipelineCell(
    string Id,
    string Description,
    string PatternJson,
    string ResultUnit,
    int ExpectedCandidates,
    int ExpectedParsedRecords,
    int ExpectedParseFailures,
    string ExpectedOutputHash,
    int ExpectedOutputBytes,
    bool ParsesRecords,
    SearchBytePipelineRun Warmup,
    IReadOnlyList<SearchBytePipelineTrial> Trials,
    double MedianMilliseconds);

public sealed record SearchBytePipelineTrial(
    int Number,
    SearchBytePipelineRun Run);

public sealed record SearchBytePipelineRun(
    double DurationMilliseconds,
    long AllocatedBytes,
    long WorkingSetBytes,
    int FilesVisited,
    long BytesRead,
    int CandidateCount,
    int ParsedRecords,
    int ParseFailures,
    int WindowValues,
    int CompleteWindows,
    int IncompleteWindows,
    int OutputBytes,
    string OutputHash,
    bool Complete,
    string TerminalState);

public sealed record SearchBytePipelineFixtureFile(
    string RelativePath,
    string FullPath,
    long Length);

public sealed class SearchBytePipelineCorpus : IDisposable
{
    private const int FileSize = 2 * 1024 * 1024;
    private static readonly Encoding DigestEncoding = new UTF8Encoding(false);
    private readonly IReadOnlyDictionary<string, byte[]> _contents;

    private SearchBytePipelineCorpus(
        string root,
        IReadOnlyList<SearchBytePipelineFixtureFile> files,
        IReadOnlyDictionary<string, byte[]> contents,
        string fixtureDigest,
        long eligibleBytes)
    {
        Root = root;
        Files = files;
        _contents = contents;
        FixtureDigest = fixtureDigest;
        EligibleBytes = eligibleBytes;
    }

    public string Root { get; }

    public IReadOnlyList<SearchBytePipelineFixtureFile> Files { get; }

    public string FixtureDigest { get; }

    public long EligibleBytes { get; }

    public static SearchBytePipelineCorpus Create(int seed = SearchBytePipelineMeasurement.DefaultSeed)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-byte-pipeline-{seed:X8}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var contents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var files = new List<SearchBytePipelineFixtureFile>();
            for (var fileIndex = 0; fileIndex < 2; fileIndex++)
            {
                var relativePath = $"binary-{fileIndex:00}.bin";
                var bytes = CreateContent(seed, fileIndex, FileSize);
                var fullPath = Path.Combine(root, relativePath);
                File.WriteAllBytes(fullPath, bytes);
                contents.Add(relativePath, bytes);
                files.Add(new SearchBytePipelineFixtureFile(
                    relativePath,
                    fullPath,
                    bytes.LongLength));
            }

            files.Sort(static (left, right) =>
                StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
            return new SearchBytePipelineCorpus(
                root,
                files,
                contents,
                ComputeFixtureDigest(files, contents),
                files.Sum(static file => file.Length));
        }
        catch
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            throw;
        }
    }

    internal SearchBytePipelineMeasurement.ExpectedPipeline GetExpected(
        SearchBytePattern pattern,
        bool parseRecords)
    {
        var candidateCount = 0;
        var parsedCount = 0;
        var parseFailures = 0;
        var completeWindows = 0;
        var incompleteWindows = 0;
        var outputLines = new List<ExpectedLine>();

        foreach (var file in Files)
        {
            var bytes = _contents[file.RelativePath];
            var offsets = SearchBytePipelineOracle.FindNonOverlapping(
                bytes,
                pattern.Bytes.Span,
                pattern.Masks.Span);
            foreach (var offset in offsets)
            {
                candidateCount = checked(candidateCount + 1);
                if (!parseRecords)
                {
                    outputLines.Add(new ExpectedLine(
                        file.RelativePath,
                        offset,
                        $"{file.RelativePath}|{offset}|{Convert.ToHexString(bytes.AsSpan(offset, pattern.Length)).ToLowerInvariant()}"));
                    continue;
                }

                var requiredEnd = checked(offset + pattern.Length + pattern.Window.AfterBytes);
                var end = Math.Min(requiredEnd, bytes.Length);
                var window = bytes.AsSpan(offset, checked(end - offset));
                if (end == requiredEnd)
                    completeWindows = checked(completeWindows + 1);
                else
                    incompleteWindows = checked(incompleteWindows + 1);

                if (SearchBytePipelineOracle.TryParseRecord(
                        window,
                        out var payloadLength,
                        out var checksum))
                {
                    parsedCount = checked(parsedCount + 1);
                    outputLines.Add(new ExpectedLine(
                        file.RelativePath,
                        offset,
                        $"{file.RelativePath}|{offset}|{payloadLength}|{checksum}"));
                }
                else
                {
                    parseFailures = checked(parseFailures + 1);
                }
            }
        }

        var serialized = string.Join('\n', outputLines
            .OrderBy(static line => line.RelativePath, StringComparer.Ordinal)
            .ThenBy(static line => line.Offset)
            .Select(static line => line.Value));
        return new SearchBytePipelineMeasurement.ExpectedPipeline(
            candidateCount,
            parsedCount,
            parseFailures,
            completeWindows,
            incompleteWindows,
            Encoding.UTF8.GetByteCount(serialized),
            Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(serialized)))
                .ToLowerInvariant());
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    private sealed record ExpectedLine(
        string RelativePath,
        int Offset,
        string Value);

    private static byte[] CreateContent(int seed, int fileIndex, int length)
    {
        var bytes = new byte[length];
        uint state = unchecked((uint)seed) ^ (unchecked((uint)fileIndex) * 0x9E3779B9u);
        for (var index = 0; index < bytes.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            bytes[index] = (byte)(state >> 24);
        }

        for (var offset = 64; offset + 1 < bytes.Length; offset += 97)
            bytes[offset] = 0xCA;

        var recordIndex = 0;
        for (var offset = 128; offset + 20 < bytes.Length; offset += 257)
        {
            bytes[offset] = 0xCA;
            switch (recordIndex++ % 4)
            {
                case 0:
                case 3:
                {
                    var payloadLength = 1 + (recordIndex % 6);
                    bytes[offset + 1] = 0xFE;
                    bytes[offset + 2] = (byte)payloadLength;
                    for (var payloadIndex = 0; payloadIndex < payloadLength; payloadIndex++)
                        bytes[offset + 3 + payloadIndex] = (byte)(0x20 + payloadIndex + recordIndex);
                    bytes[offset + 3 + payloadLength] = 0x7F;
                    break;
                }
                case 1:
                    bytes[offset + 1] = 0x10;
                    bytes[offset + 2] = 0x02;
                    bytes[offset + 3] = 0xAA;
                    bytes[offset + 4] = 0xBB;
                    bytes[offset + 5] = 0x7F;
                    break;
                default:
                    bytes[offset + 1] = 0xFE;
                    bytes[offset + 2] = 0x0F;
                    bytes[offset + 3] = 0x01;
                    break;
            }
        }

        var truncatedOffset = bytes.Length - 5;
        bytes[truncatedOffset] = 0xCA;
        bytes[truncatedOffset + 1] = 0xFE;
        bytes[truncatedOffset + 2] = 0x08;
        bytes[truncatedOffset + 3] = 0x11;
        bytes[truncatedOffset + 4] = 0x22;
        return bytes;
    }

    private static string ComputeFixtureDigest(
        IReadOnlyList<SearchBytePipelineFixtureFile> files,
        IReadOnlyDictionary<string, byte[]> contents)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            hash.AppendData(DigestEncoding.GetBytes(file.RelativePath));
            hash.AppendData([0]);
            hash.AppendData(SHA256.HashData(contents[file.RelativePath]));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}

internal static class SearchBytePipelineOracle
{
    public static IReadOnlyList<int> FindNonOverlapping(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> pattern,
        ReadOnlySpan<byte> masks)
    {
        var offsets = new List<int>();
        var nextAllowed = 0;
        for (var offset = 0; offset <= input.Length - pattern.Length; offset++)
        {
            if (offset < nextAllowed || !Matches(input.Slice(offset, pattern.Length), pattern, masks))
                continue;

            offsets.Add(offset);
            nextAllowed = checked(offset + pattern.Length);
        }

        return offsets;
    }

    public static bool TryParseRecord(
        ReadOnlySpan<byte> bytes,
        out int payloadLength,
        out int checksum)
    {
        payloadLength = 0;
        checksum = 0;
        if (bytes.Length < 4 ||
            bytes[0] != 0xCA ||
            bytes[1] != 0xFE)
            return false;

        var declaredLength = bytes[2];
        if (declaredLength == 0 || declaredLength > 8)
            return false;

        var trailerIndex = declaredLength + 3;
        if (trailerIndex >= bytes.Length || bytes[trailerIndex] != 0x7F)
            return false;

        payloadLength = declaredLength;
        for (var index = 3; index < trailerIndex; index++)
            checksum = checked(checksum + bytes[index]);
        return true;
    }

    private static bool Matches(
        ReadOnlySpan<byte> candidate,
        ReadOnlySpan<byte> pattern,
        ReadOnlySpan<byte> masks)
    {
        for (var index = 0; index < pattern.Length; index++)
        {
            if ((candidate[index] & masks[index]) != (pattern[index] & masks[index]))
                return false;
        }

        return true;
    }
}
