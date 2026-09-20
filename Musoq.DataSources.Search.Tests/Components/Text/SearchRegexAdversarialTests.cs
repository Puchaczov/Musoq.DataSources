#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchRegexAdversarialTests
{
    [TestInitialize]
    public void Initialize()
    {
        SearchRegexBackend.ResetForTests();
    }

    [TestCleanup]
    public void Cleanup()
    {
        SearchRegexBackend.ResetForTests();
    }

    [TestMethod]
    public void SafeProfile_ShouldFinishPathologicalNestedAlternation()
    {
        var regex = SearchRegexBackend.Compile("^(a|aa)+$");
        var input = new string('a', 100_000) + "!";

        Assert.IsFalse(regex.IsMatch(input));
        Assert.IsTrue(regex.Options.HasFlag(RegexOptions.NonBacktracking));
    }

    [TestMethod]
    public void RegexTimeout_ShouldBecomeTypedResourceLimit()
    {
        var regex = new Regex(
            "(a|aa)+$",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromTicks(1));
        var sink = new RecordingSink();

        var exception = Assert.ThrowsException<SearchResourceLimitException>(() =>
            Scan(
                regex,
                new string('a', 200_000) + "!",
                CancellationToken.None,
                sink));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Resource, exception.Diagnostic.Phase);
        Assert.AreEqual("pattern", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, "did not finish");
        Assert.IsInstanceOfType(exception.InnerException, typeof(RegexMatchTimeoutException));
        Assert.AreEqual(0, sink.Matches.Count);
    }

    [TestMethod]
    public void CancellationDuringDenseMatchOutput_ShouldDiscardPartialScan()
    {
        using var cancellation = new CancellationTokenSource();
        var sink = new CancellingSink(cancellation, cancelAfter: 512);

        var exception = Assert.ThrowsException<OperationCanceledException>(() =>
            Scan(
                SearchRegexBackend.Compile("a"),
                new string('a', 100_000),
                cancellation.Token,
                sink));

        Assert.IsNotNull(exception);
        Assert.AreEqual(512, sink.Matches.Count);
    }

    private static void Scan(
        Regex regex,
        string content,
        CancellationToken cancellationToken,
        RecordingSink sink)
    {
        using var buffer = SearchCharBuffer.Rent();
        SearchRegexScanner.ScanFile(
            regex,
            "adversarial.txt",
            buffer,
            wholeWord: false,
            encodingMode: SearchEncodingMode.Utf8,
            recordMode: SearchRecordMode.PhysicalLine,
            maxRecordBytes: SearchRegexScanner.MaxMultilineRecordBytes,
            cancellationToken: cancellationToken,
            readerFactory: _ => new StringReader(content),
            sink: sink);
    }

    private class RecordingSink : ISearchTextScanSink
    {
        public List<MatchSpan> Matches { get; } = [];

        public bool NeedsLineText => false;

        public bool NeedsMatchText => false;

        public bool NeedsCaptures => false;

        public bool NeedsLineCompletion => false;

        public long RowsEmitted => Matches.Count;

        public virtual bool AcceptMatch(MatchSpan span)
        {
            Matches.Add(span);
            return false;
        }

        public bool HasMatchesOnLine(long lineNumber) => false;

        public void CompleteLine(long lineNumber, string? lineText, long? byteOffset)
        {
        }
    }

    private sealed class CancellingSink(
        CancellationTokenSource cancellation,
        int cancelAfter) : RecordingSink
    {
        private readonly CancellationTokenSource _cancellation = cancellation;
        private readonly int _cancelAfter = cancelAfter;

        public override bool AcceptMatch(MatchSpan span)
        {
            var result = base.AcceptMatch(span);
            if (Matches.Count == _cancelAfter)
                _cancellation.Cancel();

            return result;
        }
    }
}
