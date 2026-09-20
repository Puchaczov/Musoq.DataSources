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

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchRegexMatchingTests
{
    [TestMethod]
    public void RegexRows_ShouldApplyRecordAnchorsAndSourceOrder()
    {
        var sourceOrder = ReadMatches(
            "ab\nTODO now\nTODO",
            "a|ab");
        Assert.AreEqual(1, sourceOrder.Length);
        Assert.AreEqual("a", sourceOrder[0].MatchText);
        Assert.AreEqual(1L, sourceOrder[0].LineNumber);
        Assert.AreEqual(0L, sourceOrder[0].Utf16Column);

        var anchored = ReadMatches(
            "TODO\nTODO now\nTODO",
            "^TODO$");
        CollectionAssert.AreEqual(
            new long[] { 1, 3 },
            anchored.Select(static row => row.LineNumber).ToArray());
        CollectionAssert.AreEqual(
            new[] { "TODO", "TODO" },
            anchored.Select(static row => row.MatchText).ToArray());
    }

    [TestMethod]
    public void RegexRows_ShouldKeepGreedyAlternativesWithinOneRecord()
    {
        var shortAlternativeFirst = ReadMatches("ab\nnext", "a+|ab");
        var longAlternativeFirst = ReadMatches("ab\nnext", "ab|a+");

        Assert.AreEqual("a", shortAlternativeFirst.Single().MatchText);
        Assert.AreEqual(1L, shortAlternativeFirst[0].Utf16Length);
        Assert.AreEqual("ab", longAlternativeFirst.Single().MatchText);
        Assert.AreEqual(2L, longAlternativeFirst[0].Utf16Length);

        var crossRecord = ReadMatches("a\nb", "a.*b");
        Assert.AreEqual(0, crossRecord.Length);
    }

    [TestMethod]
    public void RegexRows_ShouldProgressZeroWidthMatchesPerRecord()
    {
        var rows = ReadMatchesFromFile("abc\nxyz", "^|$");

        Assert.AreEqual(4, rows.Length);
        CollectionAssert.AreEqual(
            new long[] { 1, 1, 2, 2 },
            rows.Select(static row => row.LineNumber).ToArray());
        CollectionAssert.AreEqual(
            new long[] { 0, 3, 0, 3 },
            rows.Select(static row => row.Utf16Column).ToArray());
        Assert.IsTrue(rows.All(static row => row.Utf16Length == 0));
        Assert.IsTrue(rows.All(static row => row.ByteLength == 0));
        Assert.IsTrue(rows.All(static row => row.MatchText == string.Empty));
    }

    [TestMethod]
    public void RegexRows_ShouldMapOriginalUtf8Coordinates()
    {
        var rows = ReadMatchesFromFile("😀 TODO", "TODO");

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual(5L, rows[0].ByteOffset);
        Assert.AreEqual(4L, rows[0].ByteLength);
        Assert.AreEqual(3L, rows[0].Utf16Column);
        Assert.AreEqual("TODO", rows[0].MatchText);
    }

    [TestMethod]
    public void RegexRows_ShouldIgnoreReaderBlockBoundaries()
    {
        var rows = ReadMatches(
            "prefix TODO\nTODO suffix\n",
            "^.*TODO.*$",
            chunks: [1, 2, 3, 1]);

        Assert.AreEqual(2, rows.Length);
        CollectionAssert.AreEqual(
            new[] { "prefix TODO", "TODO suffix" },
            rows.Select(static row => row.MatchText).ToArray());
        CollectionAssert.AreEqual(
            new long[] { 1, 2 },
            rows.Select(static row => row.LineNumber).ToArray());
    }

    [TestMethod]
    public void RegexLines_ShouldEmitOneRowPerMatchingPhysicalRecord()
    {
        var rows = ReadLines(
            "TODO TODO\nno match\nTODO",
            "TODO",
            chunks: [1, 1, 2, 3]);

        Assert.AreEqual(2, rows.Length);
        Assert.AreEqual(2L, rows[0].OccurrenceCount);
        Assert.AreEqual("TODO TODO\n", rows[0].LineText);
        Assert.AreEqual(1L, rows[0].LineNumber);
        Assert.AreEqual(1L, rows[1].OccurrenceCount);
        Assert.AreEqual("TODO", rows[1].LineText);
        Assert.AreEqual(3L, rows[1].LineNumber);
    }

    [TestMethod]
    public void RegexWholeWord_ShouldUseTheDeclaredUnicodeBoundaryPolicy()
    {
        var rows = ReadMatches(
            "foo foo2 😀foo foo😀",
            "foo",
            wholeWord: true);

        CollectionAssert.AreEqual(
            new long[] { 0, 11, 15 },
            rows.Select(static row => row.Utf16Column).ToArray());
    }

    [TestMethod]
    public void RegexRows_ShouldRejectRecordsBeyondTheBound()
    {
        var exception = Assert.ThrowsException<SearchResourceLimitException>(() =>
            ReadMatches(
                new string('x', SearchRegexScanner.MaxRecordLength + 1),
                "x",
                chunks: [8192]));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.AreEqual("record", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, "UTF-16 characters");
    }

    [TestMethod]
    public void EmptyFile_ShouldProduceNoRegexRows()
    {
        var rows = ReadMatchesFromFile(string.Empty, "^");

        Assert.AreEqual(0, rows.Length);
    }

    private static SearchMatch[] ReadMatches(
        string content,
        string pattern,
        string? caseMode = null,
        bool wholeWord = false,
        IReadOnlyList<int>? chunks = null)
    {
        return ReadMatchesCore(content, pattern, caseMode, wholeWord, chunks);
    }

    private static SearchMatch[] ReadMatchesFromFile(
        string content,
        string pattern)
    {
        return ReadMatchesCore(content, pattern, null, false, chunks: null, useFileReader: true);
    }

    private static SearchMatch[] ReadMatchesCore(
        string content,
        string pattern,
        string? caseMode,
        bool wholeWord,
        IReadOnlyList<int>? chunks,
        bool useFileReader = false)
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "input.txt");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, useFileReader ? content : "reader content", new UTF8Encoding(false));
            var request = SearchRequest.CreateRegex(
                root,
                pattern,
                caseMode: caseMode,
                wholeWord: wholeWord);
            Func<string, TextReader>? readerFactory = useFileReader
                ? null
                : _ => chunks is null
                    ? new StringReader(content)
                    : new ChunkedTextReader(content, chunks);
            return new SearchMatchesSource(
                    request,
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    readerFactory)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static SearchLine[] ReadLines(
        string content,
        string pattern,
        IReadOnlyList<int> chunks)
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "input.txt");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "reader content", new UTF8Encoding(false));
            var request = SearchRequest.CreateRegex(root, pattern);
            return new SearchLinesSource(
                    request,
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ => new ChunkedTextReader(content, chunks))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-regex-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

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
}
