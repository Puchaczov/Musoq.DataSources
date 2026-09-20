#nullable enable

using System.Text;

namespace Musoq.DataSources.Search.Testing;

public sealed record SearchReferenceInput(string Path, string Content);

public readonly record struct SearchReferenceOccurrence(
    string Path,
    long MatchIndex,
    long LineNumber,
    long Utf16Column,
    string MatchText,
    long? ByteOffset = null,
    long? ByteLength = null,
    long? Utf16Length = null);

public readonly record struct SearchReferenceLine(
    string Path,
    long LineNumber,
    long OccurrenceCount);

public sealed class SearchReferenceScan
{
    internal SearchReferenceScan(
        IReadOnlyList<SearchReferenceOccurrence> occurrences,
        IReadOnlyList<SearchReferenceLine> matchingLines,
        IReadOnlyDictionary<string, long> fileCounts)
    {
        Occurrences = occurrences;
        MatchingLines = matchingLines;
        FileCounts = fileCounts;
    }

    public IReadOnlyList<SearchReferenceOccurrence> Occurrences { get; }

    public IReadOnlyList<SearchReferenceLine> MatchingLines { get; }

    public IReadOnlyDictionary<string, long> FileCounts { get; }

    public long OccurrenceCount => Occurrences.Count;

    public long MatchingLineCount => MatchingLines.Count;

    public long MatchingFileCount => FileCounts.Count(pair => pair.Value > 0);
}

public static class SearchReferenceOracle
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public static SearchReferenceScan Scan(
        IEnumerable<SearchReferenceInput> inputs,
        string literal,
        StringComparison comparison = StringComparison.Ordinal)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrEmpty(literal);

        var occurrences = new List<SearchReferenceOccurrence>();
        var matchingLines = new List<SearchReferenceLine>();
        var fileCounts = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            ArgumentException.ThrowIfNullOrEmpty(input.Path);
            ArgumentNullException.ThrowIfNull(input.Content);
            if (!fileCounts.TryAdd(input.Path, 0))
                throw new ArgumentException($"Duplicate reference input path '{input.Path}'.", nameof(inputs));

            ScanFile(input, literal, comparison, occurrences, matchingLines, fileCounts);
        }

        return new SearchReferenceScan(
            occurrences.AsReadOnly(),
            matchingLines.AsReadOnly(),
            new Dictionary<string, long>(fileCounts, StringComparer.Ordinal));
    }

    private static void ScanFile(
        SearchReferenceInput input,
        string literal,
        StringComparison comparison,
        ICollection<SearchReferenceOccurrence> occurrences,
        ICollection<SearchReferenceLine> matchingLines,
        IDictionary<string, long> fileCounts)
    {
        var content = input.Content;
        var lineStart = 0;
        var lineNumber = 1L;
        var matchIndex = 0L;

        while (lineStart < content.Length)
        {
            var newline = content.IndexOf('\n', lineStart);
            var lineEnd = newline >= 0 ? newline : content.Length;
            var searchStart = lineStart;
            var lineOccurrences = 0L;

            while (searchStart < lineEnd)
            {
                var match = content.IndexOf(literal, searchStart, comparison);
                if (match < 0 || match >= lineEnd)
                    break;

                if (match + literal.Length > lineEnd)
                {
                    searchStart = match + 1;
                    continue;
                }

                var matchText = content.Substring(match, literal.Length);
                occurrences.Add(new SearchReferenceOccurrence(
                    input.Path,
                    matchIndex++,
                    lineNumber,
                    match - lineStart,
                    matchText,
                    Utf8.GetByteCount(content.AsSpan(0, match)),
                    Utf8.GetByteCount(matchText),
                    matchText.Length));
                fileCounts[input.Path]++;
                lineOccurrences++;
                searchStart = match + literal.Length;
            }

            if (lineOccurrences > 0)
                matchingLines.Add(new SearchReferenceLine(input.Path, lineNumber, lineOccurrences));

            if (newline < 0)
                break;

            lineStart = newline + 1;
            lineNumber++;
        }
    }
}
