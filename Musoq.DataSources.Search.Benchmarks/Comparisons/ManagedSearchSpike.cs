#nullable enable

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Musoq.DataSources.Search.Benchmarks.Comparisons;

public sealed record SearchSpikePattern
{
    public SearchSpikePattern(string id, string literal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrEmpty(literal);
        Id = id;
        Literal = literal;
    }

    public string Id { get; }

    public string Literal { get; }
}

public sealed record SearchSpikeMatch(
    string RelativePath,
    string PatternId,
    long LineNumber,
    long Utf16Column,
    string Text);

public sealed record SearchSpikeCohort(
    string Id,
    string Title,
    string Distribution);

public sealed record SearchSpikeStageTimings(
    double TraversalMilliseconds,
    double ReadDecodeMilliseconds,
    double MatchingMilliseconds,
    double MaterializationMilliseconds,
    double BridgeMilliseconds);

public sealed class SearchSpikeResult
{
    public SearchSpikeResult(
        IReadOnlyList<SearchSpikeMatch> matches,
        int filesVisited,
        long bytesRead,
        int processInvocations = 0,
        int? exitCode = null,
        long rawOutputBytes = 0,
        SearchSpikeStageTimings? stageTimings = null)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var materializationStart = Stopwatch.GetTimestamp();
        Matches = matches
            .OrderBy(static match => match.RelativePath, StringComparer.Ordinal)
            .ThenBy(static match => match.LineNumber)
            .ThenBy(static match => match.Utf16Column)
            .ThenBy(static match => match.PatternId, StringComparer.Ordinal)
            .ToArray();
        FilesVisited = filesVisited;
        BytesRead = bytesRead;
        ProcessInvocations = processInvocations;
        ExitCode = exitCode;
        RawOutputBytes = rawOutputBytes;

        var serialized = SearchSpikeResultFormatter.Serialize(Matches);
        OutputHash = SearchSpikeResultFormatter.Hash(serialized);
        OutputBytes = Encoding.UTF8.GetByteCount(serialized);
        StageTimings = stageTimings is null
            ? null
            : stageTimings with
            {
                MaterializationMilliseconds =
                    Stopwatch.GetElapsedTime(materializationStart).TotalMilliseconds
            };
    }

    public IReadOnlyList<SearchSpikeMatch> Matches { get; }

    public int FilesVisited { get; }

    public long BytesRead { get; }

    public long OutputBytes { get; }

    public string OutputHash { get; }

    public int ProcessInvocations { get; }

    public int? ExitCode { get; }

    public long RawOutputBytes { get; }

    public SearchSpikeStageTimings? StageTimings { get; }
}

public static class SearchSpikeResultFormatter
{
    public static string Serialize(IEnumerable<SearchSpikeMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        return string.Join(
            '\n',
            matches.Select(static match => string.Join(
                '|',
                match.RelativePath,
                match.PatternId,
                match.LineNumber,
                match.Utf16Column,
                match.Text)));
    }

    public static string Hash(string serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(serialized)))
            .ToLowerInvariant();
    }

    public static string Signature(SearchSpikeMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return string.Join(
            '|',
            match.RelativePath,
            match.PatternId,
            match.LineNumber,
            match.Utf16Column,
            match.Text);
    }
}

public static class ManagedSearchRunner
{
    public static SearchSpikeResult Run(
        string root,
        IReadOnlyList<SearchSpikePattern> patterns,
        CancellationToken cancellationToken = default,
        int cancelAfterFiles = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Count == 0)
            throw new ArgumentException("At least one search pattern is required.", nameof(patterns));
        if (cancelAfterFiles == 0 || cancelAfterFiles < -1)
            throw new ArgumentOutOfRangeException(nameof(cancelAfterFiles));

        foreach (var pattern in patterns)
            ArgumentNullException.ThrowIfNull(pattern);

        cancellationToken.ThrowIfCancellationRequested();

        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException(fullRoot);

        var matchers = patterns
            .Select(static pattern => new LiteralMatcher(pattern))
            .ToArray();
        var matches = new List<SearchSpikeMatch>();
        var filesVisited = 0;
        long bytesRead = 0;
        var traversalMilliseconds = 0d;
        var readDecodeMilliseconds = 0d;
        var matchingMilliseconds = 0d;

        using var fileEnumerator = Directory
            .EnumerateFiles(fullRoot, "*.txt", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .GetEnumerator();
        while (true)
        {
            var traversalStart = Stopwatch.GetTimestamp();
            if (!fileEnumerator.MoveNext())
            {
                traversalMilliseconds += Stopwatch.GetElapsedTime(traversalStart).TotalMilliseconds;
                break;
            }

            traversalMilliseconds += Stopwatch.GetElapsedTime(traversalStart).TotalMilliseconds;
            var filePath = fileEnumerator.Current;
            cancellationToken.ThrowIfCancellationRequested();
            filesVisited++;

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.SequentialScan);
            bytesRead += stream.Length;

            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false);

            var relativePath = ToRelativePath(fullRoot, filePath);
            var lineNumber = 1L;
            while (true)
            {
                var readStart = Stopwatch.GetTimestamp();
                var line = reader.ReadLine();
                readDecodeMilliseconds += Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
                if (line is null)
                    break;

                cancellationToken.ThrowIfCancellationRequested();
                var matchingStart = Stopwatch.GetTimestamp();
                for (var patternIndex = 0; patternIndex < matchers.Length; patternIndex++)
                {
                    matchers[patternIndex].AppendMatches(
                        line.AsSpan(),
                        relativePath,
                        lineNumber,
                        matches);
                }

                matchingMilliseconds += Stopwatch.GetElapsedTime(matchingStart).TotalMilliseconds;

                lineNumber++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (cancelAfterFiles > 0 && filesVisited >= cancelAfterFiles)
                throw new OperationCanceledException(
                    $"The managed feasibility runner was cancelled after {filesVisited} file(s).",
                    cancellationToken);
        }

        return new SearchSpikeResult(
            matches,
            filesVisited,
            bytesRead,
            stageTimings: new SearchSpikeStageTimings(
                traversalMilliseconds,
                readDecodeMilliseconds,
                matchingMilliseconds,
                MaterializationMilliseconds: 0,
                BridgeMilliseconds: 0));
    }

    private static string ToRelativePath(string root, string filePath)
    {
        var relative = Path.GetRelativePath(root, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
            relative = relative.Replace(Path.AltDirectorySeparatorChar, '/');
        return relative;
    }

    private sealed class LiteralMatcher
    {
        private readonly SearchSpikePattern _pattern;
        private readonly SearchValues<char> _firstCharacters;

        public LiteralMatcher(SearchSpikePattern pattern)
        {
            _pattern = pattern;
            _firstCharacters = SearchValues.Create(pattern.Literal.AsSpan(0, 1));
        }

        public void AppendMatches(
            ReadOnlySpan<char> line,
            string relativePath,
            long lineNumber,
            ICollection<SearchSpikeMatch> matches)
        {
            var searchStart = 0;
            while (searchStart < line.Length)
            {
                var candidateOffset = line[searchStart..].IndexOfAny(_firstCharacters);
                if (candidateOffset < 0)
                    return;

                var matchOffset = searchStart + candidateOffset;
                if (matchOffset + _pattern.Literal.Length <= line.Length &&
                    line.Slice(matchOffset, _pattern.Literal.Length)
                        .SequenceEqual(_pattern.Literal.AsSpan()))
                {
                    matches.Add(new SearchSpikeMatch(
                        relativePath,
                        _pattern.Id,
                        lineNumber,
                        matchOffset,
                        _pattern.Literal));
                    searchStart = matchOffset + _pattern.Literal.Length;
                }
                else
                {
                    searchStart = matchOffset + 1;
                }
            }
        }
    }
}

public sealed class SearchSpikeCorpus : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private SearchSpikeCorpus(
        string root,
        IReadOnlyList<SearchSpikeMatch> expectedMatches,
        string fixtureDigest,
        IReadOnlyList<SearchSpikePattern> patternSet)
    {
        Root = root;
        ExpectedMatches = expectedMatches
            .OrderBy(static match => match.RelativePath, StringComparer.Ordinal)
            .ThenBy(static match => match.LineNumber)
            .ThenBy(static match => match.Utf16Column)
            .ThenBy(static match => match.PatternId, StringComparer.Ordinal)
            .ToArray();
        FixtureDigest = fixtureDigest;
        PatternSet = patternSet;
    }

    public string Root { get; }

    public string FixtureDigest { get; }

    public IReadOnlyList<SearchSpikeMatch> ExpectedMatches { get; }

    public IReadOnlyList<SearchSpikePattern> PatternSet { get; }

    public static IReadOnlyList<SearchSpikePattern> Patterns { get; } =
    [
        new SearchSpikePattern("todo", "TODO"),
        new SearchSpikePattern("fixme", "FIXME")
    ];

    public static IReadOnlyList<SearchSpikeCohort> MeasurementCohorts { get; } =
    [
        new SearchSpikeCohort(
            "B02",
            "Large UTF-8 tree / absent literal",
            "No requested literal occurs in any eligible file."),
        new SearchSpikeCohort(
            "B03",
            "Large UTF-8 tree / sparse literal",
            "One requested literal occurs in one of every seventeen physical lines."),
        new SearchSpikeCohort(
            "B04",
            "Dense literal matches",
            "Four non-overlapping requested occurrences occur on every physical line.")
    ];

    private static readonly IReadOnlyList<SearchSpikePattern> SingleLiteralPattern =
    [
        new SearchSpikePattern("todo", "TODO")
    ];

    public static SearchSpikeCorpus Create()
    {
        return CreateFixture("mixed-literal", null, Patterns);
    }

    public static SearchSpikeCorpus Create(SearchSpikeCohort cohort)
    {
        ArgumentNullException.ThrowIfNull(cohort);
        return CreateFixture(cohort.Id, cohort, SingleLiteralPattern);
    }

    private static SearchSpikeCorpus CreateFixture(
        string fixtureName,
        SearchSpikeCohort? cohort,
        IReadOnlyList<SearchSpikePattern> patterns)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-feasibility-{fixtureName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var expected = new List<SearchSpikeMatch>();
            for (var fileIndex = 0; fileIndex < 64; fileIndex++)
            {
                var relativePath = fileIndex % 4 == 0
                    ? $"nested/case-{fileIndex:000}.txt"
                    : $"case-{fileIndex:000}.txt";
                var lines = new string[256];
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    lines[lineIndex] = cohort is null
                        ? CreateLine(fileIndex, lineIndex)
                        : CreateCohortLine(cohort.Id, fileIndex, lineIndex);
                    AppendExpected(
                        expected,
                        relativePath,
                        lineIndex + 1,
                        lines[lineIndex],
                        patterns);
                }

                var path = Path.Combine(
                    root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Join('\n', lines) + '\n', Utf8);
            }

            const string explicitUnicodePath = "nested/unicode.txt";
            var explicitUnicodeLines = cohort?.Id switch
            {
                "B02" => new[]
                {
                    "prefix 😀 ordinary suffix",
                    "FIXME is not selected",
                    "nothing"
                },
                "B03" => new[]
                {
                    "prefix 😀 TODO suffix",
                    "ordinary",
                    "nothing"
                },
                "B04" => new[]
                {
                    "TODO 😀 TODO TODO",
                    "TODO TODO TODO TODO",
                    "nothing"
                },
                _ => new[]
                {
                    "prefix 😀 TODO suffix",
                    "FIXME and TODO",
                    "nothing"
                }
            };
            AppendExpected(
                expected,
                explicitUnicodePath,
                1,
                explicitUnicodeLines[0],
                patterns);
            AppendExpected(
                expected,
                explicitUnicodePath,
                2,
                explicitUnicodeLines[1],
                patterns);
            var explicitUnicodeFullPath = Path.Combine(
                root,
                explicitUnicodePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(
                explicitUnicodeFullPath,
                string.Join('\n', explicitUnicodeLines) + '\n',
                Utf8);

            return new SearchSpikeCorpus(
                root,
                expected,
                ComputeFixtureDigest(root),
                patterns);
        }
        catch
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            throw;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    private static string CreateLine(int fileIndex, int lineIndex)
    {
        return (lineIndex % 17) switch
        {
            0 => $"TODO alpha {fileIndex:00} TODO",
            1 => $"FIXME 😀 marker {lineIndex:000}",
            2 => $"prefix 😀 TODO suffix {fileIndex:00}",
            _ => $"ordinary-{fileIndex:00}-{lineIndex:000} payload without the marker"
        };
    }

    private static string CreateCohortLine(string cohortId, int fileIndex, int lineIndex)
    {
        return cohortId switch
        {
            "B02" => $"ordinary-{fileIndex:00}-{lineIndex:000} payload without the marker",
            "B03" => lineIndex % 17 == 0
                ? $"TODO sparse {fileIndex:00}-{lineIndex:000}"
                : $"ordinary-{fileIndex:00}-{lineIndex:000} payload without the marker",
            "B04" => $"TODO TODO TODO TODO dense {fileIndex:00}-{lineIndex:000}",
            _ => throw new ArgumentException($"Unknown measurement cohort '{cohortId}'.", nameof(cohortId))
        };
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
