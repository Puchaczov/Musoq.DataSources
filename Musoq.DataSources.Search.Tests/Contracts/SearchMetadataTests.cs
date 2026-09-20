#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchMetadataTests
{
    [TestMethod]
    public void MetadataPrefilters_ShouldRejectBeforeOpeningContent()
    {
        var root = CreateTemporaryRoot();
        var opened = new List<string>();
        var counters = new SearchScopeCounters();

        try
        {
            Write(root, "UPPER.CS", "TODO");
            Write(root, "small.cs", "T");
            Write(root, "large.cs", "TODO!");
            Write(root, "source.cs.bak", "TODO");

            var actual = ReadPaths(
                root,
                new ScopePolicy(
                    metadata: new SearchMetadataPolicy(
                        extensionIncludes: ["CS"],
                        minimumSizeBytes: 4,
                        maximumSizeBytes: 4)),
                counters,
                opened);

            CollectionAssert.AreEquivalent(new[] { "UPPER.CS" }, actual);
            Assert.AreEqual(4L, counters.FilesConsidered);
            Assert.AreEqual(3L, counters.MetadataReads);
            Assert.AreEqual(3L, counters.MetadataRejected);
            Assert.AreEqual(1L, counters.CandidatesYielded);
            Assert.AreEqual(1L, counters.ContentOpenAttempts);
            CollectionAssert.AreEqual(
                new[] { Path.Combine(root, "UPPER.CS") },
                opened);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void MetadataSize_ShouldBeReadFreshAfterAFileChanges()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "changing.cs");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "TODO");
            var policy = new ScopePolicy(
                metadata: new SearchMetadataPolicy(minimumSizeBytes: 5));

            var firstCounters = new SearchScopeCounters();
            var firstOpened = new List<string>();
            var first = ReadPaths(root, policy, firstCounters, firstOpened);
            CollectionAssert.AreEqual(Array.Empty<string>(), first);
            Assert.AreEqual(1L, firstCounters.MetadataReads);
            Assert.AreEqual(1L, firstCounters.MetadataRejected);
            Assert.AreEqual(0L, firstCounters.ContentOpenAttempts);
            Assert.AreEqual(0, firstOpened.Count);

            File.AppendAllText(path, "x");

            var secondCounters = new SearchScopeCounters();
            var secondOpened = new List<string>();
            var second = ReadPaths(root, policy, secondCounters, secondOpened);
            CollectionAssert.AreEqual(new[] { "changing.cs" }, second);
            Assert.AreEqual(1L, secondCounters.MetadataReads);
            Assert.AreEqual(0L, secondCounters.MetadataRejected);
            Assert.AreEqual(1L, secondCounters.ContentOpenAttempts);
            CollectionAssert.AreEqual(new[] { path }, secondOpened);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void MetadataTimeAndExtension_ShouldHonorBoundariesWithoutPruningDirectories()
    {
        var root = CreateTemporaryRoot();
        var boundary = new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var counters = new SearchScopeCounters();
        var opened = new List<string>();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "docs.txt"));
            Write(root, "before.CS");
            Write(root, "BOUNDARY.CS");
            Write(root, "after.CS");
            Write(root, Path.Combine("docs.txt", "nested.CS"));

            SetLastWriteTime(root, "before.CS", boundary.AddSeconds(-1));
            SetLastWriteTime(root, "BOUNDARY.CS", boundary);
            SetLastWriteTime(root, "after.CS", boundary.AddSeconds(1));
            SetLastWriteTime(root, Path.Combine("docs.txt", "nested.CS"), boundary);

            var actual = ReadPaths(
                root,
                new ScopePolicy(
                    metadata: new SearchMetadataPolicy(
                        extensionIncludes: [".cs"],
                        modifiedAfterOrEqualUtc: boundary,
                        modifiedBeforeOrEqualUtc: boundary)),
                counters,
                opened);

            CollectionAssert.AreEquivalent(
                new[] { "BOUNDARY.CS", "docs.txt/nested.CS" },
                actual);
            Assert.AreEqual(4L, counters.FilesConsidered);
            Assert.AreEqual(4L, counters.MetadataReads);
            Assert.AreEqual(2L, counters.MetadataRejected);
            Assert.AreEqual(2L, counters.CandidatesYielded);
            Assert.AreEqual(2L, counters.ContentOpenAttempts);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    Path.Combine(root, "BOUNDARY.CS"),
                    Path.Combine(root, "docs.txt", "nested.CS")
                },
                opened);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string[] ReadPaths(
        string root,
        ScopePolicy policy,
        SearchScopeCounters counters,
        ICollection<string> opened)
    {
        return new SearchMatchesSource(
                SearchRequest.Create(root, "TODO", policy),
                RuntimeV2TestContexts.CreateExecutionContext(),
                path =>
                {
                    opened.Add(path);
                    return new StreamReader(path);
                },
                counters)
            .Chunks
            .SelectMany(static chunk => chunk)
            .Select(static row => row.Path)
            .ToArray();
    }

    private static void Write(string root, string relativePath, string content = "TODO")
    {
        var path = Path.Combine(root, relativePath);
        var parent = Path.GetDirectoryName(path);
        if (parent is not null)
            Directory.CreateDirectory(parent);

        File.WriteAllText(path, content);
    }

    private static void SetLastWriteTime(
        string root,
        string relativePath,
        DateTimeOffset timestamp)
    {
        File.SetLastWriteTimeUtc(
            Path.Combine(root, relativePath),
            timestamp.UtcDateTime);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-metadata-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
