#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Sources.Counts;

[TestClass]
public sealed class SearchCountTests
{
    [TestMethod]
    public void CountsSource_ShouldEmitCompleteRowsForEveryEligibleFileIncludingZeroHits()
    {
        var root = CreateTemporaryRoot();
        var matchingContent = "TODO TODO\nno match\nTODO";

        try
        {
            Write(root, "first.txt", matchingContent);
            Write(root, "empty.txt", string.Empty);
            Write(root, "none.txt", "DONE\n");

            var rows = new SearchCountsSource(
                    root,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(static chunk => chunk)
                .OrderBy(static row => row.Path, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(3, rows.Length);

            var matching = rows[0];
            Assert.AreEqual("empty.txt", matching.Path);
            Assert.AreEqual(0L, matching.OccurrenceCount);
            Assert.AreEqual(0L, matching.MatchingLineCount);
            Assert.AreEqual(0L, matching.BytesScanned);
            Assert.IsTrue(matching.Complete);
            Assert.IsNull(matching.Origin);
            Assert.IsNull(matching.PatternId);

            matching = rows[1];
            Assert.AreEqual("first.txt", matching.Path);
            Assert.AreEqual(3L, matching.OccurrenceCount);
            Assert.AreEqual(2L, matching.MatchingLineCount);
            Assert.AreEqual(Encoding.UTF8.GetByteCount(matchingContent), matching.BytesScanned);
            Assert.IsTrue(matching.Complete);

            matching = rows[2];
            Assert.AreEqual("none.txt", matching.Path);
            Assert.AreEqual(0L, matching.OccurrenceCount);
            Assert.AreEqual(0L, matching.MatchingLineCount);
            Assert.AreEqual(Encoding.UTF8.GetByteCount("DONE\n"), matching.BytesScanned);
            Assert.IsTrue(matching.Complete);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CountsSource_ShouldExposeGroupedCountsThroughCompiledProjection()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);

        try
        {
            Write(root, "first.txt", "TODO TODO\nTODO\n");
            Write(root, "empty.txt", string.Empty);

            var result = Compile(
                    $"select Path, OccurrenceCount, MatchingLineCount, BytesScanned, Complete " +
                    $"from search.counts('{escapedRoot}', 'TODO') order by Path")
                .Run();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("empty.txt", result.Rows[0][0]);
            Assert.AreEqual(0L, result.Rows[0][1]);
            Assert.AreEqual(0L, result.Rows[0][2]);
            Assert.AreEqual(0L, result.Rows[0][3]);
            Assert.AreEqual(true, result.Rows[0][4]);

            Assert.AreEqual("first.txt", result.Rows[1][0]);
            Assert.AreEqual(3L, result.Rows[1][1]);
            Assert.AreEqual(2L, result.Rows[1][2]);
            Assert.AreEqual((long)Encoding.UTF8.GetByteCount("TODO TODO\nTODO\n"), result.Rows[1][3]);
            Assert.AreEqual(true, result.Rows[1][4]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CountAccumulator_ShouldUseChecked64BitBoundaries()
    {
        var occurrenceOverflow = new SearchCountAccumulator(long.MaxValue, 0, 0);
        Assert.ThrowsException<OverflowException>(() => occurrenceOverflow.AddMatch(1));

        var lineOverflow = new SearchCountAccumulator(0, long.MaxValue, 0);
        Assert.ThrowsException<OverflowException>(() => lineOverflow.AddMatch(1));

        var byteOverflow = new SearchCountAccumulator(0, 0, long.MaxValue);
        Assert.ThrowsException<OverflowException>(() => byteOverflow.AddBytes(1));

        var negativeBytes = new SearchCountAccumulator();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => negativeBytes.AddBytes(-1));
    }

    [TestMethod]
    public void CountsSource_ShouldNotPublishAnExactRowAfterPartialRead()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "partial.txt");
        var reader = new PartialReadTextReader();

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "TODO complete physical file");

            var source = new SearchCountsSource(
                path,
                "TODO",
                RuntimeV2TestContexts.CreateExecutionContext(),
                _ => reader);

            var exception = Assert.ThrowsException<SearchSourceReadException>(
                () => source.Chunks.ToArray());

            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, exception.Diagnostic.Code);
            Assert.IsTrue(reader.Disposed);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
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
        return Path.Combine(Path.GetTempPath(), $"musoq-search-counts-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class PartialReadTextReader : TextReader
    {
        private bool _firstRead = true;

        public bool Disposed { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            if (!_firstRead)
                throw new IOException("synthetic partial read failure");

            _firstRead = false;
            "TODO".CopyTo(0, buffer, index, 4);
            return 4;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
