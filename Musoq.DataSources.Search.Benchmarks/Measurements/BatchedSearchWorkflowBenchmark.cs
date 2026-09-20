#nullable enable

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Many;
using Musoq.DataSources.Search.Components.Traversal;

using Musoq.DataSources.Search.Benchmarks.Comparisons;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public sealed record BatchedSearchCohort(
    string Id,
    string Description,
    bool Dense);

public sealed record BatchedSearchAutomatonSnapshot(
    int PatternCount,
    int NodeCount,
    int TransitionCount,
    int OutputReferenceCount,
    long ConstructionAllocatedBytes);

public sealed record BatchedSearchRun(
    string Id,
    SearchSpikeResult Result,
    double EndToEndMilliseconds,
    double? ScanMilliseconds,
    long AllocatedBytes,
    long ScannedBytes,
    long FilesConsidered,
    long CandidatesYielded,
    long ContentOpenAttempts)
{
    public bool Complete => Result.ExitCode is null;

    public string TerminalState => Complete ? "complete" : "failed";
}

public sealed class BatchedSearchCorpus : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private BatchedSearchCorpus(
        string root,
        BatchedSearchCohort cohort,
        IReadOnlyList<SearchSpikePattern> allPatterns,
        IReadOnlyList<SearchSpikeMatch> expectedMatches,
        string fixtureDigest,
        long eligibleBytes,
        int fileCount)
    {
        Root = root;
        Cohort = cohort;
        AllPatterns = allPatterns;
        ExpectedMatches = expectedMatches;
        FixtureDigest = fixtureDigest;
        EligibleBytes = eligibleBytes;
        FileCount = fileCount;
    }

    public string Root { get; }

    public BatchedSearchCohort Cohort { get; }

    public IReadOnlyList<SearchSpikePattern> AllPatterns { get; }

    public IReadOnlyList<SearchSpikeMatch> ExpectedMatches { get; }

    public string FixtureDigest { get; }

    public long EligibleBytes { get; }

    public int FileCount { get; }

    public static BatchedSearchCorpus Create(BatchedSearchCohort cohort)
    {
        ArgumentNullException.ThrowIfNull(cohort);

        const int fileCount = 4;
        const int linesPerFile = 32;
        var allPatterns = BatchedSearchWorkflow.CreatePatterns(100);
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-batched-{cohort.Id}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var expected = new List<SearchSpikeMatch>();
            long eligibleBytes = 0;
            for (var fileIndex = 0; fileIndex < fileCount; fileIndex++)
            {
                var relativePath = fileIndex % 2 == 0
                    ? $"nested/batched-{fileIndex:00}.txt"
                    : $"batched-{fileIndex:00}.txt";
                var lines = new string[linesPerFile];
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var hasHits = cohort.Dense || lineIndex == 0;
                    lines[lineIndex] = hasHits
                        ? CreateHitLine(fileIndex, lineIndex, allPatterns)
                        : CreateOrdinaryLine(fileIndex, lineIndex);
                    if (hasHits)
                    {
                        AppendExpected(
                            expected,
                            relativePath,
                            lineIndex + 1,
                            lines[lineIndex],
                            allPatterns);
                    }
                }

                var path = Path.Combine(
                    root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Join('\n', lines) + '\n', Utf8);
                eligibleBytes = checked(eligibleBytes + new FileInfo(path).Length);
            }

            var orderedExpected = expected
                .OrderBy(static match => match.RelativePath, StringComparer.Ordinal)
                .ThenBy(static match => match.LineNumber)
                .ThenBy(static match => match.Utf16Column)
                .ThenBy(static match => match.PatternId, StringComparer.Ordinal)
                .ToArray();
            return new BatchedSearchCorpus(
                root,
                cohort,
                allPatterns,
                orderedExpected,
                ComputeFixtureDigest(root),
                eligibleBytes,
                fileCount);
        }
        catch
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            throw;
        }
    }

    public IReadOnlyList<SearchSpikeMatch> GetExpectedMatches(
        IReadOnlyList<SearchSpikePattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var selectedIds = patterns
            .Select(static pattern => pattern.Id)
            .ToHashSet(StringComparer.Ordinal);
        return ExpectedMatches
            .Where(match => selectedIds.Contains(match.PatternId))
            .ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    private static string CreateHitLine(
        int fileIndex,
        int lineIndex,
        IReadOnlyList<SearchSpikePattern> patterns)
    {
        var builder = new StringBuilder()
            .Append("row ")
            .Append(fileIndex.ToString("D2"))
            .Append('-')
            .Append(lineIndex.ToString("D2"))
            .Append(" 😀");
        foreach (var pattern in patterns)
            builder.Append(' ').Append(pattern.Literal);
        return builder.ToString();
    }

    private static string CreateOrdinaryLine(int fileIndex, int lineIndex)
    {
        return $"ordinary-{fileIndex:00}-{lineIndex:00} payload without requested markers 😀";
    }

    private static void AppendExpected(
        ICollection<SearchSpikeMatch> expected,
        string relativePath,
        long lineNumber,
        string line,
        IReadOnlyList<SearchSpikePattern> patterns)
    {
        foreach (var pattern in patterns)
        {
            var searchStart = 0;
            while (searchStart < line.Length)
            {
                var matchOffset = line.IndexOf(
                    pattern.Literal,
                    searchStart,
                    StringComparison.Ordinal);
                if (matchOffset < 0)
                    break;

                expected.Add(new SearchSpikeMatch(
                    relativePath,
                    pattern.Id,
                    lineNumber,
                    matchOffset,
                    pattern.Literal));
                searchStart = matchOffset + pattern.Literal.Length;
            }
        }
    }

    private static string ComputeFixtureDigest(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory
                     .EnumerateFiles(root, "*.txt", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(root, path)
                .Replace(Path.DirectorySeparatorChar, '/');
            hash.AppendData(Utf8.GetBytes(relativePath));
            hash.AppendData([0]);
            hash.AppendData(File.ReadAllBytes(path));
            hash.AppendData([0]);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}

public static class BatchedSearchWorkflow
{
    public const string ResultUnit = "labeled_occurrence";

    public static IReadOnlyList<int> PatternCounts { get; } = [1, 10, 100];

    public static IReadOnlyList<BatchedSearchCohort> Cohorts { get; } =
    [
        new BatchedSearchCohort(
            "sparse",
            "Four hit lines across four files; every selected literal is present on each hit line.",
            Dense: false),
        new BatchedSearchCohort(
            "dense",
            "Every physical line contains every selected literal.",
            Dense: true)
    ];

    public static IReadOnlyList<SearchSpikePattern> CreatePatterns(int count)
    {
        if (!PatternCounts.Contains(count))
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "The registered batched benchmark counts are 1, 10 and 100.");

        return Enumerable.Range(0, count)
            .Select(index => new SearchSpikePattern(
                $"pattern-{index:000}",
                $"needle-{index:000}"))
            .ToArray();
    }

    public static BatchedSearchAutomatonSnapshot MeasureAutomaton(
        IReadOnlyList<SearchSpikePattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        var request = CreateRequest(patterns);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var matcher = new SearchManyLiteralMatcher(request);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return new BatchedSearchAutomatonSnapshot(
            patterns.Count,
            matcher.AutomatonNodeCount,
            matcher.AutomatonTransitionCount,
            matcher.AutomatonOutputReferenceCount,
            allocatedBytes);
    }

    public static BatchedSearchRun RunManaged(
        BatchedSearchCorpus corpus,
        IReadOnlyList<SearchSpikePattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(patterns);
        var request = CreateRequest(patterns);
        var patternById = patterns.ToDictionary(
            static pattern => pattern.Id,
            StringComparer.Ordinal);
        var matches = new List<SearchSpikeMatch>();
        var counters = new SearchScopeCounters();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var totalStart = Stopwatch.GetTimestamp();
        var scanStart = Stopwatch.GetTimestamp();

        SearchManyLiteralScan.ScanScope(
            corpus.Root,
            request,
            CancellationToken.None,
            (relativePath, span) =>
            {
                var patternId = span.PatternId ??
                    throw new InvalidOperationException(
                        "The batched matcher emitted an unlabeled span.");
                if (!patternById.TryGetValue(patternId, out var pattern))
                    throw new InvalidOperationException(
                        $"The batched matcher emitted unknown pattern '{patternId}'.");
                matches.Add(new SearchSpikeMatch(
                    relativePath,
                    patternId,
                    span.LineNumber,
                    span.Utf16Column,
                    span.MatchText ?? pattern.Literal));
            },
            readerFactory: null,
            counters: counters);

        var scanMilliseconds = Stopwatch.GetElapsedTime(scanStart).TotalMilliseconds;
        var result = new SearchSpikeResult(
            matches,
            checked((int)counters.CandidatesYielded),
            corpus.EligibleBytes,
            processInvocations: 1,
            stageTimings: new SearchSpikeStageTimings(
                TraversalMilliseconds: 0,
                ReadDecodeMilliseconds: 0,
                MatchingMilliseconds: scanMilliseconds,
                MaterializationMilliseconds: 0,
                BridgeMilliseconds: 0));
        var totalMilliseconds = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
        return new BatchedSearchRun(
            "search-backend",
            result,
            totalMilliseconds,
            scanMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
            corpus.EligibleBytes,
            counters.FilesConsidered,
            counters.CandidatesYielded,
            counters.ContentOpenAttempts);
    }

    internal static SearchManyRequest CreateRequest(
        IReadOnlyList<SearchSpikePattern> patterns)
    {
        return new SearchManyRequest(
            patterns.Select(static pattern => new SearchManyPattern(
                pattern.Id,
                pattern.Literal,
                SearchPatternMode.Literal)));
    }
}
