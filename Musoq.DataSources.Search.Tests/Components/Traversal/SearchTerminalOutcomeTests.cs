#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Execution;

namespace Musoq.DataSources.Search.Tests.Components.Traversal;

[TestClass]
public sealed class SearchTerminalOutcomeTests
{
    [TestMethod]
    public void NoMatch_ShouldReturnScopeExhaustedSummaryWithExactZeroCounters()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "DONE\n");
            Write(root, "second.txt", "still no match\n");

            var source = new SearchMatchesSource(
                root,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new StringReader("DONE\n"));
            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(0, rows.Length);
            var summary = RequireSummary(source);
            Assert.AreEqual(SearchOutcome.ScopeExhausted, summary.Outcome);
            Assert.IsTrue(summary.Complete);
            Assert.IsTrue(summary.ScopeExhausted);
            Assert.IsFalse(summary.QuerySatisfied);
            Assert.IsTrue(summary.CountsExact);
            Assert.IsTrue(summary.ScopeResolved);
            Assert.AreEqual("no-match", summary.TerminalReason);
            Assert.AreEqual(2L, summary.EligibleFiles);
            Assert.AreEqual(2L, summary.FilesOpened);
            Assert.AreEqual(2L, summary.FilesRead);
            Assert.AreEqual(2L, summary.FilesCompleted);
            Assert.AreEqual(0L, summary.FilesFailed);
            Assert.AreEqual(0L, summary.FilesMatched);
            Assert.AreEqual(0L, summary.MatchingLines);
            Assert.AreEqual(0L, summary.Occurrences);
            Assert.AreEqual(0L, summary.ObservedRows);
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.ScanId));
            StringAssert.StartsWith(summary.ScopeFingerprint, "sha256:");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void AllExcludedFiles_ShouldBeACompleteEmptyScopeNotAReadFailure()
    {
        var root = CreateTemporaryRoot();
        var readerOpened = false;

        try
        {
            Write(root, "note.txt", "TODO\n");
            var request = SearchRequest.Create(
                root,
                "TODO",
                new ScopePolicy(include: ["*.cs"]));
            var source = new SearchMatchesSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ =>
                {
                    readerOpened = true;
                    return new StringReader("TODO");
                });

            Assert.AreEqual(0, source.Chunks.SelectMany(static chunk => chunk).Count());
            var summary = RequireSummary(source);
            Assert.AreEqual(SearchOutcome.ScopeExhausted, summary.Outcome);
            Assert.AreEqual("no-eligible-files", summary.TerminalReason);
            Assert.IsTrue(summary.Complete);
            Assert.IsTrue(summary.CountsExact);
            Assert.AreEqual(1L, summary.VisitedFiles);
            Assert.AreEqual(0L, summary.EligibleFiles);
            Assert.AreEqual(0L, summary.FilesOpened);
            Assert.IsFalse(readerOpened);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void AcceptedTake_ShouldEndAsQuerySatisfiedBeforeTheEligibleScopeIsExhausted()
    {
        var root = CreateTemporaryRoot();
        var opened = new List<string>();

        try
        {
            Write(root, "first.txt", "TODO\n");
            Write(root, "second.txt", "TODO\n");
            var plan = new SourceExecutionPlan
            {
                Identity = new SourceIdentity("test", "search", "matches", Guid.NewGuid().ToString()),
                AcceptedTake = 1
            };
            var source = new SearchMatchesSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan),
                filePath =>
                {
                    opened.Add(Path.GetFileName(filePath));
                    return new StringReader("TODO\n");
                });

            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            var summary = RequireSummary(source);
            Assert.AreEqual(SearchOutcome.QuerySatisfied, summary.Outcome);
            Assert.AreEqual("take-reached", summary.TerminalReason);
            Assert.IsTrue(summary.Complete);
            Assert.IsTrue(summary.QuerySatisfied);
            Assert.IsFalse(summary.ScopeExhausted);
            Assert.IsFalse(summary.CountsExact);
            // The coordinator may discover the next candidate before draining
            // the first completed file. It must not open that candidate after
            // the accepted TAKE is satisfied.
            Assert.AreEqual(2L, summary.EligibleFiles);
            Assert.AreEqual(1L, summary.FilesOpened);
            Assert.AreEqual(1L, summary.FilesCompleted);
            Assert.AreEqual(1L, summary.ObservedRows);
            Assert.AreEqual(1, opened.Count);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ReadFailureAfterACompletedFile_ShouldRetainFailureAndObservedPrefix()
    {
        var root = CreateTemporaryRoot();
        var readerCalls = 0;

        try
        {
            Write(root, "first.txt", "fixture");
            Write(root, "second.txt", "fixture");
            var source = new SearchMatchesSource(
                root,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => Interlocked.Increment(ref readerCalls) == 1
                    ? new StringReader("TODO\n")
                    : throw new IOException("synthetic read failure"));

            var exception = Assert.ThrowsException<SearchSourceAccessException>(
                () => source.Chunks.ToArray());

            Assert.AreEqual(SearchDiagnosticCodes.SourceOpenFailed, exception.Diagnostic.Code);
            var summary = RequireSummary(source);
            Assert.AreEqual(SearchOutcome.Failed, summary.Outcome);
            Assert.AreEqual("unreadable-file", summary.TerminalReason);
            Assert.IsFalse(summary.Complete);
            Assert.IsFalse(summary.ScopeExhausted);
            Assert.IsFalse(summary.QuerySatisfied);
            Assert.IsFalse(summary.CountsExact);
            Assert.AreEqual(SearchDiagnosticCodes.SourceOpenFailed, summary.FailureCode);
            Assert.AreEqual(2L, summary.EligibleFiles);
            Assert.AreEqual(2L, summary.FilesOpened);
            Assert.AreEqual(1L, summary.FilesRead);
            Assert.AreEqual(1L, summary.FilesCompleted);
            Assert.AreEqual(1L, summary.FilesFailed);
            Assert.AreEqual(1L, summary.ObservedRows);
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.FailurePath));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CancellationBeforeTraversal_ShouldBeFailedAndNeverACompleteEmptyResult()
    {
        var root = CreateTemporaryRoot();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Write(root, "input.txt", "TODO\n");
            var source = new SearchMatchesSource(
                root,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(cancellation.Token));

            Assert.ThrowsException<OperationCanceledException>(
                () => source.Chunks.ToArray());

            var summary = RequireSummary(source);
            Assert.AreEqual(SearchOutcome.Failed, summary.Outcome);
            Assert.AreEqual("cancelled", summary.TerminalReason);
            Assert.AreEqual("cancelled", summary.FailureCode);
            Assert.IsFalse(summary.Complete);
            Assert.IsFalse(summary.ScopeExhausted);
            Assert.IsFalse(summary.CountsExact);
            Assert.AreEqual(0L, summary.ObservedRows);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static SearchTerminalSummary RequireSummary<T>(SearchTextSourceBase<T> source)
    {
        return source.LastExecution ??
               throw new AssertFailedException("Search did not publish a terminal summary.");
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-terminal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
