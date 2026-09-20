#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Sources.Paths;

[TestClass]
public sealed class SearchPathTests
{
    [TestMethod]
    public void PathsSource_ShouldEmitEligiblePathsWithoutOpeningContent()
    {
        var root = CreateTemporaryRoot();
        var counters = new SearchScopeCounters();

        try
        {
            Write(root, "first.cs", "TODO");
            Write(root, Path.Combine("nested", "second.txt"), "TODO");
            Write(root, Path.Combine("nested", "third.cs"), "TODO");

            var source = new SearchPathsSource(
                root,
                RuntimeV2TestContexts.CreateExecutionContext(),
                new ScopePolicy(include: ["*.cs"]),
                counters);
            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            CollectionAssert.AreEquivalent(
                new[] { "first.cs", "nested/third.cs" },
                rows.Select(static row => row.Path).ToArray());
            Assert.IsTrue(rows.All(static row => row.Origin is null));
            Assert.IsTrue(rows.All(static row => row.EntryKind == "file"));
            Assert.AreEqual(0L, counters.ContentOpenAttempts);

            var explanation = source.Explanation;
            Assert.IsNotNull(explanation);
            Assert.IsTrue(explanation!.ScopeResolved);
            Assert.AreEqual(3L, explanation.FilesConsidered);
            Assert.AreEqual(2L, explanation.EligibleFiles);
            Assert.AreEqual(2L, explanation.CandidatesYielded);
            StringAssert.Contains(explanation.Summary, "content-open-attempts=0");
            StringAssert.Contains(explanation.Summary, "include-count=1");
            Assert.IsFalse(explanation.Summary.Contains("first.cs", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void PathsSource_ShouldDistinguishEmptyResolvedScopeFromMissingRoot()
    {
        var emptyRoot = CreateTemporaryRoot();
        var missingRoot = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(emptyRoot);
            var emptySource = new SearchPathsSource(
                emptyRoot,
                RuntimeV2TestContexts.CreateExecutionContext());

            var rows = emptySource.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(0, rows.Length);
            Assert.IsNotNull(emptySource.Explanation);
            Assert.AreEqual("directory", emptySource.Explanation!.RootKind);
            Assert.IsTrue(emptySource.Explanation.ScopeResolved);
            Assert.AreEqual(0L, emptySource.Explanation.EligibleFiles);

            var missingSource = new SearchPathsSource(
                missingRoot,
                RuntimeV2TestContexts.CreateExecutionContext());
            var exception = Assert.ThrowsException<SearchSourceAccessException>(
                () => missingSource.Chunks.ToArray());

            Assert.AreEqual(SearchDiagnosticCodes.MissingRoot, exception.Diagnostic.Code);
            Assert.IsNull(missingSource.Explanation);
        }
        finally
        {
            DeleteTemporaryRoot(emptyRoot);
            DeleteTemporaryRoot(missingRoot);
        }
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
            Directory.CreateDirectory(parent);

        File.WriteAllText(path, content);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-paths-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
