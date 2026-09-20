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
public sealed class SearchRegexMultilineTests
{
    [TestMethod]
    public void BoundedMultiline_ShouldMatchAcrossReaderBlocks()
    {
        var rows = ReadMatches(
            "prefix A\nmiddle\nB suffix",
            @"A[\s\S]*B",
            chunks: [1, 2, 1, 3, 2],
            maxRecordBytes: 128);

        var row = rows.Single();
        Assert.AreEqual("A\nmiddle\nB", row.MatchText);
        Assert.AreEqual(1L, row.LineNumber);
        Assert.AreEqual(7L, row.Utf16Column);
        Assert.AreEqual(10L, row.Utf16Length);
    }

    [TestMethod]
    public void BoundedMultiline_ShouldRejectRecordsBeyondByteLimit()
    {
        var exception = Assert.ThrowsException<SearchResourceLimitException>(() =>
            ReadMatches(
                "123456789",
                "[\\s\\S]",
                maxRecordBytes: 8));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.AreEqual("recordBytes", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, "original bytes");
    }

    [TestMethod]
    public void BoundedMultiline_ShouldEvaluateAnUnterminatedEofRecord()
    {
        var rows = ReadMatches(
            "first\nneedle",
            @"first[\s\n]*needle",
            chunks: [2, 1, 4, 1],
            maxRecordBytes: 128);

        var row = rows.Single();
        Assert.AreEqual("first\nneedle", row.MatchText);
        Assert.AreEqual(1L, row.LineNumber);
        Assert.AreEqual(0L, row.Utf16Column);
        Assert.AreEqual(12L, row.Utf16Length);
    }

    [TestMethod]
    public void BoundedMultiline_ShouldEmitZeroWidthMatchAtFinalBoundary()
    {
        var rows = ReadMatches(
            "first\nlast",
            "$",
            chunks: [1, 1, 2, 1],
            maxRecordBytes: 128);

        var row = rows.Single();
        Assert.AreEqual(2L, row.LineNumber);
        Assert.AreEqual(4L, row.Utf16Column);
        Assert.AreEqual(0L, row.Utf16Length);
    }

    [TestMethod]
    public void FramedMultiline_ShouldMatchWhenDelimitersSpanReaderBlocks()
    {
        const string content =
            "preamble\n<record>\nfirst\nmiddle\n</record>\n<record>\nsecond\n</record>\n";
        var rows = ReadMatches(
            content,
            @"first[\s\S]*middle",
            chunks: [1],
            framing: new SearchRecordFraming("<record>", "</record>"));

        var row = rows.Single();
        Assert.AreEqual("first\nmiddle", row.MatchText);
        Assert.AreEqual(3L, row.LineNumber);
        Assert.AreEqual(0L, row.Utf16Column);
    }

    [TestMethod]
    public void FramedMultiline_ShouldKeepDelimiterLikePayloadInsideTheRecord()
    {
        var rows = ReadMatches(
            "<record>\nvalue </record> still payload\n</record>\n",
            @"value </record> still payload",
            chunks: [2, 1, 3],
            framing: new SearchRecordFraming("<record>", "</record>"));

        var row = rows.Single();
        Assert.AreEqual("value </record> still payload", row.MatchText);
        Assert.AreEqual(2L, row.LineNumber);
    }

    [TestMethod]
    public void FramedMultiline_ShouldExposeOriginAndSourceRangeForEachRecord()
    {
        const string content =
            "ignored\n<record>\nfirst\n</record>\n<record>\nsecond\n</record>\n";
        var frames = new List<SearchRecordFrame>();
        var framer = new SearchRecordFramer(
            "input.log",
            new SearchRecordFraming("<record>", "</record>"),
            maxRecordBytes: 128,
            encodingMode: SearchEncodingMode.Utf8,
            hasCoordinateReader: false,
            cancellationToken: default,
            recordCompleted: frame =>
            {
                frames.Add(frame);
                return false;
            });

        foreach (var character in content)
            framer.Append(character, default, hasCoordinate: false);
        framer.Complete();

        Assert.AreEqual(2, frames.Count);
        Assert.AreEqual(0L, frames[0].RecordIndex);
        Assert.AreEqual("input.log", frames[0].Origin);
        Assert.AreEqual((long)content.IndexOf("first", StringComparison.Ordinal), frames[0].Start);
        Assert.AreEqual(
            frames[0].Start + frames[0].Content.Length,
            frames[0].EndExclusive);
        Assert.AreEqual(3L, frames[0].FirstLineNumber);
        Assert.AreEqual("first\n", frames[0].Content);
        Assert.IsTrue(frames[0].Terminated);
        Assert.AreEqual("second\n", frames[1].Content);
    }

    [TestMethod]
    public void FramedMultiline_ShouldRejectAnUnterminatedRecordByDefault()
    {
        var exception = Assert.ThrowsException<SearchRecordFramingException>(() =>
            ReadMatches(
                "<record>\nneedle",
                "needle",
                framing: new SearchRecordFraming("<record>", "</record>")));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidRecordFraming, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Syntax, exception.Diagnostic.Phase);
        StringAssert.Contains(exception.Diagnostic.Explanation, "end delimiter");
    }

    [TestMethod]
    public void FramedMultiline_ShouldEvaluateAnUnterminatedEofRecordWhenAllowed()
    {
        var rows = ReadMatches(
            "<record>\nfirst\nneedle",
            @"first\nneedle",
            chunks: [1, 2, 1],
            framing: new SearchRecordFraming(
                "<record>",
                "</record>",
                allowUnterminatedEof: true));

        var row = rows.Single();
        Assert.AreEqual("first\nneedle", row.MatchText);
        Assert.AreEqual(2L, row.LineNumber);
        Assert.AreEqual(0L, row.Utf16Column);
    }

    [TestMethod]
    public void FramedMultiline_ShouldRejectPayloadBeyondByteLimit()
    {
        var exception = Assert.ThrowsException<SearchResourceLimitException>(() =>
            ReadMatches(
                "<record>\n123456789\n</record>\n",
                "123",
                maxRecordBytes: 8,
                framing: new SearchRecordFraming("<record>", "</record>")));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.AreEqual("recordBytes", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void FramedMultiline_ShouldPreserveMappedByteCoordinates()
    {
        const string content = "<record>\nα\nneedle\n</record>\n";
        var rows = ReadMatches(
            content,
            "needle",
            useFileReader: true,
            framing: new SearchRecordFraming("<record>", "</record>"));

        var row = rows.Single();
        var expectedByteOffset = new UTF8Encoding(false).GetByteCount(
            content[..content.IndexOf("needle", StringComparison.Ordinal)]);
        Assert.AreEqual((long)expectedByteOffset, row.ByteOffset);
        Assert.AreEqual(6L, row.ByteLength);
    }

    [TestMethod]
    public void FramedMultiline_ShouldHonorAnExistentialSinkStop()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-regex-framed-files-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "input.log");
        const string content = "<record>\nneedle\n</record>\n<record>\nneedle\n</record>\n";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            var request = SearchRequest.CreateRegex(
                root,
                "needle",
                recordMode: SearchRecordMode.BoundedMultiline,
                recordFraming: new SearchRecordFraming("<record>", "</record>"));
            var rows = new SearchFilesSource(
                    request,
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ => new StringReader(content))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("input.log", rows[0].Path);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static SearchMatch[] ReadMatches(
        string content,
        string pattern,
        IReadOnlyList<int>? chunks = null,
        int maxRecordBytes = SearchRegexScanner.MaxMultilineRecordBytes,
        SearchRecordFraming? framing = null,
        bool useFileReader = false)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-regex-multiline-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "input.txt");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            var request = SearchRequest.CreateRegex(
                root,
                pattern,
                recordMode: SearchRecordMode.BoundedMultiline,
                maxRecordBytes: maxRecordBytes,
                recordFraming: framing);
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
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
