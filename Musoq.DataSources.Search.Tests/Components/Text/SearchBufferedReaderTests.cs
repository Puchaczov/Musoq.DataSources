#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchBufferedReaderTests
{
    [TestMethod]
    public void PooledBuffer_ShouldHaveBoundedLengthAndReturnOwnershipDeterministically()
    {
        using var buffer = SearchCharBuffer.Rent();

        Assert.AreEqual(SearchCharBuffer.RequestedLength, buffer.Length);
        _ = buffer.Array;
        Assert.IsFalse(buffer.IsReturned);

        buffer.Dispose();

        Assert.IsTrue(buffer.IsReturned);
        Assert.ThrowsException<ObjectDisposedException>(() => _ = buffer.Array);
        buffer.Dispose();
    }

    [TestMethod]
    public void DirectSource_ShouldHandleIrregularShortReadsAndPrematureEof()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "short.txt");
        var eofPath = Path.Combine(root, "eof.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "ignored");
            File.WriteAllText(eofPath, "ignored");

            var shortReadSource = new SearchMatchesSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new IrregularTextReader("xTODO TODO", [1, 3, 2, 1]));
            var shortReadRows = shortReadSource.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(2, shortReadRows.Length);
            Assert.AreEqual(1L, shortReadRows[0].Utf16Column);
            Assert.AreEqual(6L, shortReadRows[1].Utf16Column);

            var prematureEofSource = new SearchMatchesSource(
                eofPath,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => new PrematureEofTextReader("TODOxTODO", visibleLength: 5));
            var prematureEofRows = prematureEofSource.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(1, prematureEofRows.Length);
            Assert.AreEqual("TODO", prematureEofRows[0].MatchText);
            Assert.AreEqual(0L, prematureEofRows[0].MatchIndex);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Rows_ShouldRemainOwnedAfterPooledBufferReuseAndInterruptedReads()
    {
        var root = CreateTemporaryRoot();
        var firstPath = Path.Combine(root, "first.txt");
        var secondPath = Path.Combine(root, "second.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(firstPath, "ignored");
            File.WriteAllText(secondPath, "ignored");

            var source = new SearchMatchesSource(
                root,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                filePath => new IrregularTextReader(
                    Path.GetFileName(filePath) == "first.txt" ? "TODO" : "TODO TODO",
                    [2, 1, 3]));
            var rows = source.Chunks
                .SelectMany(static chunk => chunk)
                .OrderBy(static row => row.Path, StringComparer.Ordinal)
                .ThenBy(static row => row.MatchIndex)
                .ToArray();

            Assert.AreEqual(3, rows.Length);
            CollectionAssert.AreEquivalent(
                new[] { "first.txt", "second.txt", "second.txt" },
                rows.Select(static row => row.Path).ToArray());
            Assert.IsTrue(rows.All(static row => row.MatchText == "TODO"));
            Assert.AreEqual(0L, rows[0].MatchIndex);
            Assert.AreEqual(0L, rows[1].MatchIndex);
            Assert.AreEqual(1L, rows[2].MatchIndex);

            var interruptedReader = new InterruptingTextReader();
            var interruptedSource = new SearchMatchesSource(
                firstPath,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => interruptedReader);

            Assert.ThrowsException<OperationCanceledException>(
                () => interruptedSource.Chunks.ToArray());
            Assert.IsTrue(interruptedReader.Disposed);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-buffer-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class IrregularTextReader(
        string content,
        IReadOnlyList<int> chunkSizes) : TextReader
    {
        private int _position;
        private int _chunkIndex;

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position >= content.Length)
                return 0;

            var requested = chunkSizes[Math.Min(_chunkIndex++, chunkSizes.Count - 1)];
            var length = Math.Min(Math.Min(requested, count), content.Length - _position);
            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }

    private sealed class PrematureEofTextReader(string content, int visibleLength) : TextReader
    {
        private int _position;

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position >= visibleLength)
                return 0;

            var length = Math.Min(count, visibleLength - _position);
            content.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }

    private sealed class InterruptingTextReader : TextReader
    {
        public bool Disposed { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            throw new OperationCanceledException();
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
