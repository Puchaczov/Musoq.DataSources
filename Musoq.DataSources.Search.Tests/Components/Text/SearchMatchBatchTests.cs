#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchMatchBatchTests
{
    [TestMethod]
    public void LiteralScanner_ShouldDeliverDenseHitsAsOwnedConsumerCopies()
    {
        const int expectedMatches = 4_096;
        var content = string.Concat(Enumerable.Repeat("TODO ", expectedMatches));
        var sink = new RecordingBatchSink();

        ScanLiteral(content, sink);

        Assert.IsTrue(sink.BatchCount > 1);
        Assert.AreEqual(0, sink.SingleMatchCount);
        Assert.AreEqual(expectedMatches, sink.CopiedMatches.Count);
        Assert.AreEqual(
            expectedMatches,
            sink.BatchSizes.Sum());
        CollectionAssert.AreEqual(
            Enumerable.Range(0, expectedMatches)
                .Select(index => index * 5L)
                .ToArray(),
            sink.CopiedMatches.Select(static span => span.Start).ToArray());
    }

    [TestMethod]
    public void RegexScanner_ShouldDeliverLargeCaptureBatchWithoutLoss()
    {
        const int expectedMatches = 2_048;
        var content = string.Concat(Enumerable.Repeat("A1", expectedMatches));
        var sink = new RecordingBatchSink(needsCaptures: true);
        var regex = new Regex(
            "(?<letter>[A-Z])(?<digit>[0-9])",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

        using var buffer = SearchCharBuffer.Rent();
        SearchRegexScanner.ScanFile(
            regex,
            "capture-batch.txt",
            buffer,
            wholeWord: false,
            encodingMode: SearchEncodingMode.Utf8,
            recordMode: SearchRecordMode.PhysicalLine,
            maxRecordBytes: SearchRegexScanner.MaxMultilineRecordBytes,
            cancellationToken: CancellationToken.None,
            readerFactory: _ => new StringReader(content),
            sink: sink);

        Assert.AreEqual(1, sink.BatchCount);
        Assert.AreEqual(0, sink.SingleMatchCount);
        Assert.AreEqual(expectedMatches, sink.CopiedMatches.Count);
        Assert.AreEqual(
            expectedMatches * 2,
            sink.CopiedMatches.Sum(static span => span.Captures?.Count ?? 0));
        Assert.IsTrue(sink.CopiedMatches.All(static span =>
            span.Captures is not null &&
            span.Captures.Count == 2 &&
            span.Captures[0].Success &&
            span.Captures[1].Success));
    }

    [TestMethod]
    public void BatchBridgeFailure_ShouldBeTypedAndDisposeTheReader()
    {
        var reader = new TrackingTextReader("TODO");
        var sink = new ThrowingBatchSink();

        var exception = Assert.ThrowsException<SearchSourceReadException>(() =>
            ScanLiteral(reader, sink));

        Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, exception.Diagnostic.Code);
        Assert.AreEqual(1, sink.BatchCount);
        Assert.IsTrue(reader.Disposed);
    }

    [TestMethod]
    public void RegexScanner_ShouldPreserveImmediateDeliveryForExistentialSinks()
    {
        var sink = new ImmediateStopSink();
        var reader = new TrackingTextReader(
            "TODO " + string.Concat(Enumerable.Repeat("tail ", 10_000)));

        using var buffer = SearchCharBuffer.Rent();
        SearchRegexScanner.ScanFile(
            new Regex("TODO", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking),
            "immediate.txt",
            buffer,
            wholeWord: false,
            encodingMode: SearchEncodingMode.Utf8,
            recordMode: SearchRecordMode.PhysicalLine,
            maxRecordBytes: SearchRegexScanner.MaxMultilineRecordBytes,
            cancellationToken: CancellationToken.None,
            readerFactory: _ => reader,
            sink: sink);

        Assert.AreEqual(1, sink.SingleMatchCount);
        Assert.AreEqual(0, sink.BatchCount);
        Assert.IsTrue(reader.Disposed);
    }

    [TestMethod]
    public void RepeatedBatchScans_ShouldDisposeEveryReader()
    {
        var readers = new List<TrackingTextReader>();
        try
        {
            for (var index = 0; index < 64; index++)
            {
                var reader = new TrackingTextReader("TODO TODO");
                readers.Add(reader);
                ScanLiteral(reader, new RecordingBatchSink());
            }

            Assert.IsTrue(readers.All(static reader => reader.Disposed));
        }
        finally
        {
            foreach (var reader in readers)
                reader.Dispose();
        }
    }

    private static void ScanLiteral(
        string content,
        RecordingBatchSink sink)
    {
        using var reader = new StringReader(content);
        ScanLiteral(reader, sink);
    }

    private static void ScanLiteral(
        TextReader reader,
        ISearchTextScanSink sink)
    {
        using var buffer = SearchCharBuffer.Rent();
        SearchTextScanner.ScanFile(
            new LiteralMatcher("TODO"),
            "batch.txt",
            buffer,
            [],
            CancellationToken.None,
            _ => reader,
            sink);
    }

    private sealed class RecordingBatchSink(bool needsCaptures = false) : ISearchTextScanSink
    {
        public List<MatchSpan> CopiedMatches { get; } = [];

        public List<int> BatchSizes { get; } = [];

        public int BatchCount { get; private set; }

        public int SingleMatchCount { get; private set; }

        public bool NeedsLineText => false;

        public bool NeedsMatchText => needsCaptures;

        public bool NeedsCaptures => needsCaptures;

        public bool NeedsLineCompletion => false;

        public long RowsEmitted => CopiedMatches.Count;

        public bool AcceptMatch(MatchSpan span)
        {
            SingleMatchCount++;
            CopiedMatches.Add(span);
            return false;
        }

        public bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(spans);
            cancellationToken.ThrowIfCancellationRequested();
            BatchCount++;
            BatchSizes.Add(spans.Count);
            for (var index = 0; index < spans.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopiedMatches.Add(spans[index]);
            }

            return false;
        }

        public bool HasMatchesOnLine(long lineNumber) => false;

        public void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
        {
        }
    }

    private sealed class ThrowingBatchSink : ISearchTextScanSink
    {
        public int BatchCount { get; private set; }

        public bool NeedsLineText => false;

        public bool NeedsMatchText => false;

        public bool NeedsCaptures => false;

        public bool NeedsLineCompletion => false;

        public long RowsEmitted => 0;

        public bool AcceptMatch(MatchSpan span) => false;

        public bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            BatchCount++;
            throw new InvalidOperationException("Injected bridge failure.");
        }

        public bool HasMatchesOnLine(long lineNumber) => false;

        public void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
        {
        }
    }

    private sealed class ImmediateStopSink : ISearchTextScanSink
    {
        public int SingleMatchCount { get; private set; }

        public int BatchCount { get; private set; }

        public bool NeedsLineText => false;

        public bool NeedsMatchText => false;

        public bool NeedsCaptures => false;

        public bool NeedsLineCompletion => false;

        public bool RequiresImmediateMatchDelivery => true;

        public long RowsEmitted => SingleMatchCount;

        public bool AcceptMatch(MatchSpan span)
        {
            SingleMatchCount++;
            return true;
        }

        public bool AcceptMatches(
            IReadOnlyList<MatchSpan> spans,
            CancellationToken cancellationToken = default)
        {
            BatchCount++;
            return false;
        }

        public bool HasMatchesOnLine(long lineNumber) => false;

        public void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
        {
        }
    }

    private sealed class TrackingTextReader(string content) : StringReader(content)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
