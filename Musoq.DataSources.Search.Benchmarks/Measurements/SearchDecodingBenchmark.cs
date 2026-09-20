#nullable enable

using System.Security.Cryptography;
using System.Text;

using Musoq.DataSources.Search.Benchmarks.Comparisons;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

public sealed record SearchDecodingCohort(
    string Id,
    string EncodingName,
    string ContentProfile);

public sealed class SearchDecodingCorpus : IDisposable
{
    private static readonly UTF8Encoding DigestEncoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private SearchDecodingCorpus(
        SearchDecodingCohort cohort,
        string root,
        IReadOnlyList<SearchSpikeMatch> expectedMatches,
        string fixtureDigest,
        long eligibleBytes,
        IReadOnlyList<SearchSpikePattern> patternSet)
    {
        Cohort = cohort;
        Root = root;
        ExpectedMatches = expectedMatches
            .OrderBy(static match => match.RelativePath, StringComparer.Ordinal)
            .ThenBy(static match => match.LineNumber)
            .ThenBy(static match => match.Utf16Column)
            .ThenBy(static match => match.PatternId, StringComparer.Ordinal)
            .ToArray();
        FixtureDigest = fixtureDigest;
        EligibleBytes = eligibleBytes;
        PatternSet = patternSet;
    }

    public static IReadOnlyList<SearchDecodingCohort> Cohorts { get; } =
    [
        new SearchDecodingCohort("D01", "ASCII", "ASCII-only text without a BOM"),
        new SearchDecodingCohort("D02", "UTF-8", "UTF-8 text without a BOM with non-ASCII scalars"),
        new SearchDecodingCohort("D03", "UTF-16", "UTF-16 little-endian text with a BOM")
    ];

    private static readonly IReadOnlyList<SearchSpikePattern> SinglePattern =
    [
        new SearchSpikePattern("todo", "TODO")
    ];

    public SearchDecodingCohort Cohort { get; }

    public string Root { get; }

    public IReadOnlyList<SearchSpikeMatch> ExpectedMatches { get; }

    public string FixtureDigest { get; }

    public long EligibleBytes { get; }

    public IReadOnlyList<SearchSpikePattern> PatternSet { get; }

    public static SearchDecodingCorpus Create(SearchDecodingCohort cohort)
    {
        ArgumentNullException.ThrowIfNull(cohort);

        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-decoding-{cohort.Id}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var encoding = ResolveEncoding(cohort);
            var expected = new List<SearchSpikeMatch>();
            for (var fileIndex = 0; fileIndex < 32; fileIndex++)
            {
                var relativePath = fileIndex % 4 == 0
                    ? $"nested/case-{fileIndex:000}.txt"
                    : $"case-{fileIndex:000}.txt";
                var lines = new string[128];
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    lines[lineIndex] = CreateLine(cohort, fileIndex, lineIndex);
                    AppendExpected(
                        expected,
                        relativePath,
                        lineIndex + 1,
                        lines[lineIndex]);
                }

                var path = Path.Combine(
                    root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Join('\n', lines) + '\n', encoding);
            }

            var files = Directory
                .EnumerateFiles(root, "*.txt", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            var eligibleBytes = files.Sum(static path => new FileInfo(path).Length);

            return new SearchDecodingCorpus(
                cohort,
                root,
                expected,
                ComputeFixtureDigest(root, files),
                eligibleBytes,
                SinglePattern);
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

    private static Encoding ResolveEncoding(SearchDecodingCohort cohort)
    {
        return cohort.Id switch
        {
            "D01" => new ASCIIEncoding(),
            "D02" => new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            "D03" => new UnicodeEncoding(
                bigEndian: false,
                byteOrderMark: true,
                throwOnInvalidBytes: true),
            _ => throw new ArgumentException(
                $"Unknown decoding cohort '{cohort.Id}'.",
                nameof(cohort))
        };
    }

    private static string CreateLine(
        SearchDecodingCohort cohort,
        int fileIndex,
        int lineIndex)
    {
        if (lineIndex % 9 == 0)
        {
            return cohort.Id switch
            {
                "D01" => $"TODO ascii {fileIndex:000}-{lineIndex:000}",
                "D02" => $"TODO café 😀 utf8 {fileIndex:000}-{lineIndex:000}",
                "D03" => $"TODO Ω 😀 utf16 {fileIndex:000}-{lineIndex:000}",
                _ => throw new ArgumentException(
                    $"Unknown decoding cohort '{cohort.Id}'.",
                    nameof(cohort))
            };
        }

        return cohort.Id switch
        {
            "D01" => $"ordinary ascii {fileIndex:000}-{lineIndex:000}",
            "D02" => $"ordinary café 😀 utf8 {fileIndex:000}-{lineIndex:000}",
            "D03" => $"ordinary Ω 😀 utf16 {fileIndex:000}-{lineIndex:000}",
            _ => throw new ArgumentException(
                $"Unknown decoding cohort '{cohort.Id}'.",
                nameof(cohort))
        };
    }

    private static void AppendExpected(
        ICollection<SearchSpikeMatch> expected,
        string relativePath,
        long lineNumber,
        string line)
    {
        var searchStart = 0;
        while (searchStart < line.Length)
        {
            var matchOffset = line.IndexOf("TODO", searchStart, StringComparison.Ordinal);
            if (matchOffset < 0)
                return;

            expected.Add(new SearchSpikeMatch(
                relativePath,
                "todo",
                lineNumber,
                matchOffset,
                "TODO"));
            searchStart = matchOffset + "TODO".Length;
        }
    }

    private static string ComputeFixtureDigest(
        string root,
        IReadOnlyList<string> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in files)
        {
            var relative = Path.GetRelativePath(root, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            hash.AppendData(DigestEncoding.GetBytes(relative));
            hash.AppendData([0]);
            hash.AppendData(SHA256.HashData(File.ReadAllBytes(path)));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
