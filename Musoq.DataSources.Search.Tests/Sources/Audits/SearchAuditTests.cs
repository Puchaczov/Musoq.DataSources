#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Tests.Sources.Audits;

[TestClass]
public sealed class SearchAuditTests
{
    [TestMethod]
    public void RepeatedInvocation_ShouldPublishIndependentFreshSummaries()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "TODO TODO\n");
            Write(root, "second.txt", "DONE\n");
            var source = new SearchAuditSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext());

            var first = ReadAudit(source);
            var second = ReadAudit(source);

            Assert.AreNotEqual(first.ScanId, second.ScanId);
            Assert.AreEqual(first.ScopeFingerprint, second.ScopeFingerprint);
            Assert.AreEqual(SearchOutcome.ScopeExhausted, first.Outcome);
            Assert.AreEqual(SearchOutcome.ScopeExhausted, second.Outcome);
            Assert.IsTrue(first.Complete);
            Assert.IsTrue(second.Complete);
            Assert.IsTrue(first.CountsExact);
            Assert.IsTrue(second.CountsExact);
            Assert.AreEqual(2L, first.EligibleFiles);
            Assert.AreEqual(2L, second.EligibleFiles);
            Assert.AreEqual(2L, first.Occurrences);
            Assert.AreEqual(2L, second.Occurrences);
            Assert.AreEqual(2L, first.ObservedRows);
            Assert.AreEqual(2L, second.ObservedRows);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public async Task ConcurrentRuns_ShouldPublishIndependentSummaries()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "first.txt", "TODO\n");
            Write(root, "second.txt", "TODO TODO\n");
            var request = SearchRequest.Create(root, "TODO");

            var firstTask = Task.Run(() => ReadAudit(new SearchAuditSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext())));
            var secondTask = Task.Run(() => ReadAudit(new SearchAuditSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext())));
            var audits = await Task.WhenAll(firstTask, secondTask);

            Assert.AreEqual(2, audits.Length);
            Assert.AreNotEqual(audits[0].ScanId, audits[1].ScanId);
            Assert.AreEqual(audits[0].ScopeFingerprint, audits[1].ScopeFingerprint);
            Assert.AreEqual(3L, audits[0].Occurrences);
            Assert.AreEqual(3L, audits[1].Occurrences);
            Assert.AreEqual(2L, audits[0].EligibleFiles);
            Assert.AreEqual(2L, audits[1].EligibleFiles);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SameRootDifferentProfile_ShouldHaveDistinctScopeAndCounters()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "source.cs", "TODO\n");
            Write(root, "notes.txt", "TODO TODO\n");
            var unrestricted = ReadAudit(new SearchAuditSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext()));
            var sourceOnly = ReadAudit(new SearchAuditSource(
                SearchRequest.Create(root, "TODO", new ScopePolicy(include: ["*.cs"])),
                RuntimeV2TestContexts.CreateExecutionContext()));

            Assert.AreNotEqual(unrestricted.ScopeFingerprint, sourceOnly.ScopeFingerprint);
            Assert.AreEqual(2L, unrestricted.EligibleFiles);
            Assert.AreEqual(3L, unrestricted.Occurrences);
            Assert.AreEqual(1L, sourceOnly.EligibleFiles);
            Assert.AreEqual(1L, sourceOnly.Occurrences);
            Assert.AreEqual(SearchOutcome.ScopeExhausted, sourceOnly.Outcome);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void EmptyScan_ShouldPublishOneCompleteExactAuditRow()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var audit = ReadAudit(new SearchAuditSource(
                SearchRequest.Create(root, "TODO"),
                RuntimeV2TestContexts.CreateExecutionContext()));

            Assert.AreEqual(root, audit.Root);
            Assert.AreEqual(SearchOutcome.ScopeExhausted, audit.Outcome);
            Assert.AreEqual("no-eligible-files", audit.TerminalReason);
            Assert.IsTrue(audit.Complete);
            Assert.IsTrue(audit.ScopeExhausted);
            Assert.IsTrue(audit.CountsExact);
            Assert.IsTrue(audit.ScopeResolved);
            Assert.AreEqual(0L, audit.VisitedFiles);
            Assert.AreEqual(0L, audit.EligibleFiles);
            Assert.AreEqual(0L, audit.FilesOpened);
            Assert.AreEqual(0L, audit.FilesCompleted);
            Assert.AreEqual(0L, audit.Occurrences);
            Assert.IsNull(audit.FailureCode);
            Assert.IsNull(audit.FailurePath);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ChangedSourceBetweenRuns_ShouldReportEachRunCurrentContent()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var path = Write(root, "changing.txt", "TODO\n");
            var request = SearchRequest.Create(root, "TODO");
            var first = ReadAudit(new SearchAuditSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext()));

            File.WriteAllText(path, "DONE\n");
            var second = ReadAudit(new SearchAuditSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext()));

            Assert.AreNotEqual(first.ScanId, second.ScanId);
            Assert.AreEqual(first.ScopeFingerprint, second.ScopeFingerprint);
            Assert.AreEqual(1L, first.Occurrences);
            Assert.AreEqual(0L, second.Occurrences);
            Assert.AreEqual("completed-scan", first.TerminalReason);
            Assert.AreEqual("no-match", second.TerminalReason);
            Assert.IsTrue(first.Complete);
            Assert.IsTrue(second.Complete);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void MissingRoot_ShouldReturnTypedFailedAuditRow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-audit-missing-{Guid.NewGuid():N}");

        var audit = ReadAudit(new SearchAuditSource(
            SearchRequest.Create(root, "TODO"),
            RuntimeV2TestContexts.CreateExecutionContext()));

        Assert.AreEqual(SearchOutcome.Failed, audit.Outcome);
        Assert.IsFalse(audit.Complete);
        Assert.IsFalse(audit.CountsExact);
        Assert.AreEqual(SearchDiagnosticCodes.MissingRoot, audit.FailureCode);
        Assert.AreEqual("root-missing", audit.TerminalReason);
        Assert.AreEqual(root, audit.FailurePath);
    }

    private static SearchAudit ReadAudit(SearchAuditSource source)
    {
        var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();
        Assert.AreEqual(1, rows.Length);
        return rows[0];
    }

    private static string Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
