#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Many;

using Musoq.DataSources.Search.Components.Execution;

namespace Musoq.DataSources.Search.Tests.Components.Traversal;

[TestClass]
public sealed class SearchResourceLimitTests
{
    [TestMethod]
    public void FileAndTotalByteBudgets_ShouldFailWithStableCodes()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "1234");
            Write(root, "second.txt", "5678");

            var fileSource = new SearchMatchesSource(
                SearchRequest.Create(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxFileBytes: 3)),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new StringReader("no match"));
            var fileException = Assert.ThrowsException<SearchResourceLimitException>(
                () => fileSource.Chunks.ToArray());

            Assert.AreEqual("file-bytes", fileException.BudgetCode);
            Assert.AreEqual(SearchOutcome.Failed, RequireSummary(fileSource).Outcome);
            Assert.AreEqual("budget-exhausted", RequireSummary(fileSource).TerminalReason);
            Assert.AreEqual("file-bytes", RequireSummary(fileSource).FailureCode);

            var totalSource = new SearchMatchesSource(
                SearchRequest.Create(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxTotalBytes: 6)),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new StringReader("no match"));
            var totalException = Assert.ThrowsException<SearchResourceLimitException>(
                () => totalSource.Chunks.ToArray());

            Assert.AreEqual("read-bytes", totalException.BudgetCode);
            Assert.AreEqual("read-bytes", RequireSummary(totalSource).FailureCode);
            Assert.AreEqual(1L, RequireSummary(totalSource).FilesCompleted);
            Assert.AreEqual(1L, RequireSummary(totalSource).FilesFailed);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void FileCountAndMatchCountBudgets_ShouldNeverTruncateAsSuccess()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "TODO\n");
            Write(root, "second.txt", "TODO\n");

            var fileSource = new SearchMatchesSource(
                SearchRequest.Create(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxFiles: 1)),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new StringReader("no match"));

            Assert.ThrowsException<SearchResourceLimitException>(
                () => fileSource.Chunks.ToArray());
            var fileSummary = RequireSummary(fileSource);
            Assert.AreEqual(SearchOutcome.Failed, fileSummary.Outcome);
            Assert.AreEqual("budget-exhausted", fileSummary.TerminalReason);
            Assert.AreEqual("file-count", fileSummary.FailureCode);

            var matchSource = new SearchMatchesSource(
                SearchRequest.Create(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxMatchCount: 512)),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new StringReader(
                    string.Concat(Enumerable.Repeat("TODO\n", 2_000))));

            Assert.ThrowsException<SearchResourceLimitException>(
                () => matchSource.Chunks.ToArray());
            var matchSummary = RequireSummary(matchSource);
            Assert.AreEqual(SearchOutcome.Failed, matchSummary.Outcome);
            Assert.AreEqual("budget-exhausted", matchSummary.TerminalReason);
            Assert.AreEqual("match-count", matchSummary.FailureCode);
            Assert.IsFalse(matchSummary.ScopeExhausted);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void OutputBudget_ShouldFailBeforeUnboundedStagingAndDisposeReader()
    {
        var root = CreateTemporaryRoot();
        var readers = new List<TrackingReader>();

        try
        {
            Write(root, "fixture.txt", "fixture");
            var source = new SearchMatchesSource(
                SearchRequest.Create(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxInFlightOutputBytes: 1)),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ =>
                {
                    var reader = new TrackingReader("TODO\nTODO\n");
                    readers.Add(reader);
                    return reader;
                });

            Assert.ThrowsException<SearchResourceLimitException>(
                () => source.Chunks.ToArray());

            var summary = RequireSummary(source);
            Assert.AreEqual(SearchOutcome.Failed, summary.Outcome);
            Assert.AreEqual("budget-exhausted", summary.TerminalReason);
            Assert.AreEqual("output-cap", summary.FailureCode);
            Assert.IsTrue(readers.All(static reader => reader.WasDisposed));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void RecordBudget_ShouldRejectAHugeLiteralLineAndCleanUpReader()
    {
        var root = CreateTemporaryRoot();
        TrackingReader? reader = null;

        try
        {
            Write(root, "fixture.txt", "fixture");
            var source = new SearchMatchesSource(
                SearchRequest.Create(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxRecordBytes: 4)),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ =>
                {
                    reader = new TrackingReader("12345");
                    return reader;
                });

            var exception = Assert.ThrowsException<SearchResourceLimitException>(
                () => source.Chunks.ToArray());

            Assert.AreEqual("record-bytes", exception.BudgetCode);
            Assert.AreEqual("record-bytes", RequireSummary(source).FailureCode);
            Assert.IsTrue(reader?.WasDisposed);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void RegexPhysicalRecordBudget_ShouldCountDecodedBytes()
    {
        var root = CreateTemporaryRoot();
        TrackingReader? reader = null;

        try
        {
            Write(root, "fixture.txt", "fixture");
            var source = new SearchMatchesSource(
                SearchRequest.CreateRegex(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxRecordBytes: 4)),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ =>
                {
                    reader = new TrackingReader("ééé\n");
                    return reader;
                });

            var exception = Assert.ThrowsException<SearchResourceLimitException>(
                () => source.Chunks.ToArray());

            Assert.AreEqual("record-bytes", exception.BudgetCode);
            Assert.AreEqual("record-bytes", RequireSummary(source).FailureCode);
            Assert.IsTrue(reader?.WasDisposed);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void PatternCompileAndContextBudgets_ShouldRejectBeforeScanning()
    {
        var patternException = Assert.ThrowsException<SearchResourceLimitException>(() =>
            SearchRequest.Create(
                "root",
                "TODO",
                limits: new SearchResourceLimits(maxPatternLength: 3)));
        Assert.AreEqual("pattern-size", patternException.BudgetCode);

        var patternBytesException = Assert.ThrowsException<SearchResourceLimitException>(() =>
            SearchRequest.Create(
                "root",
                "TODO",
                limits: new SearchResourceLimits(maxPatternBytes: 3)));
        Assert.AreEqual("pattern-size", patternBytesException.BudgetCode);

        var contextException = Assert.ThrowsException<SearchResourceLimitException>(() =>
            SearchRequest.Create(
                "root",
                "TODO",
                context: new SearchContextOptions(
                    beforeLines: 1,
                    afterLines: 1,
                    maxBytes: 64),
                limits: new SearchResourceLimits(maxContextBytes: 32)));
        Assert.AreEqual("context-bytes", contextException.BudgetCode);

        var patternCountException = Assert.ThrowsException<SearchResourceLimitException>(() =>
            new SearchManyRequest(
                [
                    new SearchManyPattern("one", "one", SearchPatternMode.Literal),
                    new SearchManyPattern("two", "two", SearchPatternMode.Literal)
                ],
                new SearchManyOptions(
                    limits: new SearchResourceLimits(maxPatternCount: 1))));
        Assert.AreEqual("pattern-count", patternCountException.BudgetCode);

        SearchRegexBackend.ResetForTests();
        try
        {
            var compileException = Assert.ThrowsException<SearchResourceLimitException>(() =>
                SearchRequest.CreateRegex(
                    "root",
                    "^(a|aa)+$",
                    limits: new SearchResourceLimits(
                        maxPatternCompilationMilliseconds: 0)));
            Assert.AreEqual("compile-cost", compileException.BudgetCode);
        }
        finally
        {
            SearchRegexBackend.ResetForTests();
        }
    }

    [TestMethod]
    public void ExplicitPartialPolicy_ShouldExposeBudgetAsPartialNotComplete()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "fixture.txt", "fixture");
            Write(root, "second.txt", "fixture");
            var source = new SearchMatchesSource(
                SearchRequest.Create(
                    root,
                    "TODO",
                    limits: new SearchResourceLimits(maxTotalBytes: 10),
                    partialPolicy: SearchPartialPolicy.Allow),
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new StringReader("TODO\n"));

            Assert.ThrowsException<SearchResourceLimitException>(
                () => source.Chunks.ToArray());

            var summary = RequireSummary(source);
            Assert.AreEqual(SearchOutcome.Partial, summary.Outcome);
            Assert.IsFalse(summary.Complete);
            Assert.AreEqual("budget-exhausted", summary.TerminalReason);
            Assert.AreEqual("read-bytes", summary.FailureCode);
            Assert.IsTrue(summary.ObservedRows > 0);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static SearchTerminalSummary RequireSummary<T>(SearchTextSourceBase<T> source)
    {
        return source.LastExecution ??
               throw new AssertFailedException(
                   "Search did not publish a terminal summary.");
    }

    private static void Write(string root, string relativePath, string content)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, relativePath), content);
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-budget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class TrackingReader(string content) : StringReader(content)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                WasDisposed = true;

            base.Dispose(disposing);
        }
    }
}
