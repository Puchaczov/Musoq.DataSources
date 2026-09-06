#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Traversal;

[TestClass]
public sealed class SearchTraversalTests
{
    [TestMethod]
    public void Traversal_ShouldConsumeOneEntryAtATimeAndBoundOpenFrontier()
    {
        var rootEntries = new TrackingEntryEnumerator(
        [
            new SearchTraversalEntry("root/child", IsDirectory: true, IsFile: false),
            new SearchTraversalEntry("root/root.txt", IsDirectory: false, IsFile: true)
        ]);
        var childEntries = new TrackingEntryEnumerator(
        [
            new SearchTraversalEntry("root/child/child.txt", IsDirectory: false, IsFile: true)
        ]);
        var opened = new List<string>();

        using var traversal = SearchFileTraversal
            .EnumerateDirectory(
                "root",
                CancellationToken.None,
                path =>
                {
                    opened.Add(path);
                    return path switch
                    {
                        "root" => rootEntries,
                        "root/child" => childEntries,
                        _ => throw new InvalidOperationException($"Unexpected directory '{path}'.")
                    };
                })
            .GetEnumerator();

        Assert.IsTrue(traversal.MoveNext());
        Assert.AreEqual("root/child/child.txt", traversal.Current);
        Assert.AreEqual(1, rootEntries.MoveNextCount);
        Assert.AreEqual(1, childEntries.MoveNextCount);
        CollectionAssert.AreEqual(new[] { "root", "root/child" }, opened);
        Assert.IsFalse(rootEntries.IsDisposed);
        Assert.IsFalse(childEntries.IsDisposed);

        Assert.IsTrue(traversal.MoveNext());
        Assert.AreEqual("root/root.txt", traversal.Current);
        Assert.AreEqual(2, rootEntries.MoveNextCount);
        Assert.AreEqual(2, childEntries.MoveNextCount);
        Assert.IsTrue(childEntries.IsDisposed);

        Assert.IsFalse(traversal.MoveNext());
        Assert.IsTrue(rootEntries.IsDisposed);
    }

    [TestMethod]
    public void Traversal_Cancellation_ShouldDisposeEveryOpenDirectory()
    {
        var entries = new TrackingEntryEnumerator(
        [
            new SearchTraversalEntry("root/first.txt", IsDirectory: false, IsFile: true),
            new SearchTraversalEntry("root/second.txt", IsDirectory: false, IsFile: true)
        ]);
        using var cancellation = new CancellationTokenSource();
        using var traversal = SearchFileTraversal
            .EnumerateDirectory(
                "root",
                cancellation.Token,
                _ => entries)
            .GetEnumerator();

        Assert.IsTrue(traversal.MoveNext());
        cancellation.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() => traversal.MoveNext());
        Assert.IsTrue(entries.IsDisposed);
    }

    [TestMethod]
    public void RealTraversal_ShouldVisitDeepWideTreesAndIgnoreEmptyDirectories()
    {
        const int depth = 24;
        const int width = 512;
        var root = CreateTemporaryRoot();
        var expected = new List<string>(depth + width);

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "empty", "nested"));

            var current = root;
            for (var index = 0; index < depth; index++)
            {
                current = Path.Combine(current, $"level-{index:000}");
                Directory.CreateDirectory(current);
                var path = Path.Combine(current, "deep.txt");
                File.WriteAllText(path, "TODO");
                expected.Add(path);
            }

            for (var index = 0; index < width; index++)
            {
                var path = Path.Combine(root, $"wide-{index:0000}.txt");
                File.WriteAllText(path, "TODO");
                expected.Add(path);
            }

            var actual = SearchFileTraversal
                .Enumerate(root, CancellationToken.None)
                .ToArray();

            Assert.AreEqual(expected.Count, actual.Length);
            CollectionAssert.AreEquivalent(expected, actual);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void EmptyDirectories_ShouldProduceNoCandidates()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "one", "two"));
            Directory.CreateDirectory(Path.Combine(root, "three"));

            var actual = SearchFileTraversal
                .Enumerate(root, CancellationToken.None)
                .ToArray();

            Assert.AreEqual(0, actual.Length);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void DeletionAfterCandidateDiscovery_ShouldRemainTypedSourceFailure()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "vanished.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "TODO");

            var source = new SearchMatchesSource(
                root,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                filePath =>
                {
                    File.Delete(filePath);
                    return new StreamReader(filePath);
                });

            var exception = Assert.ThrowsException<SearchSourceAccessException>(
                () => source.Chunks.ToArray());

            Assert.AreEqual(
                SearchDiagnosticCodes.SourceOpenFailed,
                exception.Diagnostic.Code);
            Assert.AreEqual(path, exception.Diagnostic.Location?.Path);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-traversal-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class TrackingEntryEnumerator : IEnumerator<SearchTraversalEntry>
    {
        private readonly IReadOnlyList<SearchTraversalEntry> _entries;
        private int _index = -1;

        public TrackingEntryEnumerator(IReadOnlyList<SearchTraversalEntry> entries)
        {
            _entries = entries;
        }

        public int MoveNextCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public SearchTraversalEntry Current =>
            _index >= 0 && _index < _entries.Count
                ? _entries[_index]
                : throw new InvalidOperationException("Enumerator is not positioned on an entry.");

        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext()
        {
            MoveNextCount++;
            _index++;
            return _index < _entries.Count;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
