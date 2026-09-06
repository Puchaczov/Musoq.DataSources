#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchLiteralBoundaryTests
{
    [TestMethod]
    public void LiteralMatcher_ShouldMatchReferenceForEveryPossibleBoundarySplit()
    {
        const string literal = "TODO";
        var content = "prefix TODO TODO\n😀TODOx\nline with TODO at the end";
        var expected = ExpectedSpans(content, literal);

        for (var split = 0; split <= content.Length; split++)
        {
            var actual = ScanAtSplit(content, literal, split);
            AssertSpansEqual(expected, actual, $"boundary split {split}");
        }
    }

    [TestMethod]
    public void LiteralMatcher_ShouldRetainContinuationWhenPatternExceedsEveryBlock()
    {
        var literal = new string('a', 9_001);
        var content = "prefix\n" + literal + "x" + literal;
        var expected = ExpectedSpans(content, literal);
        var actual = ScanInBlocks(content, literal, [1, 2, 31, 4_096, 7, 8_191]);

        AssertSpansEqual(expected, actual, "pattern longer than every reader block");
    }

    [TestMethod]
    public void LiteralMatcher_ShouldPreserveAdjacentAndEofEndingMatches()
    {
        const string literal = "TODO";
        const string content = "TODOTODO";
        var expected = ExpectedSpans(content, literal);
        var actual = ScanInBlocks(content, literal, [3, 2, 1]);

        AssertSpansEqual(expected, actual, "adjacent EOF-ending matches");
    }

    [TestMethod]
    public void LiteralMatcher_ShouldKeepMatchesWithinPhysicalRecords()
    {
        const string literal = "TODO";
        const string content = "TO\nDO\nTODO";
        var expected = ExpectedSpans(content, literal);
        var actual = ScanInBlocks(content, literal, [2, 1, 2]);

        AssertSpansEqual(expected, actual, "physical record boundaries");
        Assert.AreEqual(1, actual.Count);

        var newlineLiteral = ScanInBlocks(content, "\n", [1]);
        Assert.AreEqual(0, newlineLiteral.Count, "a physical newline is not part of a record");
    }

    [TestMethod]
    public void LiteralMatcher_ShouldMatchReferenceForRandomInputsAndEveryChunkSize()
    {
        var random = new BoundaryXorShift32(0x9e3779b9u);
        var literals = new[] { "a", "ab", "aba", "TODO", "😀T", "T😀O", "abcabc" };

        for (var caseIndex = 0; caseIndex < 48; caseIndex++)
        {
            var content = CreateRandomContent(random);
            var literal = literals[random.Next(literals.Length)];
            var expected = ExpectedSpans(content, literal);
            var maximumChunkSize = Math.Max(1, content.Length);

            for (var chunkSize = 1; chunkSize <= maximumChunkSize; chunkSize++)
            {
                var actual = ScanInBlocks(content, literal, [chunkSize]);
                AssertSpansEqual(
                    expected,
                    actual,
                    $"random case {caseIndex}, chunk size {chunkSize}, literal '{literal}'");
            }
        }
    }

    [TestMethod]
    public void SearchSource_ShouldEmitOracleRowsForIrregularReaderBlocks()
    {
        const string literal = "TODO";
        var content = "TODO x\n😀TODO TODO\nend TODO";
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-literal-boundary-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "boundary.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "reader supplied content", new UTF8Encoding(false));

            var source = new SearchMatchesSource(
                path,
                literal,
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new ChunkedTextReader(content, [1, 2, 5, 3]));
            var actual = source.Chunks.SelectMany(static chunk => chunk).ToArray();
            var expected = SearchReferenceScanner.Scan(
                    [new SearchReferenceInput("boundary.txt", content)],
                    literal)
                .Occurrences;

            CollectionAssert.AreEqual(
                expected.Select(Signature).ToArray(),
                actual.Select(Signature).ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static IReadOnlyList<ExpectedSpan> ExpectedSpans(string content, string literal)
    {
        var scan = SearchReferenceScanner.Scan(
            [new SearchReferenceInput("boundary.txt", content)],
            literal);
        var lineStarts = new List<long> { 0 };
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == '\n')
                lineStarts.Add(index + 1L);
        }

        return scan.Occurrences
            .Select(occurrence => new ExpectedSpan(
                lineStarts[checked((int)occurrence.LineNumber - 1)] + occurrence.Utf16Column,
                literal.Length,
                occurrence.LineNumber,
                occurrence.Utf16Column))
            .ToArray();
    }

    private static IReadOnlyList<MatchSpan> ScanAtSplit(
        string content,
        string literal,
        int split)
    {
        var matcher = new LiteralMatcher(literal);
        var spans = new List<MatchSpan>();
        var lineNumber = 1L;
        var utf16Column = 0L;

        matcher.ConsumeBlock(
            content.AsSpan(0, split),
            ref lineNumber,
            ref utf16Column,
            spans);
        matcher.ConsumeBlock(
            content.AsSpan(split),
            ref lineNumber,
            ref utf16Column,
            spans);

        return spans;
    }

    private static IReadOnlyList<MatchSpan> ScanInBlocks(
        string content,
        string literal,
        IReadOnlyList<int> chunkSizes)
    {
        var matcher = new LiteralMatcher(literal);
        var spans = new List<MatchSpan>();
        var lineNumber = 1L;
        var utf16Column = 0L;
        var position = 0;
        var chunkIndex = 0;

        while (position < content.Length)
        {
            var requestedLength = chunkSizes[Math.Min(chunkIndex++, chunkSizes.Count - 1)];
            Assert.IsTrue(requestedLength > 0);
            var length = Math.Min(requestedLength, content.Length - position);
            matcher.ConsumeBlock(
                content.AsSpan(position, length),
                ref lineNumber,
                ref utf16Column,
                spans);
            position += length;
        }

        return spans;
    }

    private static void AssertSpansEqual(
        IReadOnlyList<ExpectedSpan> expected,
        IReadOnlyList<MatchSpan> actual,
        string message)
    {
        Assert.AreEqual(expected.Count, actual.Count, message);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].Start, actual[index].Start, message);
            Assert.AreEqual(expected[index].Length, actual[index].Length, message);
            Assert.AreEqual(expected[index].LineNumber, actual[index].LineNumber, message);
            Assert.AreEqual(expected[index].Utf16Column, actual[index].Utf16Column, message);
        }

        CollectionAssert.AreEquivalent(
            expected.Select(SpanSignature).ToArray(),
            actual.Select(SpanSignature).ToArray(),
            message);
    }

    private static string SpanSignature(ExpectedSpan span)
    {
        return string.Join('|', span.Start, span.Length, span.LineNumber, span.Utf16Column);
    }

    private static string SpanSignature(MatchSpan span)
    {
        return string.Join('|', span.Start, span.Length, span.LineNumber, span.Utf16Column);
    }

    private static string Signature(SearchReferenceOccurrence occurrence)
    {
        return string.Join(
            '|',
            occurrence.Path,
            occurrence.MatchIndex,
            occurrence.LineNumber,
            occurrence.Utf16Column,
            occurrence.MatchText);
    }

    private static string Signature(SearchMatch row)
    {
        return string.Join(
            '|',
            row.Path,
            row.MatchIndex,
            row.LineNumber,
            row.Utf16Column,
            row.MatchText);
    }

    private static string CreateRandomContent(BoundaryXorShift32 random)
    {
        var alphabet = new[] { "a", "b", "c", "T", "O", "D", " ", "\n", "😀", "é" };
        var length = random.Next(161);
        var builder = new StringBuilder(length);
        for (var index = 0; index < length; index++)
            builder.Append(alphabet[random.Next(alphabet.Length)]);
        return builder.ToString();
    }

    private readonly record struct ExpectedSpan(
        long Start,
        long Length,
        long LineNumber,
        long Utf16Column);

    private sealed class ChunkedTextReader(
        string content,
        IReadOnlyList<int> chunkSizes) : TextReader
    {
        private int _position;
        private int _chunkIndex;

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position >= content.Length)
                return 0;

            var requestedLength = chunkSizes[Math.Min(_chunkIndex++, chunkSizes.Count - 1)];
            var length = Math.Min(Math.Min(requestedLength, count), content.Length - _position);
            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }

    private sealed class BoundaryXorShift32(uint seed)
    {
        private uint _state = seed == 0 ? 0x6d2b79f5u : seed;

        public int Next(int exclusiveMax)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)exclusiveMax);
        }
    }
}
